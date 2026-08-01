using UnityEngine;
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
            noteButtons[i].onClick.AddListener(() => PlayNote(noteNames[index]));
        }

        if (waveButtonSine)
            waveButtonSine.onClick.AddListener(() => oscillator.waveform = AdditiveWaveformType.Sine);

        if (waveButtonSquare)
            waveButtonSquare.onClick.AddListener(() => ToggleWaveform(AdditiveWaveformType.Square));

        if (waveButtonTriangle)
            waveButtonTriangle.onClick.AddListener(() => ToggleWaveform(AdditiveWaveformType.Triangle));

        if (waveButtonSawtooth)
            waveButtonSawtooth.onClick.AddListener(() => ToggleWaveform(AdditiveWaveformType.Sawtooth));
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

    void Update()
    {
        if (Keyboard.current == null)
            return;

        int count = Mathf.Min(noteKeys.Length, noteNames.Length);
        for (int i = 0; i < count; i++)
        {
            if (Keyboard.current[noteKeys[i]].wasPressedThisFrame)
                PlayNote(noteNames[i]);
        }
    }

    void PlayNote(string note)
    {
        oscillator.frequency = GetFrequencyFromOctave0(note, octave);
        oscillator.isPlaying = true;
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
