using UnityEngine;
using System.Globalization;
using System.IO;

/// <summary>
/// Lee un archivo de texto con muestras separadas por comas desde:
/// Assets/Wavetables/wavetable1.txt
///
/// Tambien permite asignar el archivo como TextAsset por inspector,
/// lo cual suele ser mas comodo en builds.
///
/// El parseo vive en el estatico Parse(): asi el oscilador puede convertir un
/// TextAsset a float[] sin necesitar un GameObject de este tipo en la escena.
/// </summary>
public class WavetableTxtLoader : MonoBehaviour
{
    [Header("Opcion recomendada: asignar el txt como TextAsset desde el Inspector")]
    public TextAsset wavetableTextAsset;

    [Header("Ruta relativa dentro de Assets si no se usa TextAsset")]
    public string relativePathInsideAssets = "Wavetables/wavetable1.txt";

    [Header("Datos cargados")]
    public float[] samples;

    [Header("Opciones")]
    public bool loadOnStart = true;
    public bool logResult = true;

    private void Start()
    {
        if (loadOnStart)
        {
            LoadWavetable();
        }
    }

    [ContextMenu("Load Wavetable")]
    public void LoadWavetable()
    {
        string rawText = string.Empty;

        // Opcion 1: leer desde TextAsset asignado en inspector
        if (wavetableTextAsset != null)
        {
            rawText = wavetableTextAsset.text;
        }
        else
        {
            // Opcion 2: leer directamente desde Assets usando ruta absoluta
            string fullPath = Path.Combine(Application.dataPath, relativePathInsideAssets);

            if (!File.Exists(fullPath))
            {
                Debug.LogError("No se encontro el archivo wavetable en: " + fullPath);
                samples = null;
                return;
            }

            rawText = File.ReadAllText(fullPath);
        }

        ParseSamples(rawText);
    }

    private void ParseSamples(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            Debug.LogError("El archivo wavetable esta vacio.");
            samples = null;
            return;
        }

        samples = Parse(rawText);

        if (logResult && samples != null)
        {
            Debug.Log("Wavetable cargada correctamente. Numero de muestras: " + samples.Length);
        }
    }

    /// <summary>
    /// Convierte el contenido del archivo en un float[] normalizado entre -1 y 1.
    /// Descarta los tokens vacios y los que no son numero, asi el arreglo final
    /// solo contiene muestras validas (por eso el redimensionado del final).
    /// Estatico y sin estado: lo usa tanto este componente como el oscilador.
    /// </summary>
    public static float[] Parse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        string cleaned = rawText.Replace("\n", "").Replace("\r", "").Trim();
        string[] parts = cleaned.Split(',');

        var valores = new float[parts.Length];
        int validCount = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim();

            if (string.IsNullOrEmpty(token))
                continue;

            // InvariantCulture a proposito: el archivo usa punto decimal y en
            // una maquina con locale espanol float.Parse esperaria coma.
            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                valores[validCount] = Mathf.Clamp(value, -1f, 1f);
                validCount++;
            }
            else
            {
                Debug.LogWarning("No se pudo convertir el valor en el indice " + i + ": " + token);
            }
        }

        if (validCount != valores.Length)
        {
            float[] resized = new float[validCount];
            for (int i = 0; i < validCount; i++)
            {
                resized[i] = valores[i];
            }
            return resized;
        }

        return valores;
    }

    public float GetSample(int index)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        index = Mathf.Clamp(index, 0, samples.Length - 1);
        return samples[index];
    }

    public float GetSampleNormalized(float phase01)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        phase01 = Mathf.Repeat(phase01, 1f);
        int index = Mathf.FloorToInt(phase01 * samples.Length) % samples.Length;
        return samples[index];
    }
}
