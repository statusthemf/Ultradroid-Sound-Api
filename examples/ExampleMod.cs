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
        "1.0.9"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        internal static ExampleMod Instance;

        private SoundHandle currentSound;
        private SoundExampleRunner runner;

        private bool soundRequested;
        private bool preloadCompleted;

        private string audioPath;
        private SoundOptions soundOptions;

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

            Info("ULTRADROID Sound Test 1.0.9 loaded.");

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

                audioPath =
                    "/storage/emulated/0/BepInEx_Android/" +
                    "com.aarongamingdev.ULTRAKILL/" +
                    "BepInEx/plugins/test.mp3";

                if (!File.Exists(audioPath))
                {
                    Error("Audio file not found: " + audioPath);
                    soundRequested = false;
                    return;
                }

                soundOptions = new SoundOptions
                {
                    Volume = 1f,
                    Pitch = 1f,
                    Loop = false,
                    PlayOnLoad = true
                };

                Info("Preloading test.mp3 into the audio cache.");

                SoundAPI.Load(audioPath, OnClipPreloaded);
            }
            catch (Exception e)
            {
                Error("Sound preload request failed: " + e);
                soundRequested = false;
            }
        }

        private void OnClipPreloaded(AudioClip clip, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Error("Audio preload failed: " + error);
                soundRequested = false;
                return;
            }

            if (clip == null)
            {
                Error("Preload returned a null AudioClip.");
                soundRequested = false;
                return;
            }

            preloadCompleted = true;

            Info(
                "Audio cached successfully. " +
                "Length=" + clip.length +
                " seconds. Cached clips=" +
                SoundAPI.CachedClipCount
            );

            PlayCachedTestSound();
        }

        private void PlayCachedTestSound()
        {
            if (!preloadCompleted)
            {
                Error("Cannot play: audio has not been preloaded.");
                return;
            }

            try
            {
                Info("Playing test.mp3 from the cache.");

                currentSound = SoundAPI.Play(
                    audioPath,
                    soundOptions
                );

                if (currentSound == null)
                {
                    Error(
                        "SoundAPI.Play returned null. " +
                        "Check that the audio is cached and the API is initialized."
                    );
                    return;
                }

                Info(
                    "Cached playback requested. IsPlaying=" +
                    currentSound.IsPlaying
                );
            }
            catch (Exception e)
            {
                Error("Cached playback failed: " + e);
            }
        }

        // Call this method when a gameplay action should trigger the sound.
        // It reuses the cached AudioClip instead of loading the file again.
        internal void PlayActionSound()
        {
            if (!preloadCompleted)
            {
                Info("Action sound ignored: preload is not ready yet.");
                return;
            }

            try
            {
                SoundHandle handle = SoundAPI.Play(
                    audioPath,
                    soundOptions
                );

                if (handle == null)
                {
                    Error("Action playback failed: no SoundHandle returned.");
                    return;
                }

                currentSound = handle;
                Info("Action sound played using the cached clip.");
            }
            catch (Exception e)
            {
                Error("Action sound failed: " + e);
            }
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
