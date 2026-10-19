ULTRADROID Sound API

A lightweight audio API for ULTRAKILL mods running on Android through BepInEx and IL2CPP.

The ULTRADROID Sound API allows mod developers to load, cache, play, pause, resume, loop, and unload custom audio files without having to implement their own audio-loading system.

Features

- Load custom audio files from disk.
- Supported formats: MP3, WAV, and OGG.
- Cache loaded audio clips to avoid loading the same file repeatedly.
- Play multiple sounds simultaneously.
- Control playback through sound handles.
- Pause, resume, stop, and adjust volume and pitch.
- Support looping audio.
- Load and play audio with a single method.
- Automatically clean up completed playback sources.
- Unload cached audio clips when they are no longer needed.
- Handle loading errors through callbacks.

Requirements

Before using the API, make sure you have:

- ULTRAKILL installed on your Android device.
- BepInEx for Android, compatible with your game version.
- ULTRADROID Sound API, including "ULTRADROID.Sound.dll".
- A C# development environment, such as Visual Studio or another compatible IDE, if you are developing your own mod.

The API is designed for Unity IL2CPP environments and uses BepInEx and Il2CppInterop.

Installation

1. Install BepInEx

Install a compatible BepInEx Android setup for your ULTRAKILL installation.

Follow the installation instructions for the specific BepInEx Android build you use. Make sure the game launches correctly with BepInEx before installing additional mods.

2. Install the Sound API

Download or build "ULTRADROID.Sound.dll".

Place the DLL in your ULTRAKILL BepInEx plugins directory.

For example, depending on your installation:

BepInEx/
└── plugins/
    └── ULTRADROID.Sound.dll

If your mod includes its own DLL, place that DLL in the same plugins directory as well.

Important: If your mod references "ULTRADROID.Sound.dll", the API assembly must be available to the game at runtime. Avoid installing multiple copies of the same assembly.

3. Add the API to your project

If you are creating a mod in C#, add a reference to "ULTRADROID.Sound.dll" in your project.

You can then import the API namespace:

using ULTRADROID.Sound;

Make sure your project targets a framework compatible with your BepInEx Android environment and references the required Unity and IL2CPP assemblies.

How It Works

The Sound API uses Unity's audio system to manage custom audio.

The general workflow is:

1. Initialize the API when your mod starts.
2. Load an audio file from its path.
3. Cache the resulting audio clip for reuse.
4. Play the cached clip when needed.
5. Control playback using a "SoundHandle".
6. Unload the clip when it is no longer needed.

Loading is asynchronous. The API provides a callback when loading succeeds or fails, allowing your mod to respond without blocking the game while the audio is loading.

Each playback creates its own audio source, so multiple sounds can play simultaneously, including multiple instances of the same clip.

Completed playback sources are cleaned up automatically.

Basic Usage

Initialize the API

Initialize the API before attempting to load or play sounds.

using BepInEx;
using ULTRADROID.Sound;

[BepInPlugin(
    "com.example.mymod",
    "My Sound Mod",
    "1.0.0"
)]
public class MyMod : BasePlugin
{
    public override void Load()
    {
        SoundAPI.Initialize();
    }
}

Load an audio file

Use "SoundAPI.Load()" to load a file.

string audioPath =
    "/storage/emulated/0/your-audio-folder/sound.mp3";

SoundAPI.Load(audioPath, (clip, error) =>
{
    if (!string.IsNullOrEmpty(error) || clip == null)
    {
        Log.LogError("Audio loading failed: " + error);
        return;
    }

    Log.LogInfo("Audio loaded successfully!");
});

Replace the example path with the actual location of your audio file on your device.

The file must exist and be accessible to the game.

Play a loaded sound

Once loading has completed successfully, play the cached audio:

SoundHandle handle = SoundAPI.Play(audioPath);

if (handle != null)
{
    Log.LogInfo("Sound playback started.");
}

