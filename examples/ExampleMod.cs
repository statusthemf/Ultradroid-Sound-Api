using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections.Generic;
using ULTRADROID.Sound;
using UnityEngine;
using UnityEngine.Networking;

namespace ULTRADROID.Sound
{
    public static class SoundAPI
    {
        private static SoundRuntime runtime;

        public static void Initialize()
        {
            if (runtime != null)
                return;

            GameObject obj = new GameObject(
                "ULTRADROID.SoundRuntime"
            );

            UnityEngine.Object.DontDestroyOnLoad(obj);
            runtime = obj.AddComponent<SoundRuntime>();

            Debug.Log("[ULTRADROID Sound] Runtime initialized.");
        }

        public static void LoadAndPlay(
            string path,
            SoundOptions options,
            Action<SoundHandle, string> callback)
        {
            if (runtime == null)
            {
                Fail(callback, "Call SoundAPI.Initialize() first.");
                return;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Fail(callback, "Audio file not found: " + path);
                return;
            }

            runtime.LoadAudio(
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

        private static void Fail(
            Action<SoundHandle, string> callback,
            string message)
        {
            Debug.LogError("[ULTRADROID Sound] " + message);

            if (callback != null)
                callback(null, message);
        }
    }

    internal sealed class SoundRuntime : MonoBehaviour
    {
        private sealed class PendingLoad
        {
            public UnityWebRequest Request;
            public SoundOptions Options;
            public Action<SoundHandle, string> Callback;
        }

        private readonly List<PendingLoad> pending =
            new List<PendingLoad>();

        internal void LoadAudio(
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
                    Fail(callback,
                        "Unsupported format. Use MP3, WAV or OGG.");
                    return;
            }

            UnityWebRequest request = null;

            try
            {
                string fullPath = Path.GetFullPath(path)
                    .Replace('\\', '/');

                // A Unix absolute path produces file:///storage/...
                string fileUri = "file://" + fullPath;

                Debug.Log(
                    "[ULTRADROID Sound] Loading: " + fileUri
                );

                request = UnityWebRequestMultimedia.GetAudioClip(
                    fileUri,
                    audioType
                );

                PendingLoad load = new PendingLoad
                {
                    Request = request,
                    Options = options,
                    Callback = callback
                };

                pending.Add(load);

                request.SendWebRequest();
            }
            catch (Exception e)
            {
                if (request != null)
                    request.Dispose();

                Fail(callback,
                    "Could not start audio loading: " + e);
            }
        }

        private void Update()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingLoad load = pending[i];

                if (load.Request == null || !load.Request.isDone)
                    continue;

                pending.RemoveAt(i);

                try
                {
                    ProcessCompletedLoad(load);
                }
                catch (Exception e)
                {
                    Fail(load.Callback,
                        "Audio processing failed: " + e);
                }
                finally
                {
                    if (load.Request != null)
                        load.Request.Dispose();
                }
            }
        }

        private void ProcessCompletedLoad(PendingLoad load)
        {
            UnityWebRequest request = load.Request;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail(load.Callback,
                    "Audio loading failed: " + request.error);
                return;
            }

            AudioClip clip =
                DownloadHandlerAudioClip.GetContent(request);

            if (clip == null)
            {
                Fail(load.Callback,
                    "Unity returned a null AudioClip.");
                return;
            }

            GameObject sourceObject =
                new GameObject("ULTRADROID Sound Source");

            sourceObject.transform.SetParent(transform, false);

            AudioSource source =
                sourceObject.AddComponent<AudioSource>();

            source.clip = clip;
            source.volume = Mathf.Clamp01(load.Options.Volume);
            source.pitch = Mathf.Clamp(load.Options.Pitch, -3f, 3f);
            source.loop = load.Options.Loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            SoundHandle handle = new SoundHandle(clip, source);

            Debug.Log(
                "[ULTRADROID Sound] Loaded clip: " +
                clip.length + " seconds."
            );

            if (load.Options.PlayOnLoad)
            {
                source.Play();
                Debug.Log("[ULTRADROID Sound] Playback requested.");
            }

            if (load.Callback != null)
                load.Callback(handle, null);
        }

        private static void Fail(
            Action<SoundHandle, string> callback,
            string message)
        {
            Debug.LogError("[ULTRADROID Sound] " + message);

            if (callback != null)
                callback(null, message);
        }

        private void OnDestroy()
        {
            foreach (PendingLoad load in pending)
            {
                if (load.Request != null)
                    load.Request.Dispose();
            }

            pending.Clear();
        }
    }
}
