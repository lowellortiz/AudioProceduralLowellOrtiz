//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;

public enum WaveformType
{
    Sine,
    Square,
    Triangle,
    Sawtooth
}

// Clase de síntesis: solo genera audio. No conoce botones ni UI (eso vive en
// OscillatorFrontController).
public class SimpleOscillator : MonoBehaviour
{
    [Range(0f, 1f)]
    public float amplitude = 0.25f;
    public float frequency = 440f;
    public WaveformType waveform = WaveformType.Sine;
    public bool isPlaying = false;

    private double phase = 0.0;
    private double sampleRate = 44100.0;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    private float SampleFromPhase(double p)
    {
        switch (waveform)
        {
            case WaveformType.Square:
                return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (float)p));

            case WaveformType.Triangle:
                return 1f - 4f * Mathf.Abs((float)p - 0.5f);

            case WaveformType.Sawtooth:
                return 1f - 2f * (float)p;

            default:
                return Mathf.Sin(2f * Mathf.PI * (float)p);
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        double phaseStep = frequency / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = isPlaying ? amplitude * SampleFromPhase(phase) : 0f;
            sample = Mathf.Clamp(sample, -1f, 1f);

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;

            phase += phaseStep;
            if (phase >= 1.0) phase -= 1.0;
        }
    }
}
