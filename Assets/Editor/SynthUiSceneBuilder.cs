//Codigo escrito por: Lowell Ortiz Mercado
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Genera la interfaz del sintetizador como GameObjects REALES dentro de la
// escena, desde el menu del editor. No es un builder de runtime: corre una
// sola vez, los objetos quedan guardados en el .unity, y a partir de ahi se
// editan a mano en el Inspector como cualquier otro Canvas.
//
// Se apoya en DefaultControls (la misma API publica que usa el menu
// GameObject > UI > Slider), asi los controles salen con la jerarquia
// estandar de Unity y no con una inventada.
public static class SynthUiSceneBuilder
{
    // ---------------------------------------------------------------- paleta
    static readonly Color Fondo = new Color(0f, 0f, 0f, 1f);
    static readonly Color Etiqueta = Color.white;
    static readonly Color Valor = new Color(0.961f, 0.910f, 0f, 1f);       // amarillo
    static readonly Color Titulo = new Color(0.961f, 0.910f, 0f, 1f);
    static readonly Color Relleno = new Color(0.133f, 0.867f, 0.867f, 1f); // cian
    static readonly Color Surco = new Color(0.141f, 0.141f, 0.141f, 1f);
    static readonly Color Manija = Color.white;
    static readonly Color BotonFondo = new Color(0.10f, 0.10f, 0.10f, 1f);
    static readonly Color TeclaBlanca = new Color(0.949f, 0.949f, 0.933f, 1f);
    static readonly Color TeclaNegra = new Color(0.039f, 0.039f, 0.039f, 1f);

    const int FuenteEtiqueta = 30;
    const int FuenteTitulo = 26;
    const int FuenteBoton = 24;
    const int FuenteNota = 26;

    static readonly Vector2 Resolucion = new Vector2(1920f, 1080f);

    static Font fuente;
    static DefaultControls.Resources recursos;

    [MenuItem("Audio Procedural/Reconstruir UI de Guia2")]
    public static void Reconstruir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (!escena.IsValid())
        {
            EditorUtility.DisplayDialog("UI Guia2", "Abre primero la escena Guia2.", "Ok");
            return;
        }

        AdditiveKeyboardController controller = null;
        GameObject canvasViejo = null;

        foreach (var raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == "Canvas" || raiz.name == "Canvas_Synth")
                canvasViejo = raiz;

