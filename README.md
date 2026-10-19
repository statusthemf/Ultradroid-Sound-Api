# 🔊 ULTRADROID Sound API

**A lightweight audio API for ULTRAKILL mods on Android.**

Load, play, pause, resume, loop, and fully control custom audio files with ease.

---

### ✨ Features

- ✅ Supports **MP3**, **WAV**, and **OGG**
- ✅ Loads and caches audio clips
- ✅ Play multiple sounds simultaneously
- ✅ Full playback controls: pause, resume, stop, volume, pitch
- ✅ Looping support
- ✅ Automatic cleanup of finished playback
- ✅ Error handling for failed loads

---

### 📋 Requirements

- [ULTRADROID](https://github.com) (Android port of ULTRAKILL)
- BepInEx for Android
- A compatible build of **ULTRADROID Sound API**

---

### 📥 Installation

#### 1. Install BepInEx
Make sure **BepInEx for Android** is installed and working with your ULTRADROID game.

#### 2. Install the Sound API via Launcher
1. Open your **BEPINEX launcher**.
2. Create a new modpack, or open the one you want to use.
3. Import the **ULTRADROID Sound API** into that modpack  
   *(include the examples mod if you want to test it)*.

#### 3. Install the DLL (if needed)
The Sound API DLL may need to be imported separately using your Android file manager.

1. Download `ULTRADROID.Sound.dll`
2. Open your file manager and locate the downloaded DLL
3. Move or copy the DLL into your chosen modpack using the launcher’s supported location/import method

> **Important:** Make sure the API is included in the **same modpack** as any mod that depends on it.

---

### ⚙️ How It Works

The API uses Unity's audio system under the hood to load and manage sound files.

**Basic flow:**

1. Initialize the API
2. Load an audio file
3. Play the loaded clip
4. Control playback using a `SoundHandle`
5. Unload the clip when no longer needed

Audio loading is **asynchronous**. The API uses callbacks to report success or failure.  
Loaded clips are **cached** so they can be reused without reloading the same file.

---

### 🚀 Basic Usage

#### Initialize

```csharp
SoundAPI.Initialize();
```

Call this **before** loading or playing any sounds.

#### Load an audio file

```csharp
SoundAPI.Load(audioPath, (clip, error) =>
{
    if (clip == null || error != null)
    {
        Log.LogError("Audio loading failed: " + error);
        return;
    }

    Log.LogInfo("Audio loaded!");
});
```

Replace `audioPath` with the full path to your audio file.

#### Play a sound

```csharp
SoundHandle handle = SoundAPI.Play(audioPath);
```

> Make sure the audio has been loaded successfully before calling `Play()`.

#### Load and play automatically

```csharp
SoundAPI.LoadAndPlay(
    audioPath,
    new SoundOptions(),
    (handle, error) =>
    {
        if (handle == null)
            Log.LogError("Playback failed: " + error);
    }
);
```

---

### 🎮 Playback Controls

Use the returned `SoundHandle` to control individual sounds:

```csharp
handle.Pause();
handle.Resume();
handle.Stop();

handle.SetVolume(0.5f);
handle.SetPitch(1.2f);
```

---

### 🎛️ Sound Options

```csharp
SoundOptions options = new SoundOptions
{
    Volume = 0.8f,
    Pitch = 1.0f,
    Loop = false,
    PlayOnLoad = true
};

SoundHandle handle = SoundAPI.Play(audioPath, options);
```

| Property     | Description                              | Range / Values     |
|--------------|------------------------------------------|--------------------|
| `Volume`     | Sound volume                             | `0.0` → `1.0`     |
| `Pitch`      | Playback pitch and speed                 | Any positive float |
| `Loop`       | Whether the sound should loop            | `true` / `false`  |
| `PlayOnLoad` | Start playback as soon as loading finishes | `true` / `false` |

---

### 📦 Caching & Unloading

```csharp
// Check if a clip is already cached
bool cached = SoundAPI.IsCached(audioPath);

// Number of currently cached clips
int cachedClips = SoundAPI.CachedClipCount;

// Number of active playback instances
int activeSounds = SoundAPI.ActiveSoundCount;

// Unload a clip (only works if it has no active sources)
SoundAPI.Unload(audioPath);
```

> A clip **cannot** be unloaded while it still has active playback sources.

---

### 📁 Supported Formats

| Format | Supported |
|--------|-----------|
| MP3    | ✅        |
| WAV    | ✅        |
| OGG    | ✅        |

---

### 🛠️ Troubleshooting

| Problem | Solution |
|---------|----------|
| Sound does not play | Ensure the API is initialized, the file loaded successfully, and the clip is cached |
| Audio file cannot be found | Double-check the file path and that the game has access to it |
| Unsupported format | Only use **MP3**, **WAV**, or **OGG** |
| Sound plays multiple times | Check if your mod is calling `Play()` more than once unintentionally |

---

### 📄 License

See the repository's license file for usage and redistribution terms.
