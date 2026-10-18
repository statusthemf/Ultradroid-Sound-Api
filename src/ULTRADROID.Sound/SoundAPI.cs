
using System;
using System.IO;
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
                    Fail(
                        callback,
                        "Unsupported format. Use MP3, WAV or OGG."
                    );
                    return;
            }

            string fullPath = Path.GetFullPath(path)
                .Replace('\\', '/');

            string fileUri = "file://" + fullPath;

            Debug.Log(
                "[ULTRADROID Sound] Loading: " + fileUri
            );

            UnityWebRequest request = null;

            try
            {
                request = UnityWebRequestMultimedia.GetAudioClip(
                    fileUri,
                    audioType
                );

                UnityWebRequest activeRequest = request;

                activeRequest.SendWebRequest().completed +=
                    delegate(AsyncOperation operation)
                    {
                        try
                        {
                            if (activeRequest.result !=
                                UnityWebRequest.Result.Success)
                            {
                                Fail(
                                    callback,
                                    "Audio loading failed: " +
                                    activeRequest.error
                                );
                                return;
                            }

                            AudioClip clip =
                                DownloadHandlerAudioClip.GetContent(
                                    activeRequest
                                );

                            if (clip == null)
                            {
                                Fail(
                                    callback,
                                    "Unity returned a null AudioClip."
                                );
                                return;
                            }

                            GameObject sourceObject =
                                new GameObject(
                                    "ULTRADROID Sound Source"
                                );

                            sourceObject.transform.SetParent(
                                transform,
                                false
                            );

                            AudioSource source =
                                sourceObject.AddComponent<AudioSource>();

                            source.clip = clip;
                            source.volume = Mathf.Clamp01(
                                options.Volume
                            );
                            source.pitch = Mathf.Clamp(
                                options.Pitch,
                                -3f,
                                3f
                            );
                            source.loop = options.Loop;
                            source.playOnAwake = false;
                            source.spatialBlend = 0f;

                            SoundHandle handle =
                                new SoundHandle(clip, source);

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
                        catch (Exception e)
                        {
                            Fail(
                                callback,
                                "Audio processing failed: " + e
                            );
                        }
                        finally
                        {
                            activeRequest.Dispose();
                        }
                    };
            }
            catch (Exception e)
            {
                if (request != null)
                    request.Dispose();

                Fail(
                    callback,
                    "Could not start audio loading: " + e
                );
            }
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
}
