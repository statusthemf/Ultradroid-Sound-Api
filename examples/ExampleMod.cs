
using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using ULTRADROID.Sound;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ULTRADROID.Sound.ExampleMod
{
    [BepInPlugin(
        "com.statusthemf.ultradroid.soundtest",
        "ULTRADROID Sound Test",
        "1.0.8"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        internal static ExampleMod Instance;

        private SoundHandle currentSound;
        private SoundExampleRunner runner;
        private bool soundRequested;

        internal void Info(string message)
        {
            Log.LogInfo("[Sound Test] " + message);
        }

        internal void Error(string message)
        {
            Log.LogError("[Sound Test] " + message);
        }

        public override void Load()
        {
            Instance = this;

            Info("ULTRADROID Sound Test 1.0.8 loaded.");

            try
            {
                SoundAPI.SetLogger(
                    message => Info(message),
                    message => Error(message)
                );

                ClassInjector.RegisterTypeInIl2Cpp<SoundExampleRunner>();

                runner = AddComponent<SoundExampleRunner>();

                if (runner == null)
                {
                    Error("Failed to attach scene detector.");
                    return;
                }

                Info("Scene detector attached.");
                Info("Waiting for gameplay scene.");
            }
            catch (Exception e)
            {
                Error("Plugin initialization failed: " + e);
            }
        }

        internal void OnGameplayDetected(string sceneName)
        {
            if (soundRequested)
                return;

            soundRequested = true;

            Info("Gameplay scene detected: " + sceneName);

            try
            {
                SoundAPI.Initialize();

                string audioPath =
                    "/storage/emulated/0/BepInEx_Android/" +
                    "com.aarongamingdev.ULTRAKILL/" +
                    "BepInEx/plugins/test.mp3";

                if (!File.Exists(audioPath))
                {
                    Error("Audio file not found: " + audioPath);
                    soundRequested = false;
                    return;
                }

                SoundOptions options = new SoundOptions
                {
                    Volume = 1f,
                    Pitch = 1f,
                    Loop = false,
                    PlayOnLoad = true
                };

                Info("Loading test.mp3.");

                SoundAPI.LoadAndPlay(
                    audioPath,
                    options,
                    OnSoundLoaded
                );
            }
            catch (Exception e)
            {
                Error("Sound playback request failed: " + e);
                soundRequested = false;
            }
        }

        private void OnSoundLoaded(SoundHandle handle, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Error("Audio loading failed: " + error);
                soundRequested = false;
                return;
            }

            if (handle == null)
            {
                Error("Sound API returned a null handle.");
                soundRequested = false;
                return;
            }

            currentSound = handle;

            Info(
                "Audio loaded. IsPlaying=" +
                currentSound.IsPlaying
            );
        }
    }

    public sealed class SoundExampleRunner : MonoBehaviour
    {
        private int previousSceneHandle = int.MinValue;
        private int stableFrames;
        private bool triggered;
        private int logCounter;

        public SoundExampleRunner(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            if (triggered)
                return;

            try
            {
                Scene scene = SceneManager.GetActiveScene();

                string sceneName = scene.name ?? "";
                int sceneHandle = scene.handle;

                if (string.IsNullOrEmpty(sceneName))
                {
                    ResetDetection();
                    return;
                }

                if (IsStartupScene(sceneName))
                {
                    ResetDetection();
                    LogOccasionally(
                        "Skipping startup scene: " + sceneName
                    );
                    return;
                }

                if (sceneHandle != previousSceneHandle)
                {
                    previousSceneHandle = sceneHandle;
                    stableFrames = 0;

                    Log("Detected scene change: " + sceneName);
                }

                stableFrames++;

                if (stableFrames < 10)
                    return;

                triggered = true;

                Log(
                    "Stable scene confirmed: " + sceneName +
                    ". Starting Sound API."
                );

                if (ExampleMod.Instance != null)
                {
                    ExampleMod.Instance.OnGameplayDetected(sceneName);
                }
                else
                {
                    LogError("Plugin instance is unavailable.");
                }
            }
            catch (Exception e)
            {
                LogError("Scene detection failed: " + e);
            }
        }

        private static bool IsStartupScene(string sceneName)
        {
            string name = (sceneName ?? "").ToLowerInvariant();

            return name.Contains("bootstrap") ||
                   name.Contains("intro") ||
                   name.Contains("main menu") ||
                   name == "menu" ||
                   name.Contains("loading") ||
                   name.Contains("credits");
        }

        private void ResetDetection()
        {
            previousSceneHandle = int.MinValue;
            stableFrames = 0;
        }

        private void LogOccasionally(string message)
        {
            logCounter++;

            if (logCounter % 180 == 1)
                Log(message);
        }

        private static void Log(string message)
        {
            if (ExampleMod.Instance != null)
            {
                ExampleMod.Instance.Info(
                    "[Scene Detector] " + message
                );
            }
        }

        private static void LogError(string message)
        {
            if (ExampleMod.Instance != null)
            {
                ExampleMod.Instance.Error(
                    "[Scene Detector] " + message
                );
            }
        }
    }
}
