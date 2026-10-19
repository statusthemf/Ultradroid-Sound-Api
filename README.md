ULTRADROID Sound API

A lightweight audio API for ULTRAKILL mods on Android. It lets mod developers load, play, pause, resume, loop, and manage custom audio files.

Features

- Supports MP3, WAV, and OGG audio.
- Loads and caches audio clips.
- Plays multiple sounds at the same time.
- Supports pause, resume, stop, volume, and pitch controls.
- Supports looping audio.
- Automatically cleans up completed playback.
- Provides error handling for failed audio loads.

Requirements

- ULTRAKILL on Android.
- BepInEx for Android.
- A compatible ULTRADROID Sound API build.

Installation

1. Install BepInEx

Make sure BepInEx for Android is installed and working with your ULTRAKILL game.

2. Install the Sound API

1. Open your Android launcher.
2. Create a new modpack, or open the modpack you want to use.
3. Import the ULTRADROID Sound API into that modpack.

3. Install the DLL

The Sound API DLL may need to be imported separately using your Android file manager.

1. Download "ULTRADROID.Sound.dll".
2. Open your file manager and find the downloaded DLL.
3. Move or copy the DLL into your chosen modpack using the launcher’s supported modpack location or import method.

Make sure the API is included in the same modpack as any mod that requires it.

How It Works

The API uses Unity's audio system to load and manage sound files.

The basic process is:

1. Initialize the API.
2. Load an audio file.
3. Play the loaded clip.
4. Control playback using a "SoundHandle".
5. Unload the clip when it is no longer needed.

Audio loading is asynchronous, and the API uses callbacks to report success or failure. Loaded clips are cached so they can be reused without loading the same file repeatedly.

Basic Usage

Initialize

SoundAPI.Initialize();

Initialize the API before loading or playing sounds.

Load an audio file

SoundAPI.Load(audioPath, (clip, error) =>
{
    if (clip == null || error != null)
    {
        Log.LogError("Audio loading failed: " + error);
        return;
    }

    Log.LogInfo("Audio loaded!");
});

Replace "audioPath" with the path to your audio file.

Play a sound

SoundHandle handle = SoundAPI.Play(audioPath);

Load the audio successfully before calling "Play()".

Load and play automatically

SoundAPI.LoadAndPlay(
    audioPath,
    new SoundOptions(),
    (handle, error) =>
    {
        if (handle == null)
            Log.LogError("Playback failed: " + error);
    }
);

Playback Controls

Use the returned "SoundHandle" to control a sound.

handle.Pause();
handle.Resume();
handle.Stop();

handle.SetVolume(0.5f);
handle.SetPitch(1.2f);

Sound Options

SoundOptions options = new SoundOptions
{
    Volume = 0.8f,
    Pitch = 1.0f,
    Loop = false,
    PlayOnLoad = true
};

SoundHandle handle = SoundAPI.Play(audioPath, options);

- "Volume": Sound volume from "0.0" to "1.0".
- "Pitch": Controls playback pitch and speed.
- "Loop": Repeats the sound until stopped.
- "PlayOnLoad": Starts playback immediately.

Caching and Unloading

Check whether a clip is cached:

bool cached = SoundAPI.IsCached(audioPath);

Check the number of cached clips:

int cachedClips = SoundAPI.CachedClipCount;

Check active playback instances:

int activeSounds = SoundAPI.ActiveSoundCount;

Unload a clip when it is no longer needed:

SoundAPI.Unload(audioPath);

A clip cannot be unloaded while it still has active playback sources.

Supported Formats

- MP3
- WAV
- OGG

Troubleshooting

The sound does not play: Make sure the API is initialized, the file loaded successfully, and the clip is cached.

The audio file cannot be found: Check the file path and make sure the game can access it.

The format is unsupported: Use MP3, WAV, or OGG.

The sound plays multiple times: Check whether your mod is calling "Play()" more than once.

License

See the repository's license file for usage and redistribution terms.
