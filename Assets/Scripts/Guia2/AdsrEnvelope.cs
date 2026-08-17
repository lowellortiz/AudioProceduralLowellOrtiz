//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;

public enum AdsrStage { Idle, Attack, Decay, Sustain, Release }


// Todos los tramos temporales (A, D, S, R) estan en MILISEGUNDOS 
[System.Serializable]
public class AdsrEnvelope
{
    [Tooltip("Attack en milisegundos: tiempo de subida de 0 a 1.")]
    public float A = 10f;

    [Tooltip("Decay en milisegundos: tiempo de caida desde el pico hasta SL.")]
    public float D = 200f;

    [Tooltip("Sustain en milisegundos: DURACION del tramo sostenido (no es el nivel).")]
    public float S = 1500f;

    [Range(0f, 1f)]
    [Tooltip("Sustain Level entre 0 y 1: amplitud a la que se mantiene el sostenido.")]
    public float SL = 0.7f;

    [Tooltip("Release en milisegundos: tiempo de apagado al soltar la nota.")]
    public float R = 300f;

    private float FM = 44100f;

    [SerializeField] private int timeIndex = 0;
    private bool noteActive = false;

    private bool releaseTriggered = false;
    private int releaseStartTimeIndex = 0;
    private float releaseStartLevel = 0f;

    public int TimeIndex => timeIndex;

    public void SetSampleRate(float sampleRate)
    {
        if (sampleRate > 0f)
            FM = sampleRate;
    }


    private int AttackSamples => Mathf.Max(1, Mathf.RoundToInt((A / 1000f) * FM));
    private int DecaySamples => Mathf.Max(0, Mathf.RoundToInt((D / 1000f) * FM));
    private int SustainSamples => Mathf.Max(0, Mathf.RoundToInt((S / 1000f) * FM));
    private int ReleaseSamples => Mathf.Max(1, Mathf.RoundToInt((R / 1000f) * FM));

    // Muestra en la que la nota deja de sonar: manda el release disparado si ya
    // se solto la tecla, y si no el recorrido programado A + D + S + R.
    public int EndSampleIndex
    {
        get
        {
            if (releaseTriggered)
                return releaseStartTimeIndex + ReleaseSamples;

            return AttackSamples + DecaySamples + SustainSamples + ReleaseSamples;
        }
    }

    public bool IsActive => noteActive && timeIndex < EndSampleIndex;

    // Etapa actual. Se calcula desde el reloj en vez de guardarse, asi no puede
    // desincronizarse.
    public AdsrStage Stage
    {
        get
        {
            if (!IsActive)
                return AdsrStage.Idle;

            if (releaseTriggered && timeIndex >= releaseStartTimeIndex)
                return AdsrStage.Release;

            int attackEnd = AttackSamples;
            int decayEnd = attackEnd + DecaySamples;
            int sustainEnd = decayEnd + SustainSamples;

            if (timeIndex < attackEnd) return AdsrStage.Attack;
            if (timeIndex < decayEnd) return AdsrStage.Decay;
            if (timeIndex < sustainEnd) return AdsrStage.Sustain;
            return AdsrStage.Release;
        }
    }

    // KeyboardDown() de la guia: reinicia el reloj y el estado del release.
    public void NoteOn()
    {
        timeIndex = 0;
        releaseTriggered = false;
        releaseStartTimeIndex = 0;
        releaseStartLevel = 0f;
        noteActive = true;
    }

    // KeyboardUp() de la guia: toma el nivel actual de la envolvente y desde ahi
    // arranca el release, sin esperar a que termine la ADSR programada.
    public void NoteOff()
    {
        // Si la nota ya se agoto sola no hay que resucitarla.
        if (!noteActive || releaseTriggered)
            return;

        releaseStartTimeIndex = timeIndex;
        releaseStartLevel = GetScheduledAdsr(timeIndex);
        releaseTriggered = true;
    }

    // El "TimeIndex++" de la guia. Se congela al terminar la nota.
    public void AdvanceTime()
    {
        if (IsActive)
            timeIndex++;
    }

    // ADSR programada (seccion 8.3): el ataque sube linealmente de 0 a 1, el
    // decay baja de 1 a SL, el sustain se mantiene en SL y el release va de SL a 0.
    public float GetScheduledAdsr(int t)
    {
        int attackEnd = AttackSamples;
        int decayEnd = attackEnd + DecaySamples;
        int sustainEnd = decayEnd + SustainSamples;
        int releaseEnd = sustainEnd + ReleaseSamples;

        if (t < attackEnd)
        {
            return (float)t / attackEnd;
        }
        else if (t < decayEnd)
        {
            float p = (float)(t - attackEnd) / Mathf.Max(1, decayEnd - attackEnd);
            return Mathf.Lerp(1f, SL, p);
        }
        else if (t < sustainEnd)
        {
            return SL;
        }
        else if (t < releaseEnd)
        {
            float p = (float)(t - sustainEnd) / Mathf.Max(1, releaseEnd - sustainEnd);
            return Mathf.Lerp(SL, 0f, p);
        }
        else
        {
            return 0f;
        }
    }

    // Valor instantaneo real (0..1). Si se solto la tecla, el release disparado
    // tiene prioridad sobre el recorrido programado (seccion 8.4).
    public float GetAdsr(int t)
    {
        if (!noteActive)
            return 0f;

        if (releaseTriggered && t >= releaseStartTimeIndex)
        {
            float progress = (float)(t - releaseStartTimeIndex) / ReleaseSamples;
            return Mathf.Lerp(releaseStartLevel, 0f, Mathf.Clamp01(progress));
        }

        return GetScheduledAdsr(t);
    }
}
