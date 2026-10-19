
using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.Networking;

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
        private static bool shuttingDown;
        private static bool creatingRuntime;

        private static Action<string> infoLogger;
        private static Action<string> errorLogger;

        private static readonly List<QueuedLoad> queuedLoads =
            new List<QueuedLoad>();

        private static readonly List<PendingLoad> pending =
            new List<PendingLoad>();

        public static bool IsInitialized
        {
            get { return initialized && !shuttingDown; }
        }

        public static bool IsRuntimeAvailable
        {
            get
            {
                try
                {
                    return runtime != null &&
                           runtime.gameObject != null &&
                           runtime.gameObject.activeInHierarchy;
                }
                catch
                {
                    return false;
                }
            }
        }

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
            if (shuttingDown)
            {
                LogError(
                    "Sound API has been shut down and cannot be " +
                    "reinitialized in the current lifecycle."
                );
                return;
            }

            if (initialized)
            {
                LogInfo("Sound API already initialized.");

                if (!IsRuntimeAvailable)
                    CreateRuntime();

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

                LogInfo("Initializing ULTRADROID.Sound API.");

                initialized = true;

                CreateRuntime();

                if (IsRuntimeAvailable)
                    LogInfo("Sound API initialized successfully.");
                else
                    LogError(
                        "Sound API initialized, but its runtime is " +
                        "currently unavailable."
                    );
            }
            catch (Exception e)
            {
                initialized = false;
                LogError("Sound API initialization failed: " + e);
            }
        }

        private static void CreateRuntime()
        {
            if (shuttingDown || creatingRuntime)
                return;

            if (IsRuntimeAvailable)
            {
                StartQueuedLoads();
                return;
            }

            runtime = null;
            creatingRuntime = true;

            GameObject obj = null;

            try
            {
                LogInfo("Creating SoundRuntime.");

                obj = new GameObject("ULTRADROID.SoundRuntime");

                UnityEngine.Object.DontDestroyOnLoad(obj);

                runtime = obj.AddComponent<SoundRuntime>();

                if (runtime == null)
                {
                    LogError("Could not attach SoundRuntime.");
                    UnityEngine.Object.Destroy(obj);
                    obj = null;
                    return;
                }

                LogInfo(
                    "SoundRuntime created. Active=" +
                    obj.activeInHierarchy +
                    ", Scene=" + obj.scene.name
                );

                StartQueuedLoads();
            }
            catch (Exception e)
            {
                runtime = null;
                LogError("Could not create SoundRuntime: " + e);

                if (obj != null)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(obj);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                creatingRuntime = false;
            }
        }

        public static void LoadAndPlay(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            LogInfo("LoadAndPlay called.");

            if (!initialized || shuttingDown)
            {
                FailCallback(
                    callback,
                    "Call SoundAPI.Initialize() before loading audio."
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                FailCallback(callback, "Audio path is empty.");
                return;
            }

            if (!File.Exists(path))
            {
                FailCallback(
                    callback,
                    "Audio file not found: " + path
                );
                return;
            }

            try
            {
                if (options == null)
                    options = new SoundOptions();

                string fullPath = Path.GetFullPath(path);
                FileInfo file = new FileInfo(fullPath);

                LogInfo(
                    "Audio file found. Size=" + file.Length + " bytes."
                );

                queuedLoads.Add(new QueuedLoad
                {
                    Path = fullPath,
                    Options = options,
                    Callback = callback
                });

                LogInfo(
                    "Audio queued. Queue count=" + queuedLoads.Count
                );

                if (!IsRuntimeAvailable)
                {
                    LogInfo(
                        "Runtime unavailable. Attempting recreation."
                    );
                    CreateRuntime();
                }
                else
                {
                    StartQueuedLoads();
                }
            }
            catch (Exception e)
            {
                FailCallback(
                    callback,
                    "Could not queue audio: " + e
                );
            }
        }

        private static void StartQueuedLoads()
        {
            if (shuttingDown || !IsRuntimeAvailable)
                return;

            while (queuedLoads.Count > 0 &&
                   !shuttingDown &&
                   IsRuntimeAvailable)
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

                request = UnityWebRequestMultimedia.GetAudioClip(
                    uri,
                    audioType
                );

                PendingLoad item = new PendingLoad
                {
                    Request = request,
                    Options = options,
                    Callback = callback
                };

                pending.Add(item);

                UnityWebRequestAsyncOperation operation =
                    request.SendWebRequest();

                if (operation == null)
                {
                    pending.Remove(item);

                    try
                    {
                        request.Dispose();
                    }
                    catch
                    {
                    }

                    FailCallback(
                        callback,
                        "Unity returned no async operation."
                    );
                    return;
                }

                LogInfo(
                    "Audio request started. Pending=" + pending.Count
                );
            }
            catch (Exception e)
            {
                if (request != null)
                {
                    try
                    {
                        request.Dispose();
                    }
                    catch
                    {
                    }
                }

                FailCallback(
                    callback,
                    "Could not start audio request: " + e
                );
            }
        }

        internal static void Tick()
        {
            if (shuttingDown)
                return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingLoad item = pending[i];

                try
                {
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
                catch (Exception e)
                {
                    pending.RemoveAt(i);

                    try
                    {
                        if (item.Request != null)
                            item.Request.Dispose();
                    }
                    catch
                    {
                    }

                    FailCallback(
                        item.Callback,
                        "Error checking audio request: " + e
                    );
                }
            }
        }

        private static void ProcessCompletedLoad(PendingLoad item)
        {
            UnityWebRequest request = item.Request;
            AudioClip clip = null;

            try
            {
                if (request == null)
                {
                    FailCallback(
                        item.Callback,
                        "Completed audio request was null."
                    );
                    return;
                }

                if (request.isNetworkError || request.isHttpError)
                {
                    string error = request.error;

                    try
                    {
                        request.Dispose();
                    }
                    catch
                    {
                    }

                    FailCallback(
                        item.Callback,
                        "Audio request failed: " + error
                    );
                    return;
                }

                clip = DownloadHandlerAudioClip.GetContent(request);

                try
                {
                    request.Dispose();
                }
                catch
                {
                }

                if (clip == null)
                {
                    FailCallback(
                        item.Callback,
                        "Unity returned no AudioClip."
                    );
                    return;
                }

                if (shuttingDown || !IsRuntimeAvailable)
                {
                    UnityEngine.Object.Destroy(clip);

                    FailCallback(
                        item.Callback,
                        "SoundRuntime is unavailable after loading."
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
                source.pitch = Mathf.Clamp(item.Options.Pitch, -3f, 3f);
                source.loop = item.Options.Loop;
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                SoundHandle handle = new SoundHandle(clip, source);

                if (item.Options.PlayOnLoad)
                    source.Play();

                LogInfo(
                    "Audio loaded successfully. Length=" +
                    clip.length +
                    ", IsPlaying=" + source.isPlaying
                );

                if (item.Callback != null)
                    item.Callback(handle, null);
            }
            catch (Exception e)
            {
                if (request != null)
                {
                    try
                    {
                        request.Dispose();
                    }
                    catch
                    {
                    }
                }

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
                "SoundRuntime destroyed unexpectedly. Pending requests=" +
                pending.Count +
                ", queued loads=" + queuedLoads.Count
            );

            // Keep pending requests. A future runtime can process them.
            // Runtime recreation requires a subsequent safe API call.
            LogInfo("Pending requests retained.");
        }

        internal static void Shutdown()
        {
            if (shuttingDown)
                return;

            shuttingDown = true;
            initialized = false;

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
                catch
                {
                }

                FailCallback(item.Callback, "Sound API shut down.");
            }

            pending.Clear();

            for (int i = queuedLoads.Count - 1; i >= 0; i--)
            {
                FailCallback(
                    queuedLoads[i].Callback,
                    "Sound API shut down before loading began."
                );
            }

            queuedLoads.Clear();
            runtime = null;

            LogInfo("Sound API shutdown complete.");
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
                "Awake. Active=" +
                gameObject.activeInHierarchy +
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
                LogLifecycle(
                    "Still running. Frames=" + framesObserved
                );

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