            var c = raiz.GetComponentInChildren<AdditiveKeyboardController>(true);
            if (c != null) controller = c;
        }

        if (controller == null)
        {
            EditorUtility.DisplayDialog("UI Guia2",
                "No encontre un AdditiveKeyboardController en la escena abierta.", "Ok");
            return;
        }

        if (canvasViejo != null)
            Object.DestroyImmediate(canvasViejo);

        // El viejo SynthUiBuilder ya no existe como clase, asi que en la escena
        // quedo como "missing script" y Unity avisa en cada carga.
        int huerfanos = 0;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                huerfanos += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        if (huerfanos > 0)
            Debug.Log("[UI Guia2] Quitados " + huerfanos + " componentes con script faltante.");

        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        recursos = new DefaultControls.Resources
        {
            standard = Builtin("UI/Skin/UISprite.psd"),
            background = Builtin("UI/Skin/Background.psd"),
            inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
            knob = Builtin("UI/Skin/Knob.psd"),
            checkmark = Builtin("UI/Skin/Checkmark.psd"),
            dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
            mask = Builtin("UI/Skin/UIMask.psd"),
        };

        Undo.RecordObject(controller, "Reconstruir UI");

        AsignarWavetables(controller.oscillator);

        var canvas = CrearCanvas();
        ConstruirBarraSuperior(canvas, controller);
        ConstruirSeccionOnda(canvas, controller);
        ConstruirPiano(canvas, controller);
        ConstruirArmonicos(canvas, controller);
        ConstruirAdsr(canvas, controller);

        controller.activeColor = Relleno;
        controller.waveButtonColor = BotonFondo;
        controller.waveButtonTextColor = Color.white;

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log("[UI Guia2] Canvas reconstruido en la escena. Guarda con Ctrl+S.", canvas);
    }

    static Sprite Builtin(string ruta) => AssetDatabase.GetBuiltinExtraResource<Sprite>(ruta);

    // Guia 4. Carga los .txt de Guia4/Wavetables en el oscilador para no tener
    // que arrastrar siete archivos a mano cada vez. El orden es el del array de
    // abajo, no el alfabetico: las cuatro ondas basicas primero y las tres
    // personalizadas al final, que es como se leen en el menu.
    static readonly string[] ArchivosWavetable =
    {
        "wavetable_sine_1024",
        "wavetable_square_1024",
        "wavetable_triangle_1024",
        "wavetable_sawtooth_1024",
        "wavetable_custom_func1_1024",
        "wavetable_custom_func2_1024",
        "wavetable_custom_func3_1024",
    };

    const string CarpetaWavetables = "Assets/Scripts/Guia2/Guia3/Guia4/Wavetables";

    static void AsignarWavetables(SimpleAdditiveOscillator osc)
    {
        if (osc == null)
            return;

        var tablas = new TextAsset[ArchivosWavetable.Length];
        int encontradas = 0;

        for (int i = 0; i < ArchivosWavetable.Length; i++)
        {
            string ruta = CarpetaWavetables + "/" + ArchivosWavetable[i] + ".txt";
            tablas[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(ruta);

            if (tablas[i] != null)
                encontradas++;
            else
                Debug.LogWarning("[Wavetable] No encontre " + ruta);
        }

        if (encontradas == 0)
        {
            Debug.LogWarning("[Wavetable] Ninguna tabla en " + CarpetaWavetables +
                             ": el menu de wavetable quedara solo con 'Calculada'.");
            return;
        }

        Undo.RecordObject(osc, "Asignar wavetables");
        osc.wavetableFiles = tablas;
        EditorUtility.SetDirty(osc);

        Debug.Log("[Wavetable] " + encontradas + " tablas asignadas al oscilador.", osc);
    }

    // =====================================================================
    //  BLOQUES
    // =====================================================================
    static RectTransform CrearCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Canvas");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Resolucion;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var rect = go.GetComponent<RectTransform>();

        var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        fondo.transform.SetParent(rect, false);
        var img = fondo.GetComponent<Image>();
        img.color = Fondo;
        img.raycastTarget = false;
        Estirar(fondo.GetComponent<RectTransform>());

        return rect;
    }

    // Volumen, octava e instrumento.
    static void ConstruirBarraSuperior(RectTransform canvas, AdditiveKeyboardController c)
    {
        var bloque = Bloque(canvas, "BarraSuperior", anclaY: 1f, alto: 150f, margenY: 20f);
        var columna = Columna(bloque, espacio: 6f);

        var osc = c.oscillator;

        c.amplitudeSlider = FilaSlider(columna, "Amplitud", 0f, 1f, false, "0.000", "",
            osc != null ? osc.amplitude : 0.25f).slider;
        c.octaveSlider = FilaSlider(columna, "Octava", 0f, 8f, true, "0", "", c.octave).slider;

        // Fila del menu de instrumentos.
        var fila = Fila(columna, "Fila_Instrumento", alto: 44f);
        Texto(fila, "Instrumento", FuenteEtiqueta, TextAnchor.MiddleLeft, Etiqueta, ancho: 260f);

        var dropdownGo = DefaultControls.CreateDropdown(recursos);
        dropdownGo.name = "Dropdown_Instrumento";
        dropdownGo.transform.SetParent(fila, false);
        EstilizarDropdown(dropdownGo);
        Flexible(dropdownGo, ancho: 1f);
        c.presetDropdown = dropdownGo.GetComponent<Dropdown>();

        // Los botones quedan sin usar: ahora el instrumento se elige del menu.
        c.instrumentButtons = new Button[0];
    }

    // 4 botones de onda + indicador SE/CU/TR/SA + cantidad de armonicos
    // + los controles de wavetable de la Guia 4.
    static void ConstruirSeccionOnda(RectTransform canvas, AdditiveKeyboardController c)
    {
        // 272 = 56 (botones de onda) + 40 (indicador) + 44 (armonicos)
        // + 44 (wavetable) + 44 (tamano de tabla) + 4 espacios de 8 + holgura.
        // Si se queda corto, la ultima fila se sale del bloque.
        var bloque = Bloque(canvas, "SeccionOnda", anclaY: 1f, alto: 272f, margenY: 178f);
        var columna = Columna(bloque, espacio: 8f);

        // Selector de 5 posiciones: las 4 formulas directas y la suma aditiva.
        var filaBotones = Fila(columna, "Fila_Botones", alto: 56f);
        c.waveButtonSine = BotonOnda(filaBotones, "Seno");
        c.waveButtonSquare = BotonOnda(filaBotones, "Cuadrada");
        c.waveButtonTriangle = BotonOnda(filaBotones, "Triangular");
        c.waveButtonSawtooth = BotonOnda(filaBotones, "Sierra");
        c.waveButtonAdditive = BotonOnda(filaBotones, "Aditiva");

        var filaModo = Fila(columna, "Fila_Onda", alto: 40f);
        Texto(filaModo, "Onda", FuenteEtiqueta, TextAnchor.MiddleLeft, Etiqueta, ancho: 260f);

        var hueco = Texto(filaModo, "", FuenteEtiqueta, TextAnchor.MiddleLeft, Etiqueta, ancho: 0f);
        Flexible(hueco.gameObject, ancho: 1f);

        c.waveModeLabel = Texto(filaModo, "--", FuenteEtiqueta, TextAnchor.MiddleRight, Valor, ancho: 150f);

        c.harmonicSlider = FilaSlider(columna, "Armonicos", 1f, 10f, true, "0", "",
            c.oscillator != null ? c.oscillator.harmonicCount : 10f).slider;

        ConstruirControlesWavetable(columna, c);
    }

    // Guia 4. Dos toggles y el menu de tabla caben en una sola fila; el tamano
    // necesita su propia fila porque es un slider con lectura numerica.
    static void ConstruirControlesWavetable(Transform columna, AdditiveKeyboardController c)
    {
        var fila = Fila(columna, "Fila_Wavetable", alto: 44f);

        c.wavetableToggleButton = BotonOnda(fila, "Wavetable: OFF");
        c.interpolateToggleButton = BotonOnda(fila, "Interp.: ON");

        var dropdownGo = DefaultControls.CreateDropdown(recursos);
        dropdownGo.name = "Dropdown_Wavetable";
        dropdownGo.transform.SetParent(fila, false);
        EstilizarDropdown(dropdownGo);
        // Peso 2 contra 1 de cada boton: los nombres de archivo son largos.
        Flexible(dropdownGo, ancho: 2f);
        c.wavetableSourceDropdown = dropdownGo.GetComponent<Dropdown>();

        // El slider guarda el INDICE (0..4) y el mapa traduce a 128..2048, asi
        // que la lectura muestra el tamano real y no el numero del indice.
        int tamanoActual = c.oscillator != null ? c.oscillator.wavetableSize : 1024;
        int indice = 0;
        for (int i = 0; i < AdditiveKeyboardController.TamanosTabla.Length; i++)
            if (AdditiveKeyboardController.TamanosTabla[i] <= tamanoActual)
                indice = i;

        var filaTamano = FilaSlider(columna, "Tam. tabla",
            0f, AdditiveKeyboardController.TamanosTabla.Length - 1f, true, "0", "", indice);

        c.tableSizeSlider = filaTamano.slider;

        var lectura = filaTamano.slider.transform.parent.GetComponentInChildren<SliderReadout>(true);
        if (lectura != null)
        {
            lectura.mapa = AdditiveKeyboardController.TamanosTabla;
            lectura.label.text = AdditiveKeyboardController.TamanosTabla[indice].ToString();
        }
    }

    // Teclado real: 7 blancas y 5 negras mas cortas y angostas encima.
    static void ConstruirPiano(RectTransform canvas, AdditiveKeyboardController c)
    {
        // Bajado 104 px y 50 mas bajo que antes: la seccion de onda crecio con
        // los controles de wavetable y el piano tenia que dejarles sitio sin
        // meterse encima de los paneles de abajo.
        var bloque = Bloque(canvas, "Piano", anclaY: 1f, alto: 200f, margenY: 472f);

        int[] blancas = { 0, 2, 4, 5, 7, 9, 11 };
        int[] negras = { 1, 3, 6, 8, 10 };
        int[] negraTrasBlanca = { 0, 1, 3, 4, 5 };
        string[] fisicas = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "Q", "W" };

        const float anchoBlanca = 1f / 7f;
        const float anchoNegra = 0.60f;
        const float altoNegra = 0.62f;

        var botones = new Button[12];

        for (int i = 0; i < blancas.Length; i++)
        {
            int n = blancas[i];
            var boton = Tecla(bloque, c.noteNames[n], fisicas[n], TeclaBlanca, Color.black);

            var r = boton.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(i * anchoBlanca, 0f);
            r.anchorMax = new Vector2((i + 1) * anchoBlanca, 1f);
            r.offsetMin = new Vector2(2f, 0f);
            r.offsetMax = new Vector2(-2f, 0f);

            botones[n] = boton;
        }

        // Despues de las blancas a proposito: hermanas posteriores => se dibujan
        // encima y el raycast las toma primero.
        for (int i = 0; i < negras.Length; i++)
        {
            int n = negras[i];
            var boton = Tecla(bloque, c.noteNames[n], fisicas[n], TeclaNegra, Color.white);

            float centro = (negraTrasBlanca[i] + 1) * anchoBlanca;
            float mitad = anchoBlanca * anchoNegra * 0.5f;

            var r = boton.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(centro - mitad, 1f - altoNegra);
            r.anchorMax = new Vector2(centro + mitad, 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            botones[n] = boton;
        }

        // En orden CROMATICO (C, C#, D, ...), no visual: el controlador indexa
        // este array contra noteNames.
        c.noteButtons = botones;
    }

    // H1..H10 en dos columnas de cinco, como el original.
    static void ConstruirArmonicos(RectTransform canvas, AdditiveKeyboardController c)
    {
        var bloque = Bloque(canvas, "PanelArmonicos", anclaY: 0f, alto: 400f, margenY: 20f);
        var r = bloque.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0.5f, 0f);
        r.offsetMin = new Vector2(20f, 20f);
        // -45 y no -10: el valor de H6..H10 quedaba pegado a las etiquetas del
        // ADSR y se leia como una sola linea ("-0,17 Attack").
        // El techo baja de 420 a 400 para no chocar con el piano, que ahora
        // termina 104 px mas abajo.
        r.offsetMax = new Vector2(-45f, 400f);

        var columna = Columna(bloque, espacio: 6f);
        Texto(columna, "ARMONICOS", FuenteTitulo, TextAnchor.MiddleLeft, Titulo, alto: 34f, negrita: true);

        var dosColumnas = Fila(columna, "Columnas", alto: 0f);
        Flexible(dosColumnas.gameObject, alto: 1f);
        // Sin esto el valor de H1..H5 queda pegado a la etiqueta de H6..H10 y
        // se lee como si fueran una sola fila ("1,00 H6").
        dosColumnas.GetComponent<HorizontalLayoutGroup>().spacing = 40f;

        var izq = Columna(SubColumna(dosColumnas, "Col_H1_H5"), espacio: 4f);
        var der = Columna(SubColumna(dosColumnas, "Col_H6_H10"), espacio: 4f);

        c.amplitudeSliders = new Slider[10];
        c.harmonicRows = new CanvasGroup[10];

        var niveles = c.oscillator != null ? c.oscillator.harmonicLevels : null;

        for (int i = 0; i < 10; i++)
        {
            // Rango 0..1 y tres decimales. Es honesto porque LoadPreset ya solo
            // escribe pesos positivos (ver el comentario en SimpleAdditiveOscillator).
            float inicial = (niveles != null && i < niveles.Length) ? niveles[i] : 0f;

            var fila = FilaSlider(i < 5 ? izq : der, "H" + (i + 1), 0f, 1f, false, "0.000", "",
                inicial, anchoEtiqueta: 70f, anchoValor: 130f);

            c.amplitudeSliders[i] = fila.slider;
            c.harmonicRows[i] = fila.grupo;
        }
    }

    static void ConstruirAdsr(RectTransform canvas, AdditiveKeyboardController c)
    {
        var bloque = Bloque(canvas, "PanelAdsr", anclaY: 0f, alto: 400f, margenY: 20f);
        var r = bloque.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.offsetMin = new Vector2(45f, 20f);
        // 400 y no 420: misma razon que en el panel de armonicos.
        r.offsetMax = new Vector2(-20f, 400f);

        var columna = Columna(bloque, espacio: 6f);
        Texto(columna, "ENVOLVENTE", FuenteTitulo, TextAnchor.MiddleLeft, Titulo, alto: 34f, negrita: true);

        var env = c.oscillator != null ? c.oscillator.envelope : new AdsrEnvelope();

        // Ojo con las unidades: A, D, S y R son TIEMPOS en ms; S Level es 0..1.
        c.attackSlider = FilaSlider(columna, "Attack", 0f, 2000f, false, "0", " ms", env.A, 210f, 150f).slider;
        c.decaySlider = FilaSlider(columna, "Decay", 0f, 2000f, false, "0", " ms", env.D, 210f, 150f).slider;
        c.sustainSlider = FilaSlider(columna, "S Level", 0f, 1f, false, "0.00", "", env.SL, 210f, 150f).slider;
        c.sustainTimeSlider = FilaSlider(columna, "Sustain", 0f, 5000f, false, "0", " ms", env.S, 210f, 150f).slider;
        c.releaseSlider = FilaSlider(columna, "Release", 0f, 3000f, false, "0", " ms", env.R, 210f, 150f).slider;
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================
    struct FilaResultado
    {
        public Slider slider;
        public CanvasGroup grupo;
    }

    static FilaResultado FilaSlider(Transform padre, string etiqueta, float min, float max,
        bool enteros, string formato, string sufijo, float valorInicial,
        float anchoEtiqueta = 260f, float anchoValor = 170f)
    {
        var fila = Fila(padre, "Fila_" + etiqueta, alto: 44f);
        var grupo = fila.gameObject.AddComponent<CanvasGroup>();

        Texto(fila, etiqueta, FuenteEtiqueta, TextAnchor.MiddleLeft, Etiqueta, ancho: anchoEtiqueta);

        var sliderGo = DefaultControls.CreateSlider(recursos);
        sliderGo.name = "Slider_" + etiqueta;
        sliderGo.transform.SetParent(fila, false);
        var slider = EstilizarSlider(sliderGo, min, max, enteros, valorInicial);
        Flexible(sliderGo, ancho: 1f);
        Altura(sliderGo, 28f);

        // El texto se escribe ya aqui, no solo en Play: asi la escena se ve
        // coherente al abrirla en el editor, que es justo lo que se pidio.
        var valor = Texto(fila, slider.value.ToString(formato) + sufijo,
            FuenteEtiqueta, TextAnchor.MiddleRight, Valor, ancho: anchoValor);

        var lectura = valor.gameObject.AddComponent<SliderReadout>();
        lectura.slider = slider;
        lectura.label = valor;
        lectura.formato = formato;
        lectura.sufijo = sufijo;

        return new FilaResultado { slider = slider, grupo = grupo };
    }

    static Slider EstilizarSlider(GameObject go, float min, float max, bool enteros, float valorInicial)
    {
        var slider = go.GetComponent<Slider>();

        var fondo = go.transform.Find("Background").GetComponent<Image>();
        fondo.color = Surco;

        var fill = go.transform.Find("Fill Area/Fill").GetComponent<Image>();
        fill.color = Relleno;

        var handle = go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
        handle.color = Manija;
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 30f);

        // wholeNumbers ANTES de min/max para que el primer valor no se redondee
        // contra un rango que todavia no existe.
        slider.wholeNumbers = enteros;
        slider.minValue = min;
        slider.maxValue = max;

        // El valor sale del MODELO, no del minimo. Con el slider de Armonicos
        // esto no es cosmetico: el controlador lo LEE en su Start
        // (HarmonicChange(harmonicSlider.value)), asi que dejarlo en 1 bajaba
        // harmonicCount a 1 al arrancar la escena.
        slider.SetValueWithoutNotify(Mathf.Clamp(valorInicial, min, max));

        return slider;
    }

    static void EstilizarDropdown(GameObject go)
    {
        var dropdown = go.GetComponent<Dropdown>();

        // Blanco: el Selectable multiplica Image.color por el color del estado.
        go.GetComponent<Image>().color = Color.white;
        dropdown.colors = Colores(BotonFondo);

        var label = go.transform.Find("Label").GetComponent<Text>();
        label.font = fuente;
        label.fontSize = FuenteEtiqueta;
        label.color = Valor;
        // DefaultControls deja el Label en Truncate: con fuente 30 en una caja
        // de 31 px la linea no cabe y el texto NO se dibuja (queda invisible).
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var plantilla = go.transform.Find("Template");
        plantilla.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 1f);
        plantilla.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 260f);

        var item = plantilla.Find("Viewport/Content/Item");
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
        item.GetComponent<Toggle>().colors = Colores(new Color(0.16f, 0.16f, 0.16f, 1f));

        var itemLabel = item.Find("Item Label").GetComponent<Text>();
        itemLabel.font = fuente;
        itemLabel.fontSize = FuenteBoton;
        itemLabel.color = Color.white;
        itemLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        itemLabel.verticalOverflow = VerticalWrapMode.Overflow;

        // Blanco por la misma razon que arriba: el Toggle lo multiplica.
        item.Find("Item Background").GetComponent<Image>().color = Color.white;
        item.Find("Item Checkmark").GetComponent<Image>().color = Relleno;

        var arrow = go.transform.Find("Arrow").GetComponent<Image>();
        arrow.color = Relleno;
    }

    static Button BotonOnda(Transform padre, string texto)
    {
        var go = DefaultControls.CreateButton(recursos);
        go.name = "Btn_" + texto;
        go.transform.SetParent(padre, false);

        // Blanco en la Image: el Selectable MULTIPLICA Image.color por el color
        // del estado, asi que pintar aqui el color final lo elevaria al cuadrado.
        go.GetComponent<Image>().color = Color.white;

        var boton = go.GetComponent<Button>();
        boton.colors = Colores(BotonFondo);

        var label = go.transform.Find("Text (Legacy)") ?? go.transform.Find("Text");
        var t = label.GetComponent<Text>();
        t.font = fuente;
        t.text = texto;
        t.fontSize = FuenteBoton;
        t.color = Color.white;

        Flexible(go, ancho: 1f, alto: 1f);
        return boton;
    }

    static Button Tecla(Transform padre, string nota, string fisica, Color fondo, Color colorTexto)
    {
        var go = DefaultControls.CreateButton(recursos);
        go.name = "Key_" + nota;
        go.transform.SetParent(padre, false);

        go.GetComponent<Image>().color = Color.white;

        var boton = go.GetComponent<Button>();
        boton.colors = Colores(fondo);

        var label = go.transform.Find("Text (Legacy)") ?? go.transform.Find("Text");
        var t = label.GetComponent<Text>();
        t.font = fuente;
        t.text = nota + "\n[" + fisica + "]";
        t.fontSize = FuenteNota;
        t.fontStyle = FontStyle.Bold;
        t.color = colorTexto;
        t.alignment = TextAnchor.LowerCenter;
        t.raycastTarget = false;

        var lr = label.GetComponent<RectTransform>();
        lr.offsetMin = new Vector2(0f, 12f);
        lr.offsetMax = new Vector2(0f, -12f);

        return boton;
    }

    static ColorBlock Colores(Color baseColor)
    {
        var c = ColorBlock.defaultColorBlock;
        c.normalColor = baseColor;
        c.highlightedColor = Color.Lerp(baseColor, Color.white, 0.18f);
        c.pressedColor = Color.Lerp(baseColor, Color.black, 0.25f);
        c.selectedColor = baseColor;
        // El gris claro por defecto de Unity se ve MAS brillante que el estado
        // normal sobre fondo negro, o sea al reves de lo que debe leerse.
        c.disabledColor = Color.Lerp(baseColor, Color.black, 0.55f);
        c.fadeDuration = 0.05f;
        return c;
    }

    // Bloque grande anclado a mano: se puede arrastrar entero en el editor sin
    // que ningun layout group lo vuelva a colocar.
    static Transform Bloque(RectTransform canvas, string nombre, float anclaY, float alto, float margenY)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(canvas, false);

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, anclaY);
        r.anchorMax = new Vector2(1f, anclaY);
        r.pivot = new Vector2(0.5f, anclaY);
        r.offsetMin = new Vector2(20f, anclaY > 0.5f ? -margenY - alto : margenY);
        r.offsetMax = new Vector2(-20f, anclaY > 0.5f ? -margenY : margenY + alto);

        return go.transform;
    }

    static Transform Columna(Transform padre, float espacio)
    {
        var grupo = padre.gameObject.GetComponent<VerticalLayoutGroup>();
        if (grupo == null) grupo = padre.gameObject.AddComponent<VerticalLayoutGroup>();

        grupo.spacing = espacio;
        grupo.childControlWidth = true;
        grupo.childControlHeight = true;
        grupo.childForceExpandWidth = true;
        grupo.childForceExpandHeight = false;
        grupo.childAlignment = TextAnchor.UpperLeft;

        return padre;
    }

    static Transform SubColumna(Transform padre, string nombre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Flexible(go, ancho: 1f, alto: 1f);
        return go.transform;
    }

    static Transform Fila(Transform padre, string nombre, float alto)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        var grupo = go.AddComponent<HorizontalLayoutGroup>();
        grupo.spacing = 12f;
        grupo.childControlWidth = true;
        grupo.childControlHeight = true;
        // En el eje del grupo NO se fuerza: childForceExpandWidth repartiria el
        // sobrante entre todos e ignoraria los anchos fijos de las etiquetas.
        grupo.childForceExpandWidth = false;
        grupo.childForceExpandHeight = true;
        grupo.childAlignment = TextAnchor.MiddleLeft;

        if (alto > 0f) Altura(go, alto);
        return go.transform;
    }

    static Text Texto(Transform padre, string contenido, int tamano, TextAnchor alineacion,
        Color color, float ancho = -1f, float alto = -1f, bool negrita = false)
    {
        var go = new GameObject("Txt_" + (string.IsNullOrEmpty(contenido) ? "Hueco" : contenido),
            typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);

        var t = go.GetComponent<Text>();
        t.font = fuente;
        t.text = contenido;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        if (ancho >= 0f) Ancho(go, ancho);
        if (alto >= 0f) Altura(go, alto);

        return t;
    }

    static LayoutElement Elemento(GameObject go)
    {
        var e = go.GetComponent<LayoutElement>();
        return e != null ? e : go.AddComponent<LayoutElement>();
    }

    static void Ancho(GameObject go, float v)
    {
        var e = Elemento(go);
        e.minWidth = v; e.preferredWidth = v; e.flexibleWidth = 0f;
    }

    static void Altura(GameObject go, float v)
    {
        var e = Elemento(go);
        e.minHeight = v; e.preferredHeight = v; e.flexibleHeight = 0f;
    }

    static void Flexible(GameObject go, float ancho = -1f, float alto = -1f)
    {
        var e = Elemento(go);
        if (ancho >= 0f) { e.minWidth = 0f; e.preferredWidth = 0f; e.flexibleWidth = ancho; }
        if (alto >= 0f) e.flexibleHeight = alto;
    }

    static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
