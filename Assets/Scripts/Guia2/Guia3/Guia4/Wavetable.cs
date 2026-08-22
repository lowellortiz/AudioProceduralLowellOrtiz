//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;

// De donde salen las muestras del periodo almacenado (seccion 8 de la guia).
public enum WavetableSourceMode
{
    Calculated,   // se evalua una formula y se guarda el resultado
    TextFile,     // se parsea un .txt de floats separados por comas
    AudioClip     // se extrae un fragmento de un AudioClip
}

// La tabla en si: guarda UN periodo y sabe leerlo a cualquier fase.
//
// Nota sobre el 'step' de la guia: alli el indice avanza en unidades de tabla
// (step = f * tamano / Fs) y hay que hacerle modulo a mano. Aqui el oscilador
// ya acumula fase NORMALIZADA (0..1 = un ciclo), que es exactamente lo mismo
// dividido entre el tamano. Por eso Read() recibe 0..1 y multiplica por Size:
// misma matematica, pero conservando la fase continua que evita los clicks al
// cambiar de nota a mitad de sonido.
[System.Serializable]
public class Wavetable
{
    // NUNCA se escribe in-place: RebuildWavetable construye un array nuevo y
    // reemplaza esta referencia de golpe. OnAudioFilterRead corre en el hilo de
    // audio mientras la UI reconstruye en el hilo principal, y una asignacion de
    // referencia si es atomica; llenar el array a la vista del lector no lo es.
    private float[] table;

    public int Size => table != null ? table.Length : 0;
    public bool IsReady => table != null && table.Length > 0;

    // Seccion 3: se recorre una fase normalizada 0..1 y se guarda un valor por
    // posicion. El generador decide la FORMA; este metodo decide la RESOLUCION.
    public void BuildFromGenerator(System.Func<double, float> generator, int size)
    {
        if (generator == null || size < 2)
        {
            table = null;
            return;
        }

        var nueva = new float[size];
        for (int i = 0; i < size; i++)
        {
            double phase = (double)i / size;
            nueva[i] = Mathf.Clamp(generator(phase), -1f, 1f);
        }

        table = nueva;
    }

    // Seccion 6: las muestras vienen de fuera (archivo o clip) y casi nunca
    // traen la longitud de la tabla, asi que se remuestrean al tamano interno.
    public void BuildFromSamples(float[] source, int targetSize)
    {
        if (source == null || source.Length == 0 || targetSize < 2)
        {
            table = null;
            return;
        }

        table = Resample(source, targetSize);
    }

    // ResampleToWavetable de la seccion 6.3: cada posicion destino cae en un
    // punto fraccionario del origen y se interpola entre sus dos vecinos. El
    // modulo cierra el ciclo, de modo que la ultima muestra interpola contra la
    // primera y el loop no salta.
    public static float[] Resample(float[] source, int targetSize)
    {
        var result = new float[targetSize];

        for (int i = 0; i < targetSize; i++)
        {
            float sourcePos = (i / (float)targetSize) * source.Length;
            int i0 = Mathf.FloorToInt(sourcePos) % source.Length;
            int i1 = (i0 + 1) % source.Length;
            float frac = sourcePos - Mathf.Floor(sourcePos);

            result[i] = Mathf.Clamp(Mathf.Lerp(source[i0], source[i1], frac), -1f, 1f);
        }

        return result;
    }

    // Seccion 7: toma una de cada 'factor' muestras. Se conserva porque la guia
    // lo pide como concepto, pero el camino normal es Resample: diezmar sin
    // filtrar antes es justo lo que introduce aliasing.
    public static float[] Decimate(float[] source, int factor)
    {
        if (source == null || factor < 1)
            return source;

        int newSize = source.Length / factor;
        if (newSize < 2)
            return source;

        var reduced = new float[newSize];
        for (int i = 0; i < newSize; i++)
            reduced[i] = source[i * factor];

        return reduced;
    }

    // Lectura de la tabla. 'phase01' es la fase normalizada del oscilador.
    //
    //   interpolate == false -> seccion 4: se trunca al indice entero.
    //   interpolate == true  -> seccion 5: mezcla lineal entre las dos vecinas.
    public float Read(double phase01, bool interpolate)
    {
        // Copia local: si otro hilo reemplaza 'table' a mitad de este metodo,
        // seguimos leyendo la tabla vieja completa en vez de reventar.
        var t = table;
        if (t == null || t.Length == 0)
            return 0f;

        double wrapped = phase01 - System.Math.Floor(phase01);
        double pos = wrapped * t.Length;

        int index0 = (int)pos % t.Length;
        if (index0 < 0) index0 += t.Length;

        if (!interpolate)
            return t[index0];

        int index1 = (index0 + 1) % t.Length;
        float frac = (float)(pos - System.Math.Floor(pos));

        return Mathf.Lerp(t[index0], t[index1], frac);
    }
}
