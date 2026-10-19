
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
        private static SoundRuntime runtime;
        private static bool runtimeTypeRegistered;

        private static Action<string> infoLogger;
        private static Action<string> errorLogger;

        private sealed class PendingLoad
        {
            public UnityWebRequest Request;
            public SoundOptions Options;
            public Action<SoundHandle, string> Callback;
        }

        private static readonly List<PendingLoad> pending =
            new List<PendingLoad>();

        public static void SetLogger(
            Action<string> info,
            Action<string> error)
        {
            infoLogger = info;
            errorLogger = error;

            LogInfo("Logger connected. BUILD TEST 006");
        }

        internal static void LogInfo(string message)
        {
            if (infoLogger != null)
            {
                infoLogger(message);
                return;
            }

            Debug.Log(message);
        }

        internal static void LogError(string message)
        {
            if (errorLogger != null)
            {
                errorLogger(message);
                return;
            }

            Debug.LogError(message);
        }

        public static void Initialize()
        {
            if (runtime != null)
            {
                LogInfo("Runtime already initialized.");
                return;
            }

            try
            {
                if (!runtimeTypeRegistered)
                {
                    ClassInjector.RegisterTypeInIl2Cpp<SoundRuntime>();
                    runtimeTypeRegistered = true;

                    LogInfo("SoundRuntime type registered.");
                }

                GameObject obj =
                    new GameObject("ULTRADROID.SoundRuntime");

                UnityEngine.Object.DontDestroyOnLoad(obj);

                runtime = obj.AddComponent<SoundRuntime>();

                if (runtime == null)
                {
                    UnityEngine.Object.Destroy(obj);

                    throw new InvalidOperationException(
                        "Unity failed to create SoundRuntime."
                    );
                }

                LogInfo("BUILD TEST 006 - NEW API LOADED");
            }
            catch (Exception e)
            {
                runtime = null;
                LogError("Initialization failed: " + e);
                throw;
            }
        }

        public static void LoadAndPlay(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            LogInfo("LoadAndPlay called. BUILD TEST 006");

            if (runtime == null)
            {
                Fail(callback, "Call SoundAPI.Initialize() first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                Fail(callback, "Audio path is empty.");
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    Fail(callback, "Audio file not found: " + path);
                    return;
                }

                FileInfo fileInfo = new FileInfo(path);

                LogInfo(
                    "File found. Size=" + fileInfo.Length + " bytes."
                );
            }
            catch (Exception e)
            {
                Fail(callback, "Cannot access audio file: " + e);
                return;
            }

            LoadAudio(
                path,
                options ?? new SoundOptions(),
                callback
            );
        }

        public static void LoadAndPlay(
            string path,
            Action<SoundHandle, string> callback)
        {
            LoadAndPlay(path, new SoundOptions(), callback);
        }

        private static void LoadAudio(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            AudioType audioType;

            switch (Path.GetExtension(path).ToLowerInvariant())
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
                    Fail(
                        callback,
                        "Unsupported format. Use MP3, WAV or OGG."
                    );
                    return;
            }

            UnityWebRequest request = null;

            try
            {
                string fullPath = Path.GetFullPath(path)
                    .Replace('\\', '/');

                string fileUri = new Uri(fullPath).AbsoluteUri;

                LogInfo("Starting request: " + fileUri);

                request = UnityWebRequestMultimedia.GetAudioClip(
                    fileUri,
                    audioType
                );

                if (request == null)
                {
                    Fail(
                        callback,
                        "Unity returned a null UnityWebRequest."
                    );
                    return;
                }

                PendingLoad load = new PendingLoad
                {
                    Request = request,
                    Options = options,
                    Callback = callback
                };

                pending.Add(load);

                LogInfo("Sending audio request.");

                UnityWebRequestAsyncOperation operation =
                    request.SendWebRequest();

                if (operation == null)
                {
                    pending.Remove(load);
                    request.Dispose();

                    Fail(
                        callback,
                        "Unity returned a null async operation."
                    );
                    return;
                }

                LogInfo("Request started. Pending=" + pending.Count);
            }
            catch (Exception e)
            {
                pending.RemoveAll(item => item.Request == request);

                if (request != null)
                    request.Dispose();

                Fail(callback, "Could not start audio loading: " + e);
            }
        }

        internal static void Tick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingLoad load = pending[i];

                if (load.Request == null)
                {
                    pending.RemoveAt(i);
                    Fail(load.Callback, "Audio request was null.");
                    continue;
                }

                bool isDone;

                try
                {
                    isDone = load.Request.isDone;
                }
                catch (Exception e)
                {
                    pending.RemoveAt(i);

                    Fail(
                        load.Callback,
                        "Could not check request status: " + e
                    );
                    continue;
                }

                if (!isDone)
                    continue;

                LogInfo(
                    "Request completed. Result=" + load.Request.result
                    + ", Error=" + load.Request.error
                );

                pending.RemoveAt(i);

                try
                {
                    ProcessCompletedLoad(load);
                }
                catch (Exception e)
                {
                    Fail(
                        load.Callback,
                        "Audio processing failed: " + e
                    );
                }
                finally
                {
                    if (load.Request != null)
                        load.Request.Dispose();
                }
            }
        }

        private static void ProcessCompletedLoad(PendingLoad load)
        {
            UnityWebRequest request = load.Request;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail(
                    load.Callback,
                    "Audio loading failed: " + request.error
                );
                return;
            }

            AudioClip clip =
                DownloadHandlerAudioClip.GetContent(request);

            if (clip == null)
            {
                Fail(load.Callback, "Unity returned a null AudioClip.");
                return;
            }

            GameObject sourceObject =
                new GameObject("ULTRADROID Sound Source");

            if (runtime == null)
            {
                UnityEngine.Object.Destroy(sourceObject);

                Fail(
                    load.Callback,
                    "SoundRuntime was destroyed during loading."
                );
                return;
            }

            sourceObject.transform.SetParent(runtime.transform, false);

            AudioSource source =
                sourceObject.AddComponent<AudioSource>();

            if (source == null)
            {
                UnityEngine.Object.Destroy(sourceObject);

                Fail(
                    load.Callback,
                    "Unity failed to create AudioSource."
                );
                return;
            }

            source.clip = clip;
            source.volume = Mathf.Clamp01(load.Options.Volume);
            source.pitch = Mathf.Clamp(load.Options.Pitch, -3f, 3f);
            source.loop = load.Options.Loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            SoundHandle handle = new SoundHandle(clip, source);

            LogInfo(
                "Loaded clip. Duration=" + clip.length
                + " seconds; samples=" + clip.samples
                + "; frequency=" + clip.frequency
            );

            if (load.Options.PlayOnLoad)
            {
                source.Play();

                LogInfo(
                    "Playback requested. IsPlaying=" + source.isPlaying
                );
            }

            if (load.Callback != null)
                load.Callback(handle, null);
        }

        internal static void RuntimeDestroyed()
        {
            runtime = null;

            foreach (PendingLoad load in pending)
            {
                if (load.Request != null)
                    load.Request.Dispose();
            }

            pending.Clear();

            LogInfo("Runtime destroyed.");
        }

        private static void Fail(
            Action<SoundHandle, string> callback,
            string message)
        {
            LogError(message);

            if (callback != null)
            {
                try
                {
                    callback(null, message);
                }
                catch (Exception e)
                {
                    LogError("Error callback threw: " + e);
                }
            }
        }
    }


