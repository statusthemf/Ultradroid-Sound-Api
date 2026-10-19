
using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace ULTRADROID.Sound
{
    public static class SoundAPI
    {
        private sealed class QueuedLoad
        {
            public string Path;
            public SoundOptions Options;
            public Action<SoundHandle, string> Callback;
        }

        private sealed class PendingLoad
        {
            public UnityWebRequest Request;
            public SoundOptions Options;
            public Action<SoundHandle, string> Callback;
        }

        private static SoundRuntime runtime;
        private static bool typeRegistered;
        private static bool initialized;
        private static bool sceneEventRegistered;

        private static Action<string> infoLogger;
        private static Action<string> errorLogger;

        private static readonly List<QueuedLoad> queuedLoads =
            new List<QueuedLoad>();

        private static readonly List<PendingLoad> pending =
            new List<PendingLoad>();

        public static void SetLogger(
            Action<string> info,
            Action<string> error)
        {
            infoLogger = info;
            errorLogger = error;
        }

        internal static void LogInfo(string message)
        {
            try
            {
                if (infoLogger != null)
                    infoLogger(message);
                else
                    Debug.Log(message);
            }
            catch
            {
                Debug.Log(message);
            }
        }

        internal static void LogError(string message)
        {
            try
            {
                if (errorLogger != null)
                    errorLogger(message);
                else
                    Debug.LogError(message);
            }
            catch
            {
                Debug.LogError(message);
            }
        }

        public static void Initialize()
        {
            if (initialized)
            {
                LogInfo("Sound API already initialized.");
                return;
            }

            try
            {
                if (!typeRegistered)
                {
                    ClassInjector.RegisterTypeInIl2Cpp<SoundRuntime>();
                    typeRegistered = true;
                    LogInfo("SoundRuntime type registered.");
                }

                if (!sceneEventRegistered)
                {
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    sceneEventRegistered = true;
                }

                initialized = true;

                LogInfo(
                    "BUILD TEST 010: Sound API initialized. " +
                    "Waiting for a scene load before creating runtime."
                );

                // Handle the case where Initialize is called after
                // a scene has already loaded.
                Scene activeScene = SceneManager.GetActiveScene();

                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    LogInfo(
                        "An active scene already exists: " +
                        activeScene.name
                    );

                    CreateRuntime();
                }
            }
            catch (Exception e)
            {
                initialized = false;
                LogError("Sound API initialization failed: " + e);
            }
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            try
            {
                LogInfo(
                    "Scene loaded: " + scene.name +
                    ", mode=" + mode +
                    ", loaded=" + scene.isLoaded
                );

                if (scene.isLoaded)
                    CreateRuntime();
            }
            catch (Exception e)
            {
                LogError("Scene-loaded handler failed: " + e);
            }
        }

        private static void CreateRuntime()
        {
            if (runtime != null)
            {
                LogInfo("SoundRuntime already exists.");
                StartQueuedLoads();
                return;
            }

            GameObject obj = null;

            try
            {
                obj = new GameObject("ULTRADROID.SoundRuntime");

                UnityEngine.Object.DontDestroyOnLoad(obj);

                runtime = obj.AddComponent<SoundRuntime>();

                LogInfo(
                    "BUILD TEST 010: Runtime created. " +
                    "Object=" + obj.name +
                    ", active=" + obj.activeInHierarchy +
                    ", scene=" + obj.scene.name +
                    ", runtimeNull=" + (runtime == null)
                );

                StartQueuedLoads();
            }
            catch (Exception e)
            {
                LogError("Could not create SoundRuntime: " + e);

                if (obj != null)
                    UnityEngine.Object.Destroy(obj);
            }
        }

        public static void LoadAndPlay(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            LogInfo("LoadAndPlay called. BUILD TEST 010");

            if (string.IsNullOrWhiteSpace(path))
            {
                FailCallback(callback, "Audio path is empty.");
                return;
            }

            if (!File.Exists(path))
            {
                FailCallback(callback, "Audio file not found: " + path);
                return;
            }

            try
            {
                if (options == null)
                    options = new SoundOptions();

                FileInfo file = new FileInfo(path);

                LogInfo(
                    "Audio file found. Size=" +
                    file.Length + " bytes."
                );

                queuedLoads.Add(new QueuedLoad
                {
                    Path = Path.GetFullPath(path),
                    Options = options,
                    Callback = callback
                });

                LogInfo(
                    "Audio queued. Queue count=" +
                    queuedLoads.Count
                );

                if (runtime != null)
                    StartQueuedLoads();
            }
            catch (Exception e)
            {
                FailCallback(callback, "Could not queue audio: " + e);
            }
        }

        private static void StartQueuedLoads()
        {
            if (runtime == null)
                return;

            while (queuedLoads.Count > 0 && runtime != null)
            {
                QueuedLoad item = queuedLoads[0];
                queuedLoads.RemoveAt(0);

                LoadAudio(
                    item.Path,
                    item.Options,
                    item.Callback
                );
            }
        }

        private static void LoadAudio(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            UnityWebRequest request = null;

            try
            {
                string extension =
                    Path.GetExtension(path).ToLowerInvariant();

                AudioType audioType;

                switch (extension)
                {
                    case ".mp3":
                        audioType = AudioType.MPEG;
                        break;

                    case ".wav":
                        audioType = AudioType.WAV;
                        break;

                    case ".ogg":
                        audioType = AudioType.OGGVORBIS;
                        break;

                    default:
                        FailCallback(
                            callback,
                            "Unsupported audio format: " + extension
                        );
                        return;
                }

                string uri = new Uri(path).AbsoluteUri;

                LogInfo("Starting audio request: " + uri);

                request =
                    UnityWebRequestMultimedia.GetAudioClip(
                        uri,
                        audioType
                    );

                pending.Add(new PendingLoad
                {
                    Request = request,
                    Options = options,
                    Callback = callback
                });

                request.SendWebRequest();

                LogInfo(
                    "Audio request started. Pending=" +
                    pending.Count
                );
            }
            catch (Exception e)
            {
                if (request != null)
                {
                    try { request.Dispose(); }
                    catch { }
                }

                FailCallback(
                    callback,
                    "Could not start audio request: " + e
                );
            }
        }

        internal static void Tick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingLoad item = pending[i];

                if (item.Request == null)
                {
                    pending.RemoveAt(i);

                    FailCallback(
                        item.Callback,
                        "Audio request became null."
                    );

                    continue;
                }

                if (!item.Request.isDone)
                    continue;

                pending.RemoveAt(i);
                ProcessCompletedLoad(item);
            }
        }

        private static void ProcessCompletedLoad(PendingLoad item)
        {
            UnityWebRequest request = item.Request;

            try
            {
                if (request.isNetworkError || request.isHttpError)
                {
                    string error = request.error;

                    try { request.Dispose(); }
                    catch { }

                    FailCallback(
                        item.Callback,
                        "Audio request failed: " + error
                    );

                    return;
                }

                AudioClip clip =
                    DownloadHandlerAudioClip.GetContent(request);

                try { request.Dispose(); }
                catch { }

                if (clip == null)
                {
                    FailCallback(
                        item.Callback,
                        "Unity returned no AudioClip."
                    );

                    return;
                }

                if (runtime == null)
                {
                    FailCallback(
                        item.Callback,
                        "SoundRuntime was destroyed before playback."
                    );

                    return;
                }

                GameObject soundObject =
                    new GameObject("ULTRADROID.SoundSource");

                soundObject.transform.SetParent(
                    runtime.transform,
                    false
                );

                AudioSource source =
                    soundObject.AddComponent<AudioSource>();

                source.clip = clip;
                source.volume = Mathf.Clamp01(item.Options.Volume);
                source.pitch = item.Options.Pitch;
                source.loop = item.Options.Loop;
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                // Your existing SoundHandle uses this constructor order.
                SoundHandle handle = new SoundHandle(clip, source);

                if (item.Options.PlayOnLoad)
                    source.Play();

                LogInfo(
                    "Audio loaded successfully. " +
                    "Length=" + clip.length +
                    ", IsPlaying=" + source.isPlaying
                );

                if (item.Callback != null)
                    item.Callback(handle, null);
            }
            catch (Exception e)
            {
                try { request.Dispose(); }
                catch { }

                FailCallback(
                    item.Callback,
                    "Error processing loaded audio: " + e
                );
            }
        }

        private static void FailCallback(
            Action<SoundHandle, string> callback,
            string error)
        {
            LogError(error);

            if (callback == null)
                return;

            try
            {
                callback(null, error);
            }
            catch (Exception e)
            {
                LogError("Audio callback failed: " + e);
            }
        }

        internal static void RuntimeDestroyed()
        {
            runtime = null;

            LogError(
                "SoundRuntime destroyed. Pending requests=" +
                pending.Count
            );

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingLoad item = pending[i];

                try
                {
                    if (item.Request != null)
                    {
                        item.Request.Abort();
                        item.Request.Dispose();
                    }
                }
                catch (Exception e)
                {
                    LogError(
                        "Request cleanup failed: " + e.Message
                    );
                }

                FailCallback(
                    item.Callback,
                    "SoundRuntime was destroyed during audio loading."
                );
            }

            pending.Clear();

            LogInfo(
                "Runtime cleanup complete. Queued loads retained=" +
                queuedLoads.Count
            );
        }
    }

    public sealed class SoundRuntime : MonoBehaviour
    {
        private int framesObserved;
        private bool destroyed;

        public SoundRuntime(IntPtr ptr) : base(ptr)
        {
        }

        private void Awake()
        {
            LogLifecycle(
                "Awake. Active=" + gameObject.activeInHierarchy +
                ", Scene=" + gameObject.scene.name
            );
        }

        private void OnEnable()
        {
            LogLifecycle("OnEnable.");
        }

        private void Start()
        {
            LogLifecycle("Start.");
        }

        private void Update()
        {
            framesObserved++;

            if (framesObserved == 1)
                LogLifecycle("First Update reached.");
            else if (framesObserved % 300 == 0)
                LogLifecycle("Still running. Frames=" + framesObserved);

            SoundAPI.Tick();
        }

        private void OnDisable()
        {
            LogLifecycle(
                "OnDisable. Object=" + gameObject.name +
                ", Active=" + gameObject.activeInHierarchy
            );
        }

        private void OnDestroy()
        {
            if (destroyed)
                return;

            destroyed = true;

            LogLifecycle(
                "OnDestroy. Frames observed=" + framesObserved +
                ", Object=" + gameObject.name
            );

            SoundAPI.RuntimeDestroyed();
        }

        private void OnApplicationQuit()
        {
            LogLifecycle("OnApplicationQuit.");
        }

        private void LogLifecycle(string message)
        {
            SoundAPI.LogInfo("[ULTRADROID Sound] " + message);
        }
    }
}
