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

    public bool isPlaying => envelope.Stage != AdsrStage.Idle;

    // Fase normalizada del oscilador: 0..1 equivale a un ciclo completo.
    // Se ACUMULA muestra a muestra en vez de calcularse con la formula absoluta
    // f*n/sampleRate. Dos motivos:
    //   1) Cambiar 'frequency' a mitad de nota ya no teletransporta la fase, que
    //      era lo que producia el click al cambiar de nota con el piano. Ese
    //      click tapaba por completo un ataque de 10 ms.
    //   2) 'n' crecia sin limite y el float se quedaba sin mantisa: a los pocos
    //      minutos la onda se degradaba (peor todavia en los armonicos altos,
    //      que multiplican n por hasta 10).
    private double phase = 0.0;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;

        // Sin un AudioClip asignado, el AudioSource nunca queda "reproduciendo"
        // y OnAudioFilterRead no se llama (Play On Awake no alcanza). Se le da
        // un clip corto y silencioso en loop solo para activar el pipeline de
        // audio; su contenido no importa porque OnAudioFilterRead sobreescribe
        // cada muestra con la señal generada.
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

    // Version directa (formula exacta, sin armonicos) de la misma onda, para
    float DirectWave(AdditiveWaveformType type, double normalizedPhase)
    {
        float p = (float)(normalizedPhase - System.Math.Floor(normalizedPhase));

        switch (type)
        {
            case AdditiveWaveformType.Square:
                // Comparacion directa: mismo resultado que Mathf.Sign(Mathf.Sin(..))
                // pero sin calcular un seno solo para quedarse con su signo.
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

    void OnAudioFilterRead(float[] data, int channels)
    {
        float dt = 1f / sampleRate;

        // Copia local: el hilo principal puede cambiar 'frequency' a mitad de
        // buffer, asi al menos este bloque queda coherente consigo mismo.
        double phaseStep = frequency / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            // Se mira la etapa antes y despues para detectar el flanco
            // Idle -> Attack. Ambas lecturas son de este mismo hilo, asi que
            // son exactas y gratis (no hace falta sincronizar nada).
            AdsrStage previousStage = envelope.Stage;
            float env = envelope.Process(dt);

            // Unico punto donde reiniciar la fase es inaudible: la nota arranca
            // desde silencio, asi que en ese instante la amplitud vale ~0.
            if (previousStage == AdsrStage.Idle && envelope.Stage == AdsrStage.Attack)
                phase = 0.0;

            float sample = Mathf.Clamp(amplitude * env * GetSample(phase), -1f, 1f);

            for (int ch = 0; ch < channels; ch++)
                data[i + ch] = sample;

            phase += phaseStep;
            if (phase >= 1.0)
                phase -= System.Math.Floor(phase);
        }
    }
}