public sealed class SoundRuntime : MonoBehaviour
{
    private int diagnosticFrames;
    private bool firstUpdateLogged;

    public SoundRuntime(IntPtr ptr) : base(ptr)
    {
    }

    public void Awake()
    {
        SoundAPI.LogInfo(
            "[ULTRADROID Sound] DIAG: Runtime Awake. " +
            "Active=" + gameObject.activeInHierarchy
        );
    }

    public void OnEnable()
    {
        SoundAPI.LogInfo(
            "[ULTRADROID Sound] DIAG: Runtime OnEnable."
        );
    }

    public void Start()
    {
        SoundAPI.LogInfo(
            "[ULTRADROID Sound] DIAG: Runtime Start."
        );
    }

    public void Update()
    {
        diagnosticFrames++;

        if (!firstUpdateLogged)
        {
            firstUpdateLogged = true;

            SoundAPI.LogInfo(
                "[ULTRADROID Sound] DIAG: FIRST UPDATE RECEIVED."
            );
        }

        if (diagnosticFrames % 30 == 0)
        {
            SoundAPI.LogInfo(
                "[ULTRADROID Sound] DIAG: Update frame count=" +
                diagnosticFrames
            );
        }

        SoundAPI.Tick();
    }

    public void OnDisable()
    {
        SoundAPI.LogError(
            "[ULTRADROID Sound] DIAG: Runtime OnDisable."
        );
    }

    public void OnDestroy()
    {
        SoundAPI.LogError(
            "[ULTRADROID Sound] DIAG: Runtime OnDestroy. " +
            "Frames observed=" + diagnosticFrames
        );

        SoundAPI.RuntimeDestroyed();
    }

    public void OnApplicationQuit()
    {
        SoundAPI.LogInfo(
            "[ULTRADROID Sound] DIAG: Application quitting."
        );
    }
}
