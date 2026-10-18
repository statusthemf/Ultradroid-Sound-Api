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

            string directory = Path.GetDirectoryName(
                typeof(ExampleMod).Assembly.Location
            );

            string[] paths =
            {
                Path.Combine(directory, "test.mp3"),
                Path.Combine(directory, "assets", "test.mp3")
            };

            string audioPath = null;

            foreach (string path in paths)
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
                    "[Sound Test] test.mp3 not found. " +
                    "Put it beside the DLL or in assets/."
                );

                return;
            }

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
                            "[Sound Test] FAILED: " + error
                        );
                    }
                    else
                    {
                        Log.LogInfo(
                            "[Sound Test] Clip loaded. Duration: " +
                            handle.Clip.length + " seconds."
                        );
                    }
                }
            );
        }
    }
}
