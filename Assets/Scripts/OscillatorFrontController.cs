using UnityEngine;
using UnityEngine.UI;

// Clase controladora (front): traduce interacción del usuario en cambios sobre el
// oscilador. No calcula ninguna muestra de audio aquí.
public class OscillatorFrontController : MonoBehaviour
{
    public SimpleOscillator oscillator;
    public Button playButtonSine;
    public Button playButtonSquare;
    public Button playButtonTriangle;
    public Button playButtonSawtooth;

    void Start()
    {
        if (playButtonSine)
            playButtonSine.onClick.AddListener(() => Toggle(WaveformType.Sine));

        if (playButtonSquare)
            playButtonSquare.onClick.AddListener(() => Toggle(WaveformType.Square));

        if (playButtonTriangle)
            playButtonTriangle.onClick.AddListener(() => Toggle(WaveformType.Triangle));

        if (playButtonSawtooth)
            playButtonSawtooth.onClick.AddListener(() => Toggle(WaveformType.Sawtooth));
    }

    void Toggle(WaveformType type)
    {
        oscillator.waveform = type;
        oscillator.isPlaying = !oscillator.isPlaying;
    }
}
