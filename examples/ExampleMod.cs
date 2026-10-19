
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
        private bool lifecycleTestStarted;
        private bool lifecycleTestFinished;

        private int lifecycleStage;
        private float lifecycleStageTime;

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
                "Audio cached successfully. Length=" +
                clip.length +
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
                    Error("SoundAPI.Play returned null.");
                    return;
                }

                Info(
                    "Initial playback requested. IsPlaying=" +
                    currentSound.IsPlaying
                );

                Info(
                    "Active sounds after playback request: " +
                    SoundAPI.ActiveSoundCount
                );

                // Begin the automatic lifecycle test.
                lifecycleTestStarted = true;
                lifecycleTestFinished = false;
                lifecycleStage = 1;
                lifecycleStageTime = Time.unscaledTime;

                Info("Lifecycle test started.");
            }
            catch (Exception e)
            {
                Error("Cached playback failed: " + e);
            }
        }

        // Called every frame by SoundExampleRunner.
        internal void TickLifecycleTest()
        {
            if (!lifecycleTestStarted || lifecycleTestFinished)
                return;

            float elapsed = Time.unscaledTime - lifecycleStageTime;

            try
            {
                switch (lifecycleStage)
                {
                    case 1:
                        // Let the sound play for two seconds.
                        if (elapsed < 2f)
                            return;

                        if (currentSound == null)
                        {
                            Error("Handle became null before pause test.");
                            FinishLifecycleTest();
                            return;
                        }

                        currentSound.Pause();

                        Info(
                            "PAUSE TEST: requested. IsPlaying=" +
                            currentSound.IsPlaying
                        );

                        lifecycleStage = 2;
                        lifecycleStageTime = Time.unscaledTime;
                        break;

                    case 2:
                        // Leave it paused for one second.
                        if (elapsed < 1f)
                            return;

                        currentSound.Resume();

                        Info(
                            "RESUME TEST: requested. IsPlaying=" +
                            currentSound.IsPlaying
                        );

                        lifecycleStage = 3;
                        lifecycleStageTime = Time.unscaledTime;
                        break;

                    case 3:
                        // Allow playback to continue before testing unload.
                        if (elapsed < 2f)
                            return;

                        Info(
                            "UNLOAD TEST: trying to unload while the " +
                            "sound is active."
                        );

                        SoundAPI.Unload(audioPath);

                        Info(
                            "Cache present after active unload attempt: " +
                            SoundAPI.IsCached(audioPath)
                        );

                        lifecycleStage = 4;
                        lifecycleStageTime = Time.unscaledTime;
                        break;

                    case 4:
                        Info("STOP TEST: stopping the active sound.");

                        if (currentSound != null)
                            currentSound.Stop();

                        Info(
                            "Stop requested. Active sounds reported: " +
                            SoundAPI.ActiveSoundCount
                        );

                        lifecycleStage = 5;
                        lifecycleStageTime = Time.unscaledTime;
                        break;

                    case 5:
                        // Allow SoundAPI.Update to perform cleanup.
                        if (elapsed < 2f)
                            return;

                        Info(
                            "CLEANUP CHECK: active sounds=" +
                            SoundAPI.ActiveSoundCount
                        );

                        Info(
                            "CLEANUP CHECK: cached clips=" +
                            SoundAPI.CachedClipCount
                        );

                        Info("UNLOAD TEST: retrying after cleanup.");

                        SoundAPI.Unload(audioPath);

                        bool stillCached = SoundAPI.IsCached(audioPath);

                        Info(
                            "Cache present after final unload attempt: " +
                            stillCached
                        );

                        if (stillCached)
                        {
                            Info(
                                "The clip remains cached. Check the SoundAPI " +
                                "logs for active-source or unload reasons."
                            );
                        }
                        else
                        {
                            Info("Cache unload confirmed.");
                        }

                        FinishLifecycleTest();
                        break;
                }
            }
            catch (Exception e)
            {
                Error("Lifecycle test failed: " + e);
                FinishLifecycleTest();
            }
        }

        private void FinishLifecycleTest()
        {
            lifecycleTestFinished = true;
            lifecycleTestStarted = false;

            Info(
                "Lifecycle test finished. Review the logs for each " +
                "test result."
            );
        }

        // Retained for future gameplay-action integration.
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
            try
            {
                // Keep running lifecycle tests after scene detection.
                if (ExampleMod.Instance != null)
                    ExampleMod.Instance.TickLifecycleTest();

                if (triggered)
                    return;

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
                LogError("Scene detection or lifecycle test failed: " + e);
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
