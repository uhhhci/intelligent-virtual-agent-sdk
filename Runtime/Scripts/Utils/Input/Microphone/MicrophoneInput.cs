using System;
using System.Collections;
using IVH.Core.Utils.StaticHelper;
using UnityEngine;

namespace IVH.Core.Input
{
    public class MicrophoneInput : MonoBehaviour
    {
        public AudioSource audioSource;
        public String microphoneName;
        public int sampleRate = 16000;
        public bool useDefaultMicrophone;
        public bool captureOnStart = true;

        private string _capturingMicrophone;
        private Coroutine _waitForMicrophone;
        private bool isStarted;


        /// <summary>
        /// Returns if the current microphone configuration is started.
        /// </summary>
        public bool IsStarted
        {
            get { return isStarted; }
        }

        /// <summary>
        /// Returns of the current microphone configuration is capturing input.
        /// </summary>
        public bool IsCapturing
        {
            get
            {
                if (Microphone.IsRecording(_capturingMicrophone))
                    return true;
                return false;
            }
        }

        /// <summary>
        /// Callback on start of the application.
        /// </summary>
        void Start()
        {
            // Check for audio source to play mic input
            if (audioSource.IsNull()) audioSource = GetComponent<AudioSource>();
            if (audioSource.IsNull())
            {
                Debug.LogWarning("No audio source found to capture the microphone input.");
            }

            if (captureOnStart) StartCapturing();
        }

        /// <summary>
        /// Starts capturing audio from the microphone. 
        /// </summary>
        public void StartCapturing(bool useDefaultMicrophone = false)
        {
            // Stop last capturing
            StopCapturing();

            // Check if any microphone is connected
            if (Microphone.devices.Length < 1)
            {
                Debug.LogWarning("No microphone devices available.");
                return;
            }

            // Check if to use default or set microphone
            if (useDefaultMicrophone)
            {
                _capturingMicrophone = Microphone.devices[0];
            }
            else
            {
                _capturingMicrophone = microphoneName;
            }

            // Check if given microphone exists
            if (Array.IndexOf(Microphone.devices, _capturingMicrophone) == -1)
            {
                Debug.LogWarning("Given microphone can not be found.");
                return;
            }

            // Determine sample rate
            Microphone.GetDeviceCaps(_capturingMicrophone, out var minFreq, out int maxFreq);
            if (minFreq == 0 && maxFreq == 0)
            {
                minFreq = sampleRate;
                maxFreq = sampleRate;
            }

            int actualSampleRate = Mathf.Clamp(sampleRate, minFreq, maxFreq);

            // Start recording from the microphone (infinite length, unless specified)
            audioSource.clip = Microphone.Start(_capturingMicrophone, true, 1, actualSampleRate);

            // Start the coroutine to wait for the microphone to begin recording
            _waitForMicrophone = StartCoroutine(WaitForMicrophoneToStart());
            isStarted = true;
        }

        /// <summary>
        /// Coroutine to wait until microphone starts to record.
        /// </summary>
        private IEnumerator WaitForMicrophoneToStart()
        {
            // Wait until the microphone starts recording
            while (!(Microphone.GetPosition(_capturingMicrophone) > 0))
            {
                yield return null; // Wait for the next frame
            }

            // Play the recorded audio
            audioSource.loop = true;
            audioSource.volume = 1;
            audioSource.Play();
            Debug.Log("Started capturing microphone input from " + _capturingMicrophone + ".");
        }

        /// <summary>
        /// Stops capturing audio from the microphone. 
        /// </summary>
        public void StopCapturing()
        {
            // Check if the microphone is currently recording
            if (Microphone.IsRecording(_capturingMicrophone))
            {
                // Stop using the microphone
                Microphone.End(_capturingMicrophone);

                // Stop the audio source
                audioSource.Stop();
                Debug.Log("Stopped capturing microphone input.");
            }
            else
            {
                if (_waitForMicrophone.IsNotNull())
                {
                    StopCoroutine(_waitForMicrophone);
                    _waitForMicrophone = null;
                }
            }

            isStarted = false;
        }
    }
}