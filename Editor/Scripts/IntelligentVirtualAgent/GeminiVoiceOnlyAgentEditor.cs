using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using IVH.Core.IntelligentVirtualAgent;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    [CustomEditor(typeof(GeminiVoiceOnlyAgent))]
    public class GeminiVoiceOnlyAgentEditor : Editor
    {
        private GeminiVoiceOnlyAgent agent;

        // Configuration
        private SerializedProperty voiceNameProp;
        private SerializedProperty autoConnectProp;
        private SerializedProperty showThinkingProcessProp;
        private SerializedProperty showSpeechTranscriptsProp;
        private SerializedProperty systemInstructionProp;

        // Audio Input
        private SerializedProperty microphoneProp;
        private SerializedProperty inputGainProp;
        
        // VAD
        private SerializedProperty enableVocalInterruptionProp;
        private SerializedProperty muteMicWhileTalkingProp;
        private SerializedProperty echoInterruptionThresholdProp;
        private SerializedProperty voiceDetectionThresholdProp;
        private SerializedProperty useVocalFrequencyFilterProp;
        private SerializedProperty interruptionDebounceTimeProp;

        // UI
        private SerializedProperty logTextDisplayProp;
        private SerializedProperty scrollRectProp;

        private void OnEnable()
        {
            agent = target as GeminiVoiceOnlyAgent;

            voiceNameProp = serializedObject.FindProperty("voiceName");
            autoConnectProp = serializedObject.FindProperty("autoConnectOnStart");
            showThinkingProcessProp = serializedObject.FindProperty("showThinkingProcess");
            showSpeechTranscriptsProp = serializedObject.FindProperty("showSpeechTranscripts");
            systemInstructionProp = serializedObject.FindProperty("systemInstruction");

            microphoneProp = serializedObject.FindProperty("microphoneDeviceName");
            inputGainProp = serializedObject.FindProperty("inputGain");

            enableVocalInterruptionProp = serializedObject.FindProperty("enableVocalInterruption");
            muteMicWhileTalkingProp = serializedObject.FindProperty("muteMicWhileTalking");
            echoInterruptionThresholdProp = serializedObject.FindProperty("echoInterruptionThreshold");
            voiceDetectionThresholdProp = serializedObject.FindProperty("voiceDetectionThreshold");
            useVocalFrequencyFilterProp = serializedObject.FindProperty("useVocalFrequencyFilter");
            interruptionDebounceTimeProp = serializedObject.FindProperty("interruptionDebounceTime");

            logTextDisplayProp = serializedObject.FindProperty("logTextDisplay");
            scrollRectProp = serializedObject.FindProperty("scrollRect");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw standard unhandled properties (hiding the ones we layout custom below)
            DrawPropertiesExcluding(serializedObject,
                "m_Script",
                "voiceName", "autoConnectOnStart", "showThinkingProcess", "showSpeechTranscripts", "systemInstruction",
                "microphoneDeviceName", "inputGain",
                "enableVocalInterruption", "muteMicWhileTalking", "echoInterruptionThreshold", "voiceDetectionThreshold", "useVocalFrequencyFilter", "interruptionDebounceTime",
                "logTextDisplay", "scrollRect");

            EditorGUILayout.Space();

            // 2. Gemini Configuration
            EditorGUILayout.LabelField("Gemini Voice Settings", EditorStyles.boldLabel);
            
            string[] voices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };
            int selectedVoice = System.Array.IndexOf(voices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0;
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, voices);
            voiceNameProp.stringValue = voices[selectedVoice];

            EditorGUILayout.PropertyField(autoConnectProp, new GUIContent("Auto Connect"));
            EditorGUILayout.PropertyField(showThinkingProcessProp, new GUIContent("Show Thinking Process"));
            EditorGUILayout.PropertyField(showSpeechTranscriptsProp, new GUIContent("Show Speech Transcripts"));
            if (showSpeechTranscriptsProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Renders the user's spoken command and Gemini's spoken reply as text in the log panel. " +
                    "Auto-enables server-side transcription on the GeminiRealtimeWrapper.",
                    MessageType.None);
            }
            EditorGUILayout.PropertyField(systemInstructionProp, new GUIContent("System Instruction"));

            EditorGUILayout.Space();

            // 3. Audio Input
            EditorGUILayout.LabelField("Audio & Input", EditorStyles.boldLabel);

            string[] mics = Microphone.devices;
            if (mics.Length > 0)
            {
                int micIndex = System.Array.IndexOf(mics, microphoneProp.stringValue);
                if (micIndex == -1) micIndex = 0;
                micIndex = EditorGUILayout.Popup("Microphone Device", micIndex, mics);
                microphoneProp.stringValue = mics[micIndex];
            }
            else
            {
                EditorGUILayout.HelpBox("No microphones detected by Unity.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(inputGainProp, new GUIContent("Mic Gain"));

            // 4. VAD & Interruption
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VAD & Interruption Logic", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(muteMicWhileTalkingProp, new GUIContent("Prevent Echo (Mute Mic While Talking)"));
            EditorGUILayout.PropertyField(enableVocalInterruptionProp, new GUIContent("Enable Vocal Interruption"));

            if (enableVocalInterruptionProp.boolValue || muteMicWhileTalkingProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(voiceDetectionThresholdProp, new GUIContent("Normal Voice Threshold"));

                if (muteMicWhileTalkingProp.boolValue && enableVocalInterruptionProp.boolValue)
                {
                    EditorGUILayout.PropertyField(echoInterruptionThresholdProp, new GUIContent("Echo Interruption Threshold"));
                    EditorGUILayout.HelpBox("Because 'Prevent Echo' is ON, interruption requires a louder voice to overcome the speaker's echo volume.", MessageType.Info);
                }

                EditorGUILayout.PropertyField(useVocalFrequencyFilterProp, new GUIContent("Use Frequency Filter"));
                EditorGUILayout.PropertyField(interruptionDebounceTimeProp, new GUIContent("Debounce Time (s)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 5. UI References
            EditorGUILayout.LabelField("UI Elements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(logTextDisplayProp, new GUIContent("Log Text Display"));
            EditorGUILayout.PropertyField(scrollRectProp, new GUIContent("Scroll Rect"));

            EditorGUILayout.Space(4);

            bool hasUi = logTextDisplayProp.objectReferenceValue != null && scrollRectProp.objectReferenceValue != null;
            string buttonLabel = hasUi
                ? "Recreate Transparent Side-Panel UI"
                : "Create Transparent Side-Panel UI";

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f); // light blue
            if (GUILayout.Button(buttonLabel, GUILayout.Height(26)))
            {
                if (!hasUi || EditorUtility.DisplayDialog(
                    "Replace existing UI panel?",
                    "A log panel is already wired up. Build a new one and re-wire the references?\n\nThe old GameObjects are left in the scene — delete them manually if you don't want them.",
                    "Replace", "Cancel"))
                {
                    serializedObject.ApplyModifiedProperties();
                    CreateTransparentSidePanel();
                    serializedObject.Update();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox(
                "Builds a Canvas → transparent vertical panel anchored to the far-left edge with a thin visible border, " +
                "a ScrollRect, and a Text element wired up to the fields above. Switch the Canvas render mode to World Space if you need an XR-friendly panel.",
                MessageType.None);

            EditorGUILayout.Space(10);

            // 6. Runtime Controls
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Live Controls", EditorStyles.boldLabel);
                
                // Reconnect Button
                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f); // Orange
                if (GUILayout.Button("Force Reconnect (New Session)", GUILayout.Height(30)))
                {
                    agent.Reconnect();
                }
                GUI.backgroundColor = Color.white; // Reset color
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Builds a styled conversation-log UI in the active scene and wires it into the agent's
        /// <c>logTextDisplay</c> / <c>scrollRect</c> fields. The panel is anchored to the far-left
        /// edge so it stays out of the user's main field of view, uses a heavily transparent
        /// background with a thin outline so the boundary is still visible, and supports rich
        /// text + word-wrap for the streamed transcript fragments.
        /// </summary>
        private void CreateTransparentSidePanel()
        {
            // 1. Canvas — reuse the first Screen-Space-Overlay canvas in the scene, or create one.
            Canvas canvas = FindSceneObject<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("IVA Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                Undo.RegisterCreatedObjectUndo(canvasGo, "Create IVA Canvas");
            }

            // 2. EventSystem — ScrollRect needs one for drag input. Idempotent.
            if (FindSceneObject<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            // 3. Outer panel — anchored to the left edge, stretched vertically. Heavily transparent
            //    fill with an Outline so the boundary stays visible against any background.
            var panelGo = new GameObject("IVA Conversation Log Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            Undo.RegisterCreatedObjectUndo(panelGo, "Create IVA Log Panel");
            panelGo.transform.SetParent(canvas.transform, false);

            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot    = new Vector2(0f, 0.5f);
            panelRt.anchoredPosition = new Vector2(16f, 0f); // 16 px gap from the left edge
            panelRt.sizeDelta = new Vector2(340f, -32f);     // 340 wide, 16 px gap top + bottom

            var panelImg = panelGo.GetComponent<Image>();
            panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded 9-slice
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.05f, 0.06f, 0.09f, 0.18f); // ~18% opacity — very transparent
            panelImg.raycastTarget = true; // catch clicks so the panel can be dragged-scrolled

            var panelOutline = panelGo.GetComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.35f); // faint visible boundary
            panelOutline.effectDistance = new Vector2(1f, -1f);

            // 4. ScrollRect — fills the panel with a small inner margin.
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(10f, 10f);
            scrollRt.offsetMax = new Vector2(-10f, -10f);

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            // 5. Viewport — needs an Image + Mask so the content clips to the panel bounds. The Image
            //    alpha is essentially zero (Mask requires a Graphic, but we don't want it visible).
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportRt.pivot = new Vector2(0f, 1f);

            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.004f); // effectively invisible, still raycasts
            viewportImg.raycastTarget = true;

            var mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            // 6. Content — vertical layout, auto-resizing to fit the streamed text.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 7. Text — legacy UI Text matches the field type on GeminiVoiceOnlyAgent.
            var textGo = new GameObject("Log Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            textGo.transform.SetParent(contentGo.transform, false);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 2022.3+ shipped legacy font
            text.fontSize = 14;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "<i>System: Awaiting first connection...</i>";

            var shadow = textGo.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f); // legibility over busy backgrounds
            shadow.effectDistance = new Vector2(1f, -1f);

            // 8. Bind ScrollRect to its viewport + content references.
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            // 9. Wire up the agent's serialized fields.
            logTextDisplayProp.objectReferenceValue = text;
            scrollRectProp.objectReferenceValue = scrollRect;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(agent);

            Selection.activeGameObject = panelGo;
            EditorGUIUtility.PingObject(panelGo);
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}