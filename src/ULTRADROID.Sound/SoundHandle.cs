
using UnityEngine;

namespace ULTRADROID.Sound
{
    public sealed class SoundHandle
    {
        internal SoundHandle(AudioClip clip, AudioSource source)
        {
            Clip = clip;
            Source = source;
        }

        public AudioClip Clip { get; private set; }

        public AudioSource Source { get; private set; }

        public bool IsPlaying
        {
            get
            {
                return Source != null && Source.isPlaying;
            }
        }

        public void Play()
        {
            if (Source != null && Clip != null)
            {
                Source.clip = Clip;
                Source.Play();
            }
        }

        public void Pause()
        {
            if (Source != null)
                Source.Pause();
        }

        public void Resume()
        {
            if (Source != null)
                Source.UnPause();
        }

        public void Stop()
        {
            if (Source != null)
                Source.Stop();
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
