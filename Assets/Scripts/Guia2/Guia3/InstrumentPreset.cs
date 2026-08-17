//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;

// Resultado del analisis de UNA muestra real, guardado como asset. Los campos
// se llaman igual que las claves del script de Python (f0_estimada_hz, H1..H10,
// attack_ms, ...) para copiar los numeros tal cual, sin convertir unidades.
[CreateAssetMenu(fileName = "InstrumentPreset", menuName = "Audio Procedural/Instrument Preset")]
public class InstrumentPreset : ScriptableObject
{
    public const int HarmonicSlots = 10;

    public string displayName = "Instrumento";

    [Header("Tono de referencia")]
    [Tooltip("Frecuencia fundamental estimada por la FFT (Hz). No fija la nota: " +
             "solo selecciona la octava del teclado en la que vive el instrumento.")]
    public float f0 = 261.63f;

    [Header("Espectro: 10 armonicos ya normalizados entre 0 y 1")]
    public float[] harmonicLevels = new float[HarmonicSlots];

    [Range(1, HarmonicSlots)]
    public int harmonicCount = HarmonicSlots;

    [Header("ADSR (tiempos en ms; SL es un nivel 0..1)")]
    [Tooltip("attack_ms")] public float A = 10f;
    [Tooltip("decay_ms")] public float D = 200f;
    [Tooltip("sustain_ms: DURACION del sostenido, no el nivel")] public float S = 1500f;
    [Range(0f, 1f)]
    [Tooltip("sustain_level")] public float SL = 0.7f;
    [Tooltip("release_ms")] public float R = 300f;

    [Header("Salida")]
    [Range(0f, 1f)]
    public float amplitude = 0.25f;

    void OnValidate()
    {
        // Fuerza los 10 huecos: si el array se queda corto, los sliders H9/H10
        // no controlan nada.
        if (harmonicLevels == null || harmonicLevels.Length != HarmonicSlots)
            System.Array.Resize(ref harmonicLevels, HarmonicSlots);

        for (int i = 0; i < harmonicLevels.Length; i++)
            harmonicLevels[i] = Mathf.Clamp01(harmonicLevels[i]);

        f0 = Mathf.Max(1f, f0);
        A = Mathf.Max(0f, A);
        D = Mathf.Max(0f, D);
        S = Mathf.Max(0f, S);
        R = Mathf.Max(0f, R);
    }
}
