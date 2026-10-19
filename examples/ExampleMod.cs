
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
        "1.0.6"
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

            Info("ULTRADROID Sound Test 1.0.6 loaded.");

            try
            {
                SoundAPI.SetLogger(
                    message => Info(message),
                    message => Error(message)
                );

                Info("Sound API logger connected.");

                ClassInjector.RegisterTypeInIl2Cpp<SoundExampleRunner>();

                runner = AddComponent<SoundExampleRunner>();

                if (runner == null)
                {
                    Error("Failed to attach gameplay detector.");
                    return;
                }

                Info("Gameplay detector attached.");
                Info("Waiting for gameplay.");
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

            Info("Gameplay detected: " + sceneName);

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

                Info("Requesting test.mp3.");

                SoundAPI.LoadAndPlay(
                    audioPath,
                    options,
                    OnSoundLoaded
                );
            }
            catch (Exception e)
            {
                Error("Sound initialization or playback request failed: " + e);
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
                Error("Audio callback returned a null handle.");
                soundRequested = false;
                return;
            }

            currentSound = handle;

            Info(
                "Audio loaded successfully. IsPlaying=" +
                currentSound.IsPlaying
            );
        }
    }

    public sealed class SoundExampleRunner : MonoBehaviour
    {
        private int candidateSceneHandle = -1;
        private int stableSceneFrames;
        private int waitLogCounter;

        private bool gameplayTriggered;
        private bool exceptionLogged;

        public SoundExampleRunner(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            if (gameplayTriggered)
                return;

            try
            {
                Scene scene = SceneManager.GetActiveScene();

                // IMPORTANT:
                // Do not call scene.IsValid().
                // This game's IL2CPP runtime cannot resolve
                // Scene.IsValidInternal.

                int sceneHandle = scene.handle;
                string sceneName = scene.name;

                if (sceneHandle < 0 ||
                    string.IsNullOrEmpty(sceneName))
                {
                    ResetCandidate();
                    LogWait("Waiting for an active scene.");
                    return;
                }

                if (IsStartupScene(sceneName))
                {
                    ResetCandidate();
                    LogWait("Skipping startup scene: " + sceneName);
                    return;
                }

                Camera camera = FindSceneCamera(sceneHandle);

                if (camera == null)
                {
                    ResetCandidate();
                    LogWait(
                        "Waiting for an enabled camera in scene: " +
                        sceneName
                    );
                    return;
                }

                if (candidateSceneHandle != sceneHandle)
                {
                    candidateSceneHandle = sceneHandle;
                    stableSceneFrames = 0;

                    Log(
                        "Scene candidate: " + sceneName +
                        ", handle=" + sceneHandle +
                        ", camera=" + camera.name
                    );
                }

                stableSceneFrames++;

                if (stableSceneFrames < 10)
                    return;

                gameplayTriggered = true;

                Log(
                    "Gameplay confirmed after 10 stable frames: " +
                    sceneName
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
                // Prevent the same exception flooding the log every frame.
                if (!exceptionLogged)
                {
                    exceptionLogged = true;
                    LogError("Gameplay detection failed: " + e);
                }
            }
        }

        private static bool IsStartupScene(string sceneName)
        {
            string name = (sceneName ?? "").ToLowerInvariant();

            return name.Contains("bootstrap") ||
                   name.Contains("intro") ||
                   name.Contains("menu") ||
                   name.Contains("title") ||
                   name.Contains("loading") ||
                   name.Contains("credits");
        }

        private static Camera FindSceneCamera(int sceneHandle)
        {
            try
            {
                Camera mainCamera = Camera.main;

                if (IsUsableCamera(mainCamera, sceneHandle))
                    return mainCamera;
            }
            catch (Exception e)
            {
                LogWarning("Camera.main lookup failed: " + e.Message);
            }

            try
            {
                Camera[] cameras =
                    UnityEngine.Object.FindObjectsOfType<Camera>();

                foreach (Camera camera in cameras)
                {
                    if (IsUsableCamera(camera, sceneHandle))
                        return camera;
                }
            }
            catch (Exception e)
            {
                LogWarning("Camera search failed: " + e.Message);
            }

            return null;
        }

        private static bool IsUsableCamera(
            Camera camera,
            int sceneHandle)
        {
            if (camera == null)
                return false;

            if (!camera.isActiveAndEnabled)
                return false;

            if (!camera.gameObject.activeInHierarchy)
                return false;

            return camera.gameObject.scene.handle == sceneHandle;
        }

        private void ResetCandidate()
        {
            candidateSceneHandle = -1;
            stableSceneFrames = 0;
        }

        private void LogWait(string message)
        {
            waitLogCounter++;

            if (waitLogCounter % 180 == 1)
                Log(message);
        }

        private static void Log(string message)
        {
            if (ExampleMod.Instance != null)
            {
                ExampleMod.Instance.Info(
                    "[Gameplay Detector] " + message
                );
            }
        }

        private static void LogWarning(string message)
        {
            if (ExampleMod.Instance != null)
            {
                ExampleMod.Instance.Log.LogWarning(
                    "[Gameplay Detector] " + message
                );
            }
        }

        private static void LogError(string message)
        {
            if (ExampleMod.Instance != null)
            {
                ExampleMod.Instance.Error(
                    "[Gameplay Detector] " + message
                );
            }
        }
    }
}
