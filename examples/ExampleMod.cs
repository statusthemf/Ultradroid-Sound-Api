
using System;
using ULTRADROID.Sound;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace ULTRADROID.Sound.ExampleMod
{
    [BepInPlugin(
        "com.statusthemf.ultradroid.soundtest",
        "ULTRADROID Sound Test",
        "1.0.0"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        private SoundHandle currentSound;

        public override void Load()
        {
            Log.LogInfo("ULTRADROID Sound Test loaded.");

            SoundAPI.Initialize();

            Log.LogInfo(
                "Sound API initialized. Use the PlaySound command " +
                "or call PlayTestSound() from your test code."
            );
        }

        public void PlayTestSound()
        {
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

            Log.LogInfo("Requested test sound playback.");
        }

        private void OnSoundLoaded(
            SoundHandle handle,
            string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Log.LogError("Sound loading failed: " + error);
                return;
            }

            if (handle == null)
            {
                Log.LogError("Sound loading returned no handle.");
                return;
            }

            currentSound = handle;
            Log.LogInfo("Test sound loaded and playback requested.");
        }

        public void StopTestSound()
        {
            if (currentSound != null)
            {
                currentSound.Stop();
                Log.LogInfo("Test sound stopped.");
            }
        }

        public void PauseTestSound()
        {
            if (currentSound != null)
            {
                currentSound.Pause();
                Log.LogInfo("Test sound paused.");
            }
        }

        public void ResumeTestSound()
        {
            if (currentSound != null)
            {
                currentSound.Resume();
                Log.LogInfo("Test sound resumed.");
            }
        }
    }
}
