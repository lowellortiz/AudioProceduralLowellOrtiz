using UnityEngine;

public enum AdditiveWaveformType
{
    Sine,
    Square,
    Triangle,
    Sawtooth,
    Additive
}

// Clase de síntesis: solo genera audio (senoidal simple o suma aditiva de
// armónicos). No conoce botones ni UI (eso vive en AdditiveKeyboardController).
public class SimpleAdditiveOscillator : MonoBehaviour
{
    public float amplitude = 0.2f;
    public float frequency = 440f;
    public float sampleRate = 44100f;
    public int harmonicCount = 5;
    public float[] harmonicLevels = { 1f, 0.5f, 0.33f, 0.25f, 0.2f, 0.16f, 0.14f, 0.125f };

    public bool isPlaying = false;
    public AdditiveWaveformType waveform = AdditiveWaveformType.Additive;

    private int timeIndex = 0;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    float SineWave(float f, int n)
    {
        return Mathf.Sin(2f * Mathf.PI * f * n / sampleRate);
    }

    float AdditiveWave(float f, int n)
    {
        float sum = 0f;
        float totalLevel = 0f;

        int count = Mathf.Clamp(harmonicCount, 0, harmonicLevels.Length);
        for (int harmonic = 1; harmonic <= count; harmonic++)
        {
            float level = harmonicLevels[harmonic - 1];
            sum += level * SineWave(harmonic * f, n);
            totalLevel += level;
        }

        return totalLevel > 0f ? sum / totalLevel : 0f;
    }

    float GetSample(int n)
    {
        if (waveform == AdditiveWaveformType.Additive)
            return AdditiveWave(frequency, n);

        return SineWave(frequency, n);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = isPlaying ? amplitude * GetSample(timeIndex) : 0f;
            sample = Mathf.Clamp(sample, -1f, 1f);

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;

            timeIndex++;
        }
    }
}
