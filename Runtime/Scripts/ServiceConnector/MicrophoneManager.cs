using System;
using System.Collections;
using UnityEngine;

namespace IVH.Core.ServiceConnector
{
    public class MicrophoneManager : MonoBehaviour
    {
        public string SelectedMicrophoneDevice { get; private set; } = "";
        public bool SelectedMicrophone { get; private set; } = false;

        [Header("For Mobile")]
        public bool AutoselectMicrophone;
        
        [Header("For Desktop")]
        [Tooltip("Should display GUI on Desktop")]
        public bool ShowGUI = true;
        [HideInInspector] public AudioClip recording;
        private const int BUFFER_SECONDS = 2; // Use a slightly longer buffer

        void Start()
        {
            if (AutoselectMicrophone && Microphone.devices.Length > 0)
            {
                SelectedMicrophoneDevice = Microphone.devices[0];
                SelectedMicrophone = true;
                StartRecording();
            }

        }
        public void StartRecording()
        {
            Microphone.GetDeviceCaps(SelectedMicrophoneDevice, out var minFreq, out int maxFreq);
            WebsocketConnection.AudioConfiguration audioConfig = null;
            audioConfig = new WebsocketConnection.AudioConfiguration("LINEAR16", Mathf.Clamp(16000, minFreq, maxFreq), "en-US");
            recording = Microphone.Start(SelectedMicrophoneDevice, true, BUFFER_SECONDS , audioConfig.sampleRateHertz);
        }
        public void SelectMicrophone(string device)
        {
            SelectedMicrophoneDevice = device;
            SelectedMicrophone = true;
            StartRecording();
        }
        void OnDestroy()
        {
            // Stop the microphone when the object is destroyed.
            if (Microphone.IsRecording(SelectedMicrophoneDevice))
            {
                Microphone.End(SelectedMicrophoneDevice);
                Debug.Log("Microphone stopped.");
            }
        }
        public bool IsRecording
        {
            get
            {
                foreach (var device in Microphone.devices)
                {
                    if (Microphone.IsRecording(device))
                        return true;
                }

                return false;
            }
        }

        private void OnGUI()
        {
            if (!ShowGUI)
            {
                return;
            }
            if (!SelectedMicrophone)
            {
                var x = 10;
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

                GUI.color = Color.green;
                GUILayout.Label("Select Microphone:");
                GUI.color = Color.white;

                foreach (var device in Microphone.devices)
                {
                    if (GUILayout.Button(device, GUILayout.ExpandWidth(true), GUILayout.Height(40)))
                    {
                        SelectMicrophone(device);
                    }

                    x += 22;
                }

                GUILayout.EndVertical();
            }

            if (IsRecording)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 30, 500, 100), "Microphone is on.");

                if (GUI.Button(new Rect(10, Screen.height - 110, 100, 100), "Mic off"))
                {
                    StopAllRecordings();
                }
            }
            else
            {
                GUI.color = Color.green;
            }
        }

        public void StopAllRecordings()
        {
            foreach (var device in Microphone.devices)
            {
                if (Microphone.IsRecording(device))
                    Microphone.End(device);
            }
        }
    }
}
