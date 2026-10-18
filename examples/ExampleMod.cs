
using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.Sound;

namespace ULTRADROID.SoundExample
{
    [BepInPlugin(
        "statusthemf.ultradroid.soundtest",
        "ULTRADROID Sound Test",
        "0.1.0"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo("[Sound Test] Plugin loaded.");

            string pluginDirectory = Path.GetDirectoryName(
                typeof(ExampleMod).Assembly.Location
            );

            if (string.IsNullOrEmpty(pluginDirectory))
            {
                Log.LogError(
                    "[Sound Test] Could not determine plugin directory."
                );
                return;
            }

            string[] possiblePaths =
            {
                Path.Combine(pluginDirectory, "test.mp3"),
                Path.Combine(pluginDirectory, "assets", "test.mp3")
            };

            string audioPath = null;

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    audioPath = path;
                    break;
                }
            }

            if (audioPath == null)
            {
                Log.LogError(
                    "[Sound Test] test.mp3 was not found. " +
                    "Place it beside ULTRADROID.SoundTest.dll " +
                    "or inside the assets folder."
                );
                return;
            }

            Log.LogInfo(
                "[Sound Test] Audio file found: " + audioPath
            );

            try
            {
                SoundAPI.Initialize();

                SoundAPI.LoadAndPlay(
                    audioPath,
                    new SoundOptions
                    {
                        Volume = 1f,
                        Pitch = 1f,
                        Loop = false,
                        PlayOnLoad = true
                    },
                    (handle, error) =>
                    {
                        if (handle == null)
                        {
                            Log.LogError(
                                "[Sound Test] Audio loading failed: " +
                                error
                            );
                            return;
                        }

                        Log.LogInfo(
                            "[Sound Test] Audio loaded successfully. " +
                            "Duration: " +
                            handle.Clip.length +
                            " seconds."
                        );

                        Log.LogInfo(
                            "[Sound Test] Playback was requested."
                        );
                    }
                );
            }
            catch (Exception exception)
            {
                Log.LogError(
                    "[Sound Test] Exception: " + exception
                );
            }
        }
    }
}
