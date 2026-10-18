using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.Sound;

namespace ULTRADROID.Sound.ExampleMod
{
    [BepInPlugin(
        "com.statusthemf.ultradroid.soundtest",
        "ULTRADROID Sound Test",
        "1.0.1"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo(
                "Sound Test plugin loaded."
            );

            Log.LogInfo(
                "Testing SoundAPI initialization..."
            );

            try
            {
                SoundAPI.Initialize();

                Log.LogInfo(
                    "SoundAPI.Initialize() completed successfully."
                );
            }
            catch (Exception e)
            {
                Log.LogError(
                    "SoundAPI initialization failed: " + e
                );
            }
        }
    }
}
