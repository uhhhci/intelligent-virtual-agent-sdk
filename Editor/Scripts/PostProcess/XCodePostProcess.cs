using System.IO;
using System.Xml;
using IVH.Core.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace IVH.Core.IntelligentVirtualAgent
{
    public class XcodePostProcess : IPostprocessBuildWithReport
    {
        const string k_SchemeEnvVarName  = "GEMINI_API_KEY";
        const string k_SchemeRelPath =
            "Unity-VisionOS.xcodeproj/xcshareddata/xcschemes/Unity-VisionOS.xcscheme";

        public int callbackOrder => 1;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.VisionOS)
                return;

            string buildPath = report.summary.outputPath;
            string projPath = Path.Combine(buildPath, "Unity-VisionOS.xcodeproj", "project.pbxproj");

            PBXProject proj = new PBXProject();
            proj.ReadFromFile(projPath);

            string frameworkTarget = proj.GetUnityFrameworkTargetGuid();

            string[] flags = new string[]
            {
                "-ld_classic",
                "-Wl",
            };

            foreach (var flag in flags)
            {
                proj.AddBuildProperty(frameworkTarget, "OTHER_LDFLAGS", flag);
            }

            proj.WriteToFile(projPath);
            Debug.Log("✅ visionOS linker flags added.");

            SetSchemeEnvironmentVariable(buildPath);
        }

        static void SetSchemeEnvironmentVariable(string buildPath)
        {
            var schemePath = Path.Combine(buildPath, k_SchemeRelPath);
            if (!File.Exists(schemePath))
            {
                Debug.LogWarning($"[XcodePostProcess] Scheme not found at {schemePath}; skipping env var.");
                return;
            }

            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.Load(schemePath);

            var launchAction = doc.SelectSingleNode("//LaunchAction");
            if (launchAction == null)
            {
                Debug.LogWarning("[XcodePostProcess] No <LaunchAction> in scheme; skipping env var.");
                return;
            }

            var envVars = launchAction.SelectSingleNode("EnvironmentVariables")
                          ?? launchAction.AppendChild(doc.CreateElement("EnvironmentVariables"));

            XmlElement entry = null;
            foreach (XmlNode child in envVars.ChildNodes)
            {
                if (child is XmlElement el && el.GetAttribute("key") == k_SchemeEnvVarName)
                {
                    entry = el;
                    break;
                }
            }

            string k_SchemeEnvVarValue = GeneralModelHelper.GetGeminiApiKey();
            if (entry == null)
            {
                entry = doc.CreateElement("EnvironmentVariable");
                entry.SetAttribute("key", k_SchemeEnvVarName);
                entry.SetAttribute("isEnabled", "YES");
                envVars.AppendChild(entry);
            }
            entry.SetAttribute("value", k_SchemeEnvVarValue);

            doc.Save(schemePath);
            Debug.Log($"✅ visionOS scheme env var set: {k_SchemeEnvVarName}={k_SchemeEnvVarValue}");
        }
    }
}