using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IVH.Core.IntelligentVirtualAgent
{
    /// <summary>
    /// In-game runtime control surface for a <see cref="GeminiLiveAgent"/>. Drives Reconnect, Vision
    /// start/stop, Force-Interrupt, microphone selection, an optional collapsible settings drawer,
    /// an optional toggleable conversation-log panel, and an optional live camera preview (webcam
    /// feed or agent-egocentric feed). Designed to be attached by the editor scaffolder's
    /// "Create Minimal Settings Panel" button, but works fine wired by hand.
    /// </summary>
    /// <remarks>
    /// All button / GameObject / Toggle references are optional. References left null are silently
    /// skipped, which keeps the component backward-compatible with the v3.0.0 always-visible layout
    /// (only the buttons + dropdown wired) while supporting the v3.0.1 minimalist drawer layout.
    /// </remarks>
    public class GeminiLiveAgentUIControls : MonoBehaviour
    {
        [Header("Target Agent")]
        /// <summary>Agent the controls drive. Falls back to <c>GetComponentInParent</c> if left blank.</summary>
        public GeminiLiveAgent agent;

        [Header("Buttons")]
        /// <summary>Calls <see cref="GeminiLiveAgent.Connect"/>. Use to recover from a stuck session.</summary>
        public Button reconnectButton;

        /// <summary>Calls <see cref="GeminiLiveAgent.ToggleVisionStream"/> with <c>true</c>.</summary>
        public Button startVisionButton;

        /// <summary>Calls <see cref="GeminiLiveAgent.ToggleVisionStream"/> with <c>false</c>.</summary>
        public Button stopVisionButton;

        /// <summary>Calls <see cref="GeminiLiveAgent.RequestInterrupt"/> with a UI-button reason.</summary>
        public Button forceInterruptButton;

        [Header("Microphone Selector")]
        /// <summary>Dropdown populated from <c>Microphone.devices</c> on enable. Writing to its value
        /// updates <see cref="GeminiLiveAgent.microphoneDeviceName"/>. The change takes effect on the
        /// next session — call <c>Reconnect</c> to apply immediately.</summary>
        public Dropdown microphoneDropdown;

        /// <summary>Optional label rendered next to the dropdown for visual context. Not required.</summary>
        public Text microphoneLabel;

        [Header("Settings Drawer (Optional)")]
        /// <summary>Button that toggles <see cref="settingsPanel"/>'s active state. Typically a small
        /// gear icon in the always-visible header.</summary>
        public Button settingsToggleButton;

        /// <summary>The drawer GameObject that holds reconnect / mic / camera / log controls. Hidden
        /// on Start when <see cref="startWithSettingsOpen"/> is false.</summary>
        public GameObject settingsPanel;

        /// <summary>If true, the settings drawer is visible from the first frame. Defaults to false
        /// so the in-game HUD stays minimal until the user opts in.</summary>
        public bool startWithSettingsOpen = false;

        [Header("Conversation Log Toggle (Optional)")]
        /// <summary>The conversation-log GameObject (typically the ScrollView root). Hidden on Start
        /// when <see cref="startWithLogVisible"/> is false.</summary>
        public GameObject logPanel;

        /// <summary>Top-level button that toggles <see cref="logPanel"/>'s visibility. Used by the
        /// dual-panel HUD (v3.0.4+). Independent of <see cref="showLogToggle"/> — wire only one.</summary>
        public Button transcriptionToggleButton;

        /// <summary>Legacy in-drawer toggle that mirrors <see cref="logPanel"/>'s visibility. Kept for
        /// backward-compat with the v3.0.1–v3.0.3 single-pill HUD; the new HUD uses
        /// <see cref="transcriptionToggleButton"/> instead.</summary>
        public Toggle showLogToggle;

        /// <summary>If true, the conversation log is visible from the first frame.</summary>
        public bool startWithLogVisible = false;

        [Header("Camera Preview (Optional)")]
        /// <summary>RawImage that mirrors the agent's vision input — webcam texture when
        /// <see cref="AgentBase.targetCameraType"/> is <c>WebCam</c>, or the agent's egocentric
        /// camera when it is <c>AgentCamera</c>.</summary>
        public RawImage cameraPreviewImage;

        /// <summary>Toggle that turns the preview on / off without affecting the agent's vision
        /// stream to Gemini. Off by default to save GPU cost.</summary>
        public Toggle cameraPreviewToggle;

        /// <summary>Resolution of the RenderTexture used when previewing the agent's egocentric
        /// camera. Width × height in pixels. Webcam previews ignore this and use the device texture
        /// directly.</summary>
        public Vector2Int agentCameraPreviewResolution = new Vector2Int(256, 144);

        [Header("Camera Source (Optional)")]
        /// <summary>Dropdown that switches <see cref="AgentBase.targetCameraType"/> between
        /// <c>WebCam</c> (index 0) and <c>AgentCamera</c> (index 1) at runtime. Picking WebCam also
        /// lazily allocates the agent's <c>WebCamTexture</c> via <c>EnsureWebCamReady</c>.</summary>
        public Dropdown cameraSourceDropdown;

        [Header("Vision Stream (Optional)")]
        /// <summary>Slider bound to <see cref="GeminiLiveAgent.visionUpdateFrequency"/>. Min/max are
        /// expected to be configured in the inspector to match the agent's <c>[Range(0.2, 5.0)]</c>.</summary>
        public Slider visionFrequencySlider;

        /// <summary>Text label that prints the live value of the slider in seconds.</summary>
        public Text visionFrequencyValueLabel;

        /// <summary>Toggle that mirrors <see cref="AgentBase.vision"/>. Flipping it calls
        /// <see cref="GeminiLiveAgent.ToggleVisionStream"/>; the toggle's visual state is also
        /// resynced from the agent on enable and in <c>Update</c> so external state changes
        /// (e.g., the Start / Stop Vision buttons or programmatic toggles) stay reflected.</summary>
        public Toggle visionEnabledToggle;

        [Header("Interruption (Optional)")]
        /// <summary>Toggle bound to <see cref="GeminiLiveAgent.enableVocalInterruption"/>. Lets the
        /// user disable mid-sentence interruption from the in-game HUD without recompiling.</summary>
        public Toggle enableInterruptionToggle;

        /// <summary>Toggle bound to <see cref="GeminiLiveAgent.muteMicWhileTalking"/>. When on, the
        /// mic is muted while the agent is speaking — useful when the user isn't wearing headphones
        /// and the speaker output would otherwise feed back into the mic as echo. Note that turning
        /// this on disables vocal interruption (the agent can't hear the user while talking).</summary>
        public Toggle preventEchoToggle;

        private RenderTexture _agentPreviewRT;
        private Coroutine _previewRoutine;

        private void Awake()
        {
            if (agent == null) agent = GetComponentInParent<GeminiLiveAgent>();
            if (agent == null)
            {
                Debug.LogWarning($"{nameof(GeminiLiveAgentUIControls)} on '{name}' has no agent reference and no GeminiLiveAgent in its parents — buttons will no-op.");
                return;
            }

            if (reconnectButton != null) reconnectButton.onClick.AddListener(HandleReconnectClicked);
            if (startVisionButton != null) startVisionButton.onClick.AddListener(HandleStartVisionClicked);
            if (stopVisionButton != null) stopVisionButton.onClick.AddListener(HandleStopVisionClicked);
            if (forceInterruptButton != null) forceInterruptButton.onClick.AddListener(HandleForceInterruptClicked);
            if (settingsToggleButton != null) settingsToggleButton.onClick.AddListener(HandleSettingsToggleClicked);
            if (transcriptionToggleButton != null) transcriptionToggleButton.onClick.AddListener(HandleTranscriptionToggleClicked);
            if (showLogToggle != null) showLogToggle.onValueChanged.AddListener(HandleLogToggleChanged);
            if (cameraPreviewToggle != null) cameraPreviewToggle.onValueChanged.AddListener(HandleCameraPreviewToggleChanged);
            if (cameraSourceDropdown != null) cameraSourceDropdown.onValueChanged.AddListener(HandleCameraSourceChanged);
            if (visionFrequencySlider != null) visionFrequencySlider.onValueChanged.AddListener(HandleVisionFrequencyChanged);
            if (enableInterruptionToggle != null) enableInterruptionToggle.onValueChanged.AddListener(HandleInterruptionToggleChanged);
            if (visionEnabledToggle != null) visionEnabledToggle.onValueChanged.AddListener(HandleVisionToggleChanged);
            if (preventEchoToggle != null) preventEchoToggle.onValueChanged.AddListener(HandlePreventEchoToggleChanged);

            PopulateMicrophoneDropdown();
            PopulateCameraSourceDropdown();
            SyncVisionFrequencyUi();
            SyncInterruptionToggleUi();
            SyncVisionToggleUi();
            SyncPreventEchoToggleUi();
        }

        private void Update()
        {
            // Keep the vision toggle in sync with the agent's live state. Cheap bool compare;
            // covers the case where another caller (legacy Start/Stop buttons, scripted toggle,
            // tool-call) changes the vision flag without going through the toggle UI.
            if (agent == null || visionEnabledToggle == null) return;
            if (visionEnabledToggle.isOn != agent.vision)
            {
                visionEnabledToggle.SetIsOnWithoutNotify(agent.vision);
            }
        }

        private void Start()
        {
            if (settingsPanel != null) settingsPanel.SetActive(startWithSettingsOpen);
            if (logPanel != null) logPanel.SetActive(startWithLogVisible);
            if (showLogToggle != null) showLogToggle.SetIsOnWithoutNotify(startWithLogVisible);
            if (cameraPreviewToggle != null) cameraPreviewToggle.SetIsOnWithoutNotify(false);
            if (cameraPreviewImage != null) cameraPreviewImage.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (reconnectButton != null) reconnectButton.onClick.RemoveListener(HandleReconnectClicked);
            if (startVisionButton != null) startVisionButton.onClick.RemoveListener(HandleStartVisionClicked);
            if (stopVisionButton != null) stopVisionButton.onClick.RemoveListener(HandleStopVisionClicked);
            if (forceInterruptButton != null) forceInterruptButton.onClick.RemoveListener(HandleForceInterruptClicked);
            if (settingsToggleButton != null) settingsToggleButton.onClick.RemoveListener(HandleSettingsToggleClicked);
            if (transcriptionToggleButton != null) transcriptionToggleButton.onClick.RemoveListener(HandleTranscriptionToggleClicked);
            if (showLogToggle != null) showLogToggle.onValueChanged.RemoveListener(HandleLogToggleChanged);
            if (cameraPreviewToggle != null) cameraPreviewToggle.onValueChanged.RemoveListener(HandleCameraPreviewToggleChanged);
            if (cameraSourceDropdown != null) cameraSourceDropdown.onValueChanged.RemoveListener(HandleCameraSourceChanged);
            if (visionFrequencySlider != null) visionFrequencySlider.onValueChanged.RemoveListener(HandleVisionFrequencyChanged);
            if (enableInterruptionToggle != null) enableInterruptionToggle.onValueChanged.RemoveListener(HandleInterruptionToggleChanged);
            if (visionEnabledToggle != null) visionEnabledToggle.onValueChanged.RemoveListener(HandleVisionToggleChanged);
            if (preventEchoToggle != null) preventEchoToggle.onValueChanged.RemoveListener(HandlePreventEchoToggleChanged);
            if (microphoneDropdown != null) microphoneDropdown.onValueChanged.RemoveListener(HandleMicrophoneSelected);

            StopAgentCameraPreview();
        }

        private void HandleReconnectClicked() => agent.Connect();
        private void HandleStartVisionClicked() => agent.ToggleVisionStream(true);
        private void HandleStopVisionClicked() => agent.ToggleVisionStream(false);
        private void HandleForceInterruptClicked() => agent.RequestInterrupt("ui_button");

        private void HandleSettingsToggleClicked()
        {
            if (settingsPanel == null) return;
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        private void HandleTranscriptionToggleClicked()
        {
            if (logPanel == null) return;
            logPanel.SetActive(!logPanel.activeSelf);
        }

        private void HandleLogToggleChanged(bool isVisible)
        {
            if (logPanel != null) logPanel.SetActive(isVisible);
        }

        private void HandleCameraPreviewToggleChanged(bool isVisible)
        {
            if (cameraPreviewImage != null) cameraPreviewImage.gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                StopAgentCameraPreview();
                if (cameraPreviewImage != null) cameraPreviewImage.texture = null;
                return;
            }

            if (agent.targetCameraType == TargetCameraType.WebCam)
            {
                WireWebcamPreview();
            }
            else
            {
                StartAgentCameraPreview();
            }
        }

        private void WireWebcamPreview()
        {
            if (cameraPreviewImage == null) return;

            // EnsureWebCamReady lazily allocates the agent's webcam if vision was never enabled, so
            // the preview works even on agents launched in AgentCamera mode. Returns null when no
            // webcam device is connected — in that case the preview surface stays blank.
            StopAgentCameraPreview();
            var wct = agent.EnsureWebCamReady(string.IsNullOrEmpty(agent.selectedWebCamName) ? null : agent.selectedWebCamName);
            cameraPreviewImage.texture = wct;
            if (wct == null)
            {
                agent.AppendLog("<color=#E0A858>System: No webcam detected for preview.</color>");
            }
        }

        private void StartAgentCameraPreview()
        {
            if (cameraPreviewImage == null) return;
            if (agent.agentVisionCamera == null)
            {
                cameraPreviewImage.texture = null;
                return;
            }

            if (_agentPreviewRT == null
                || _agentPreviewRT.width != agentCameraPreviewResolution.x
                || _agentPreviewRT.height != agentCameraPreviewResolution.y)
            {
                if (_agentPreviewRT != null) _agentPreviewRT.Release();
                _agentPreviewRT = new RenderTexture(
                    Mathf.Max(16, agentCameraPreviewResolution.x),
                    Mathf.Max(16, agentCameraPreviewResolution.y),
                    16);
                _agentPreviewRT.name = "GeminiLiveAgent_EgocentricPreview";
            }

            cameraPreviewImage.texture = _agentPreviewRT;
            _previewRoutine = StartCoroutine(AgentCameraPreviewLoop());
        }

        private IEnumerator AgentCameraPreviewLoop()
        {
            // We re-assign targetTexture every frame because AgentBase.CaptureEgocentricImage* paths
            // temporarily steal the camera's targetTexture during a capture and reset it to null
            // afterwards. Re-asserting here keeps the preview alive between captures.
            var wait = new WaitForEndOfFrame();
            while (cameraPreviewToggle != null && cameraPreviewToggle.isOn && agent != null && agent.agentVisionCamera != null)
            {
                agent.agentVisionCamera.targetTexture = _agentPreviewRT;
                agent.agentVisionCamera.Render();
                yield return wait;
            }

            if (agent != null && agent.agentVisionCamera != null
                && agent.agentVisionCamera.targetTexture == _agentPreviewRT)
            {
                agent.agentVisionCamera.targetTexture = null;
            }
        }

        private void StopAgentCameraPreview()
        {
            if (_previewRoutine != null)
            {
                StopCoroutine(_previewRoutine);
                _previewRoutine = null;
            }
            if (_agentPreviewRT != null)
            {
                _agentPreviewRT.Release();
                Object.Destroy(_agentPreviewRT);
                _agentPreviewRT = null;
            }
        }

        private void PopulateMicrophoneDropdown()
        {
            if (microphoneDropdown == null) return;

            string[] devices = Microphone.devices;
            microphoneDropdown.ClearOptions();

            if (devices.Length == 0)
            {
                microphoneDropdown.AddOptions(new List<string> { "<no microphones detected>" });
                microphoneDropdown.interactable = false;
                return;
            }

            microphoneDropdown.interactable = true;
            microphoneDropdown.AddOptions(new List<string>(devices));

            int active = System.Array.IndexOf(devices, agent.microphoneDeviceName);
            if (active < 0) active = 0;
            microphoneDropdown.SetValueWithoutNotify(active);
            // Sync the agent in case the previously-saved name doesn't match any current device.
            agent.microphoneDeviceName = devices[active];

            microphoneDropdown.onValueChanged.AddListener(HandleMicrophoneSelected);
        }

        private void HandleMicrophoneSelected(int index)
        {
            string[] devices = Microphone.devices;
            if (index < 0 || index >= devices.Length) return;
            agent.microphoneDeviceName = devices[index];
            agent.AppendLog($"<color=#9BB4FF>System: Microphone set to {devices[index]}. Reconnect to apply.</color>");
        }

        private void PopulateCameraSourceDropdown()
        {
            if (cameraSourceDropdown == null) return;
            cameraSourceDropdown.ClearOptions();
            cameraSourceDropdown.AddOptions(new List<string> { "Webcam", "Agent Camera" });
            int active = agent.targetCameraType == TargetCameraType.WebCam ? 0 : 1;
            cameraSourceDropdown.SetValueWithoutNotify(active);
        }

        private void HandleCameraSourceChanged(int index)
        {
            var newSource = index == 0 ? TargetCameraType.WebCam : TargetCameraType.AgentCamera;
            agent.targetCameraType = newSource;

            if (newSource == TargetCameraType.WebCam)
            {
                agent.EnsureWebCamReady(string.IsNullOrEmpty(agent.selectedWebCamName) ? null : agent.selectedWebCamName);
            }

            agent.AppendLog($"<color=#9BB4FF>System: Camera source set to {newSource}.</color>");

            // If the preview is open, refresh it so the user immediately sees the new source.
            if (cameraPreviewToggle != null && cameraPreviewToggle.isOn)
            {
                HandleCameraPreviewToggleChanged(true);
            }
        }

        private void SyncVisionFrequencyUi()
        {
            if (visionFrequencySlider == null) return;
            // Mirror the agent's [Range(0.2, 5.0)] attribute. We hardcode it here because slider min
            // / max can't read field attributes at runtime without reflection-heavy plumbing.
            visionFrequencySlider.minValue = 0.2f;
            visionFrequencySlider.maxValue = 5f;
            visionFrequencySlider.SetValueWithoutNotify(agent.visionUpdateFrequency);
            UpdateVisionFrequencyLabel(agent.visionUpdateFrequency);
        }

        private void HandleVisionFrequencyChanged(float value)
        {
            agent.visionUpdateFrequency = value;
            UpdateVisionFrequencyLabel(value);
        }

        private void UpdateVisionFrequencyLabel(float value)
        {
            if (visionFrequencyValueLabel == null) return;
            float fps = value > 0f ? 1f / value : 0f;
            visionFrequencyValueLabel.text = $"{value:0.0} s  •  {fps:0.0} fps";
        }

        private void SyncInterruptionToggleUi()
        {
            if (enableInterruptionToggle == null) return;
            enableInterruptionToggle.SetIsOnWithoutNotify(agent.enableVocalInterruption);
        }

        private void HandleInterruptionToggleChanged(bool isOn)
        {
            agent.enableVocalInterruption = isOn;
        }

        private void SyncVisionToggleUi()
        {
            if (visionEnabledToggle == null) return;
            visionEnabledToggle.SetIsOnWithoutNotify(agent.vision);
        }

        private void HandleVisionToggleChanged(bool isOn)
        {
            // Route through ToggleVisionStream so the auto-capture coroutine is started / stopped
            // alongside the bool flag — writing agent.vision directly leaves the coroutine state
            // inconsistent.
            agent.ToggleVisionStream(isOn);
        }

        private void SyncPreventEchoToggleUi()
        {
            if (preventEchoToggle == null) return;
            preventEchoToggle.SetIsOnWithoutNotify(agent.muteMicWhileTalking);
        }

        private void HandlePreventEchoToggleChanged(bool isOn)
        {
            agent.muteMicWhileTalking = isOn;
        }
    }
}
