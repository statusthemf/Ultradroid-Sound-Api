using System;
using System.IO;
using System.Collections;
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

            runtime.StartCoroutine(
                runtime.LoadRoutine(
                    path,
                    options ?? new SoundOptions(),
                    callback
                )
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
        internal IEnumerator LoadRoutine(
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
                {
                    const string error =
                        "Unsupported format. Use MP3, WAV or OGG.";

                    Debug.LogError("[ULTRADROID Sound] " + error);

                    if (callback != null)
                        callback(null, error);

                    yield break;
                }
            }

            string uri;

            try
            {
                uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[ULTRADROID Sound] Invalid file URI: " + e
                );

                if (callback != null)
                    callback(null, e.Message);

                yield break;
            }

            Debug.Log("[ULTRADROID Sound] Loading: " + path);

            using (UnityWebRequest request =
                UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error =
                        "Audio loading failed: " + request.error;

                    Debug.LogError("[ULTRADROID Sound] " + error);

                    if (callback != null)
                        callback(null, error);

                    yield break;
                }

                AudioClip clip =
                    DownloadHandlerAudioClip.GetContent(request);

                if (clip == null)
                {
                    const string error =
                        "Unity returned a null AudioClip.";

                    Debug.LogError("[ULTRADROID Sound] " + error);

                    if (callback != null)
                        callback(null, error);

                    yield break;
                }

                GameObject sourceObject = new GameObject(
                    "ULTRADROID Sound Source"
                );

                sourceObject.transform.SetParent(transform, false);

                AudioSource source =
                    sourceObject.AddComponent<AudioSource>();

                source.clip = clip;
                source.volume = Mathf.Clamp01(options.Volume);
                source.pitch = Mathf.Clamp(options.Pitch, -3f, 3f);
                source.loop = options.Loop;
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                SoundHandle handle = new SoundHandle(clip, source);

                Debug.Log(
                    "[ULTRADROID Sound] Loaded clip: " +
                    clip.length + " seconds."
                );

                if (options.PlayOnLoad)
                {
                    source.Play();

                    Debug.Log(
                        "[ULTRADROID Sound] Playback requested."
                    );
                }

                if (callback != null)
                    callback(handle, null);
            }
        }
    }
}
