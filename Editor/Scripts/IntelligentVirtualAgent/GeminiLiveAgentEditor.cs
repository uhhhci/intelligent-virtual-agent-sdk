using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector;
using IVH.Core.UI;
using UnityEngine.AI;

namespace IVH.Core.IntelligentVirtualAgent.EditorScripts
{
    /// <summary>
    /// Custom inspector for <see cref="GeminiLiveAgent"/>. Organizes the ~40 serialized fields
    /// across the agent and its base into four tabs to keep the inspector scan-able. v3.0.1 —
    /// the UI tab now scaffolds a minimalist collapsible settings drawer instead of the always-
    /// visible side panel.
    /// </summary>
    [CustomEditor(typeof(GeminiLiveAgent))]
    public class GeminiLiveAgentEditor : Editor
    {
        private const string TabPrefKey = "IVA.GeminiLiveAgent.InspectorTab";
        private const string PersonaFoldKey = "IVA.GeminiLiveAgent.PersonaFold";
        private const string AvatarFoldKey = "IVA.GeminiLiveAgent.AvatarFold";
        private static readonly string[] TabLabels = { "Basic", "Conversation", "Vision", "Animation", "UI" };
        private int _tabIndex;

        private GeminiLiveAgent agent;

        // Basic — voice + mic
        private SerializedProperty voiceNameProp;
        private SerializedProperty autoConnectProp;
        private SerializedProperty microphoneProp;
        private SerializedProperty inputGainProp;

        // Conversation — VAD/interruption/reaction
        private SerializedProperty enableVocalInterruptionProp;
        private SerializedProperty muteMicWhileTalkingProp;
        private SerializedProperty echoInterruptionThresholdProp;
        private SerializedProperty voiceDetectionThresholdProp;
        private SerializedProperty useVocalFrequencyFilterProp;
        private SerializedProperty interruptionDebounceTimeProp;
        private SerializedProperty postInterruptDropTimeoutProp;

        // Vision
        private SerializedProperty visionProp;
        private SerializedProperty targetCameraTypeProp;
        private SerializedProperty resolutionProp;
        private SerializedProperty rawImageProp;
        private SerializedProperty selectedWebCamNameProp;
        private SerializedProperty visionUpdateFrequencyProp;

        // Animation
        private SerializedProperty enableLocomotionProp;
        private SerializedProperty characterTypeProp;

        // UI panel
        private SerializedProperty showSpeechTranscriptsProp;
        private SerializedProperty logTextDisplayProp;
        private SerializedProperty scrollRectProp;

        private bool _personaFold = true;
        private bool _avatarFold = true;

        // locomotion-related
        private bool isNavMeshInScene;
        private bool CheckForNavMesh()
        {
            return NavMesh.SamplePosition(Vector3.zero, out _, 1000000f, NavMesh.AllAreas);
        }

