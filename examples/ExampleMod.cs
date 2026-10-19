
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
        "1.0.10"
    )]
    public sealed class ExampleMod : BasePlugin
    {
        internal static ExampleMod Instance;

        private SoundExampleRunner runner;
        private SoundHandle currentSound;

        private bool soundRequested;
        private bool preloadCompleted;
        private bool lifecycleTestStarted;
        private bool lifecycleTestFinished;

        private int lifecycleStage;
        private float stageStartedAt;

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

            Info("ULTRADROID Sound Test 1.0.10 loaded.");

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

                Info("Preloading test.mp3.");

                SoundAPI.Load(audioPath, OnClipPreloaded);
            }
            catch (Exception e)
            {
                Error("Sound initialization or preload failed: " + e);
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
                "Audio cached. Length=" + clip.length +
                " seconds; cached clips=" + SoundAPI.CachedClipCount
            );

            StartNaturalCompletionTest();
        }

        private void StartNaturalCompletionTest()
        {
            if (!preloadCompleted)
            {
                Error("Cannot play: preload is not complete.");
                return;
            }

            try
            {
                Info("Starting cached playback.");

                currentSound = SoundAPI.Play(audioPath, soundOptions);

                if (currentSound == null)
                {
                    Error("SoundAPI.Play returned null.");
                    return;
                }

                Info("Playback started. IsPlaying=" + currentSound.IsPlaying);
                Info("Active sounds=" + SoundAPI.ActiveSoundCount);

                lifecycleTestStarted = true;
                lifecycleTestFinished = false;

                // Stage 1: allow playback to run.
                lifecycleStage = 1;
                stageStartedAt = Time.unscaledTime;

                Info("Natural-completion lifecycle test started.");
            }
            catch (Exception e)
            {
                Error("Could not start playback: " + e);
            }
        }

        internal void TickLifecycleTest()
        {
            if (!lifecycleTestStarted || lifecycleTestFinished)
                return;

            try
            {
                float elapsed = Time.unscaledTime - stageStartedAt;

                switch (lifecycleStage)
                {
                    case 1:
                        // Briefly test pause/resume, then let the clip
                        // play all the way to its natural end.
                        if (elapsed < 2f)
                            return;

                        if (currentSound == null)
                        {
                            Error("SoundHandle became null.");
                            FinishTest();
                            return;
                        }

                        currentSound.Pause();

                        Info(
                            "Pause requested. IsPlaying=" +
                            currentSound.IsPlaying
                        );

                        lifecycleStage = 2;
                        stageStartedAt = Time.unscaledTime;
                        break;

                    case 2:
                        if (elapsed < 1f)
                            return;

                        currentSound.Resume();

                        Info(
                            "Resume requested. IsPlaying=" +
                            currentSound.IsPlaying
                        );

                        lifecycleStage = 3;
                        stageStartedAt = Time.unscaledTime;
                        break;

                    case 3:
                        // Wait longer than the expected remaining clip
                        // duration. Crucially, do not call Stop().
                        if (elapsed < 15f)
                            return;

                        Info("NATURAL END CHECK: no Stop() was called.");
                        Info("Active sounds=" + SoundAPI.ActiveSoundCount);
                        Info("Cached clips=" + SoundAPI.CachedClipCount);

                        if (currentSound.IsPlaying)
                        {
                            Info(
                                "Audio is still playing. The test cannot " +
                                "confirm natural completion yet."
                            );

                            // Do not destroy a clip that is still playing.
                            FinishTest();
                            return;
                        }

                        Info(
                            "Playback is no longer reported as playing. " +
                            "Waiting for source cleanup."
                        );

                        lifecycleStage = 4;
                        stageStartedAt = Time.unscaledTime;
                        break;

                    case 4:
                        // Allow SoundRuntime.Update to process cleanup.
                        if (elapsed < 3f)
                            return;

                        int activeCount = SoundAPI.ActiveSoundCount;

                        Info("NATURAL CLEANUP RESULT: active sounds=" + activeCount);

                        if (activeCount == 0)
                        {
                            Info("PASS: no active playback sources remain.");

                            Info("Attempting to unload the cached clip.");
                            SoundAPI.Unload(audioPath);

                            bool stillCached = SoundAPI.IsCached(audioPath);

                            Info("Clip still cached=" + stillCached);

                            if (!stillCached)
                                Info("PASS: cached clip unloaded after cleanup.");
                            else
                                Error("Cache unload did not succeed.");
                        }
                        else
                        {
                            Error(
                                "Natural cleanup was not confirmed. " +
                                "Active sounds=" + activeCount
                            );

                            Info(
                                "Keeping the clip cached because a playback " +
                                "source may still be registered."
                            );
                        }

                        FinishTest();
                        break;
                }
            }
            catch (Exception e)
            {
                Error("Lifecycle test exception: " + e);
                FinishTest();
            }
        }

        private void FinishTest()
        {
            lifecycleTestFinished = true;
            lifecycleTestStarted = false;

            Info("Natural-completion test finished.");
        }

        // Retained for future gameplay-action integration.
        internal void PlayActionSound()
        {
            if (!preloadCompleted)
            {
                Info("Action sound ignored: preload is not ready.");
                return;
            }

            try
            {
                SoundHandle handle = SoundAPI.Play(audioPath, soundOptions);

                if (handle == null)
                {
                    Error("Action playback returned no handle.");
                    return;
                }

                currentSound = handle;
                Info("Action sound played from the cache.");
            }
            catch (Exception e)
            {
                Error("Action playback failed: " + e);
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

                Log("Stable scene confirmed: " + sceneName);

                if (ExampleMod.Instance != null)
                    ExampleMod.Instance.OnGameplayDetected(sceneName);
                else
                    LogError("Plugin instance is unavailable.");
            }
            catch (Exception e)
            {
                LogError("Scene detector or lifecycle test failed: " + e);
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
                ExampleMod.Instance.Info("[Scene Detector] " + message);
        }

        private static void LogError(string message)
        {
            if (ExampleMod.Instance != null)
                ExampleMod.Instance.Error("[Scene Detector] " + message);
        }
    }
}
