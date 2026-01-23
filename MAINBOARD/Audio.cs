// ============================================================================
// Project:     SpaceInvaders
// File:        Audio.cs
// Description: SFML-based audio playback engine with sound caching for arcade
//              sound effects
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

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