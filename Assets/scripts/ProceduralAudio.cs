using UnityEngine;

/// <summary>
/// Synthesizes every sound effect at runtime - no .wav/.mp3 files anywhere,
/// so nothing can go missing or be forgotten right before submission.
/// </summary>
public static class ProceduralAudio
{
    public enum WaveShape { Sine, Square, Triangle }

    public static AudioClip Tone(float frequency, float duration, float volume = 0.5f, WaveShape shape = WaveShape.Sine, float fadeSeconds = 0.02f)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        float fadeSamples = Mathf.Max(1f, fadeSeconds * sampleRate);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float phase = t * frequency * Mathf.PI * 2f;
            float value = shape switch
            {
                WaveShape.Square => Mathf.Sign(Mathf.Sin(phase)),
                WaveShape.Triangle => Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f,
                _ => Mathf.Sin(phase),
            };

            float envelope = Mathf.Min(
                Mathf.Clamp01(i / fadeSamples),
                Mathf.Clamp01((sampleCount - i) / fadeSamples));

            samples[i] = value * volume * envelope;
        }

        AudioClip clip = AudioClip.Create("ProceduralTone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip Footstep() => Tone(180f, 0.06f, 0.16f, WaveShape.Triangle, 0.012f);

    public static AudioClip AlertStinger() => Sweep(520f, 900f, 0.22f, WaveShape.Square, 0.4f);

    public static AudioClip CaughtWhistle() => Sweep(1300f, 260f, 0.5f, WaveShape.Square, 0.45f);

    public static AudioClip WinChime()
    {
        // three quick ascending notes read as more "victorious" than one sweep.
        AudioClip a = Sweep(660f, 660f, 0.12f, WaveShape.Triangle, 0.4f);
        AudioClip b = Sweep(880f, 880f, 0.12f, WaveShape.Triangle, 0.4f);
        AudioClip c = Sweep(1320f, 1320f, 0.28f, WaveShape.Triangle, 0.45f);
        return Concat(a, b, c);
    }

    private static AudioClip Sweep(float startFreq, float endFreq, float duration, WaveShape shape, float volume)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += freq / sampleRate * Mathf.PI * 2f;

            float value = shape == WaveShape.Square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
            float envelope = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            samples[i] = value * volume * envelope;
        }

        AudioClip clip = AudioClip.Create("ProceduralSweep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip Concat(params AudioClip[] clips)
    {
        int total = 0;
        foreach (AudioClip c in clips) total += c.samples;

        float[] combined = new float[total];
        int offset = 0;
        foreach (AudioClip c in clips)
        {
            float[] buffer = new float[c.samples];
            c.GetData(buffer, 0);
            System.Array.Copy(buffer, 0, combined, offset, buffer.Length);
            offset += buffer.Length;
        }

        AudioClip result = AudioClip.Create("ProceduralConcat", total, 1, 44100, false);
        result.SetData(combined, 0);
        return result;
    }
}