"SoundAPI.Play()" expects the audio to be loaded and cached first. If the clip is not cached, the method returns "null".

Load and play in one operation

You can also use "LoadAndPlay()" to load a sound and start playback automatically.

SoundAPI.LoadAndPlay(
    audioPath,
    new SoundOptions(),
    (handle, error) =>
    {
        if (!string.IsNullOrEmpty(error) || handle == null)
        {
            Log.LogError("Could not play sound: " + error);
            return;
        }

        Log.LogInfo("Sound started successfully.");
    }
);

Playback Controls

A "SoundHandle" represents one playback instance. Keep the handle if you want to control that sound later.

Pause and resume

handle.Pause();
handle.Resume();

Stop playback

handle.Stop();

Stopping a sound ends that playback instance. Its playback source is then cleaned up by the API's update loop.

Change volume

handle.SetVolume(0.5f);

Volume values are clamped to the range "0.0" to "1.0".

Change pitch

handle.SetPitch(1.2f);

A pitch above "1.0" plays the sound faster and higher. A pitch below "1.0" plays it slower and lower. Pitch behavior should be tested on the target Unity and Android build.

Sound Options

You can configure playback with "SoundOptions".

SoundOptions options = new SoundOptions
{
    Volume = 0.8f,
    Pitch = 1.0f,
    Loop = false,
    PlayOnLoad = true
};

SoundHandle handle = SoundAPI.Play(audioPath, options);

Option| Description
"Volume"| Playback volume, from "0.0" to "1.0".
"Pitch"| Playback pitch and speed.
"Loop"| Repeats the sound until stopped.
"PlayOnLoad"| Starts playback immediately when the source is created.

When "PlayOnLoad" is "false", you can start the sound later through the handle's "Play()" method.

Caching and Unloading

Loaded clips are cached so that you can reuse them without repeatedly reading and decoding the file.

Check whether a clip is cached:

bool cached = SoundAPI.IsCached(audioPath);

Get the number of cached clips:

int cachedClips = SoundAPI.CachedClipCount;

Get the number of registered active playback instances:

int activeSounds = SoundAPI.ActiveSoundCount;

When you no longer need a cached clip, unload it:

SoundAPI.Unload(audioPath);

The API will not unload a clip while one of its playback sources is still active. Stop the relevant handles and wait for cleanup before trying again.

Error Handling

Audio loading uses a callback with two parameters:

(AudioClip clip, string error)

- If loading succeeds, "clip" contains the loaded audio and "error" is "null".
- If loading fails, "clip" is "null" and "error" describes the failure.

Common causes of errors include:

- The audio file does not exist.
- The path is invalid.
- The file extension is unsupported.
- Unity cannot decode the audio file.
- The Sound API has not been initialized.
- The sound runtime is unavailable.

Always check the callback's error and clip values before attempting playback.

Recommended Practices

- Initialize the API once during your mod's startup.
- Use absolute file paths when loading audio from Android storage.
- Keep audio files inside a directory accessible to the game.
- Load sounds before calling "Play()".
- Reuse cached clips instead of repeatedly loading the same file.
- Keep "SoundHandle" references for sounds you need to control.
- Stop looping sounds when they are no longer needed.
- Unload clips when your mod no longer needs them.
- Handle errors instead of assuming every file will load successfully.

Troubleshooting

The sound does not play

Check that the API was initialized, the file exists, loading succeeded, and the clip is cached before calling "Play()".

The file cannot be found

Check the absolute path, spelling, filename extension, and Android storage permissions.

The audio format is unsupported

Use MP3, WAV, or OGG.

The sound plays more than once

Check that your mod does not call "Play()" multiple times unintentionally. Each successful call creates a separate playback instance.

The clip cannot be unloaded

Make sure all playback instances using that file have stopped and their sources have been cleaned up.

License

See the repository's license file for the applicable terms of use and redistribution.
