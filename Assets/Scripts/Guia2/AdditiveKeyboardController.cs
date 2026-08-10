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

    // Botones de forma de onda (arriba del piano, como en la Guia 1).
    public Button waveButtonSine;
    public Button waveButtonSquare;
    public Button waveButtonTriangle;
    public Button waveButtonSawtooth;
    public int presetHarmonicCount = 8;

    // Slider "Armónicos" (Min 1, Max 10, Whole Numbers) creado en el Canvas.
    public Slider harmonicSlider;

    // 10 sliders (Min 0, Max 1) en el Canvas: amplitudeSliders[i] controla
    // harmonicLevels[i], la amplitud del armónico i+1.
    public Slider[] amplitudeSliders = new Slider[10];

    // Controles de la envolvente. Ojo con las unidades: attack, decay y release
    // son TIEMPOS en segundos (cuanto tarda la rampa), mientras que sustain es
    // un NIVEL 0..1 (a que amplitud se queda mientras se sostiene la tecla).
    [Header("ADSR")]
    public Slider attackSlider;
    public Slider decaySlider;
    public Slider sustainSlider;
    public Slider releaseSlider;

    // Recuerda que forma quedo cargada en modo aditivo, para saber si un
    // segundo clic en el mismo boton debe pasar a la version directa.
    private AdditiveWaveformType lastPresetShape = AdditiveWaveformType.Square;

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

        if (waveButtonSine)
            waveButtonSine.onClick.AddListener(() => oscillator.waveform = AdditiveWaveformType.Sine);

        if (waveButtonSquare)
            waveButtonSquare.onClick.AddListener(() => ToggleWaveform(AdditiveWaveformType.Square));

        if (waveButtonTriangle)
            waveButtonTriangle.onClick.AddListener(() => ToggleWaveform(AdditiveWaveformType.Triangle));

        if (waveButtonSawtooth)
            waveButtonSawtooth.onClick.AddListener(() => ToggleWaveform(AdditiveWaveformType.Sawtooth));

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

        BindEnvelopeSlider(attackSlider, oscillator.envelope.attack, value => oscillator.envelope.attack = value);
        BindEnvelopeSlider(decaySlider, oscillator.envelope.decay, value => oscillator.envelope.decay = value);
        BindEnvelopeSlider(sustainSlider, oscillator.envelope.sustain, value => oscillator.envelope.sustain = value);
        BindEnvelopeSlider(releaseSlider, oscillator.envelope.release, value => oscillator.envelope.release = value);
    }

    // Deja el slider mostrando el valor que ya trae serializado la envolvente y
    // a partir de ahi escribe cada cambio directo sobre ella. Va en el mismo
    // sentido que amplitudeSliders (oscilador -> slider al arrancar), asi la
    // escena sigue siendo la fuente de verdad de los valores iniciales.
    void BindEnvelopeSlider(Slider slider, float currentValue, UnityAction<float> apply)
    {
        if (slider == null)
            return;

        slider.SetValueWithoutNotify(Mathf.Clamp(currentValue, slider.minValue, slider.maxValue));
        slider.onValueChanged.AddListener(apply);
    }

    // Ajusta cuántos armónicos del array harmonicLevels se suman en AdditiveWave.
    void HarmonicChange(float value)
    {
        oscillator.harmonicCount = Mathf.Clamp((int)value, 1, oscillator.harmonicLevels.Length);
    }

    // Ajusta la amplitud individual del armónico index+1.
    void AmplitudeChange(int index, float value)
    {
        oscillator.harmonicLevels[index] = value;
    }

    // Primer clic en una forma: carga su preset de Fourier y suena en modo
    // aditivo (aproximada). Segundo clic sobre la misma forma: pasa a la
    // version directa (formula exacta) para comparar de oido.
    void ToggleWaveform(AdditiveWaveformType shape)
    {
        bool showingAdditivePreset = oscillator.waveform == AdditiveWaveformType.Additive && lastPresetShape == shape;

        if (showingAdditivePreset)
        {
            oscillator.waveform = shape;
        }
        else
        {
            oscillator.LoadPreset(shape, presetHarmonicCount);
            oscillator.waveform = AdditiveWaveformType.Additive;
            lastPresetShape = shape;
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

    // Calcula la frecuencia de la nota indiceNota desde la octava 0, la
    // asigna al oscilador y dispara el ataque de la ADSR (NoteOn).
    public void KeyboardDown(int indiceNota)
    {
        oscillator.frequency = GetFrequencyFromOctave0(noteNames[indiceNota], octave);
        oscillator.NoteOn();
        currentNoteIndex = indiceNota;
    }

    // Inicia la liberacion de la ADSR (NoteOff).
    public void KeyboardUp()
    {
        oscillator.NoteOff();
        currentNoteIndex = -1;
    }

    // Los botones del piano NO usan onClick: ese evento solo se dispara al
    // completar el clic (bajar y soltar), asi que nunca informa el momento de
    // soltar y la nota se quedaba sonando en Sustain para siempre. Con
    // PointerDown/PointerUp el boton se comporta igual que una tecla fisica:
    // suena mientras se mantenga oprimido y libera la ADSR al soltar.
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

        // Solo suelta si la nota que suena es la suya: si mientras se sostiene
        // este boton se dispara otra nota (teclado fisico u otro boton), soltar
        // este ya no debe cortar la que quedo sonando.
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ =>
        {
            if (currentNoteIndex == indiceNota)
                KeyboardUp();
        });
        trigger.triggers.Add(up);
    }

    float GetFrequencyFromOctave0(string note, int selectedOctave)
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
