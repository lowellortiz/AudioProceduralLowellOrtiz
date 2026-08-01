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
    [Range(0f, 1f)]
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
            totalLevel += Mathf.Abs(level);
        }

        return totalLevel > 0f ? sum / totalLevel : 0f;
    }

    // Version directa (formula exacta, sin armonicos) de la misma onda, para
    // comparar de oido contra la aproximacion aditiva.
    float DirectWave(AdditiveWaveformType type, float f, int n)
    {
        double phase = (f * n / sampleRate) % 1.0;

        switch (type)
        {
            case AdditiveWaveformType.Square:
                return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (float)phase));

            case AdditiveWaveformType.Triangle:
                return 1f - 4f * Mathf.Abs((float)phase - 0.5f);

            case AdditiveWaveformType.Sawtooth:
                return 1f - 2f * (float)phase;

            default:
                return SineWave(f, n);
        }
    }

    float GetSample(int n)
    {
        if (waveform == AdditiveWaveformType.Additive)
            return AdditiveWave(frequency, n);

        if (waveform == AdditiveWaveformType.Sine)
            return SineWave(frequency, n);

        return DirectWave(waveform, frequency, n);
    }

    // Carga en harmonicLevels los pesos de Fourier que aproximan la forma
    // indicada (ver guia: cuadrada = impares 1/n, triangular = impares 1/n^2
    // alternando signo, sierra = todos los armonicos 1/n alternando signo).
    public void LoadPreset(AdditiveWaveformType shape, int harmonics)
    {
        harmonicCount = harmonics;
        harmonicLevels = new float[harmonics];

        for (int n = 1; n <= harmonics; n++)
        {
            float value = 0f;
            switch (shape)
            {
                case AdditiveWaveformType.Square:
                    if (n % 2 == 1) value = 1f / n;
                    break;

                case AdditiveWaveformType.Triangle:
                    if (n % 2 == 1)
                    {
                        float sign = ((n - 1) / 2) % 2 == 0 ? 1f : -1f;
                        value = sign / (n * n);
                    }
                    break;

                case AdditiveWaveformType.Sawtooth:
                    value = (n % 2 == 1 ? 1f : -1f) / n;
                    break;
            }

            harmonicLevels[n - 1] = value;
        }
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
