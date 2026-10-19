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
        private sealed class PendingLoad
        {
            public string Path;
            public UnityWebRequest Request;
        }

        private static SoundRuntime runtime;
        private static bool typeRegistered;
        private static bool initialized;
        private static bool shuttingDown;
        private static bool creatingRuntime;

        private static Action<string> infoLogger;
        private static Action<string> errorLogger;

        private static readonly Dictionary<string, AudioClip> cache =
            new Dictionary<string, AudioClip>(
                StringComparer.OrdinalIgnoreCase
            );

        private static readonly Dictionary<string, List<Action<AudioClip, string>>>
            loadCallbacks =
                new Dictionary<string, List<Action<AudioClip, string>>>(
                    StringComparer.OrdinalIgnoreCase
                );

        private static readonly List<PendingLoad> pending =
            new List<PendingLoad>();

        private static readonly List<AudioSource> sources =
            new List<AudioSource>();

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

        public static int CachedClipCount
        {
            get { return cache.Count; }
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
                LogError("Sound API has been shut down.");
                return;
            }

            if (!typeRegistered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<SoundRuntime>();
                typeRegistered = true;
                LogInfo("SoundRuntime type registered.");
            }

            initialized = true;

            if (!IsRuntimeAvailable)
                CreateRuntime();

            if (IsRuntimeAvailable)
                LogInfo("Sound API initialized successfully.");
            else
                LogError("Sound runtime is unavailable.");
        }

        private static void CreateRuntime()
        {
            if (shuttingDown || creatingRuntime)
                return;

            if (IsRuntimeAvailable)
                return;

            creatingRuntime = true;
            GameObject obj = null;

            try
            {
                obj = new GameObject("ULTRADROID.SoundRuntime");
                UnityEngine.Object.DontDestroyOnLoad(obj);

                runtime = obj.AddComponent<SoundRuntime>();

                if (runtime == null)
                {
                    UnityEngine.Object.Destroy(obj);
                    obj = null;
                    LogError("Could not attach SoundRuntime.");
                    return;
                }

                LogInfo("SoundRuntime created.");
            }
            catch (Exception e)
            {
                runtime = null;
                LogError("Could not create SoundRuntime: " + e);

                if (obj != null)
                    UnityEngine.Object.Destroy(obj);
            }
            finally
            {
                creatingRuntime = false;
            }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path);
        }

        // Loads an audio file once and keeps its AudioClip cached.
        // Calling Load again for a cached file does not reload it.
        public static void Load(
            string path,
            Action<AudioClip, string> callback)
        {
            if (!initialized || shuttingDown)
            {
                CompleteCallback(
                    callback, null,
                    "Call SoundAPI.Initialize() first."
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                CompleteCallback(callback, null, "Audio path is empty.");
                return;
            }

            try
            {
                string fullPath = NormalizePath(path);

                AudioClip cachedClip;

                if (cache.TryGetValue(fullPath, out cachedClip) &&
                    cachedClip != null)
                {
                    LogInfo("Using cached audio: " + fullPath);
                    CompleteCallback(callback, cachedClip, null);
                    return;
                }

                if (!File.Exists(fullPath))
                {
                    CompleteCallback(
                        callback, null,
                        "Audio file not found: " + fullPath
                    );
                    return;
                }

                if (loadCallbacks.ContainsKey(fullPath))
                {
                    if (callback != null)
                        loadCallbacks[fullPath].Add(callback);

                    LogInfo("Audio is already loading: " + fullPath);
                    return;
                }

                if (!IsRuntimeAvailable)
                    CreateRuntime();

                if (!IsRuntimeAvailable)
                {
                    CompleteCallback(
                        callback, null,
                        "SoundRuntime is unavailable."
                    );
                    return;
                }

                List<Action<AudioClip, string>> callbacks =
                    new List<Action<AudioClip, string>>();

                if (callback != null)
                    callbacks.Add(callback);

                loadCallbacks.Add(fullPath, callbacks);

                StartLoading(fullPath);
            }
            catch (Exception e)
            {
                CompleteCallback(
                    callback, null,
                    "Could not load audio: " + e
                );
            }
        }

        private static void StartLoading(string path)
        {
            UnityWebRequest request = null;

            try
            {
                string extension =
                    Path.GetExtension(path).ToLowerInvariant();

                AudioType type;

                switch (extension)
                {
                    case ".mp3":
                        type = AudioType.MPEG;
                        break;
                    case ".wav":
                        type = AudioType.WAV;
                        break;
                    case ".ogg":
                        type = AudioType.OGGVORBIS;
                        break;
                    default:
                        FinishLoading(
                            path, null,
                            "Unsupported audio format: " + extension
                        );
                        return;
                }

                request = UnityWebRequestMultimedia.GetAudioClip(
                    new Uri(path).AbsoluteUri,
                    type
                );

                PendingLoad item = new PendingLoad
                {
                    Path = path,
                    Request = request
                };

                pending.Add(item);

                UnityWebRequestAsyncOperation operation =
                    request.SendWebRequest();

                if (operation == null)
                {
                    pending.Remove(item);
                    request.Dispose();

                    FinishLoading(
                        path, null,
                        "Unity returned no async operation."
                    );
                    return;
                }

                LogInfo("Started loading audio: " + path);
            }
            catch (Exception e)
            {
                if (request != null)
                {
                    try { request.Dispose(); }
                    catch { }
                }

                FinishLoading(
                    path, null,
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
                        FinishLoading(
                            item.Path, null,
                            "Audio request became null."
                        );
                        continue;
                    }

                    if (!item.Request.isDone)
                        continue;

                    pending.RemoveAt(i);

                    AudioClip clip = null;
                    string error = null;

                    if (item.Request.isNetworkError ||
                        item.Request.isHttpError)
                    {
                        error = item.Request.error;
                    }
                    else
                    {
                        clip = DownloadHandlerAudioClip.GetContent(
                            item.Request
                        );

                        if (clip == null)
                            error = "Unity returned no AudioClip.";
                    }

                    try { item.Request.Dispose(); }
                    catch { }

                    FinishLoading(item.Path, clip, error);
                }
                catch (Exception e)
                {
                    pending.RemoveAt(i);

                    try
                    {
                        if (item.Request != null)
                            item.Request.Dispose();
                    }
                    catch { }

                    FinishLoading(
                        item.Path, null,
                        "Error processing audio request: " + e
                    );
                }
            }
        }

        private static void FinishLoading(
            string path,
            AudioClip clip,
            string error)
        {
            List<Action<AudioClip, string>> callbacks;

            if (!loadCallbacks.TryGetValue(path, out callbacks))
                callbacks = new List<Action<AudioClip, string>>();
            else
                loadCallbacks.Remove(path);

            if (!string.IsNullOrEmpty(error) || clip == null)
            {
                if (clip != null)
                    UnityEngine.Object.Destroy(clip);

                if (string.IsNullOrEmpty(error))
                    error = "Audio loading failed.";

                LogError(error);

                for (int i = 0; i < callbacks.Count; i++)
                    CompleteCallback(callbacks[i], null, error);

                return;
            }

            cache[path] = clip;

            LogInfo(
                "Cached audio: " + path +
                ", length=" + clip.length
            );

            for (int i = 0; i < callbacks.Count; i++)
                CompleteCallback(callbacks[i], clip, null);
        }

        private static void CompleteCallback(
            Action<AudioClip, string> callback,
            AudioClip clip,
            string error)
        {
            if (callback == null)
                return;

            try
            {
                callback(clip, error);
            }
            catch (Exception e)
            {
                LogError("Audio callback failed: " + e);
            }
        }

        // Plays a clip that has already been loaded.
        // Returns null if the clip is not cached.
        public static SoundHandle Play(
            string path,
            SoundOptions options = null)
        {
            if (!initialized || shuttingDown)
            {
                LogError("Initialize the Sound API before playback.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                LogError("Audio path is empty.");
                return null;
            }

            string fullPath;

            try
            {
                fullPath = NormalizePath(path);
            }
            catch (Exception e)
            {
                LogError("Invalid audio path: " + e);
                return null;
            }

            AudioClip clip;

            if (!cache.TryGetValue(fullPath, out clip) || clip == null)
            {
                LogError(
                    "Audio is not cached. Call SoundAPI.Load() first: " +
                    fullPath
                );
                return null;
            }

            if (!IsRuntimeAvailable)
                CreateRuntime();

            if (!IsRuntimeAvailable)
            {
                LogError("SoundRuntime is unavailable.");
                return null;
            }

            if (options == null)
                options = new SoundOptions();

            try
            {
                GameObject obj = new GameObject(
                    "ULTRADROID.SoundSource"
                );

                obj.transform.SetParent(runtime.transform, false);

                AudioSource source = obj.AddComponent<AudioSource>();

                source.clip = clip;
                source.volume = Mathf.Clamp01(options.Volume);
                source.pitch = Mathf.Clamp(options.Pitch, -3f, 3f);
                source.loop = options.Loop;
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                SoundHandle handle = new SoundHandle(clip, source);

                sources.Add(source);

                if (options.PlayOnLoad)
                    source.Play();

                return handle;
            }
            catch (Exception e)
            {
                LogError("Could not start audio playback: " + e);
                return null;
            }
        }

        // Backwards-compatible method.
        // Loads if necessary, then plays once loading completes.
        public static void LoadAndPlay(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            Load(path, (clip, error) =>
            {
                if (!string.IsNullOrEmpty(error) || clip == null)
                {
                    CompleteSoundCallback(callback, null, error);
                    return;
                }

                SoundHandle handle = Play(path, options);

                if (handle == null)
                {
                    CompleteSoundCallback(
                        callback, null,
                        "Audio was loaded, but playback could not start."
                    );
                    return;
                }

                CompleteSoundCallback(callback, handle, null);
            });
        }

        private static void CompleteSoundCallback(
            Action<SoundHandle, string> callback,
            SoundHandle handle,
            string error)
        {
            if (callback == null)
                return;

            try { callback(handle, error); }
            catch (Exception e)
            {
                LogError("Sound callback failed: " + e);
            }
        }

        // Optional cache management.
        public static bool IsCached(string path)
        {
            try
            {
                AudioClip clip;
                return cache.TryGetValue(
                    NormalizePath(path), out clip
                ) && clip != null;
            }
            catch
            {
                return false;
            }
        }

        public static void Unload(string path)
        {
            try
            {
                string fullPath = NormalizePath(path);
                AudioClip clip;

                if (!cache.TryGetValue(fullPath, out clip))
                    return;

                cache.Remove(fullPath);

                if (clip != null)
                    UnityEngine.Object.Destroy(clip);

                LogInfo("Unloaded cached audio: " + fullPath);
            }
            catch (Exception e)
            {
                LogError("Could not unload audio: " + e);
            }
        }

        internal static void RuntimeDestroyed()
        {
            runtime = null;
            LogError("SoundRuntime was destroyed.");
        }

        internal static void Shutdown()
        {
            if (shuttingDown)
                return;

            shuttingDown = true;
            initialized = false;

            for (int i = 0; i < pending.Count; i++)
            {
                try
                {
                    if (pending[i].Request != null)
                    {
                        pending[i].Request.Abort();
                        pending[i].Request.Dispose();
                    }
                }
                catch { }
            }

            pending.Clear();

            foreach (var entry in loadCallbacks)
            {
                foreach (var callback in entry.Value)
                    CompleteCallback(
                        callback, null, "Sound API shut down."
                    );
            }

            loadCallbacks.Clear();

            foreach (var entry in cache)
            {
                if (entry.Value != null)
                    UnityEngine.Object.Destroy(entry.Value);
            }

            cache.Clear();

            for (int i = 0; i < sources.Count; i++)
            {
                try
                {
                    if (sources[i] != null)
                        UnityEngine.Object.Destroy(
                            sources[i].gameObject
                        );
                }
                catch { }
            }

            sources.Clear();
            runtime = null;

            LogInfo("Sound API shutdown complete.");
        }
    }

    public sealed class SoundRuntime : MonoBehaviour
    {
        public SoundRuntime(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            SoundAPI.Tick();
        }

        private void OnDestroy()
        {
            SoundAPI.RuntimeDestroyed();
        }
    }
}
