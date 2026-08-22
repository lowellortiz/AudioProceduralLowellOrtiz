//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Clase controladora (front): traduce interacción del usuario (teclado de una
// octava, por botones o por teclado fisico 1-12) en cambios sobre el
// oscilador. No calcula ninguna muestra de audio.
public class AdditiveKeyboardController : MonoBehaviour
{
    public SimpleAdditiveOscillator oscillator;
    public Button[] noteButtons;
    public string[] noteNames =
    {
        "C", "C#", "D", "D#", "E", "F",
        "F#", "G", "G#", "A", "A#", "B"
    };
    public int octave = 3;

    // Controles maestros: afectan a todo el instrumento, no a una nota.
    // Los presets los cambian tambien, por eso RefreshUiFromOscillator los
    // vuelve a empujar a la UI (antes se movian en silencio).
    [Header("Maestro")]
    public Slider amplitudeSlider;     // volumen general -> oscillator.amplitude
    public Slider octaveSlider;        // octava del teclado (entera, 0..8)

    // Selector de 5 posiciones sobre oscillator.waveform. Los cuatro primeros
    // son la FORMULA DIRECTA de esa onda; el quinto suma los 10 armonicos.
    //
    // Ninguno toca harmonicLevels ni harmonicCount: los sliders de armonicos
    // son el instrumento del modo Aditiva y se quedan donde el usuario los deje.
    [Header("Forma de onda")]
    public Button waveButtonSine;
    public Button waveButtonSquare;
    public Button waveButtonTriangle;
    public Button waveButtonSawtooth;
    public Button waveButtonAdditive;

    // Abreviatura de la onda activa: SE / CU / TR / SA / AD.
    public Text waveModeLabel;
    public Color activeColor = new Color(0.13f, 0.87f, 0.87f, 1f);   // cian
    public Color activeTextColor = Color.black;
    public Color waveButtonColor = new Color(0.10f, 0.10f, 0.10f, 1f);
    public Color waveButtonTextColor = Color.white;

    // Slider "Armónicos" (Min 1, Max 10, Whole Numbers) creado en el Canvas.
    public Slider harmonicSlider;

    // Guia 4. La wavetable no sustituye a los botones de onda: decide si esa
    // onda se CALCULA muestra a muestra o se LEE de un periodo guardado. Los
    // cuatro controles solo escriben estado en el oscilador y le piden
    // reconstruir la tabla; ninguno toca una muestra de audio.
    [Header("Wavetable (Guia 4)")]
    public Button wavetableToggleButton;     // ON/OFF de la lectura por tabla
    public Button interpolateToggleButton;   // ON/OFF de la interpolacion lineal
    public Dropdown wavetableSourceDropdown; // Calculada / los .txt / AudioClip
    public Slider tableSizeSlider;           // indice 0..4 -> TamanosTabla

    // Los tamanos que compara la guia (secciones 3.1 y 7). El slider guarda el
    // INDICE, no el tamano: asi no se pueden pedir tamanos intermedios sin
    // sentido y la comparacion entre resoluciones es de a saltos claros.
    public static readonly int[] TamanosTabla = { 128, 256, 512, 1024, 2048 };

    const string OpcionCalculada = "Calculada (formula)";
    const string OpcionAudioClip = "AudioClip";

    // 10 sliders (Min 0, Max 1) en el Canvas: amplitudeSliders[i] controla
    // harmonicLevels[i], la amplitud del armónico i+1.
    public Slider[] amplitudeSliders = new Slider[10];

    // Fila completa (etiqueta + slider + valor) de cada armonico. Se atenua la
    // de los armonicos que quedan por encima de harmonicCount: un solo alpha
    // por fila en vez de tocar cada Graphic por separado.
    public CanvasGroup[] harmonicRows = new CanvasGroup[10];

