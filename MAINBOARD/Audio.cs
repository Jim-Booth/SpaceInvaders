using SFML.Audio;

internal class AudioPlaybackEngine : IDisposable
{
    private readonly List<Sound> activeSounds = new();
    private readonly object soundLock = new();

    public AudioPlaybackEngine(int sampleRate, int channelCount)
    {
        // SFML doesn't require explicit initialization for audio playback
    }

    public void PlaySound(string fileName)
    {
        lock (soundLock)
        {
            var soundBuffer = new SoundBuffer(fileName);
            var sound = new Sound(soundBuffer);
            activeSounds.Add(sound);
            sound.Play();
            
            // Clean up finished sounds
            activeSounds.RemoveAll(s => s.Status == SoundStatus.Stopped);
        }
    }

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

    //SampleRate & Channel count values are handled by SFML automatically
    public static readonly AudioPlaybackEngine Instance = new(11025, 1);
}

internal class CachedSound
{
    public SoundBuffer SoundBuffer { get; private set; }

    public CachedSound(string audioFileName)
    {
        SoundBuffer = new SoundBuffer(audioFileName);
    }
}