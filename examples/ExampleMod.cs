
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
        "1.0.7"
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

            Info("ULTRADROID Sound Test 1.0.7 loaded.");

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
                Info("Waiting for gameplay scene detection.");
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
                Error("Sound initialization failed: " + e);
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
                Error("Audio callback returned a null SoundHandle.");
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
        private int candidateSceneHandle = int.MinValue;
        private int stableSceneFrames;
        private int diagnosticFrames;

        private bool gameplayTriggered;
        private bool exceptionLogged;

        public SoundExampleRunner(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            if (gameplayTriggered)
                return;

            diagnosticFrames++;

            try
            {
                Scene activeScene = SceneManager.GetActiveScene();

                int activeHandle = activeScene.handle;
                string activeName = activeScene.name;

                // Periodically print actual scene data.
                if (diagnosticFrames % 180 == 1)
                {
                    Log(
                        "SCENE DIAGNOSTIC: ActiveName='" +
                        (activeName ?? "<null>") +
                        "', ActiveHandle=" + activeHandle +
                        ", SceneCount=" + SceneManager.sceneCount
                    );

                    LogAllScenes();
                }

                Scene candidateScene = activeScene;
                string candidateName = activeName;
                int candidateHandle = activeHandle;

                // If the active scene has no usable name or handle,
                // inspect scenes reported by SceneManager.
                if (candidateHandle < 0 ||
                    string.IsNullOrEmpty(candidateName))
                {
                    bool found = false;
                    int count = SceneManager.sceneCount;

                    for (int i = 0; i < count; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);

                        string name = scene.name;
                        int handle = scene.handle;

                        if (handle < 0 || string.IsNullOrEmpty(name))
                            continue;

                        if (IsStartupScene(name))
                            continue;

                        Camera sceneCamera = FindSceneCamera(handle);

                        if (sceneCamera == null)
                            continue;

                        candidateScene = scene;
                        candidateName = name;
                        candidateHandle = handle;
                        found = true;

                        Log(
                            "Fallback scene found: '" + name +
                            "', Handle=" + handle +
                            ", Camera=" + sceneCamera.name
                        );

                        break;
                    }

                    if (!found)
                    {
                        ResetCandidate();
                        return;
                    }
                }

                if (IsStartupScene(candidateName))
                {
                    ResetCandidate();
                    LogWait("Skipping startup scene: " + candidateName);
                    return;
                }

                Camera camera = FindSceneCamera(candidateHandle);

                if (camera == null)
                {
                    ResetCandidate();
                    LogWait(
                        "No enabled camera in scene '" +
                        candidateName + "'."
                    );
                    return;
                }

                if (candidateSceneHandle != candidateHandle)
                {
                    candidateSceneHandle = candidateHandle;
                    stableSceneFrames = 0;

                    Log(
                        "Scene candidate: '" + candidateName +
                        "', Handle=" + candidateHandle +
                        ", Camera='" + camera.name + "'."
                    );
                }

                stableSceneFrames++;

                if (stableSceneFrames < 10)
                    return;

                gameplayTriggered = true;

                Log(
                    "Gameplay confirmed after 10 stable frames: " +
                    candidateName
                );

                if (ExampleMod.Instance != null)
                {
                    ExampleMod.Instance.OnGameplayDetected(candidateName);
                }
                else
                {
                    LogError("ExampleMod.Instance is null.");
                }
            }
            catch (Exception e)
            {
                if (!exceptionLogged)
                {
                    exceptionLogged = true;
                    LogError("Scene detection exception: " + e);
                }
            }
        }

        private void LogAllScenes()
        {
            try
            {
                int count = SceneManager.sceneCount;

                for (int i = 0; i < count; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);

                    Log(
                        "SCENE[" + i + "]: Name='" +
                        (scene.name ?? "<null>") +
                        "', Handle=" + scene.handle
                    );
                }
            }
            catch (Exception e)
            {
                LogError("Could not enumerate scenes: " + e.Message);
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
                LogWarning("Camera.main failed: " + e.Message);
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
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.gameObject.activeInHierarchy &&
                   camera.gameObject.scene.handle == sceneHandle;
        }

        private void ResetCandidate()
        {
            candidateSceneHandle = int.MinValue;
            stableSceneFrames = 0;
        }

        private void LogWait(string message)
        {
            if (diagnosticFrames % 180 == 1)
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