    // Ojo con las unidades: A, D, S y R son TIEMPOS en milisegundos, mientras
    // que sustainSlider es el NIVEL SL, un 0..1.
    [Header("ADSR (tiempos en ms)")]
    public Slider attackSlider;        // A  : ms
    public Slider decaySlider;         // D  : ms
    public Slider sustainTimeSlider;   // S  : ms  (duracion del sostenido)
    public Slider sustainSlider;       // SL : nivel 0..1
    public Slider releaseSlider;       // R  : ms

    // Cada asset trae la f0, los 10 armonicos normalizados y la ADSR del
    // analisis en Python. El boton i carga el preset i: mismo orden en ambos.
    [Header("Presets de instrumento")]
    public InstrumentPreset[] instrumentPresets;

    // Menu desplegable de instrumentos. La opcion 0 es "Personalizado": el
    // sonido actual no corresponde a ningun preset.
    public Dropdown presetDropdown;

    // Se conserva por si algun dia se vuelve a los botones; es null-safe.
    public Button[] instrumentButtons;

    const string OpcionPersonalizado = "- Personalizado -";

    public const int MinOctave = 0;
    public const int MaxOctave = 8;

    // Indice del preset cargado, o -1 si el sonido actual ya no corresponde a
    // ningun instrumento (porque se movio un slider o se cambio la onda).
    // La UI lo lee para resaltar el boton correspondiente.
    public int ActivePresetIndex { get; private set; } = -1;

    // Se dispara cuando el estado del modelo se empujo a los controles. Como
    // ese empuje usa SetValueWithoutNotify, los onValueChanged NO saltan y las
    // lecturas numericas se quedarian mintiendo sin este aviso.
    public event System.Action UiRefreshed;

