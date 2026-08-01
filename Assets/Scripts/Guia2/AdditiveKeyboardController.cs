using UnityEngine;
using UnityEngine.UI;

// Clase controladora (front): traduce interacción del usuario (teclado de una
// octava) en cambios sobre el oscilador. No calcula ninguna muestra de audio.
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

    void Start()
    {
        for (int i = 0; i < noteButtons.Length; i++)
        {
            int index = i;
            noteButtons[i].onClick.AddListener(() => PlayNote(noteNames[index]));
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
