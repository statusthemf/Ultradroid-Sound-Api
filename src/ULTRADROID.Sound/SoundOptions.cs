namespace ULTRADROID.Sound
{
    public sealed class SoundOptions
    {
        public float Volume { get; set; } = 1f;
        public float Pitch { get; set; } = 1f;
        public bool Loop { get; set; } = false;
        public bool PlayOnLoad { get; set; } = true;
    }
}
