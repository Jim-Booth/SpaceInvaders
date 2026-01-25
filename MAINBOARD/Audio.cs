// ============================================================================
// Project:     SpaceInvaders
// File:        Audio.cs
// Description: SFML-based audio playback engine with sound caching for arcade
//              sound effects, including low-pass filter for authentic cabinet sound
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// ============================================================================

using SFML.Audio;

internal class AudioPlaybackEngine : IDisposable
{
    private readonly List<Sound> _activeSounds = new();
    private readonly object _soundLock = new();

    public void PlaySound(CachedSound cachedSound)
    {
        lock (_soundLock)
        {
            var sound = new Sound(cachedSound.SoundBuffer);
            _activeSounds.Add(sound);
            sound.Play();
            
            // Clean up finished sounds
            _activeSounds.RemoveAll(s => s.Status == SoundStatus.Stopped);
        }
    }

    public void Dispose()
    {
        lock (_soundLock)
        {
            foreach (var sound in _activeSounds)
            {
                sound.Stop();
                sound.Dispose();
            }
            _activeSounds.Clear();
        }
    }

    public static readonly AudioPlaybackEngine Instance = new();
}

internal class CachedSound
{
    public SoundBuffer SoundBuffer { get; private set; }
    
    // Low-pass filter cutoff frequency (Hz) - arcade speakers had limited high frequency response
    private const float LowpassCutoff = 4000f;

    public CachedSound(string audioFileName)
    {
        var originalBuffer = new SoundBuffer(audioFileName);
        
        // Apply low-pass filter to simulate arcade cabinet speaker
        short[] samples = ApplyLowPassFilter(originalBuffer.Samples, originalBuffer.SampleRate, originalBuffer.ChannelCount);
        
        // Create channel map based on channel count
        SoundChannel[] channelMap = originalBuffer.ChannelCount switch
        {
            1 => [SoundChannel.Mono],
            2 => [SoundChannel.FrontLeft, SoundChannel.FrontRight],
            _ => [SoundChannel.Mono]
        };
        
        SoundBuffer = new SoundBuffer(samples, originalBuffer.ChannelCount, originalBuffer.SampleRate, channelMap);
        originalBuffer.Dispose();
    }
    
    /// <summary>
    /// Applies a simple single-pole low-pass filter to simulate arcade cabinet speakers.
    /// Arcade cabinets had limited frequency response, typically rolling off above 4-5 kHz.
    /// </summary>
    private static short[] ApplyLowPassFilter(short[] input, uint sampleRate, uint channelCount)
    {
        short[] output = new short[input.Length];
        
        // Calculate filter coefficient (RC low-pass filter)
        // alpha = dt / (RC + dt) where RC = 1 / (2 * PI * cutoff)
        float rc = 1.0f / (2.0f * MathF.PI * LowpassCutoff);
        float dt = 1.0f / sampleRate;
        float alpha = dt / (rc + dt);
        
        // Process each channel separately
        for (uint channel = 0; channel < channelCount; channel++)
        {
            float previousOutput = 0;
            
            for (int i = (int)channel; i < input.Length; i += (int)channelCount)
            {
                // Single-pole low-pass filter: y[n] = y[n-1] + alpha * (x[n] - y[n-1])
                float filtered = previousOutput + alpha * (input[i] - previousOutput);
                previousOutput = filtered;
                
                // Clamp to short range
                output[i] = (short)Math.Clamp(filtered, short.MinValue, short.MaxValue);
            }
        }
        
        return output;
    }
}