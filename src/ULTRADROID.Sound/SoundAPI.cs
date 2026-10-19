
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

        private sealed class ActivePlayback
        {
            public string Path;
            public AudioClip Clip;
            public SoundHandle Handle;
            public AudioSource Source;
        }

        private static SoundRuntime runtime;
        private static bool typeRegistered;
        private static bool initialized;
        private static bool shuttingDown;
        private static bool creatingRuntime;

        private static Action<string> errorLogger;

        private static readonly Dictionary<string, AudioClip> cache =
            new Dictionary<string, AudioClip>(
                StringComparer.OrdinalIgnoreCase
            );

        private static readonly Dictionary<
            string, List<Action<AudioClip, string>>
        > loadCallbacks =
            new Dictionary<string, List<Action<AudioClip, string>>>(
                StringComparer.OrdinalIgnoreCase
            );

        private static readonly List<PendingLoad> pending =
            new List<PendingLoad>();

        private static readonly List<ActivePlayback> sources =
            new List<ActivePlayback>();

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

        public static int ActiveSoundCount
        {
            get { return sources.Count; }
        }

        // Kept for compatibility with existing callers.
        // Routine informational logging is intentionally disabled.
        public static void SetLogger(
            Action<string> info,
            Action<string> error)
        {
            errorLogger = error;
        }

        internal static void LogError(string message)
        {
            try
            {
                if (errorLogger != null)
                    errorLogger(message);
                else
                    Debug.LogError("[ULTRADROID Sound] " + message);
            }
            catch
            {
                Debug.LogError("[ULTRADROID Sound] " + message);
            }
        }

        public static void Initialize()
        {
            if (shuttingDown)
            {
                LogError("The Sound API has been shut down.");
                return;
            }

            try
            {
                if (!typeRegistered)
                {
                    ClassInjector.RegisterTypeInIl2Cpp<SoundRuntime>();
                    typeRegistered = true;
                }

                initialized = true;

                if (!IsRuntimeAvailable)
                    CreateRuntime();

                if (!IsRuntimeAvailable)
                    LogError("The sound runtime is unavailable.");
            }
            catch (Exception e)
            {
                initialized = false;
                LogError("Initialization failed: " + e.Message);
            }
        }

        private static void CreateRuntime()
        {
            if (shuttingDown || creatingRuntime || IsRuntimeAvailable)
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
                    LogError("Could not attach the sound runtime.");
                }
            }
            catch (Exception e)
            {
                runtime = null;
                LogError("Could not create the sound runtime: " + e.Message);

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
                    IsUsableClip(cachedClip))
                {
                    CompleteCallback(callback, cachedClip, null);
                    return;
                }

                // Remove a stale or invalid cache entry.
                if (cache.TryGetValue(fullPath, out cachedClip))
                {
                    cache.Remove(fullPath);

                    if (cachedClip != null)
                        UnityEngine.Object.Destroy(cachedClip);
                }

                if (!File.Exists(fullPath))
                {
                    CompleteCallback(
                        callback, null,
                        "Audio file not found: " + fullPath
                    );
                    return;
                }

                List<Action<AudioClip, string>> callbacks;

                // Coalesce requests for the same file.
                if (loadCallbacks.TryGetValue(fullPath, out callbacks))
                {
                    if (callback != null)
                        callbacks.Add(callback);

                    return;
                }

                if (!IsRuntimeAvailable)
                    CreateRuntime();

                if (!IsRuntimeAvailable)
                {
                    CompleteCallback(
                        callback, null,
                        "The sound runtime is unavailable."
                    );
                    return;
                }

                callbacks = new List<Action<AudioClip, string>>();

                if (callback != null)
                    callbacks.Add(callback);

                loadCallbacks.Add(fullPath, callbacks);

                StartLoading(fullPath);
            }
            catch (Exception e)
            {
                CompleteCallback(
                    callback, null,
                    "Could not load audio: " + e.Message
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

                    try { request.Dispose(); }
                    catch { }

                    FinishLoading(
                        path, null,
                        "Unity could not start the audio request."
                    );
                }
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
                    "Could not start audio request: " + e.Message
                );
            }
        }

        internal static void Tick()
        {
            if (shuttingDown)
                return;

            // Complete pending audio loads.
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
                            "The audio request became unavailable."
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

                        if (!IsUsableClip(clip))
                            error = "The audio file could not be decoded or is empty.";
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
                        "Error processing audio: " + e.Message
                    );
                }
            }

            // Clean up stopped and naturally completed playback.
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                ActivePlayback playback = sources[i];
                bool remove = false;

                try
                {
                    if (playback == null || playback.Source == null)
                    {
                        remove = true;
                    }
                    else if (playback.Handle == null ||
                             playback.Handle.WasStopped)
                    {
                        remove = true;
                    }
                    else if (!playback.Source.isPlaying &&
                             !playback.Source.loop &&
                             !playback.Handle.IsPaused &&
                             playback.Source.clip != null &&
                             playback.Source.time >=
                                 playback.Source.clip.length - 0.05f)
                    {
                        remove = true;
                    }

                    if (remove)
                    {
                        if (playback != null && playback.Source != null)
                        {
                            GameObject obj = playback.Source.gameObject;

                            playback.Source.Stop();

                            if (obj != null)
                                UnityEngine.Object.Destroy(obj);
                        }

                        sources.RemoveAt(i);
                    }
                }
                catch (Exception e)
                {
                    LogError("Audio cleanup failed: " + e.Message);

                    try
                    {
                        if (playback != null && playback.Source != null)
                        {
                            GameObject obj = playback.Source.gameObject;
                            playback.Source.Stop();

                            if (obj != null)
                                UnityEngine.Object.Destroy(obj);
                        }
                    }
                    catch { }

                    sources.RemoveAt(i);
                }
            }
        }

        private static bool IsUsableClip(AudioClip clip)
        {
            try
            {
                return clip != null &&
                       clip.samples > 0 &&
                       clip.length > 0f &&
                       clip.channels > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void FinishLoading(
            string path,
            AudioClip clip,
            string error)
        {
            List<Action<AudioClip, string>> callbacks;

            if (loadCallbacks.TryGetValue(path, out callbacks))
            {
                // Remove before callbacks so callbacks can call Load again.
                loadCallbacks.Remove(path);
            }
            else
            {
                callbacks = new List<Action<AudioClip, string>>();
            }

            if (string.IsNullOrEmpty(error) && !IsUsableClip(clip))
                error = "The audio file produced an invalid or empty clip.";

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
                LogError("Audio callback failed: " + e.Message);
            }
        }

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
                LogError("Invalid audio path: " + e.Message);
                return null;
            }

            AudioClip clip;

            if (!cache.TryGetValue(fullPath, out clip) ||
                !IsUsableClip(clip))
            {
                LogError(
                    "Audio is not cached or is invalid. Call SoundAPI.Load() first: " +
                    fullPath
                );
                return null;
            }

            if (!IsRuntimeAvailable)
                CreateRuntime();

            if (!IsRuntimeAvailable)
            {
                LogError("The sound runtime is unavailable.");
                return null;
            }

            if (options == null)
                options = new SoundOptions();

            GameObject obj = null;
            ActivePlayback playback = null;

            try
            {
                obj = new GameObject("ULTRADROID.SoundSource");
                obj.transform.SetParent(runtime.transform, false);

                AudioSource source = obj.AddComponent<AudioSource>();

                source.clip = clip;
                source.volume = Mathf.Clamp01(options.Volume);
                source.pitch = Mathf.Clamp(options.Pitch, -3f, 3f);
                source.loop = options.Loop;
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                SoundHandle handle = new SoundHandle(clip, source);

                playback = new ActivePlayback
                {
                    Path = fullPath,
                    Clip = clip,
                    Handle = handle,
                    Source = source
                };

                sources.Add(playback);

                if (options.PlayOnLoad)
                    source.Play();

                return handle;
            }
            catch (Exception e)
            {
                if (playback != null)
                    sources.Remove(playback);

                if (obj != null)
                    UnityEngine.Object.Destroy(obj);

                LogError("Could not start audio playback: " + e.Message);
                return null;
            }
        }

        public static void LoadAndPlay(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            Load(path, (clip, error) =>
            {
                if (!string.IsNullOrEmpty(error) || !IsUsableClip(clip))
                {
                    CompleteSoundCallback(
                        callback, null,
                        string.IsNullOrEmpty(error)
                            ? "The loaded audio clip is invalid."
                            : error
                    );
                    return;
                }

                SoundHandle handle = Play(path, options);

                if (handle == null)
                {
                    CompleteSoundCallback(
                        callback, null,
                        "Audio loaded, but playback could not start."
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

            try
            {
                callback(handle, error);
            }
            catch (Exception e)
            {
                LogError("Sound callback failed: " + e.Message);
            }
        }

        public static bool IsCached(string path)
        {
            try
            {
                AudioClip clip;

                return cache.TryGetValue(
                    NormalizePath(path), out clip
                ) && IsUsableClip(clip);
            }
            catch
            {
                return false;
            }
        }

        // Active sounds must be stopped before their clip can be unloaded.
        public static void Unload(string path)
        {
            try
            {
                string fullPath = NormalizePath(path);
                AudioClip clip;

                if (!cache.TryGetValue(fullPath, out clip))
                    return;

                for (int i = 0; i < sources.Count; i++)
                {
                    ActivePlayback playback = sources[i];

                    if (playback != null &&
                        string.Equals(
                            playback.Path,
                            fullPath,
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        playback.Source != null)
                    {
                        LogError(
                            "Cannot unload audio while playback is active: " +
                            fullPath
                        );
                        return;
                    }
                }

                cache.Remove(fullPath);

                if (clip != null)
                    UnityEngine.Object.Destroy(clip);
            }
            catch (Exception e)
            {
                LogError("Could not unload audio: " + e.Message);
            }
        }

        internal static void RuntimeDestroyed()
        {
            runtime = null;

            if (!shuttingDown)
                LogError("The sound runtime was destroyed unexpectedly.");
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

            // Clear the callback dictionary before invoking user callbacks.
            List<Action<AudioClip, string>> callbacksToNotify =
                new List<Action<AudioClip, string>>();

            foreach (var entry in loadCallbacks)
                callbacksToNotify.AddRange(entry.Value);

            loadCallbacks.Clear();

            for (int i = 0; i < callbacksToNotify.Count; i++)
            {
                CompleteCallback(
                    callbacksToNotify[i], null,
                    "The Sound API was shut down."
                );
            }

            for (int i = 0; i < sources.Count; i++)
            {
                try
                {
                    if (sources[i] != null &&
                        sources[i].Source != null)
                    {
                        sources[i].Source.Stop();

                        if (sources[i].Source.gameObject != null)
                        {
                            UnityEngine.Object.Destroy(
                                sources[i].Source.gameObject
                            );
                        }
                    }
                }
                catch { }
            }

            sources.Clear();

            foreach (var entry in cache)
            {
                if (entry.Value != null)
                    UnityEngine.Object.Destroy(entry.Value);
            }

            cache.Clear();
            runtime = null;
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
