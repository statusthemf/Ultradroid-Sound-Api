
using System;
using ULTRADROID.Sound;
using UnityEngine;

namespace ULTRADROID.Sound.ExampleMod
{
    public sealed class ExampleMod : MonoBehaviour
    {
        private SoundHandle currentSound;

        private void Start()
        {
            SoundAPI.Initialize();

            Debug.Log("[ULTRADROID Sound Example] Initialized.");

            // Replace this with the real path to an audio file
            // on your Android device.
            string audioPath =
                "/storage/emulated/0/Download/example.wav";

            SoundOptions options = new SoundOptions
            {
                Volume = 1f,
                Pitch = 1f,
                Loop = false,
                PlayOnLoad = true
            };

            SoundAPI.LoadAndPlay(
                audioPath,
                options,
                OnSoundLoaded
            );
        }

        private void OnSoundLoaded(
            SoundHandle handle,
            string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(
                    "[ULTRADROID Sound Example] " + error
                );
                return;
            }

            if (handle == null)
            {
                Debug.LogError(
                    "[ULTRADROID Sound Example] No sound handle returned."
                );
                return;
            }

            currentSound = handle;

            Debug.Log(
                "[ULTRADROID Sound Example] Sound loaded successfully."
            );
        }

        public void StopSound()
        {
            if (currentSound != null)
                currentSound.Stop();
        }

        public void PauseSound()
        {
            if (currentSound != null)
                currentSound.Pause();
        }

        public void ResumeSound()
        {
            if (currentSound != null)
                currentSound.Resume();
        }
    }
}
