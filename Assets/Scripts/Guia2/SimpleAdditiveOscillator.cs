//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;

public enum AdditiveWaveformType
{
    Sine,
    Square,
    Triangle,
    Sawtooth,
    Additive
}

// Clase de síntesis: solo genera audio (senoidal simple o suma aditiva de armonicos)

[RequireComponent(typeof(AudioSource))]
public class SimpleAdditiveOscillator : MonoBehaviour
{
    [Range(0f, 1f)]
    public float amplitude = 0.2f;
    public float frequency = 440f;
    public float sampleRate = 44100f;
    public int harmonicCount = 5;
    public float[] harmonicLevels = { 1f, 0.5f, 0.33f, 0.25f, 0.2f, 0.16f, 0.14f, 0.125f, 0.11f, 0.1f };

    public AdditiveWaveformType waveform = AdditiveWaveformType.Additive;

    // ADSR desacoplada en su propia clase (ver AdsrEnvelope.cs): el oscilador
    // solo la dispara (NoteOn/NoteOff) y consume su valor instantaneo.
    [Header("ADSR")]
    public AdsrEnvelope envelope = new AdsrEnvelope();

    public bool isPlaying => envelope.IsActive;

    // Fase normalizada: 0..1 equivale a un ciclo completo. Se ACUMULA muestra a
    // muestra, asi cambiar 'frequency' a mitad de nota no teletransporta la fase
    // (era el click al cambiar de nota).
    private double phase = 0.0;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;

        // La envolvente convierte milisegundos a muestras, asi que necesita la
        // frecuencia real de salida. Se inyecta ANTES del Play().
        envelope.SetSampleRate(sampleRate);

        // Sin un AudioClip asignado, OnAudioFilterRead no se llama. El clip
        // silencioso solo activa el pipeline: cada muestra se sobreescribe.
        var audioSource = GetComponent<AudioSource>();
        if (audioSource.clip == null)
        {
            audioSource.clip = AudioClip.Create("SilentDriver", (int)sampleRate, 1, (int)sampleRate, false);
            audioSource.loop = true;
        }

        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    public void NoteOn() => envelope.NoteOn();
    public void NoteOff() => envelope.NoteOff();

    // Seno a partir de la fase normalizada (0..1 = un ciclo).
    float SineFromPhase(double normalizedPhase)
    {
        double wrapped = normalizedPhase - System.Math.Floor(normalizedPhase);
        return Mathf.Sin(2f * Mathf.PI * (float)wrapped);
    }

    // Sintesis aditiva: suma de los armonicos ponderados por harmonicLevels.
    float AdditiveWave(double normalizedPhase)
    {
        float sum = 0f;
        float totalLevel = 0f;

        int count = Mathf.Clamp(harmonicCount, 0, harmonicLevels.Length);
        for (int harmonic = 1; harmonic <= count; harmonic++)
        {
            float level = harmonicLevels[harmonic - 1];
            // El armonico n va n veces mas rapido: fase * n.
            sum += level * SineFromPhase(normalizedPhase * harmonic);
            totalLevel += Mathf.Abs(level);
        }

        return totalLevel > 0f ? sum / totalLevel : 0f;
    }

    // Version directa (formula exacta, sin armonicos) de la misma onda.
    float DirectWave(AdditiveWaveformType type, double normalizedPhase)
    {
        float p = (float)(normalizedPhase - System.Math.Floor(normalizedPhase));

        switch (type)
        {
            case AdditiveWaveformType.Square:
                return p < 0.5f ? 1f : -1f;

            case AdditiveWaveformType.Triangle:
                return 1f - 4f * Mathf.Abs(p - 0.5f);

            case AdditiveWaveformType.Sawtooth:
                return 1f - 2f * p;

            default:
                return SineFromPhase(normalizedPhase);
        }
    }

    float GetSample(double normalizedPhase)
    {
        if (waveform == AdditiveWaveformType.Additive)
            return AdditiveWave(normalizedPhase);

        if (waveform == AdditiveWaveformType.Sine)
            return SineFromPhase(normalizedPhase);

        return DirectWave(waveform, normalizedPhase);
    }

    // Carga en harmonicLevels los pesos de Fourier que aproximan la forma
    public void LoadPreset(AdditiveWaveformType shape, int harmonics)
    {
        harmonicCount = harmonics;
        System.Array.Clear(harmonicLevels, 0, harmonicLevels.Length);

        for (int n = 1; n <= harmonics && n <= harmonicLevels.Length; n++)
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

    // Seccion 8.5 de la guia: se genera la muestra del oscilador, se multiplica
    // por la envolvente y el resultado se escribe en el buffer.
    void OnAudioFilterRead(float[] data, int channels)
    {
        // Nota terminada: silencio explicito.
        if (!envelope.IsActive)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        double phaseStep = frequency / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            // TimeIndex vale 0 solo en la primera muestra de la nota: reinicia
            // la fase una vez por nota, cuando la envolvente vale ~0.
            if (envelope.TimeIndex == 0)
                phase = 0.0;

            float env = envelope.GetAdsr(envelope.TimeIndex);

            float sample = Mathf.Clamp(amplitude * env * GetSample(phase), -1f, 1f);

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;

            phase += phaseStep;
            if (phase >= 1.0)
                phase -= System.Math.Floor(phase);

            envelope.AdvanceTime();
        }
    }
}
