using SFML.Audio;

internal class AudioPlaybackEngine : IDisposable
{
    private readonly List<Sound> activeSounds = new();
    private readonly object soundLock = new();

    public void PlaySound(CachedSound cachedSound)
    {
        lock (soundLock)
        {
            var sound = new Sound(cachedSound.SoundBuffer);
            activeSounds.Add(sound);
            sound.Play();
            
            // Clean up finished sounds
            activeSounds.RemoveAll(s => s.Status == SoundStatus.Stopped);
        }
    }

    public void Dispose()
    {
        lock (soundLock)
        {
            foreach (var sound in activeSounds)
            {
                sound.Stop();
                sound.Dispose();
            }
            activeSounds.Clear();
        }
    }

    public static readonly AudioPlaybackEngine Instance = new();
}

internal class CachedSound
{
    public SoundBuffer SoundBuffer { get; private set; }

    public CachedSound(string audioFileName)
    {
        SoundBuffer = new SoundBuffer(audioFileName);
    }
}