        public void OnEnable()
        {
            _tabIndex = EditorPrefs.GetInt(TabPrefKey, 0);
            _personaFold = EditorPrefs.GetBool(PersonaFoldKey, true);
            _avatarFold = EditorPrefs.GetBool(AvatarFoldKey, true);

            agent = target as GeminiLiveAgent;

            voiceNameProp = serializedObject.FindProperty("voiceName");
            autoConnectProp = serializedObject.FindProperty("autoConnectOnStart");
            microphoneProp = serializedObject.FindProperty("microphoneDeviceName");
            inputGainProp = serializedObject.FindProperty("inputGain");

            enableVocalInterruptionProp = serializedObject.FindProperty("enableVocalInterruption");
            muteMicWhileTalkingProp = serializedObject.FindProperty("muteMicWhileTalking");
            echoInterruptionThresholdProp = serializedObject.FindProperty("echoInterruptionThreshold");
            voiceDetectionThresholdProp = serializedObject.FindProperty("voiceDetectionThreshold");
            useVocalFrequencyFilterProp = serializedObject.FindProperty("useVocalFrequencyFilter");
            interruptionDebounceTimeProp = serializedObject.FindProperty("interruptionDebounceTime");
            postInterruptDropTimeoutProp = serializedObject.FindProperty("postInterruptDropTimeoutSeconds");

            visionProp = serializedObject.FindProperty("vision");
            targetCameraTypeProp = serializedObject.FindProperty("targetCameraType");
            resolutionProp = serializedObject.FindProperty("resolution");
            rawImageProp = serializedObject.FindProperty("rawImage");
            selectedWebCamNameProp = serializedObject.FindProperty("selectedWebCamName");
            visionUpdateFrequencyProp = serializedObject.FindProperty("visionUpdateFrequency");

            enableLocomotionProp = serializedObject.FindProperty("enableLocomotion");
            characterTypeProp = serializedObject.FindProperty("characterType");

            showSpeechTranscriptsProp = serializedObject.FindProperty("showSpeechTranscripts");
            logTextDisplayProp = serializedObject.FindProperty("logTextDisplay");
            scrollRectProp = serializedObject.FindProperty("scrollRect");

            isNavMeshInScene = CheckForNavMesh();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            _tabIndex = GUILayout.Toolbar(_tabIndex, TabLabels);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetInt(TabPrefKey, _tabIndex);

            EditorGUILayout.Space();

            switch (_tabIndex)
            {
                case 0: DrawBasicTab(); break;
                case 1: DrawConversationTab(); break;
                case 2: DrawVisionTab(); break;
                case 3: DrawAnimationTab(); break;
                case 4: DrawUITab(); break;
            }

            EditorGUILayout.Space(10);
            DrawRuntimeControls();

            serializedObject.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------------
        // Tab 1 — Basic
        // ---------------------------------------------------------------------
        private void DrawBasicTab()
        {
            EditorGUI.BeginChangeCheck();
            _personaFold = EditorGUILayout.Foldout(_personaFold, "Agent Persona", true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(PersonaFoldKey, _personaFold);
            if (_personaFold)
            {
                EditorGUI.indentLevel++;
                DrawByName("preset");
                DrawByName("agentName");
                DrawByName("age");
                DrawByName("gender");
                DrawByName("occupation");
                DrawByName("language");
                DrawByName("additionalDescription");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            _avatarFold = EditorGUILayout.Foldout(_avatarFold, "Avatar & Animation Setup", true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(AvatarFoldKey, _avatarFold);
            if (_avatarFold)
            {
                EditorGUI.indentLevel++;
                DrawByName("agentPrefab");
                DrawByName("characterType");
                DrawByName("bodyAnimationControllerType");
                DrawByName("animatorController");
                DrawByName("emotionHandlerType");
                DrawByName("descriptionMode");
                DrawByName("bodyActionFilter");
                DrawByName("facialExpressionFilter");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gemini Live", EditorStyles.boldLabel);

            string[] voices = { "Puck", "Charon", "Kore", "Fenrir", "Aoede", "Leda", "Orus", "Zephyr" };
            int selectedVoice = System.Array.IndexOf(voices, voiceNameProp.stringValue);
            if (selectedVoice == -1) selectedVoice = 0;
            selectedVoice = EditorGUILayout.Popup("Agent Voice", selectedVoice, voices);
            voiceNameProp.stringValue = voices[selectedVoice];

            EditorGUILayout.PropertyField(autoConnectProp, new GUIContent("Auto Connect"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio Input", EditorStyles.boldLabel);

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
        }

        // ---------------------------------------------------------------------
        // Tab 2 — Conversation
        // ---------------------------------------------------------------------
        private void DrawConversationTab()
        {
            EditorGUILayout.LabelField("Voice Activity Detection & Interruption", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(muteMicWhileTalkingProp, new GUIContent("Prevent Echo (Mute Mic While Talking)"));
            EditorGUILayout.PropertyField(enableVocalInterruptionProp, new GUIContent("Enable Vocal Interruption"));

            if (enableVocalInterruptionProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(voiceDetectionThresholdProp, new GUIContent("Voice Detection Threshold"));
                if (muteMicWhileTalkingProp.boolValue)
                {
                    EditorGUILayout.PropertyField(echoInterruptionThresholdProp, new GUIContent("Echo Interruption Threshold"));
                    EditorGUILayout.HelpBox("With Prevent Echo on, interrupting voice must overcome the speaker's echo. Tune the threshold above.", MessageType.None);
                }
                EditorGUILayout.PropertyField(useVocalFrequencyFilterProp, new GUIContent("Use Frequency Filter"));
                EditorGUILayout.PropertyField(interruptionDebounceTimeProp, new GUIContent("Debounce Time (s)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Post-Interrupt Safety", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(postInterruptDropTimeoutProp, new GUIContent("Drop Timeout (s)"));
            EditorGUILayout.HelpBox("After client-side interrupt, drop incoming agent audio until Gemini's server-side ack arrives, capped at this timeout. Prevents the prior turn from resuming.", MessageType.None);
        }

        // ---------------------------------------------------------------------
        // Tab 3 — Vision
        // ---------------------------------------------------------------------
        private void DrawVisionTab()
        {
            EditorGUILayout.PropertyField(visionProp);

            if (visionProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetCameraTypeProp);

                if (targetCameraTypeProp != null
                    && targetCameraTypeProp.enumNames.Length > targetCameraTypeProp.enumValueIndex
                    && targetCameraTypeProp.enumNames[targetCameraTypeProp.enumValueIndex] == "WebCam")
                {
                    WebCamDevice[] devices = WebCamTexture.devices;
                    if (devices.Length > 0 && selectedWebCamNameProp != null)
                    {
                        string[] deviceNames = new string[devices.Length];
                        for (int i = 0; i < devices.Length; i++) deviceNames[i] = devices[i].name;
                        int camIndex = System.Array.IndexOf(deviceNames, selectedWebCamNameProp.stringValue);
                        if (camIndex == -1) camIndex = 0;
                        camIndex = EditorGUILayout.Popup("Webcam Device", camIndex, deviceNames);
                        selectedWebCamNameProp.stringValue = deviceNames[camIndex];
                    }
                }

                EditorGUILayout.PropertyField(resolutionProp);
                EditorGUILayout.PropertyField(rawImageProp);

                // One-click webcam preview scaffolder. Creates a Canvas (reuses an existing one if
                // present) plus a RawImage in the bottom-right corner and wires it to the agent's
                // rawImage field. Independent of the full HUD scaffolder on the UI tab — useful when
                // the developer just wants the raw webcam feed mirrored on-screen for debugging.
                bool isWebCam = targetCameraTypeProp != null
                    && targetCameraTypeProp.enumNames.Length > targetCameraTypeProp.enumValueIndex
                    && targetCameraTypeProp.enumNames[targetCameraTypeProp.enumValueIndex] == "WebCam";
                using (new EditorGUI.DisabledScope(!isWebCam))
                {
                    string previewButtonLabel = rawImageProp.objectReferenceValue == null
                        ? "Create Webcam Preview Canvas"
                        : "Recreate Webcam Preview Canvas";
                    GUI.backgroundColor = new Color(0.35f, 0.54f, 1f);
                    if (GUILayout.Button(previewButtonLabel, GUILayout.Height(22)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        CreateWebcamPreviewCanvas();
                        serializedObject.Update();
                    }
                    GUI.backgroundColor = Color.white;
                }
                if (!isWebCam)
                {
                    EditorGUILayout.HelpBox(
                        "Switch Target Camera Type to WebCam to enable the preview-canvas scaffolder.",
                        MessageType.None);
                }

                EditorGUILayout.PropertyField(visionUpdateFrequencyProp, new GUIContent("Update Frequency (s)"));
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Scaffolds a standalone webcam-preview surface (Canvas + RawImage anchored bottom-right) and
        /// assigns the RawImage to the agent's <c>rawImage</c> field so <c>AgentBase.Awake</c> /
        /// <c>EnsureWebCamReady</c> can paint frames into it at runtime. Reuses any existing scene
        /// Canvas / EventSystem so calling this twice doesn't litter the scene with duplicates.
        /// </summary>
        private void CreateWebcamPreviewCanvas()
        {
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

            if (FindSceneObject<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            const float previewWidth = 320f;
            const float previewHeight = 180f; // 16:9
            const float screenPadding = 20f;

            var rootGo = new GameObject("IVA Webcam Preview", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rootGo, "Create Webcam Preview");
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(1f, 0f);
            rootRt.anchorMax = new Vector2(1f, 0f);
            rootRt.pivot = new Vector2(1f, 0f);
            rootRt.anchoredPosition = new Vector2(-screenPadding, screenPadding);
            rootRt.sizeDelta = new Vector2(previewWidth, previewHeight);

            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdropGo.transform.SetParent(rootGo.transform, false);
            StretchToParent((RectTransform)backdropGo.transform, new RectOffset(0, 0, 0, 0));
            var backdrop = backdropGo.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.85f);
            backdrop.raycastTarget = false;

            var textureGo = new GameObject("Texture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            textureGo.transform.SetParent(rootGo.transform, false);
            StretchToParent((RectTransform)textureGo.transform, new RectOffset(2, 2, 2, 2));
            var rawImage = textureGo.GetComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;

            rawImageProp.objectReferenceValue = rawImage;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(agent);

            Selection.activeGameObject = rootGo;
            EditorGUIUtility.PingObject(rootGo);
        }

        // ---------------------------------------------------------------------
        // Tab 4 — Animation
        // ---------------------------------------------------------------------
        private void DrawAnimationTab()
        {
            EditorGUILayout.LabelField("Locomotion (Experimental)", EditorStyles.boldLabel);
            if (characterTypeProp.enumNames[characterTypeProp.enumValueIndex] == "CC4OrDIDIMO" && isNavMeshInScene)
            {
                EditorGUILayout.PropertyField(enableLocomotionProp);
            }
            else
            {
                EditorGUILayout.HelpBox("Locomotion needs a CC4/DIDIMO character + a NavMesh in the scene.", MessageType.None);
            }
        }

        // ---------------------------------------------------------------------
        // Tab 5 — UI (in-game log panel + runtime controls)
        // ---------------------------------------------------------------------
        private void DrawUITab()
        {
            EditorGUILayout.LabelField("In-Game Conversation Log", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showSpeechTranscriptsProp, new GUIContent("Show Speech Transcripts"));
            if (showSpeechTranscriptsProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Renders the user's spoken command and Gemini's spoken reply as text in the log panel. " +
                    "Auto-enables server-side transcription on the GeminiRealtimeWrapper.",
                    MessageType.None);
            }
            EditorGUILayout.PropertyField(logTextDisplayProp, new GUIContent("Log Text Display"));
            EditorGUILayout.PropertyField(scrollRectProp, new GUIContent("Scroll Rect"));

            EditorGUILayout.Space(4);

            bool hasUi = logTextDisplayProp.objectReferenceValue != null && scrollRectProp.objectReferenceValue != null;
            string buttonLabel = hasUi
                ? "Recreate Dual-Panel HUD"
                : "Create Dual-Panel HUD";

            GUI.backgroundColor = new Color(0.35f, 0.54f, 1f);
            if (GUILayout.Button(buttonLabel, GUILayout.Height(26)))
            {
                if (!hasUi || EditorUtility.DisplayDialog(
                    "Replace existing UI panel?",
                    "A panel is already wired up. Build a new HUD and re-wire the references?\n\nThe old GameObjects are left in the scene — delete them manually if you don't want them.",
                    "Replace", "Cancel"))
                {
                    serializedObject.ApplyModifiedProperties();
                    CreateMinimalSettingsPanel();
                    serializedObject.Update();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox(
                "Builds two persistent toggle buttons in the top-left: Transcription toggles a semi-transparent " +
                "transcription panel (visible by default), Settings toggles a more opaque settings panel " +
                "(hidden by default). Settings sections: Connection (Reconnect), Microphone, Camera " +
                "(source switch, live preview, vision on/off toggle, stream-frequency slider), " +
                "Conversation (vocal-interruption toggle, prevent-echo toggle), Actions (Force Interrupt). " +
                "At runtime, the settings panel is draggable via its title bar and resizable via the " +
                "bottom-right corner grip. The transcription panel is also resizable via its bottom-right grip.",
                MessageType.None);
        }

        // ---------------------------------------------------------------------
        // Runtime control buttons (always visible at the bottom of the inspector)
        // ---------------------------------------------------------------------
        private void DrawRuntimeControls()
        {
            if (!Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Setup Virtual Agent", GUILayout.Height(25))) agent.SetupVirtualAgent();
                if (GUILayout.Button("Clear Virtual Agent", GUILayout.Height(25))) agent.DestroyVirtualAgent();
                GUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Live Controls", EditorStyles.boldLabel);

                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button("Reconnect Gemini", GUILayout.Height(28))) agent.Connect();

                GUILayout.Space(4);

                bool currentVisionState = visionProp.boolValue;
                string buttonText = currentVisionState ? "Stop Vision Stream" : "Start Vision Stream";
                GUI.backgroundColor = currentVisionState ? new Color(1f, 0.7f, 0.7f) : new Color(0.7f, 0.8f, 1f);
                if (GUILayout.Button(buttonText, GUILayout.Height(28)))
                {
                    bool newState = !currentVisionState;
                    agent.ToggleVisionStream(newState);
                    visionProp.boolValue = newState;
                }

                GUILayout.Space(4);
                GUI.backgroundColor = new Color(1f, 0.85f, 0.55f);
                if (GUILayout.Button("Force Interrupt Now", GUILayout.Height(28)))
                {
                    agent.RequestInterrupt("inspector_button");
                }

                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawByName(string propName)
        {
            var p = serializedObject.FindProperty(propName);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }

        // ---------------------------------------------------------------------
        // Minimalist UI scaffolder
        // ---------------------------------------------------------------------
        // Design tokens — refined dark palette with a single accent. Keep these here so a future
        // theme swap is a single-place edit.
        private static readonly Color ColorPanelBg     = new Color(0.078f, 0.090f, 0.110f, 0.88f);
        private static readonly Color ColorBorder      = new Color(1f,     1f,     1f,     0.07f);
        private static readonly Color ColorSurface     = new Color(0.125f, 0.141f, 0.173f, 1.00f);
        private static readonly Color ColorSurfaceHi   = new Color(0.180f, 0.200f, 0.243f, 1.00f);
        private static readonly Color ColorAccent      = new Color(0.353f, 0.541f, 1.000f, 1.00f);
        private static readonly Color ColorDanger      = new Color(0.898f, 0.369f, 0.369f, 1.00f);
        private static readonly Color ColorTextPrimary = new Color(0.949f, 0.957f, 0.972f, 1.00f);
        private static readonly Color ColorTextMuted   = new Color(0.541f, 0.576f, 0.643f, 1.00f);

        private static Font BodyFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Font-size scale — bumped in v3.0.4 so the in-game HUD remains legible at 1080p / VR distances.
        private const int FontSizeLog          = 18;
        private const int FontSizeButton       = 15;
        private const int FontSizeHeader       = 14;
        private const int FontSizeSectionLabel = 12;
        private const int FontSizeCaption      = 13;
        private const int FontSizeReadout      = 12;
        private const int FontSizeIcon         = 22;

        /// <summary>
        /// Builds the v3.0.4 dual-panel HUD:
        /// <list type="bullet">
        ///   <item>Two persistent icon toggle buttons in the top-left (💬 transcription, ⚙ settings).</item>
        ///   <item>A semi-transparent transcription panel anchored to the left edge, vertically
        ///     stretched, visible by default. Toggled by the 💬 button.</item>
        ///   <item>A more opaque settings panel anchored to the bottom-left, hidden by default. Toggled
        ///     by the ⚙ button. May overlap the lower portion of the transcription panel — intentional.</item>
        /// </list>
        /// Re-wires the agent's <c>logTextDisplay</c> / <c>scrollRect</c> fields and attaches a
        /// <see cref="GeminiLiveAgentUIControls"/> wired to every button, toggle and preview reference.
        /// </summary>
        private void CreateMinimalSettingsPanel()
        {
            // 1. Canvas + EventSystem — idempotent, reuse what's already in the scene.
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

            if (FindSceneObject<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            // Sizes used across the layout. Kept here so geometry tweaks don't get scattered.
            const float buttonWidth      = 150f;
            const float buttonHeight     = 44f;
            const float buttonGap        = 8f;
            const float screenPadding    = 20f;
            const float transcriptWidth  = 380f;
            const float transcriptHeight = 700f; // default height — user can resize via the bottom-right grip
            const float transcriptTopInset = screenPadding + buttonHeight + 16f; // leaves room for the toggle row above
            const float settingsWidth    = 360f;
            const float settingsHeight   = 520f;
            const float dragBarHeight    = 26f;  // height of the title strip that drags the settings panel
            const float resizeGripSize   = 18f;  // bottom-right corner grip size on the transcription panel

            // 2. Top-level HUD root, anchored top-left of the canvas. Holds the toggle row + the
            //    runtime controller component.
            var rootGo = new GameObject("IVA Agent HUD", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rootGo, "Create IVA Agent HUD");
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot     = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(screenPadding, -screenPadding);
            rootRt.sizeDelta = Vector2.zero;

            // 3. Two text-labelled toggle buttons side-by-side. Always visible. Clicking each
            //    toggles the corresponding panel's `SetActive`. Text labels (not glyphs) — the
            //    LegacyRuntime font doesn't carry emoji and would otherwise render boxes.
            var transcriptionToggleButton = CreateLabelledToggleButton(rootGo.transform, "Transcription Toggle", "Transcription", new Vector2(0f, 0f), new Vector2(buttonWidth, buttonHeight));
            var settingsToggleButton     = CreateLabelledToggleButton(rootGo.transform, "Settings Toggle",      "Settings",      new Vector2(buttonWidth + buttonGap, 0f), new Vector2(buttonWidth, buttonHeight));

            // 4. Transcription panel. Anchored to the top-left with a fixed default size so a
            //    UIResizeHandle in the bottom-right corner can drive width / height independently.
            //    Previously stretched vertically to fill the screen — we trade auto-stretch for
            //    user-controlled resize, which is more useful in tight HUDs.
            var transcriptPanelGo = new GameObject("Transcription Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(transcriptPanelGo, "Create Transcription Panel");
            transcriptPanelGo.transform.SetParent(canvas.transform, false);
            var transcriptPanelRt = (RectTransform)transcriptPanelGo.transform;
            transcriptPanelRt.anchorMin = new Vector2(0f, 1f);
            transcriptPanelRt.anchorMax = new Vector2(0f, 1f);
            transcriptPanelRt.pivot     = new Vector2(0f, 1f);
            transcriptPanelRt.anchoredPosition = new Vector2(screenPadding, -transcriptTopInset);
            transcriptPanelRt.sizeDelta = new Vector2(transcriptWidth, transcriptHeight);

            var transcriptImg = transcriptPanelGo.GetComponent<Image>();
            transcriptImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            transcriptImg.type = Image.Type.Sliced;
            transcriptImg.color = new Color(ColorPanelBg.r, ColorPanelBg.g, ColorPanelBg.b, 0.32f); // ~32% — see-through but readable
            transcriptImg.raycastTarget = true;
            ApplyOutline(transcriptPanelGo, ColorBorder);

            var transcriptHeaderGo = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            transcriptHeaderGo.transform.SetParent(transcriptPanelGo.transform, false);
            var transcriptHeaderRt = (RectTransform)transcriptHeaderGo.transform;
            transcriptHeaderRt.anchorMin = new Vector2(0f, 1f);
            transcriptHeaderRt.anchorMax = new Vector2(1f, 1f);
            transcriptHeaderRt.pivot     = new Vector2(0.5f, 1f);
            transcriptHeaderRt.anchoredPosition = new Vector2(0f, -14f);
            transcriptHeaderRt.sizeDelta = new Vector2(-32f, 22f);
            var transcriptHeaderText = transcriptHeaderGo.GetComponent<Text>();
            transcriptHeaderText.font = BodyFont;
            transcriptHeaderText.fontSize = FontSizeHeader;
            transcriptHeaderText.alignment = TextAnchor.UpperLeft;
            transcriptHeaderText.color = ColorTextMuted;
            transcriptHeaderText.text = "CONVERSATION";
            transcriptHeaderText.raycastTarget = false;

            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(transcriptPanelGo.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(16f, 16f);
            scrollRt.offsetMax = new Vector2(-16f, -44f);

            var scrollRectComp = scrollGo.GetComponent<ScrollRect>();
            scrollRectComp.horizontal = false;
            scrollRectComp.vertical = true;
            scrollRectComp.movementType = ScrollRect.MovementType.Clamped;
            scrollRectComp.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            StretchToParent((RectTransform)viewportGo.transform, new RectOffset(0, 0, 0, 0));
            ((RectTransform)viewportGo.transform).pivot = new Vector2(0f, 1f);
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.004f);
            viewportImg.raycastTarget = true;
            var mask = viewportGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var logTextGo = new GameObject("Log Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            logTextGo.transform.SetParent(contentGo.transform, false);
            var logText = logTextGo.GetComponent<Text>();
            logText.font = BodyFont;
            logText.fontSize = FontSizeLog;
            logText.alignment = TextAnchor.UpperLeft;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.supportRichText = true;
            logText.color = ColorTextPrimary;
            logText.raycastTarget = false;
            logText.text = "<i>System: Awaiting first connection…</i>";

            // Drop shadow keeps the text legible against any avatar/background behind the panel.
            var logShadow = logTextGo.GetComponent<Shadow>();
            logShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            logShadow.effectDistance = new Vector2(1f, -1f);

            scrollRectComp.viewport = (RectTransform)viewportGo.transform;
            scrollRectComp.content = contentRt;

            // Resize grip in the bottom-right corner. Dragging it adjusts the panel's sizeDelta.
            // The grip lives outside the ScrollRect so it doesn't steal vertical scroll drags.
            AddCornerResizeGrip(transcriptPanelGo, transcriptPanelRt, resizeGripSize, new Vector2(transcriptWidth * 0.6f, 180f));

            // Default visible — user can hide via the 💬 button.
            transcriptPanelGo.SetActive(true);

            // 5. Settings panel — top-left pivot/anchor so growth from the bottom-right resize grip
            //    extends down and right (the cursor follows the grip naturally). Default position
            //    is to the right of the transcription panel so the two don't overlap on first open.
            //    The user can drag it elsewhere via the title bar.
            var settingsPanelGo = new GameObject("Settings Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(settingsPanelGo, "Create Settings Panel");
            settingsPanelGo.transform.SetParent(canvas.transform, false);
            var settingsPanelRt = (RectTransform)settingsPanelGo.transform;
            settingsPanelRt.anchorMin = new Vector2(0f, 1f);
            settingsPanelRt.anchorMax = new Vector2(0f, 1f);
            settingsPanelRt.pivot     = new Vector2(0f, 1f);
            settingsPanelRt.anchoredPosition = new Vector2(screenPadding + transcriptWidth + 16f, -transcriptTopInset);
            settingsPanelRt.sizeDelta = new Vector2(settingsWidth, settingsHeight);

            var settingsImg = settingsPanelGo.GetComponent<Image>();
            settingsImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            settingsImg.type = Image.Type.Sliced;
            settingsImg.color = ColorPanelBg; // alpha ≈ 0.88, mostly opaque
            ApplyOutline(settingsPanelGo, ColorBorder);

            // 5a. Title strip across the top — doubles as a UIDragHandle so the user can reposition
            //     the whole settings panel by dragging this bar. Lives above the ScrollRect so it
            //     never competes with scroll input.
            AddDragHandleBar(settingsPanelGo, settingsPanelRt, dragBarHeight, "Settings");

            // 5b. ScrollRect + Viewport + Content so the panel scrolls when its dense control set
            //     overflows the fixed `settingsHeight` (always likely on smaller screens / VR HUDs).
            //     Top inset leaves room for the drag bar above.
            var settingsScrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            settingsScrollGo.transform.SetParent(settingsPanelGo.transform, false);
            var settingsScrollRt = (RectTransform)settingsScrollGo.transform;
            settingsScrollRt.anchorMin = Vector2.zero;
            settingsScrollRt.anchorMax = Vector2.one;
            settingsScrollRt.offsetMin = new Vector2(2f, 2f);   // tiny inset so the outline stays visible
            settingsScrollRt.offsetMax = new Vector2(-2f, -(dragBarHeight + 2f));

            var settingsScroll = settingsScrollGo.GetComponent<ScrollRect>();
            settingsScroll.horizontal = false;
            settingsScroll.vertical = true;
            settingsScroll.movementType = ScrollRect.MovementType.Clamped;
            settingsScroll.scrollSensitivity = 24f;

            var settingsViewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            settingsViewportGo.transform.SetParent(settingsScrollGo.transform, false);
            StretchToParent((RectTransform)settingsViewportGo.transform, new RectOffset(0, 0, 0, 0));
            ((RectTransform)settingsViewportGo.transform).pivot = new Vector2(0f, 1f);
            var settingsViewportImg = settingsViewportGo.GetComponent<Image>();
            settingsViewportImg.color = new Color(1f, 1f, 1f, 0.004f); // near-invisible but raycastable so drag-scroll works
            settingsViewportImg.raycastTarget = true;
            var settingsMask = settingsViewportGo.GetComponent<Mask>();
            settingsMask.showMaskGraphic = false;

            var settingsContentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            settingsContentGo.transform.SetParent(settingsViewportGo.transform, false);
            var settingsContentRt = (RectTransform)settingsContentGo.transform;
            settingsContentRt.anchorMin = new Vector2(0f, 1f);
            settingsContentRt.anchorMax = new Vector2(1f, 1f);
            settingsContentRt.pivot = new Vector2(0f, 1f);
            settingsContentRt.anchoredPosition = Vector2.zero;
            settingsContentRt.sizeDelta = Vector2.zero;

            var settingsLayout = settingsContentGo.GetComponent<VerticalLayoutGroup>();
            settingsLayout.padding = new RectOffset(18, 18, 16, 16);
            settingsLayout.spacing = 10f;
            settingsLayout.childAlignment = TextAnchor.UpperLeft;
            settingsLayout.childForceExpandWidth = true;
            settingsLayout.childForceExpandHeight = false;
            settingsLayout.childControlWidth = true;
            settingsLayout.childControlHeight = true;

            var settingsFitter = settingsContentGo.GetComponent<ContentSizeFitter>();
            settingsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            settingsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            settingsScroll.viewport = (RectTransform)settingsViewportGo.transform;
            settingsScroll.content = settingsContentRt;

            // 6. Section content inside the scrollable Content. Everything below references
            //    `settingsContentGo.transform` so the VerticalLayoutGroup on Content drives sizing.
            Transform settingsContent = settingsContentGo.transform;

            AddSectionLabel(settingsContent, "CONNECTION");
            var reconnectButton = AddFlatButton(settingsContent, "Reconnect", ButtonStyle.Accent);

            AddDivider(settingsContent);
            AddSectionLabel(settingsContent, "MICROPHONE");
            var micDropdown = CreateLabeledDropdown(settingsContent, "Microphone");

            AddDivider(settingsContent);
            AddSectionLabel(settingsContent, "CAMERA");
            var cameraSourceDropdown = CreateLabeledDropdown(settingsContent, "Source");

            var previewToggle = AddInlineToggle(settingsContent, "Show preview", false);
            var previewRawImage = AddCameraPreviewSurface(settingsContent);
            previewRawImage.gameObject.SetActive(false);

            // Vision stream master toggle. Doubles as the on/off control and the state indicator —
            // the checkmark always reflects the agent's current `vision` flag (synced in
            // UIControls.Update). Replaces the legacy Start / Stop button pair, which left the
            // current state ambiguous.
            var visionEnabledToggle = AddInlineToggle(settingsContent, "Enable vision stream", false);

            AddCaptionLabel(settingsContent, "Stream frequency");
            var visionFrequencySlider = AddSlider(settingsContent, 0.2f, 5f, 1f);
            var visionFrequencyValue = AddValueReadout(settingsContent, "1.0 s  •  1.0 fps");

            AddDivider(settingsContent);
            AddSectionLabel(settingsContent, "CONVERSATION");
            var interruptionToggle = AddInlineToggle(settingsContent, "Allow vocal interruption", true);
            // Mirrors GeminiLiveAgent.muteMicWhileTalking. On = mic is muted while the agent speaks,
            // preventing speaker echo from feeding back into the mic on speaker setups.
            var preventEchoToggle  = AddInlineToggle(settingsContent, "Prevent echo (mute mic while talking)", true);

            AddDivider(settingsContent);
            AddSectionLabel(settingsContent, "ACTIONS");
            var forceInterruptButton = AddFlatButton(settingsContent, "Force Interrupt", ButtonStyle.Danger);

            // Resize grip in the bottom-right corner. With top-left pivot, growth extends down /
            // right so the grip naturally follows the cursor. Min size accommodates the dense
            // control set without clipping headers.
            AddCornerResizeGrip(settingsPanelGo, settingsPanelRt, resizeGripSize, new Vector2(280f, 320f));

            // Hidden by default — opened via the Settings toggle button.
            settingsPanelGo.SetActive(false);

            // 7. Runtime controller — wires every reference and drives the toggle behavior.
            var controls = rootGo.AddComponent<GeminiLiveAgentUIControls>();
            controls.agent = agent;
            controls.reconnectButton = reconnectButton;
            controls.forceInterruptButton = forceInterruptButton;
            controls.microphoneDropdown = micDropdown;

            controls.settingsToggleButton = settingsToggleButton;
            controls.settingsPanel = settingsPanelGo;
            controls.startWithSettingsOpen = false;

            controls.transcriptionToggleButton = transcriptionToggleButton;
            controls.logPanel = transcriptPanelGo;
            controls.startWithLogVisible = true;

            controls.cameraPreviewImage = previewRawImage;
            controls.cameraPreviewToggle = previewToggle;
            controls.cameraSourceDropdown = cameraSourceDropdown;
            controls.visionFrequencySlider = visionFrequencySlider;
            controls.visionFrequencyValueLabel = visionFrequencyValue;
            controls.visionEnabledToggle = visionEnabledToggle;
            controls.enableInterruptionToggle = interruptionToggle;
            controls.preventEchoToggle = preventEchoToggle;

            // 8. Wire transcript fields on the agent.
            logTextDisplayProp.objectReferenceValue = logText;
            scrollRectProp.objectReferenceValue = scrollRectComp;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(agent);

            // Push the toggle row to the front of the render order so the panels never cover it.
            // Unity UGUI draws siblings later-on-top; with rootGo created first and the panels
            // created after, the panels would otherwise obscure the buttons.
            rootGo.transform.SetAsLastSibling();

            Selection.activeGameObject = rootGo;
            EditorGUIUtility.PingObject(rootGo);
        }

        /// <summary>
        /// Builds a labelled rectangular toggle button at <paramref name="anchoredPosition"/>
        /// relative to its parent's top-left. Used for the two persistent HUD toggles. Plain text
        /// (not emoji glyphs) so it renders reliably in the LegacyRuntime font.
        /// </summary>
        private static Button CreateLabelledToggleButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = ColorPanelBg;
            ApplyOutline(go, ColorBorder);

            var button = go.GetComponent<Button>();
            ApplySelectableTint(button, img, ColorPanelBg, ColorSurfaceHi);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            StretchToParent((RectTransform)labelGo.transform, new RectOffset(0, 0, 0, 0));
            var text = labelGo.GetComponent<Text>();
            text.font = BodyFont;
            text.fontSize = FontSizeButton;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = ColorTextPrimary;
            text.text = label;
            text.raycastTarget = false;

            return button;
        }

        // ---------------------------------------------------------------------
        // Section / control builders
        // ---------------------------------------------------------------------
        private enum ButtonStyle { Accent, Neutral, Danger }

        private static void AddSectionLabel(Transform parent, string label)
        {
            var go = new GameObject("Section Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = BodyFont;
            text.fontSize = FontSizeSectionLabel;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = ColorTextMuted;
            text.text = label;
            text.raycastTarget = false;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 18f;
            le.preferredHeight = 18f;
        }

        private static void AddDivider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = ColorBorder;
            img.raycastTarget = false;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 1f;
            le.preferredHeight = 1f;
        }

        private static Button AddFlatButton(Transform parent, string label, ButtonStyle style)
        {
            var go = new GameObject($"{label} Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;

            Color normal;
            Color hover;
            Color textColor = ColorTextPrimary;
            switch (style)
            {
                case ButtonStyle.Accent:
                    normal = ColorAccent;
                    hover  = new Color(ColorAccent.r * 1.08f, ColorAccent.g * 1.08f, ColorAccent.b * 1.08f, 1f);
                    break;
                case ButtonStyle.Danger:
                    normal = ColorSurface;
                    hover  = ColorSurfaceHi;
                    textColor = ColorDanger;
                    break;
                default:
                    normal = ColorSurface;
                    hover  = ColorSurfaceHi;
                    break;
            }
            img.color = normal;
            ApplySelectableTint(go.GetComponent<Button>(), img, normal, hover);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 36f;
            le.preferredHeight = 36f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            StretchToParent((RectTransform)textGo.transform, new RectOffset(0, 0, 0, 0));
            var text = textGo.GetComponent<Text>();
            text.font = BodyFont;
            text.fontSize = FontSizeButton;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            text.text = label;
            text.raycastTarget = false;

            return go.GetComponent<Button>();
        }

        private static Toggle AddInlineToggle(Transform parent, string label, bool initialValue)
        {
            var go = new GameObject($"{label} Toggle", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            // Label fills the row, checkbox sits on the right.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(0f, 0f);
            labelRt.offsetMax = new Vector2(-32f, 0f);
            var labelText = labelGo.GetComponent<Text>();
            labelText.font = BodyFont;
            labelText.fontSize = FontSizeCaption;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = ColorTextPrimary;
            labelText.text = label;
            labelText.raycastTarget = false;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = new Vector2(1f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot     = new Vector2(1f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(20f, 20f);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgImg.type = Image.Type.Sliced;
            bgImg.color = ColorSurface;

            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            checkGo.transform.SetParent(bgGo.transform, false);
            StretchToParent((RectTransform)checkGo.transform, new RectOffset(3, 3, 3, 3));
            var checkImg = checkGo.GetComponent<Image>();
            checkImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            checkImg.color = ColorAccent;
            checkImg.raycastTarget = false;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = initialValue;
            ApplySelectableTint(toggle, bgImg, ColorSurface, ColorSurfaceHi);

            return toggle;
        }

        private static RawImage AddCameraPreviewSurface(Transform parent)
        {
            // Parent wrapper holds a dark backdrop Image plus the RawImage on top. Splitting them keeps
            // the RawImage's color at white so the assigned WebCamTexture / RenderTexture renders as-is;
            // a colored RawImage would multiply the texture by that tint and the v3.0.4–v3.0.7 black tint
            // produced a solid black square where the webcam feed should have appeared.
            var wrapper = new GameObject("Camera Preview", typeof(RectTransform), typeof(LayoutElement));
            wrapper.transform.SetParent(parent, false);

            // Drawer inner width ≈ 288 px (320 drawer - 2*16 padding). 16:9 → 162 px height.
            const float assumedDrawerInnerWidth = 288f;
            var le = wrapper.GetComponent<LayoutElement>();
            le.minHeight = assumedDrawerInnerWidth * 0.5625f;
            le.preferredHeight = assumedDrawerInnerWidth * 0.5625f;

            var bgGo = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(wrapper.transform, false);
            StretchToParent((RectTransform)bgGo.transform, new RectOffset(0, 0, 0, 0));
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.85f);
            bgImg.raycastTarget = false;

            var rawGo = new GameObject("Texture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rawGo.transform.SetParent(wrapper.transform, false);
            StretchToParent((RectTransform)rawGo.transform, new RectOffset(0, 0, 0, 0));
            var raw = rawGo.GetComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;

            return raw;
        }

        private static void ApplyOutline(GameObject go, Color color)
        {
            var outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>
        /// Adds a horizontal title bar across the top of <paramref name="panelGo"/> that hosts a
        /// <see cref="UIDragHandle"/>. Dragging the bar moves <paramref name="dragTarget"/>.
        /// </summary>
        private static void AddDragHandleBar(GameObject panelGo, RectTransform dragTarget, float height, string label)
        {
            var barGo = new GameObject("Drag Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIDragHandle));
            barGo.transform.SetParent(panelGo.transform, false);
            var rt = (RectTransform)barGo.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -2f);
            rt.sizeDelta = new Vector2(-4f, height);

            var img = barGo.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = ColorSurfaceHi;
            img.raycastTarget = true; // required so the drag handle receives pointer events

            var drag = barGo.GetComponent<UIDragHandle>();
            drag.target = dragTarget;
            drag.clampToParent = true;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(barGo.transform, false);
            StretchToParent((RectTransform)labelGo.transform, new RectOffset(12, 12, 0, 0));
            var text = labelGo.GetComponent<Text>();
            text.font = BodyFont;
            text.fontSize = FontSizeSectionLabel;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = ColorTextMuted;
            text.text = $"≡  {label.ToUpperInvariant()}";
            text.raycastTarget = false;
        }

        /// <summary>
        /// Adds a small grip at the bottom-right corner of <paramref name="panelGo"/> that hosts a
        /// <see cref="UIResizeHandle"/>. Dragging the grip resizes <paramref name="resizeTarget"/>.
        /// </summary>
        private static void AddCornerResizeGrip(GameObject panelGo, RectTransform resizeTarget, float size, Vector2 minSize)
        {
            var gripGo = new GameObject("Resize Grip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIResizeHandle));
            gripGo.transform.SetParent(panelGo.transform, false);
            var rt = (RectTransform)gripGo.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-4f, 4f);
            rt.sizeDelta = new Vector2(size, size);

            var img = gripGo.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            // Slightly lighter than panel background so the grip is discoverable without shouting.
            img.color = new Color(ColorTextMuted.r, ColorTextMuted.g, ColorTextMuted.b, 0.55f);
            img.raycastTarget = true;

            var resize = gripGo.GetComponent<UIResizeHandle>();
            resize.target = resizeTarget;
            resize.minSize = minSize;
            resize.resizeWidth = true;
            resize.resizeHeight = true;
            resize.growDirection = new Vector2(1f, -1f); // drag right grows width; drag down grows height (UI Y is up)
        }

        private static void ApplySelectableTint(Selectable selectable, Image targetGraphic, Color normal, Color highlighted)
        {
            selectable.targetGraphic = targetGraphic;
            selectable.transition = Selectable.Transition.ColorTint;
            var colors = selectable.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = new Color(highlighted.r * 0.85f, highlighted.g * 0.85f, highlighted.b * 0.85f, highlighted.a);
            colors.selectedColor = highlighted;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.5f);
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
        }

        private static void StretchToParent(RectTransform rt, RectOffset padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding.left,   padding.bottom);
            rt.offsetMax = new Vector2(-padding.right, -padding.top);
        }

        private static Text AddCaptionLabel(Transform parent, string text)
        {
            var go = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = BodyFont;
            t.fontSize = FontSizeCaption;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = ColorTextPrimary;
            t.text = text;
            t.raycastTarget = false;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 18f;
            le.preferredHeight = 18f;
            return t;
        }

        private static Text AddValueReadout(Transform parent, string initial)
        {
            var go = new GameObject("Value Readout", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = BodyFont;
            t.fontSize = FontSizeReadout;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = ColorTextMuted;
            t.text = initial;
            t.raycastTarget = false;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 14f;
            le.preferredHeight = 14f;
            return t;
        }

        private static Slider AddSlider(Transform parent, float min, float max, float initial)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 18f;
            le.preferredHeight = 18f;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(0f, 4f);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bgImg.type = Image.Type.Sliced;
            bgImg.color = ColorSurface;

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRt = (RectTransform)fillAreaGo.transform;
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRt.offsetMin = new Vector2(0f, -2f);
            fillAreaRt.offsetMax = new Vector2(-12f, 2f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImg.type = Image.Type.Sliced;
            fillImg.color = ColorAccent;

            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(go.transform, false);
            var handleAreaRt = (RectTransform)handleAreaGo.transform;
            handleAreaRt.anchorMin = new Vector2(0f, 0f);
            handleAreaRt.anchorMax = new Vector2(1f, 1f);
            handleAreaRt.offsetMin = new Vector2(6f, 0f);
            handleAreaRt.offsetMax = new Vector2(-6f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(14f, 14f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handleImg.color = ColorTextPrimary;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(initial, min, max);
            ApplySelectableTint(slider, handleImg, ColorTextPrimary, ColorAccent);

            return slider;
        }

        // ---------------------------------------------------------------------
        // Labeled dropdown — restyled to match the minimalist palette but built the same way
        // Unity's UI/Dropdown requires (template + viewport + content + item with toggle inside).
        // ---------------------------------------------------------------------
        private static Dropdown CreateLabeledDropdown(Transform parent, string caption)
        {
            var go = new GameObject($"{caption} Dropdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = ColorSurface;

            var layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;

            var dropdown = go.GetComponent<Dropdown>();
            ApplySelectableTint(dropdown, img, ColorSurface, ColorSurfaceHi);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(12f, 2f);
            labelRt.offsetMax = new Vector2(-28f, -2f);

            var labelText = labelGo.GetComponent<Text>();
            labelText.font = BodyFont;
            labelText.fontSize = FontSizeCaption;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = ColorTextPrimary;
            labelText.text = caption;
            labelText.raycastTarget = false;

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);
            var arrowRt = (RectTransform)arrowGo.transform;
            arrowRt.anchorMin = new Vector2(1f, 0.5f);
            arrowRt.anchorMax = new Vector2(1f, 0.5f);
            arrowRt.pivot = new Vector2(1f, 0.5f);
            arrowRt.anchoredPosition = new Vector2(-8f, 0f);
            arrowRt.sizeDelta = new Vector2(10f, 10f);

            var arrowImg = arrowGo.GetComponent<Image>();
            arrowImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            arrowImg.color = ColorTextMuted;
            arrowImg.raycastTarget = false;

            // Template panel — pops open on click. Required by Dropdown.
            var templateGo = new GameObject("Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(go.transform, false);
            templateGo.SetActive(false);

            var templateRt = (RectTransform)templateGo.transform;
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, 2f);
            templateRt.sizeDelta = new Vector2(0f, 160f);

            var templateImg = templateGo.GetComponent<Image>();
            templateImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            templateImg.type = Image.Type.Sliced;
            templateImg.color = new Color(0.078f, 0.090f, 0.110f, 0.97f);

            var templateScroll = templateGo.GetComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateGo.transform, false);
            StretchToParent((RectTransform)viewportGo.transform, new RectOffset(0, 0, 0, 0));
            ((RectTransform)viewportGo.transform).pivot = new Vector2(0f, 1f);
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImg.raycastTarget = true;
            var vMask = viewportGo.GetComponent<Mask>();
            vMask.showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 28f);

            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRt = (RectTransform)itemGo.transform;
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.sizeDelta = new Vector2(0f, 26f);

            var itemBgGo = new GameObject("Item Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemBgGo.transform.SetParent(itemGo.transform, false);
            StretchToParent((RectTransform)itemBgGo.transform, new RectOffset(0, 0, 0, 0));
            var itemBgImg = itemBgGo.GetComponent<Image>();
            itemBgImg.color = ColorAccent;

            var itemCheckmarkGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            itemCheckmarkGo.transform.SetParent(itemGo.transform, false);
            var itemCheckmarkRt = (RectTransform)itemCheckmarkGo.transform;
            itemCheckmarkRt.anchorMin = new Vector2(0f, 0.5f);
            itemCheckmarkRt.anchorMax = new Vector2(0f, 0.5f);
            itemCheckmarkRt.pivot = new Vector2(0f, 0.5f);
            itemCheckmarkRt.anchoredPosition = new Vector2(10f, 0f);
            itemCheckmarkRt.sizeDelta = new Vector2(14f, 14f);
            itemCheckmarkGo.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

            var itemLabelGo = new GameObject("Item Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            itemLabelGo.transform.SetParent(itemGo.transform, false);
            var itemLabelRt = (RectTransform)itemLabelGo.transform;
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(28f, 1f);
            itemLabelRt.offsetMax = new Vector2(-10f, -2f);

            var itemLabel = itemLabelGo.GetComponent<Text>();
            itemLabel.font = BodyFont;
            itemLabel.fontSize = FontSizeCaption;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.color = ColorTextPrimary;
            itemLabel.text = "Option A";
            itemLabel.raycastTarget = false;

            var toggle = itemGo.GetComponent<Toggle>();
            toggle.targetGraphic = itemBgImg;
            toggle.graphic = itemCheckmarkGo.GetComponent<Image>();
            toggle.isOn = true;

            templateScroll.viewport = (RectTransform)viewportGo.transform;
            templateScroll.content = contentRt;

            dropdown.targetGraphic = img;
            dropdown.template = templateRt;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabel;
            dropdown.options.Clear();

            return dropdown;
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
