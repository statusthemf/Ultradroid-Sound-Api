
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.Sound;

namespace ULTRADROID.Sound.ExampleMod
{
    [BepInPlugin(
        "com.statusthemf.ultradroid.soundtest",
        "ULTRADROID Sound Test",
        "1.0.4"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        private SoundHandle currentSound;

        public override void Load()
        {
            Log.LogInfo("Sound Test 1.0.4 loaded.");

            try
            {
                SoundAPI.SetLogger(
                    message => Log.LogInfo(message),
                    message => Log.LogError(message)
                );

                Log.LogInfo("Sound API logger connected.");

                SoundAPI.Initialize();

                Log.LogInfo("Sound API initialized.");

                string audioPath =
                    "/storage/emulated/0/BepInEx_Android/com.aarongamingdev.ULTRAKILL/BepInEx/plugins/test.mp3";

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

                Log.LogInfo("Audio loading requested.");
            }
            catch (Exception e)
            {
                Log.LogError("Sound test failed: " + e);
            }
        }

        private void OnSoundLoaded(
            SoundHandle handle,
            string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Log.LogError("Audio failed: " + error);
                return;
            }

            if (handle == null)
            {
                Log.LogError("No SoundHandle returned.");
                return;
            }

            currentSound = handle;

            Log.LogInfo(
                "Audio loaded. IsPlaying=" + currentSound.IsPlaying
            );
        }
    }
}
