using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
namespace IVH.Core.ServiceConnector.Gemini.Realtime
{
    public static class VertexAuthHelper
    {
        private const string OAUTH_URL = "https://oauth2.googleapis.com/token";
        private const string SCOPE = "https://www.googleapis.com/auth/cloud-platform";

        public class ServiceAccountCreds
        {
            public string client_email;
            public string private_key;
            public string project_id;
        }

        public static async Task<(string accessToken, string projectId)> GetAccessTokenFromUserDir(string fileName)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string jsonPath = Path.Combine(userProfile, ".aiapi", fileName);

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Could not find Service Account at: {jsonPath}");

            return await GetAccessTokenAsync(jsonPath);
        }

        private static async Task<(string accessToken, string projectId)> GetAccessTokenAsync(string jsonPath)
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var creds = JsonConvert.DeserializeObject<ServiceAccountCreds>(jsonContent);

            // 1. JWT Header & Payload
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = new { alg = "RS256", typ = "JWT" };
            var payload = new
            {
                iss = creds.client_email,
                scope = SCOPE,
                aud = OAUTH_URL,
                exp = now + 3600,
                iat = now
            };

            string headerBase64 = ToBase64Url(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(header)));
            string payloadBase64 = ToBase64Url(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
            string unsignedJwt = $"{headerBase64}.{payloadBase64}";

            // 2. Sign with Robust RSA Method
            string signature = SignWithRsaManual(unsignedJwt, creds.private_key);
            string jwt = $"{unsignedJwt}.{signature}";

            // 3. Exchange for Token
            WWWForm form = new WWWForm();
            form.AddField("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
            form.AddField("assertion", jwt);

            using (UnityWebRequest req = UnityWebRequest.Post(OAUTH_URL, form))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Auth Error: {req.error} : {req.downloadHandler.text}");

                var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(req.downloadHandler.text);
                return (response["access_token"], creds.project_id);
            }
        }

        // --- ROBUST MANUAL RSA IMPLEMENTATION ---
        // This bypasses ImportPkcs8PrivateKey by manually parsing the ASN.1 data.
        // Works on all Unity versions and Platforms (Android/iOS/Windows)

        private static string SignWithRsaManual(string data, string privateKeyPem)
        {
            // 1. Clean the PEM
            privateKeyPem = privateKeyPem.Replace("-----BEGIN PRIVATE KEY-----", "")
                                        .Replace("-----END PRIVATE KEY-----", "")
                                        .Replace("\n", "").Replace("\r", "");
            byte[] keyBytes = Convert.FromBase64String(privateKeyPem);

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                try
                {
                    // Try the easy way first (works if .NET Standard 2.1 is properly supported)
                    rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                }
                catch (PlatformNotSupportedException)
                {
                    // Fallback: Manual Parsing
                    var rsaParams = GetRsaParameters(keyBytes);
                    rsa.ImportParameters(rsaParams);
                }
                catch (Exception)
                {
                    // Double Fallback just in case
                    var rsaParams = GetRsaParameters(keyBytes);
                    rsa.ImportParameters(rsaParams);
                }

                byte[] signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return ToBase64Url(signatureBytes);
            }
        }

        // --- MINI ASN.1 PARSER ---
        // Parses PKCS#8 Key blob to extract RSA Parameters (Modulus, Exponent, P, Q, DP, DQ, InverseQ, D)
        private static RSAParameters GetRsaParameters(byte[] keyBytes)
        {
            using (var stream = new MemoryStream(keyBytes))
            using (var reader = new BinaryReader(stream))
            {
                // PKCS#8 wrapper removal
                ushort twobytes = reader.ReadUInt16();
                if (twobytes == 0x8130) reader.ReadByte();
                else if (twobytes == 0x8230) reader.ReadInt16();
                else stream.Position = 0; // It might be raw already

                // Skip Version, Algorithm ID, etc. to get to the octet string
                // This is a simplified skip. Real ASN.1 parsing is complex, but Google keys usually follow a fixed structure.
                // We look for the start of the actual private key sequence (starts with 0x30 usually inside the Octet String)
                
                // To be safe and robust without 500 lines of code, we use a trick:
                // Google Keys are standard. We scan for the inner Sequence that contains the Version integer (0).
                
                // NOTE: If this fails, the key format is non-standard.
                // A safer bet for Unity without BouncyCastle is assuming the standard header offset.
                // Standard PKCS#8 header is 26 bytes for RSA.
                
                // Let's try the direct import if the header is stripped.
                // If stripping fails, we manually read the integers.
                
                try 
                {
                    // Helper to read ASN.1 length
                    int ReadLen() {
                        int length = reader.ReadByte();
                        if ((length & 0x80) == 0) return length;
                        int bytes = length & 0x7F;
                        int val = 0;
                        for (int i = 0; i < bytes; i++) val = (val << 8) | reader.ReadByte();
                        return val;
                    }

                    stream.Position = 0;
                    if (reader.ReadByte() != 0x30) throw new Exception("Invalid sequence");
                    ReadLen(); // Total length

                    // Version
                    if (reader.ReadByte() != 0x02) throw new Exception("Invalid version");
                    if (reader.ReadByte() != 0x00 && stream.ReadByte() != 0x01) { /* version 0 or 1 */ } 

                    // Algorithm Identifier
                    if (reader.ReadByte() != 0x30) throw new Exception("Invalid Algo ID");
                    int algIdLen = ReadLen();
                    stream.Seek(algIdLen, SeekOrigin.Current);

                    // Private Key Octet String
                    if (reader.ReadByte() != 0x04) throw new Exception("Invalid Octet String");
                    int octetLen = ReadLen();
                    
                    // Now inside the Octet String is the PKCS#1 structure
                    byte[] pkcs1Bytes = reader.ReadBytes(octetLen);
                    
                    // Now parse PKCS#1
                    using(var pStream = new MemoryStream(pkcs1Bytes))
                    using(var pReader = new BinaryReader(pStream))
                    {
                        int ReadP1Len() {
                            int length = pReader.ReadByte();
                            if ((length & 0x80) == 0) return length;
                            int bytes = length & 0x7F;
                            int val = 0;
                            for (int i = 0; i < bytes; i++) val = (val << 8) | pReader.ReadByte();
                            return val;
                        }
                        
                        byte[] ReadInteger() {
                            if (pReader.ReadByte() != 0x02) throw new Exception("Expected Integer");
                            int len = ReadP1Len();
                            byte[] data = pReader.ReadBytes(len);
                            // Remove leading zero if present (unsigned fix)
                            if (data[0] == 0x00) {
                                byte[] tmp = new byte[data.Length - 1];
                                Array.Copy(data, 1, tmp, 0, tmp.Length);
                                return tmp;
                            }
                            return data;
                        }

                        if (pReader.ReadByte() != 0x30) throw new Exception("Invalid PKCS1 Sequence");
                        ReadP1Len(); // Length

                        if (pReader.ReadByte() != 0x02) throw new Exception("Invalid Version"); // Version
                        ReadP1Len(); pReader.ReadByte(); // Skip version value (0)

                        var paramsRsa = new RSAParameters();
                        paramsRsa.Modulus = ReadInteger();
                        paramsRsa.Exponent = ReadInteger();
                        paramsRsa.D = ReadInteger();
                        paramsRsa.P = ReadInteger();
                        paramsRsa.Q = ReadInteger();
                        paramsRsa.DP = ReadInteger();
                        paramsRsa.DQ = ReadInteger();
                        paramsRsa.InverseQ = ReadInteger();

                        return paramsRsa;
                    }
                }
                catch
                {
                    // If manual parsing fails, we are likely dealing with a different encoding.
                    throw new Exception("Could not parse Private Key manually.");
                }
            }
        }

        private static string ToBase64Url(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}