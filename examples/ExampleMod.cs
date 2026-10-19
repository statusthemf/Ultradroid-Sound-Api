
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
        "1.0.5"
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

            Info("ULTRADROID Sound Test 1.0.5 loaded.");

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
                    Error("Failed to attach SoundExampleRunner.");
                    return;
                }

                Info("Gameplay detector attached.");
                Info("Waiting for a stable gameplay scene.");
            }
            catch (Exception e)
            {
                Error("Example initialization failed: " + e);
            }
        }

        internal void OnGameplayDetected(string sceneName)
        {
            if (soundRequested)
                return;

            soundRequested = true;

            Info("Gameplay confirmed in scene: " + sceneName);

            try
            {
                // Initialize the Sound API only after gameplay is detected.
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
                Error("Audio initialization failed: " + e);
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
                Error("Sound API returned a null SoundHandle.");
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
        private int candidateSceneHandle = -1;
        private int stableSceneFrames;
        private int waitLogCounter;
        private bool gameplayTriggered;

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

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    ResetCandidate();
                    LogWait("Waiting for a valid loaded scene.");
                    return;
                }

                string sceneName = scene.name ?? "";

                if (IsStartupScene(sceneName))
                {
                    ResetCandidate();
                    LogWait("Skipping startup scene: " + sceneName);
                    return;
                }

                Camera camera = FindSceneCamera(scene);

                if (camera == null)
                {
                    ResetCandidate();
                    LogWait(
                        "Waiting for an enabled camera in " + sceneName
                    );
                    return;
                }

                if (candidateSceneHandle != scene.handle)
                {
                    candidateSceneHandle = scene.handle;
                    stableSceneFrames = 0;

                    Log(
                        "Scene candidate: " + sceneName +
                        "; camera: " + camera.name
                    );
                }

                stableSceneFrames++;

                if (stableSceneFrames < 10)
                    return;

                gameplayTriggered = true;

                Log("Gameplay detected after 10 stable frames.");

                if (ExampleMod.Instance != null)
                {
                    ExampleMod.Instance.OnGameplayDetected(sceneName);
                }
                else
                {
                    LogError("ExampleMod.Instance is null.");
                }
            }
            catch (Exception e)
            {
                LogError("Gameplay detection exception: " + e);
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

        private static Camera FindSceneCamera(Scene scene)
        {
            try
            {
                Camera mainCamera = Camera.main;

                if (IsValidCamera(mainCamera, scene))
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
                    if (IsValidCamera(camera, scene))
                        return camera;
                }
            }
            catch (Exception e)
            {
                LogWarning("Camera search failed: " + e.Message);
            }

            return null;
        }

        private static bool IsValidCamera(Camera camera, Scene scene)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.gameObject.activeInHierarchy &&
                   camera.gameObject.scene.handle == scene.handle;
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
                    "[Sound Test] " + message
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