    // Fila superior del teclado fisico: 1 2 3 4 5 6 7 8 9 0 Q W, en el mismo
    // orden que noteNames (12 teclas para las 12 notas de la octava).
    private readonly Key[] noteKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6,
        Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.Q, Key.W
    };

    void Start()
    {
        for (int i = 0; i < noteButtons.Length; i++)
        {
            int index = i;
            BindNoteButton(noteButtons[i], index);
        }

        GuardarColoresDeTeclas();

        BindWaveButton(waveButtonSine, AdditiveWaveformType.Sine);
        BindWaveButton(waveButtonSquare, AdditiveWaveformType.Square);
        BindWaveButton(waveButtonTriangle, AdditiveWaveformType.Triangle);
        BindWaveButton(waveButtonSawtooth, AdditiveWaveformType.Sawtooth);
        BindWaveButton(waveButtonAdditive, AdditiveWaveformType.Additive);

        BindOctaveSlider();
        BindEnvelopeSlider(amplitudeSlider, oscillator.amplitude, value => oscillator.amplitude = value);

        if (harmonicSlider)
        {
            harmonicSlider.onValueChanged.AddListener(HarmonicChange);
            HarmonicChange(harmonicSlider.value);
        }

        int sliderCount = Mathf.Min(amplitudeSliders.Length, oscillator.harmonicLevels.Length);
        for (int i = 0; i < sliderCount; i++)
        {
            if (amplitudeSliders[i] == null) continue;
            int index = i;
            amplitudeSliders[i].value = oscillator.harmonicLevels[index];
            amplitudeSliders[i].onValueChanged.AddListener(value => AmplitudeChange(index, value));
        }

        BindEnvelopeSlider(attackSlider, oscillator.envelope.A, value => oscillator.envelope.A = value);
        BindEnvelopeSlider(decaySlider, oscillator.envelope.D, value => oscillator.envelope.D = value);
        BindEnvelopeSlider(sustainTimeSlider, oscillator.envelope.S, value => oscillator.envelope.S = value);
        BindEnvelopeSlider(sustainSlider, oscillator.envelope.SL, value => oscillator.envelope.SL = value);
        BindEnvelopeSlider(releaseSlider, oscillator.envelope.R, value => oscillator.envelope.R = value);

        BindPresetButtons();
        BindPresetDropdown();
        BindWavetableControls();
    }

    // =================================================================
    //  Guia 4: controles de wavetable
    // =================================================================
    void BindWavetableControls()
    {
        if (oscillator == null)
            return;

        BindToggle(wavetableToggleButton, () =>
        {
            oscillator.useWavetable = !oscillator.useWavetable;

            // Al encenderla por primera vez la tabla puede estar vacia (o venir
            // de otra forma de onda): reconstruir aqui evita el silencio.
            oscillator.RebuildWavetable();
        });

        // La interpolacion no cambia el contenido de la tabla, solo como se lee:
        // no hace falta reconstruir nada.
        BindToggle(interpolateToggleButton, () => oscillator.interpolate = !oscillator.interpolate);

        BindWavetableDropdown();
        BindTableSizeSlider();
    }

    void BindToggle(Button boton, UnityAction accion)
    {
        if (boton == null)
            return;

        boton.onClick.AddListener(accion);
    }

    // Opcion 0 = tabla calculada; 1..N = los .txt del oscilador; la ultima es el
    // AudioClip, y solo aparece si hay uno asignado (un menu no debe ofrecer
    // algo que no puede cargar).
    void BindWavetableDropdown()
    {
        if (wavetableSourceDropdown == null)
            return;

        var opciones = new System.Collections.Generic.List<string> { OpcionCalculada };

        if (oscillator.wavetableFiles != null)
        {
            foreach (var archivo in oscillator.wavetableFiles)
                opciones.Add(archivo == null ? "-" : archivo.name);
        }

        if (oscillator.wavetableClip != null)
            opciones.Add(OpcionAudioClip);

        wavetableSourceDropdown.ClearOptions();
        wavetableSourceDropdown.AddOptions(opciones);
        wavetableSourceDropdown.SetValueWithoutNotify(IndiceDeFuenteActual());

        wavetableSourceDropdown.onValueChanged.AddListener(AplicarFuenteDeTabla);
    }

    int CantidadDeArchivos =>
        oscillator != null && oscillator.wavetableFiles != null ? oscillator.wavetableFiles.Length : 0;

    int IndiceDeFuenteActual()
    {
        switch (oscillator.tableSource)
        {
            case WavetableSourceMode.TextFile:
                return Mathf.Clamp(oscillator.wavetableFileIndex, 0, Mathf.Max(0, CantidadDeArchivos - 1)) + 1;

            case WavetableSourceMode.AudioClip:
                return CantidadDeArchivos + 1;

            default:
                return 0;
        }
    }

    void AplicarFuenteDeTabla(int indice)
    {
        if (indice <= 0)
        {
            oscillator.tableSource = WavetableSourceMode.Calculated;
        }
        else if (indice <= CantidadDeArchivos)
        {
            oscillator.tableSource = WavetableSourceMode.TextFile;
            oscillator.wavetableFileIndex = indice - 1;
        }
        else
        {
            oscillator.tableSource = WavetableSourceMode.AudioClip;
        }

        oscillator.RebuildWavetable();
    }

    void BindTableSizeSlider()
    {
        if (tableSizeSlider == null)
            return;

        tableSizeSlider.SetValueWithoutNotify(IndiceDeTamano(oscillator.wavetableSize));
        tableSizeSlider.onValueChanged.AddListener(valor =>
        {
            int indice = Mathf.Clamp(Mathf.RoundToInt(valor), 0, TamanosTabla.Length - 1);
            oscillator.wavetableSize = TamanosTabla[indice];
            oscillator.RebuildWavetable();
        });
    }

    // Indice del tamano exacto, o el mas cercano por debajo si el Inspector trae
    // un valor que no esta en la lista.
    static int IndiceDeTamano(int tamano)
    {
        int indice = 0;
        for (int i = 0; i < TamanosTabla.Length; i++)
            if (TamanosTabla[i] <= tamano)
                indice = i;

        return indice;
    }

    // La tabla calculada retrata la onda ACTUAL: si cambia la forma o algun
    // armonico hay que rehacerla, o en modo wavetable el cambio no se oiria.
    // Con la tabla venida de archivo o de clip no aplica: su contenido no
    // depende de estos controles.
    void RebuildWavetableSiEsCalculada()
    {
        if (oscillator != null && oscillator.tableSource == WavetableSourceMode.Calculated)
            oscillator.RebuildWavetable();
    }

    // Deja el slider mostrando el valor serializado en la envolvente y a partir
    // de ahi escribe cada cambio directo sobre ella.
    void BindEnvelopeSlider(Slider slider, float currentValue, UnityAction<float> apply)
    {
        if (slider == null)
            return;

        // Si el rango del slider no cubre el valor, UI y envolvente quedan
        // desincronizadas en silencio: mejor que se vea en la consola.
        if (currentValue < slider.minValue || currentValue > slider.maxValue)
        {
            Debug.LogWarning(
                $"[ADSR] '{slider.name}': el valor {currentValue} esta fuera del rango " +
                $"[{slider.minValue}, {slider.maxValue}]. Revisa Min/Max en el Inspector " +
                "(los tiempos ahora estan en milisegundos, no en segundos).", slider);
        }

        slider.SetValueWithoutNotify(Mathf.Clamp(currentValue, slider.minValue, slider.maxValue));

        // Tocar el slider a mano significa que el sonido ya no es el del preset.
        slider.onValueChanged.AddListener(value =>
        {
            apply(value);
            ClearActivePreset();
        });
    }

    // Octava del teclado. Es el control que el usuario tenia que adivinar: los
    // presets la movian solos (Violin salta a la 5) y no habia forma de verlo
    // ni de volver.
    void BindOctaveSlider()
    {
        if (octaveSlider == null)
            return;

        octaveSlider.SetValueWithoutNotify(Mathf.Clamp(octave, MinOctave, MaxOctave));
        octaveSlider.onValueChanged.AddListener(OctaveChange);
    }

    void OctaveChange(float value)
    {
        int nuevo = Mathf.Clamp(Mathf.RoundToInt(value), MinOctave, MaxOctave);
        if (nuevo == octave)
            return;

        octave = nuevo;

        // Si hay una nota sonando, se reafina en el acto sin cortarla: la fase
        // se acumula, asi que cambiar la frecuencia a mitad de nota no chasquea.
        if (currentNoteIndex >= 0)
            oscillator.frequency = GetFrequencyFromOctave0(noteNames[currentNoteIndex], octave);

        NotifyUiRefresh();
    }

    void BindPresetButtons()
    {
        if (instrumentButtons == null)
            return;

        for (int i = 0; i < instrumentButtons.Length; i++)
        {
            if (instrumentButtons[i] == null) continue;
            if (instrumentPresets == null || i >= instrumentPresets.Length) continue;
            if (instrumentPresets[i] == null) continue;

            int index = i;
            instrumentButtons[i].onClick.AddListener(() =>
            {
                ActivePresetIndex = index;
                ApplyPreset(instrumentPresets[index]);
            });
        }
    }

    // Menu de instrumentos. La opcion 0 es "Personalizado", asi que el preset i
    // vive en la opcion i+1: el menu siempre tiene algo honesto que mostrar.
    void BindPresetDropdown()
    {
        if (presetDropdown == null || instrumentPresets == null)
            return;

        var opciones = new System.Collections.Generic.List<string> { OpcionPersonalizado };

        foreach (var preset in instrumentPresets)
        {
            if (preset == null)
                opciones.Add("-");
            else
                opciones.Add(string.IsNullOrEmpty(preset.displayName) ? preset.name : preset.displayName);
        }

        presetDropdown.ClearOptions();
        presetDropdown.AddOptions(opciones);
        presetDropdown.SetValueWithoutNotify(0);

        presetDropdown.onValueChanged.AddListener(indice =>
        {
            if (indice <= 0)
            {
                ClearActivePreset();
                return;
            }

            int preset = indice - 1;
            if (preset >= instrumentPresets.Length || instrumentPresets[preset] == null)
                return;

            ActivePresetIndex = preset;
            ApplyPreset(instrumentPresets[preset]);
        });
    }

    void SyncPresetDropdown()
    {
        if (presetDropdown != null)
            presetDropdown.SetValueWithoutNotify(ActivePresetIndex + 1);
    }

    // El sonido dejo de ser el del instrumento cargado. Solo avisa si de verdad
    // cambio algo: si no, arrastrar un slider dispararia un refresco por frame.
    void ClearActivePreset()
    {
        if (ActivePresetIndex == -1)
            return;

        ActivePresetIndex = -1;
        SyncPresetDropdown();
        NotifyUiRefresh();
    }

    void NotifyUiRefresh() => UiRefreshed?.Invoke();

    // Vuelca el preset sobre el oscilador y DESPUES refleja el resultado en la
    // UI. La direccion es siempre modelo -> UI y nunca al reves.
    public void ApplyPreset(InstrumentPreset preset)
    {
        if (preset == null || oscillator == null)
            return;
        oscillator.NoteOff();

        oscillator.waveform = AdditiveWaveformType.Additive;
        oscillator.amplitude = preset.amplitude;

        int count = Mathf.Min(preset.harmonicLevels.Length, oscillator.harmonicLevels.Length);
        for (int i = 0; i < count; i++)
            oscillator.harmonicLevels[i] = preset.harmonicLevels[i];

        oscillator.harmonicCount = Mathf.Clamp(preset.harmonicCount, 1, oscillator.harmonicLevels.Length);

        oscillator.envelope.A = preset.A;
        oscillator.envelope.D = preset.D;
        oscillator.envelope.S = preset.S;
        oscillator.envelope.SL = preset.SL;
        oscillator.envelope.R = preset.R;

        octave = OctaveFromFrequency(preset.f0);

        // El preset acaba de cambiar la forma y los 10 armonicos: si la tabla se
        // calcula desde la onda actual, esta retratando al instrumento anterior.
        RebuildWavetableSiEsCalculada();

        // Nota: mas arriba ApplyPreset ya puso waveform = Additive, asi que al
        // cargar un instrumento queda resaltado el boton 'Aditiva'. Es correcto:
        // el sonido sale de sumar los armonicos que acaba de traer el preset.
        RefreshUiFromOscillator();
    }

    // Empuja el estado del oscilador a los sliders sin disparar sus callbacks.
    void RefreshUiFromOscillator()
    {
        // El volumen maestro tambien viene en el preset (Guitarra lo sube a 1.0):
        // si no se empuja aqui, el slider se queda mostrando el valor anterior.
        SetSliderSilently(amplitudeSlider, oscillator.amplitude);

        // La octava tambien: Violin salta a la 5 y Trompeta a la 4.
        SetSliderSilently(octaveSlider, octave);

        SyncPresetDropdown();

        if (harmonicSlider)
            harmonicSlider.SetValueWithoutNotify(oscillator.harmonicCount);

        int count = Mathf.Min(amplitudeSliders.Length, oscillator.harmonicLevels.Length);
        for (int i = 0; i < count; i++)
        {
            if (amplitudeSliders[i] == null) continue;
            amplitudeSliders[i].SetValueWithoutNotify(oscillator.harmonicLevels[i]);
        }

        // Los controles de wavetable tambien: un preset no los cambia hoy, pero
        // si algun dia lo hace, la UI no puede quedarse mostrando lo anterior.
        if (wavetableSourceDropdown != null)
            wavetableSourceDropdown.SetValueWithoutNotify(IndiceDeFuenteActual());

        if (tableSizeSlider != null)
            tableSizeSlider.SetValueWithoutNotify(IndiceDeTamano(oscillator.wavetableSize));

        SetSliderSilently(attackSlider, oscillator.envelope.A);
        SetSliderSilently(decaySlider, oscillator.envelope.D);
        SetSliderSilently(sustainTimeSlider, oscillator.envelope.S);
        SetSliderSilently(sustainSlider, oscillator.envelope.SL);
        SetSliderSilently(releaseSlider, oscillator.envelope.R);

        // Los sliders quedaron con el valor nuevo pero SIN disparar sus eventos:
        // este aviso es lo unico que mantiene honestas las lecturas numericas.
        NotifyUiRefresh();
    }

    void SetSliderSilently(Slider slider, float value)
    {
        if (slider == null)
            return;

        if (value < slider.minValue || value > slider.maxValue)
        {
            Debug.LogWarning(
                $"[Preset] '{slider.name}' no cubre el valor {value} " +
                $"([{slider.minValue}, {slider.maxValue}]): el preset se aplico igual, " +
                "pero el slider queda mintiendo. Amplia el rango en el Inspector.", slider);
        }

        slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
    }

    // Ajusta cuántos armónicos del array harmonicLevels se suman en AdditiveWave.
    void HarmonicChange(float value)
    {
        oscillator.harmonicCount = Mathf.Clamp((int)value, 1, oscillator.harmonicLevels.Length);
        ClearActivePreset();
        RebuildWavetableSiEsCalculada();

        // Cambia cuantos armonicos suenan: la UI atenua los que quedan fuera.
        NotifyUiRefresh();
    }

    // Ajusta la amplitud individual del armónico index+1.
    void AmplitudeChange(int index, float value)
    {
        oscillator.harmonicLevels[index] = value;
        ClearActivePreset();
        RebuildWavetableSiEsCalculada();

        // No se cambia la onda a proposito. Si estas en una de las cuatro
        // formulas directas, mover este slider no se oye: para eso esta el
        // boton 'Aditiva' resaltado, que te dice cual generador esta sonando.
    }

    void BindWaveButton(Button boton, AdditiveWaveformType shape)
    {
        if (boton == null)
            return;

        // Lo unico que hace un boton de onda es elegir el generador. No toca
        // los armonicos: los 10 sliders son el instrumento del modo Aditiva y
        // se quedan tal cual, listos para cuando se vuelva a el.
        boton.onClick.AddListener(() =>
        {
            oscillator.waveform = shape;
            RebuildWavetableSiEsCalculada();
        });
    }

    // Abreviatura que se muestra al lado de los botones, como en la referencia.
    static string Abreviatura(AdditiveWaveformType shape)
    {
        switch (shape)
        {
            case AdditiveWaveformType.Sine: return "SE";
            case AdditiveWaveformType.Square: return "CU";
            case AdditiveWaveformType.Triangle: return "TR";
            case AdditiveWaveformType.Sawtooth: return "SA";
            case AdditiveWaveformType.Additive: return "AD";
            default: return "--";
        }
    }

    Button BotonDeForma(AdditiveWaveformType shape)
    {
        switch (shape)
        {
            case AdditiveWaveformType.Sine: return waveButtonSine;
            case AdditiveWaveformType.Square: return waveButtonSquare;
            case AdditiveWaveformType.Triangle: return waveButtonTriangle;
            case AdditiveWaveformType.Sawtooth: return waveButtonSawtooth;
            case AdditiveWaveformType.Additive: return waveButtonAdditive;
            default: return null;
        }
    }

    // Indice (en noteKeys) de la tecla fisica actualmente sostenida, o -1 si
    // ninguna. Solo esa tecla puede detener el sonido al soltarse.
    private int activeKeyIndex = -1;

    void Update()
    {
        if (Keyboard.current == null)
            return;

        int count = Mathf.Min(noteKeys.Length, noteNames.Length);
        for (int i = 0; i < count; i++)
        {
            if (Keyboard.current[noteKeys[i]].wasPressedThisFrame)
            {
                activeKeyIndex = i;
                KeyboardDown(i);
            }
            else if (i == activeKeyIndex && Keyboard.current[noteKeys[i]].wasReleasedThisFrame)
            {
                activeKeyIndex = -1;
                KeyboardUp();
            }
        }
    }

    // Indice (en noteNames) de la nota que suena actualmente, o -1 si ninguna.
    // Lo usa PointerUp para no cortar una nota que ya no le pertenece.
    private int currentNoteIndex = -1;

    // Refresco visual de lo que no es un slider. Va en LateUpdate y no en
    // eventos porque el estado que refleja lo cambian varios caminos distintos
    // (botones, presets, teclado) y asi ninguno se puede olvidar de avisar.
    void LateUpdate()
    {
        AtenuarArmonicosApagados();
        MostrarFormaDeOnda();
    }

    // Los armonicos por encima de N no se suman en AdditiveWave: la fila se
    // atenua entera para que se vea de un vistazo cuales estan sonando.
    void AtenuarArmonicosApagados()
    {
        if (harmonicRows == null || oscillator == null)
            return;

        for (int i = 0; i < harmonicRows.Length; i++)
        {
            if (harmonicRows[i] == null) continue;
            harmonicRows[i].alpha = i < oscillator.harmonicCount ? 1f : 0.30f;
        }
    }

    // Escribe la abreviatura de la onda activa (SE/CU/TR/SA) y su modo, y
    // resalta el boton correspondiente.
    void MostrarFormaDeOnda()
    {
        if (waveModeLabel != null)
            waveModeLabel.text = Abreviatura(oscillator.waveform);

        PintarBotonOnda(AdditiveWaveformType.Sine);
        PintarBotonOnda(AdditiveWaveformType.Square);
        PintarBotonOnda(AdditiveWaveformType.Triangle);
        PintarBotonOnda(AdditiveWaveformType.Sawtooth);
        PintarBotonOnda(AdditiveWaveformType.Additive);

        // Los dos toggles de la Guia 4 se pintan igual que los botones de onda:
        // encendido = resaltado. El texto dice ON/OFF porque, a diferencia de la
        // forma de onda, aqui no hay una fila de hermanos que de contexto.
        PintarToggle(wavetableToggleButton, "Wavetable", oscillator.useWavetable);
        PintarToggle(interpolateToggleButton, "Interp.", oscillator.interpolate);
    }

    void PintarToggle(Button boton, string etiqueta, bool activo)
    {
        if (boton == null)
            return;

        var texto = boton.GetComponentInChildren<Text>(true);
        string deseado = etiqueta + ": " + (activo ? "ON" : "OFF");

        if (texto != null && texto.text != deseado)
            texto.text = deseado;

        PintarBoton(boton, activo);
    }

    void PintarBotonOnda(AdditiveWaveformType shape)
    {
        PintarBoton(BotonDeForma(shape), oscillator.waveform == shape);
    }

    void PintarBoton(Button boton, bool activo)
    {
        if (boton == null)
            return;

        Color fondo = activo ? activeColor : waveButtonColor;

        var colores = boton.colors;
        if (colores.normalColor == fondo)
            return;   // ya esta pintado: no rehacer el ColorBlock cada frame

        colores.normalColor = fondo;
        colores.selectedColor = fondo;
        colores.highlightedColor = Color.Lerp(fondo, Color.white, 0.18f);
        colores.pressedColor = Color.Lerp(fondo, Color.black, 0.25f);
        boton.colors = colores;

        var texto = boton.GetComponentInChildren<Text>(true);
        if (texto != null)
            texto.color = activo ? activeTextColor : waveButtonTextColor;
    }

    // Calcula la frecuencia de la nota indiceNota desde la octava 0, la
    // asigna al oscilador y dispara el ataque de la ADSR (NoteOn).
    public void KeyboardDown(int indiceNota)
    {
        oscillator.frequency = GetFrequencyFromOctave0(noteNames[indiceNota], octave);
        oscillator.NoteOn();

        ApagarTecla();
        currentNoteIndex = indiceNota;
        PintarTecla(indiceNota, activeColor);
    }

    // Inicia la liberacion de la ADSR (NoteOff).
    public void KeyboardUp()
    {
        oscillator.NoteOff();
        ApagarTecla();
        currentNoteIndex = -1;
    }

    // Colores originales de las 12 teclas, para poder restaurarlos. El
    // resaltado se hace a mano porque el camino del teclado FISICO no pasa por
    // el Button y por tanto no dispara la transicion de color de uGUI.
    private Color[] coloresTecla;

    void GuardarColoresDeTeclas()
    {
        if (noteButtons == null)
            return;

        coloresTecla = new Color[noteButtons.Length];
        for (int i = 0; i < noteButtons.Length; i++)
            if (noteButtons[i] != null)
                coloresTecla[i] = noteButtons[i].colors.normalColor;
    }

    void PintarTecla(int indice, Color color)
    {
        if (noteButtons == null || indice < 0 || indice >= noteButtons.Length) return;
        if (noteButtons[indice] == null) return;

        var colores = noteButtons[indice].colors;
        colores.normalColor = color;
        colores.selectedColor = color;
        noteButtons[indice].colors = colores;
    }

    void ApagarTecla()
    {
        if (coloresTecla == null || currentNoteIndex < 0) return;
        if (currentNoteIndex >= coloresTecla.Length) return;

        PintarTecla(currentNoteIndex, coloresTecla[currentNoteIndex]);
    }

    // Los botones del piano NO usan onClick: ese evento nunca informa el momento
    // de soltar. Con PointerDown/PointerUp el boton se comporta como una tecla
    // fisica: suena mientras se mantenga oprimido y libera la ADSR al soltar.
    void BindNoteButton(Button button, int indiceNota)
    {
        if (button == null)
            return;

        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => KeyboardDown(indiceNota));
        trigger.triggers.Add(down);

        // Solo suelta si la nota que suena es la suya.
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ =>
        {
            if (currentNoteIndex == indiceNota)
                KeyboardUp();
        });
        trigger.triggers.Add(up);
    }

    // Octava del teclado en la que cae la f0 del preset. Se usa la parte ENTERA
    // del logaritmo, no el redondeo (A4 = 440 Hz da 4.75 y debe quedar en 4).
    // El epsilon evita que un C exacto, redondeado a dos decimales, caiga en la
    // octava de abajo.
    int OctaveFromFrequency(float frequencyHz)
    {
        if (frequencyHz <= 0f)
            return octave;

        float octavesOverC0 = Mathf.Log(frequencyHz / 16.3516f, 2f);
        return Mathf.Clamp(Mathf.FloorToInt(octavesOverC0 + 0.01f), 0, 8);
    }

    public float GetFrequencyFromOctave0(string note, int selectedOctave)
    {
        if (note == "C") return 16.3516f * Mathf.Pow(2f, selectedOctave);
        if (note == "C#") return 17.3239f * Mathf.Pow(2f, selectedOctave);
        if (note == "D") return 18.3540f * Mathf.Pow(2f, selectedOctave);
        if (note == "D#") return 19.4454f * Mathf.Pow(2f, selectedOctave);
        if (note == "E") return 20.6017f * Mathf.Pow(2f, selectedOctave);
        if (note == "F") return 21.8268f * Mathf.Pow(2f, selectedOctave);
        if (note == "F#") return 23.1246f * Mathf.Pow(2f, selectedOctave);
        if (note == "G") return 24.4997f * Mathf.Pow(2f, selectedOctave);
        if (note == "G#") return 25.9565f * Mathf.Pow(2f, selectedOctave);
        if (note == "A") return 27.5000f * Mathf.Pow(2f, selectedOctave);
        if (note == "A#") return 29.1353f * Mathf.Pow(2f, selectedOctave);
        return 30.8677f * Mathf.Pow(2f, selectedOctave);
    }
}
