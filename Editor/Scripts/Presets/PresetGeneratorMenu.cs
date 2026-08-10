using System.IO;
using UnityEditor;
using UnityEngine;
using IVH.Core.Actions;
using IVH.Core.IntelligentVirtualAgent.Presets;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    /// <summary>
    /// Generates the four sample <see cref="AgentPreset"/> assets bundled with the SDK.
    /// Accessed via <c>IVA SDK → Generate Sample Presets</c>. Lets users drop a working persona
    /// onto any agent without hand-filling the Inspector.
    /// </summary>
    public static class PresetGeneratorMenu
    {
        private const string DefaultOutputDir = "Assets/IVA_SDK_Presets";

        [MenuItem("IVA SDK/Generate Sample Presets")]
        public static void GenerateSamplePresets()
        {
            string dir = EditorUtility.SaveFolderPanel("Choose folder for IVA presets", "Assets", "IVA_SDK_Presets");
            if (string.IsNullOrEmpty(dir)) return;
            if (!dir.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Invalid folder", "Please pick a folder inside Assets/.", "OK");
                return;
            }
            string relative = "Assets" + dir.Substring(Application.dataPath.Length);
            Directory.CreateDirectory(dir);

            CreatePreset(relative, "TutorAgent", new AgentPresetData
            {
                agentName = "Aria",
                age = 28,
                occupation = "Patient math tutor",
                additionalDescription = "Uses short, encouraging explanations. Breaks problems into small steps. Never gives the answer directly — always asks a guiding question first.",
                voiceName = "Aoede",
            });

            CreatePreset(relative, "TherapistAgent", new AgentPresetData
            {
                agentName = "Sage",
                age = 35,
                occupation = "Empathetic listener (not a licensed therapist)",
                additionalDescription = "Calm, warm tone. Reflects the user's feelings before offering perspective. Prioritizes listening over advising. Recommends professional help when appropriate.",
                voiceName = "Kore",
            });

            CreatePreset(relative, "MuseumGuideAgent", new AgentPresetData
            {
                agentName = "Harper",
                age = 40,
                occupation = "Museum tour guide",
                additionalDescription = "Engaging storyteller. Pivots on user curiosity. Gives context on history, artists, and techniques. Invites questions after every explanation.",
                voiceName = "Puck",
            });

            CreatePreset(relative, "ResearchStudyAgent", new AgentPresetData
            {
                agentName = "Participant Assistant",
                age = 30,
                occupation = "Neutral research-study interlocutor",
                additionalDescription = "Deliberately neutral, low-emotion, fact-based delivery. Does not volunteer personal opinions. Follows the study protocol strictly. Speaks in short, deterministic sentences.",
                voiceName = "Charon",
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("IVA SDK",
                $"Generated 4 sample presets in {relative}. Drag one onto an agent's 'Preset' slot.", "OK");
        }

        private struct AgentPresetData
        {
            public string agentName;
            public int age;
            public string occupation;
            public string additionalDescription;
            public string voiceName;
        }

        private static void CreatePreset(string relativeDir, string fileName, AgentPresetData data)
        {
            string path = $"{relativeDir}/{fileName}.asset";
            var preset = ScriptableObject.CreateInstance<AgentPreset>();
            preset.agentName = data.agentName;
            preset.age = data.age;
            preset.occupation = data.occupation;
            preset.additionalDescription = data.additionalDescription;
            preset.voiceName = data.voiceName;
            AssetDatabase.CreateAsset(preset, path);
        }
    }
}
