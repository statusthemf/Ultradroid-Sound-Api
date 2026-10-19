
using UnityEngine;

namespace ULTRADROID.Sound
{
    public sealed class SoundHandle
    {
        internal SoundHandle(AudioClip clip, AudioSource source)
        {
            Clip = clip;
            Source = source;
            IsPaused = false;
            WasStopped = false;
        }

        public AudioClip Clip { get; private set; }

        public AudioSource Source { get; private set; }

        internal bool IsPaused { get; private set; }

        internal bool WasStopped { get; private set; }

        public bool IsPlaying
        {
            get
            {
                return Source != null &&
                       Source.isPlaying &&
                       !IsPaused;
            }
        }

        public void Play()
        {
            if (Source == null || Clip == null)
                return;

            Source.clip = Clip;
            Source.Play();

            IsPaused = false;
            WasStopped = false;
        }

        public void Pause()
        {
            if (Source != null && Source.isPlaying)
            {
                Source.Pause();
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (Source != null && IsPaused)
            {
                Source.UnPause();
                IsPaused = false;
            }
        }

        public void Stop()
        {
            if (Source != null)
                Source.Stop();

            IsPaused = false;
            WasStopped = true;
        }

        public void SetVolume(float volume)
        {
            if (Source != null)
                Source.volume = Mathf.Clamp01(volume);
        }

        public void SetPitch(float pitch)
        {
            if (Source != null)
                Source.pitch = Mathf.Clamp(pitch, -3f, 3f);
        }
    }
}
