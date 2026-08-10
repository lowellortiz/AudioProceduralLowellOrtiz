//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;

public enum AdsrStage { Idle, Attack, Decay, Sustain, Release }

// Envolvente ADSR independiente del oscilador: se puede reutilizar con
// distintas voces/generadores sin duplicar la maquina de estados.
[System.Serializable]
public class AdsrEnvelope
{
    public float attack = 0.01f;
    public float decay = 0.15f;
    public float sustain = 0.70f;
    public float release = 0.25f;

    public AdsrStage Stage { get; private set; } = AdsrStage.Idle;

    private float value = 0f;
    private float releaseStartValue = 0f;

    public void NoteOn()
    {
        Debug.Log($"[ADSR] NoteOn() -> stage {Stage} pasa a Attack");
        Stage = AdsrStage.Attack;
    }

    public void NoteOff()
    {
        Debug.Log($"[ADSR] NoteOff() -> stage {Stage} pasa a Release (value={value:F3})");
        releaseStartValue = value;
        Stage = AdsrStage.Release;
    }

    // Calcula y devuelve el valor instantaneo de la envolvente (0..1).
    public float Process(float dt)
    {
        AdsrStage previousStage = Stage;

        switch (Stage)
        {
            case AdsrStage.Attack:
                value += dt / Mathf.Max(attack, 0.0001f);
                if (value >= 1f) { value = 1f; Stage = AdsrStage.Decay; }
                break;

            case AdsrStage.Decay:
                value -= dt * (1f - sustain) / Mathf.Max(decay, 0.0001f);
                if (value <= sustain) { value = sustain; Stage = AdsrStage.Sustain; }
                break;

            case AdsrStage.Sustain:
                value = sustain;
                break;

            case AdsrStage.Release:
                value -= dt * releaseStartValue / Mathf.Max(release, 0.0001f);
                if (value <= 0f) { value = 0f; Stage = AdsrStage.Idle; }
                break;

            default:
                value = 0f;
                break;
        }

        // DEBUG TEMPORAL: se llama muestra a muestra, pero solo loguea en
        // los pocos instantes donde la etapa realmente cambia.
        if (Stage != previousStage)
            Debug.Log($"[ADSR] {previousStage} -> {Stage} (value={value:F3})");

        return value;
    }
}
