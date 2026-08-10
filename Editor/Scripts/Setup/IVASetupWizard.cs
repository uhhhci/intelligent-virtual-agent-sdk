using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace IVH.Core.EditorScripts.Setup
{
    /// <summary>
    /// One-stop configuration window for the IVA SDK. Shows missing dependencies with one-click
    /// install buttons, lets the user paste in credentials without hand-editing JSON files, and
    /// runs a few sanity checks on the current scene.
    /// </summary>
    public class IVASetupWizard : EditorWindow
    {
        private enum Tab { Dependencies, Credentials, SanityChecks }
        private Tab _tab;

        // Dependency status cache
        private ListRequest _listRequest;
        private Dictionary<string, bool> _installed;
        private bool _listInFlight;

        // Credentials state
        private string _apiKey = "";
        private string _serviceAccountPath = "";
        private string _status;

        /// <summary>
        /// Dependency catalogue used by the Dependencies tab.
        /// <list type="bullet">
        ///   <item><c>id</c> — Unity Package Manager name we look up to detect installed state.</item>
        ///   <item><c>label</c> — display string in the wizard.</item>
        ///   <item><c>installSource</c> — string passed to <c>Client.Add</c>. Empty falls back to <c>id</c>
        ///     (works for Unity registry packages). Set to a git URL for packages distributed via Git.</item>
        ///   <item><c>required</c> — when true, the SDK won't function correctly without it. Drawn in red.</item>
        /// </list>
        /// </summary>
        private static readonly (string id, string label, string installSource, bool required)[] Deps = new[]
        {
            ("com.unity.animation.rigging",          "Animation Rigging",                                   "",                                                                                       true),
            ("com.unity.nuget.newtonsoft-json",      "Newtonsoft JSON",                                     "",                                                                                       true),
            ("com.oculus.unity.integration.lip-sync","Oculus Lip Sync (required, for avatar lip-sync)",     "https://github.com/Trisgram/com.oculus.unity.integration.lip-sync.git",                  true),
            ("com.meta.xr.sdk.core",                 "Meta XR SDK (optional, for VR)",                      "",                                                                                       false),
            ("com.whisper.unity",                    "Whisper Unity (optional, local STT)",                 "https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity",          false),
            ("com.gpt4all.unity",                    "GPT4All Unity (optional, local LLM)",                 "https://github.com/Macoron/gpt4all.unity.git?path=/Packages/com.gpt4all.unity",          false),
        };

        [MenuItem("IVA SDK/Setup Wizard")]
        public static void Open()
        {
            var win = GetWindow<IVASetupWizard>(true, "IVA SDK Setup", true);
            win.minSize = new Vector2(480, 420);
            win.RefreshDependencies();
        }

        private void OnEnable() => RefreshDependencies();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Intelligent Virtual Agent SDK — Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _tab = (Tab)GUILayout.Toolbar((int)_tab,
                new[] { "Dependencies", "Credentials", "Sanity Checks" });

            EditorGUILayout.Space();

            switch (_tab)
            {
                case Tab.Dependencies: DrawDependenciesTab(); break;
                case Tab.Credentials: DrawCredentialsTab(); break;
                case Tab.SanityChecks: DrawSanityChecksTab(); break;
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        private void DrawDependenciesTab()
        {
            EditorGUILayout.LabelField("Unity Package Dependencies", EditorStyles.boldLabel);

            if (_listInFlight)
            {
                EditorGUILayout.HelpBox("Checking installed packages...", MessageType.Info);
                return;
            }
            if (_installed == null)
            {
                if (GUILayout.Button("Refresh")) RefreshDependencies();
                return;
            }

            foreach (var dep in Deps)
            {
                bool present = _installed.TryGetValue(dep.id, out bool v) && v;
                GUILayout.BeginHorizontal();
                GUI.color = present ? new Color(0.6f, 1f, 0.6f) : (dep.required ? new Color(1f, 0.6f, 0.6f) : Color.white);
                GUILayout.Label(present ? "✓" : (dep.required ? "✗" : "○"), GUILayout.Width(20));
                GUI.color = Color.white;
                GUILayout.Label(dep.label, GUILayout.ExpandWidth(true));
                if (!present)
                {
                    if (GUILayout.Button("Install", GUILayout.Width(70)))
                    {
                        // `Client.Add` accepts both Unity registry names and git URLs in the same
                        // argument. We prefer the explicit `installSource` (git URL) when provided
                        // because the package's manifest name (used for detection) is not what
                        // Unity needs to fetch a git package.
                        string source = string.IsNullOrEmpty(dep.installSource) ? dep.id : dep.installSource;
                        Client.Add(source);
                        _status = $"Installing {dep.id}... see Package Manager for progress.";
                    }
                }
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh")) RefreshDependencies();
        }

        private void DrawCredentialsTab()
        {
            string aiApiDir = GetAiApiDir();
            EditorGUILayout.HelpBox(
                $"Credentials live in {aiApiDir}. Paste below and click Save; the wizard will write the correct JSON shape for you.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Google AI Studio (free tier)", EditorStyles.boldLabel);
            _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            if (GUILayout.Button("Save API Key → auth.json"))
            {
                SaveApiKey(aiApiDir, _apiKey);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Google Cloud Vertex AI (paid)", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            _serviceAccountPath = EditorGUILayout.TextField("Service Account JSON", _serviceAccountPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string p = EditorUtility.OpenFilePanel("Select service-account JSON", "", "json");
                if (!string.IsNullOrEmpty(p)) _serviceAccountPath = p;
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Copy to ~/.aiapi/service_account.json"))
            {
                SaveServiceAccount(aiApiDir, _serviceAccountPath);
            }
        }

        private void DrawSanityChecksTab()
        {
            EditorGUILayout.LabelField("External Package Dependencies", EditorStyles.boldLabel);

            // Same source of truth as the Dependencies tab, but surfaced here too so users on the
            // Sanity Checks tab immediately see what's missing. Only the git-URL deps are repeated;
            // the Unity registry ones auto-install via package.json#dependencies.
            if (_installed == null || _listInFlight)
            {
                EditorGUILayout.HelpBox("Checking installed packages...", MessageType.Info);
            }
            else
            {
                foreach (var dep in Deps)
                {
                    if (string.IsNullOrEmpty(dep.installSource)) continue; // skip registry deps — they auto-install
                    bool present = _installed.TryGetValue(dep.id, out bool v) && v;
                    string hint = present
                        ? null
                        : (dep.required
                            ? $"Required. Click Install on the Dependencies tab, or paste this git URL into Package Manager: {dep.installSource}"
                            : $"Optional. Install from the Dependencies tab if you need this feature.");
                    DrawCheck(dep.label, present, hint, severity: dep.required ? CheckSeverity.Error : CheckSeverity.Warning);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene & Config Sanity", EditorStyles.boldLabel);

            string aiApiDir = GetAiApiDir();
            bool hasAuth = File.Exists(Path.Combine(aiApiDir, "auth.json"));
            bool hasSvc = File.Exists(Path.Combine(aiApiDir, "service_account.json"));

            DrawCheck("~/.aiapi/auth.json exists", hasAuth, "Needed for Google AI Studio agents.");
            DrawCheck("~/.aiapi/service_account.json exists", hasSvc, "Needed for Vertex AI agents.");

            int micCount = Microphone.devices.Length;
            DrawCheck($"{micCount} microphone(s) detected", micCount > 0, "Realtime voice requires at least one mic.");

            int camCount = WebCamTexture.devices.Length;
            DrawCheck($"{camCount} webcam(s) detected", camCount > 0, "Only needed for vision-enabled agents with WebCam target.");
        }

        private enum CheckSeverity { Warning, Error }

        private static void DrawCheck(string label, bool ok, string hint, CheckSeverity severity = CheckSeverity.Warning)
        {
            Color failColor = severity == CheckSeverity.Error
                ? new Color(1f, 0.45f, 0.45f)
                : new Color(1f, 0.7f, 0.4f);
            string failGlyph = severity == CheckSeverity.Error ? "✗" : "!";

            GUILayout.BeginHorizontal();
            GUI.color = ok ? new Color(0.6f, 1f, 0.6f) : failColor;
            GUILayout.Label(ok ? "✓" : failGlyph, GUILayout.Width(20));
            GUI.color = Color.white;
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (!ok && !string.IsNullOrEmpty(hint))
            {
                EditorGUILayout.HelpBox(hint, severity == CheckSeverity.Error ? MessageType.Error : MessageType.None);
            }
        }

        private void RefreshDependencies()
        {
            _listInFlight = true;
            _installed = null;
            // includeIndirectDependencies=true is required because Animation Rigging and Newtonsoft JSON
            // arrive via this SDK's own package.json#dependencies — i.e. as indirect dependencies of the
            // user's project. With the flag off, Client.List omits them and they appear "missing" even
            // when fully installed.
            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += WaitForList;
        }

        private void WaitForList()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;
            _installed = new Dictionary<string, bool>();
            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (var pkg in _listRequest.Result)
                {
                    _installed[pkg.name] = true;
                }
            }
            else
            {
                _status = $"Could not list packages: {_listRequest.Error?.message}";
            }
            _listInFlight = false;
            EditorApplication.update -= WaitForList;
            Repaint();
        }

        private static string GetAiApiDir()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".aiapi");
        }

        private void SaveApiKey(string dir, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _status = "API key is empty.";
                return;
            }
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "auth.json");
            File.WriteAllText(path, $"{{\n  \"api_key\": \"{apiKey.Trim()}\"\n}}\n");
            _status = $"Wrote {path}";
            _apiKey = "";
        }

        private void SaveServiceAccount(string dir, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                _status = "Please select a valid service-account JSON file.";
                return;
            }
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, "service_account.json");
            File.Copy(sourcePath, dest, true);
            _status = $"Copied service account to {dest}";
        }
    }
}
