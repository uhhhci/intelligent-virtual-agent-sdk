using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace IVH.Core.IntelligentVirtualAgent
{
    public class XcodePostProcess : IPostprocessBuildWithReport
    {
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
        }
    }
}