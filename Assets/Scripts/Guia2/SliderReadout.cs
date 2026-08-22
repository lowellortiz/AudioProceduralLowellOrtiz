//Codigo escrito por: Lowell Ortiz Mercado
using UnityEngine;
using UnityEngine.UI;

// Escribe el valor de un Slider en un Text. Uno por fila, todo configurable
// desde el Inspector: no hay que tocar codigo para cambiar un formato.
//
// Sondea en LateUpdate en vez de escuchar onValueChanged porque los presets
// empujan los valores con SetValueWithoutNotify, que NO dispara ese evento.
// Sondear atrapa todos los origenes de cambio (arrastre, preset, boton de
// onda) y deja el componente sin ningun cableado que se pueda romper.
[RequireComponent(typeof(Text))]
public class SliderReadout : MonoBehaviour
{
    public Slider slider;
    public Text label;

    [Tooltip("Formato de C#: '0.00' para niveles, '0' para tiempos y enteros.")]
    public string formato = "0.00";

    [Tooltip("Se pega despues del numero, por ejemplo ' ms'.")]
    public string sufijo = "";

    [Tooltip("Opcional. Si tiene elementos, el slider guarda un INDICE y aqui se " +
             "muestra el valor real (lo usa el tamano de tabla: 0..4 -> 128..2048).")]
    public int[] mapa;

    // NaN a proposito: NaN nunca es igual a nada, ni siquiera a si mismo, asi
    // que la primera comparacion siempre falla y la etiqueta se escribe.
    private float ultimoValor = float.NaN;

    void Reset()
    {
        label = GetComponent<Text>();
        slider = GetComponentInParent<Slider>();
    }

    void OnEnable()
    {
        ultimoValor = float.NaN;
    }

    void LateUpdate()
    {
        if (slider == null || label == null)
            return;

        float valor = slider.value;
        if (valor == ultimoValor)
            return;

        ultimoValor = valor;

        if (mapa != null && mapa.Length > 0)
        {
            int indice = Mathf.Clamp(Mathf.RoundToInt(valor), 0, mapa.Length - 1);
            label.text = mapa[indice].ToString(formato) + sufijo;
            return;
        }

        label.text = valor.ToString(formato) + sufijo;
    }
}
