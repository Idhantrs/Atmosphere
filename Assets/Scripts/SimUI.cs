using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class SimUI : MonoBehaviour
{

    [Header("Elevation key")]
    public Gradient elevationGradient = MakeElevationGradient();

    static Gradient MakeElevationGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
            new GradientColorKey(new Color(0.20f, 0.35f, 0.85f), 0.0f),   // low: blue
            new GradientColorKey(new Color(0.25f, 0.75f, 0.85f), 0.2f),
            new GradientColorKey(new Color(0.35f, 0.80f, 0.45f), 0.4f),   // green
            new GradientColorKey(new Color(0.95f, 0.90f, 0.40f), 0.6f),   // yellow
            new GradientColorKey(new Color(0.95f, 0.55f, 0.25f), 0.8f),   // orange
            new GradientColorKey(new Color(0.85f, 0.10f, 0.10f), 1.0f),   // high: red
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    PlanetAdapter adapter;
    Planet planetRef;
    Texture2D keyTex;
    Text keyLowText, keyHighText;

    public Vector2 radiusRangeKm = new Vector2(1500f, 12000f);
    public Vector2 massRangeEarths = new Vector2(0.01f, 10f);        // log slider
    public Vector2 temperatureRangeK = new Vector2(80f, 600f);
    public Vector2 atmosphereAmountRange = new Vector2(0.01f, 30f);  // log slider

    public float uiScale = 1.25f;

    public UnityEvent onPlanetChanged;

    public Key togglePanelKey = Key.Tab;
    // Order MUST match the GasType enum in Atmosphere.cs
    static readonly GasType[] AllGases = (GasType[])System.Enum.GetValues(typeof(GasType));

    static string GasLabel(GasType g)
    {
        switch (g)
        {
            case GasType.N2: return "Nitrogen";
            case GasType.O2: return "Oxygen";
            case GasType.Ar: return "Argon";
            case GasType.CO2: return "Carbon dioxide";
            default: return g.ToString();
        }
    }

    class Preset { public string name; public float radiusKm, massEarths, tempK, amount; public Dictionary<GasType, float> gas; }
    static readonly Preset[] Presets =
    {
    new Preset { name = "Earth", radiusKm = 6371f, massEarths = 1f,      tempK = 288f, amount = 1f,      gas = new Dictionary<GasType, float> { { GasType.N2, 78f }, { GasType.O2, 21f }, { GasType.Ar, 1f } } },
    new Preset { name = "Mars",  radiusKm = 3390f, massEarths = 0.107f,  tempK = 210f, amount = 0.0159f, gas = new Dictionary<GasType, float> { { GasType.CO2, 95.3f }, { GasType.N2, 2.7f }, { GasType.Ar, 1.6f } } },
    new Preset { name = "Venus", radiusKm = 6052f, massEarths = 0.815f,  tempK = 737f, amount = 100.4f,  gas = new Dictionary<GasType, float> { { GasType.CO2, 96.5f }, { GasType.N2, 3.5f } } },
    new Preset { name = "Titan", radiusKm = 2575f, massEarths = 0.0225f, tempK = 94f,  amount = 10.5f,   gas = new Dictionary<GasType, float> { { GasType.N2, 100f } } },
};

    // palette: flat charcoal, warm off-white text, one amber accent
    static readonly Color PanelBg = new Color(0.105f, 0.105f, 0.115f, 0.95f);
    static readonly Color Line = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color Ink = new Color(0.92f, 0.91f, 0.88f);
    static readonly Color Muted = new Color(0.60f, 0.60f, 0.58f);
    static readonly Color Amber = new Color(0.90f, 0.62f, 0.26f);
    static readonly Color Good = new Color(0.55f, 0.78f, 0.50f);
    static readonly Color Bad = new Color(0.86f, 0.35f, 0.30f);

    Atmosphere atm; FlightData fd; PlaneController pc; Engine eng; PlaneSetup setup; ViewManager view;
    Font font, mono;
    GameObject leftPanel, flightPanel;
    RectTransform throttleFill;
    Text stallText, viewButtonLabel;
    Slider sRadius, sMass, sTemp, sAmount;
    Slider[] sGas;
    Text[] gasPct;
    Text rGravity, rPressure, rDensity, rScale, rSound, rMolar, rStall;
    Text fSpeed, fMach, fAlt, fVs, fAoa, fG, fRho, fQ, fThrottle, eGravity, eDensity;
    readonly HashSet<Slider> logSliders = new HashSet<Slider>();
    bool built, panelHidden;
    float nextHud;

    // ---------------------------------------------------------------- setup
    void Start()
    {
        atm = Atmosphere.Instance != null ? Atmosphere.Instance : FindAnyObjectByType<Atmosphere>();
        if (atm == null) { Debug.LogError("SimUI: there is no Atmosphere object in the scene."); enabled = false; return; }

        // the plane starts switched off, so include inactive objects in the search
        fd = FindAnyObjectByType<FlightData>(FindObjectsInactive.Include);
        pc = FindAnyObjectByType<PlaneController>(FindObjectsInactive.Include);
        eng = FindAnyObjectByType<Engine>(FindObjectsInactive.Include);
        setup = FindAnyObjectByType<PlaneSetup>(FindObjectsInactive.Include);
        view = FindAnyObjectByType<ViewManager>();

        atm.derivePressureFromColumn = true;
        atm.Recalculate();

        font = LoadFont();
        mono = LoadMono();
        if (mono == null) mono = font;
        EnsureEventSystem();
        BuildUI();
        RefreshReadouts();

        if (view != null) { view.Changed += OnView; OnView(view.Current); }
        else { leftPanel.SetActive(true); flightPanel.SetActive(true); }
    }

    void OnDestroy() { if (view != null) view.Changed -= OnView; }

    Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) f = Resources.Load<Font>("UIFont"); // optional: any .ttf at Assets/Resources/UIFont.ttf
        if (f == null) f = Font.CreateDynamicFontFromOSFont(
            new[] { "Arial", "Helvetica", "Segoe UI", "Liberation Sans", "DejaVu Sans" }, 16);
        if (f == null) Debug.LogError("SimUI: no font could be loaded. Put a .ttf at Assets/Resources/UIFont.ttf");
        return f;
    }

    Font LoadMono()
    {
        try
        {
            return Font.CreateDynamicFontFromOSFont(
            new[] { "Consolas", "Menlo", "Courier New", "DejaVu Sans Mono", "Liberation Mono" }, 14);
        }
        catch { return null; }
    }

    void EnsureEventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Destroy(legacy);
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            var m = es.gameObject.AddComponent<InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("SimUI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f) / uiScale;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        BuildLeftPanel(canvasGO.transform);
        BuildFlightPanel(canvasGO.transform);
        BuildViewButton(canvasGO.transform);

        stallText = AddText(canvasGO.transform, "STALL", 44, Bad, TextAnchor.MiddleCenter, FontStyle.Bold);
        var srt = stallText.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -40f);
        srt.sizeDelta = new Vector2(400f, 70f);
        stallText.enabled = false;

        UpdateGasPercents();
        built = true;
    }

    void BuildLeftPanel(Transform root)
    {
        var p = MakeRect("Planet Panel", root);
        leftPanel = p.gameObject;
        p.anchorMin = new Vector2(0f, 0f); p.anchorMax = new Vector2(0f, 1f);
        p.pivot = new Vector2(0f, 0.5f);
        p.anchoredPosition = Vector2.zero;
        p.sizeDelta = new Vector2(380f, 0f);
        p.gameObject.AddComponent<Image>().color = PanelBg;
        Stack(p);

        var edge = MakeRect("Edge", p);
        edge.anchorMin = new Vector2(1f, 0f); edge.anchorMax = new Vector2(1f, 1f);
        edge.pivot = new Vector2(1f, 0.5f);
        edge.sizeDelta = new Vector2(1f, 0f);
        edge.gameObject.AddComponent<Image>().color = Line;
        edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        AddText(p, "Planet", 22, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddText(p, "Atmosphere editor", 13, Muted);
        BuildKey(p);
        AddSpace(p, 6f);

        for (int i = 0; i < Presets.Length; i += 4)
        {
            var row = AddRow(p, 28f);
            for (int j = i; j < Mathf.Min(i + 4, Presets.Length); j++)
            {
                var local = Presets[j];
                AddButton(row, local.name, false, () => ApplyPreset(local), out Text _);
            }
        }

        AddSection(p, "Planet");
        sRadius = AddSlider(p, "Radius", radiusRangeKm, atm.planetRadiusKm, false, v => v.ToString("F0") + " km", out Text _);
        sMass = AddSlider(p, "Mass", massRangeEarths, atm.planetMassEarths, true, v => v.ToString("F3") + " x", out Text _);

        AddSection(p, "Atmosphere");
        sTemp = AddSlider(p, "Temperature", temperatureRangeK, atm.temperatureK, false, v => v.ToString("F0") + " K", out Text _);
        sAmount = AddSlider(p, "Air amount", atmosphereAmountRange, atm.atmosphereAmount, true, v => v.ToString("F3") + " x", out Text _);

        AddSection(p, "Composition (relative amounts)");
        float[] init = new float[AllGases.Length];
        foreach (var e in atm.gases)
        {
            int idx = System.Array.IndexOf(AllGases, e.gas);
            if (idx >= 0) init[idx] += e.fraction * 100f;
        }
        sGas = new Slider[AllGases.Length];
        gasPct = new Text[AllGases.Length];
        for (int i = 0; i < AllGases.Length; i++)
            sGas[i] = AddSlider(p, GasLabel(AllGases[i]), new Vector2(0f, 100f), init[i], false, v => v.ToString("F0"), out gasPct[i]);

        AddSection(p, "Derived");
        rGravity = AddInfoRow(p, "Surface gravity");
        rPressure = AddInfoRow(p, "Surface pressure");
        rDensity = AddInfoRow(p, "Surface air density");
        rScale = AddInfoRow(p, "Scale height");
        rSound = AddInfoRow(p, "Speed of sound");
        rMolar = AddInfoRow(p, "Mean molar mass");
        rStall = AddInfoRow(p, "Stall speed (est.)");

        AddSpace(p, 6f);
        AddNote(p, "Right-drag to orbit, scroll to zoom. Mass and air amount are relative to Earth. Tab hides this panel.", 12, Muted);
    }

    void BuildFlightPanel(Transform root)
    {
        var p = MakeRect("Flight Panel", root);
        flightPanel = p.gameObject;
        p.anchorMin = p.anchorMax = new Vector2(1f, 1f);
        p.pivot = new Vector2(1f, 1f);
        p.anchoredPosition = new Vector2(-16f, -16f);
        p.sizeDelta = new Vector2(320f, 0f);
        p.gameObject.AddComponent<Image>().color = PanelBg;
        Stack(p);
        p.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddText(p, "Flight", 22, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddSpace(p, 4f);
        fSpeed = AddInfoRow(p, "Airspeed");
        fMach = AddInfoRow(p, "Mach");
        fAlt = AddInfoRow(p, "Altitude");
        fVs = AddInfoRow(p, "Vertical speed");
        fAoa = AddInfoRow(p, "Angle of attack");
        fG = AddInfoRow(p, "Load factor");
        fThrottle = AddInfoRow(p, "Throttle");

        var bar = MakeRect("Throttle Bar", p);
        bar.gameObject.AddComponent<Image>().color = Line;
        bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 4f;
        throttleFill = MakeRect("Fill", bar);
        throttleFill.anchorMin = Vector2.zero; throttleFill.anchorMax = new Vector2(0f, 1f);
        throttleFill.offsetMin = throttleFill.offsetMax = Vector2.zero;
        throttleFill.gameObject.AddComponent<Image>().color = Amber;

        AddSection(p, "Air here");
        fRho = AddInfoRow(p, "Density");
        fQ = AddInfoRow(p, "Dynamic pressure");

        AddSection(p, "Planet");
        eGravity = AddInfoRow(p, "Gravity");
        eDensity = AddInfoRow(p, "Surface density");

        AddSpace(p, 6f);
        AddText(p, "W/S pitch, A/D roll, Left/Right arrows yaw", 12, Muted);
        AddText(p, "Up/Down arrows throttle, Space brake, R reset", 12, Muted);
    }

    void BuildViewButton(Transform root)
    {
        var b = AddButton(root, "Fly here", true, () => { if (view != null) view.Toggle(); }, out viewButtonLabel);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(230f, 40f);
    }

    // ---------------------------------------------------------------- views
    void OnView(ViewManager.View v)
    {
        bool flight = v == ViewManager.View.Flight;
        leftPanel.SetActive(!flight && !panelHidden);
        flightPanel.SetActive(flight);
        string key = view != null ? view.toggleKey.ToString() : "V";
        viewButtonLabel.text = (flight ? "Back to planet" : "Fly here") + "   [" + key + "]";
        if (!flight) { stallText.enabled = false; RefreshReadouts(); RefreshKey(); }
        else UpdateFlightHud();
    }

    // ---------------------------------------------------------------- applying values
    float Real(Slider s) { return logSliders.Contains(s) ? Mathf.Pow(10f, s.value) : s.value; }
    void SetSlider(Slider s, float real) { s.value = logSliders.Contains(s) ? Mathf.Log10(Mathf.Max(real, 1e-6f)) : real; }

    void Apply()
    {
        if (!built) return;
        atm.planetRadiusKm = Real(sRadius);
        atm.planetMassEarths = Real(sMass);
        atm.temperatureK = Real(sTemp);
        atm.atmosphereAmount = Real(sAmount);
        atm.overrideGravity = false;
        atm.derivePressureFromColumn = true;

        atm.gases.Clear();
        for (int i = 0; i < sGas.Length; i++)
            atm.gases.Add(new GasEntry { gas = AllGases[i], fraction = sGas[i].value });

        atm.Recalculate();
        UpdateGasPercents();
        RefreshReadouts();
        onPlanetChanged?.Invoke();
    }

    void ApplyPreset(Preset p)
    {
        built = false; // set every slider first, then apply once
        SetSlider(sRadius, p.radiusKm);
        SetSlider(sMass, p.massEarths);
        SetSlider(sTemp, p.tempK);
        SetSlider(sAmount, p.amount);
        for (int i = 0; i < sGas.Length; i++)
        {
            sGas[i].value = p.gas.TryGetValue(AllGases[i], out float f) ? f : 0f;
        }
        built = true;
        Apply();
    }

    void UpdateGasPercents()
    {
        float total = 0f;
        foreach (var s in sGas) total += s.value;
        for (int i = 0; i < sGas.Length; i++)
            gasPct[i].text = total > 0.0001f ? (sGas[i].value / total * 100f).ToString("F1") + "%" : "0%";
    }

    float StallSpeed()
    {
        if (pc == null || pc.leftWing == null || pc.rightWing == null || setup == null) return -1f;
        float area = pc.leftWing.area + pc.rightWing.area;
        float clMax = (pc.leftWing.liftSlope * pc.leftWing.stallAngleDeg * Mathf.Deg2Rad + pc.leftWing.cl0) * atm.liftMultiplier;
        float weight = setup.massKg * atm.surfaceGravity;
        return Mathf.Sqrt(2f * weight / (Mathf.Max(atm.surfaceDensity, 1e-5f) * area * Mathf.Max(clMax, 0.1f)));
    }

    void RefreshReadouts()
    {
        if (rGravity == null) return;
        rGravity.text = atm.surfaceGravity.ToString("F2") + " m/s²  (" + (atm.surfaceGravity / 9.81f).ToString("F2") + " g)";
        rPressure.text = atm.surfacePressureKPa.ToString("F2") + " kPa";
        rDensity.text = atm.surfaceDensity.ToString("F3") + " kg/m³";
        rScale.text = (atm.scaleHeight / 1000f).ToString("F1") + " km";
        rSound.text = atm.speedOfSound.ToString("F0") + " m/s";
        rMolar.text = (atm.meanMolarMass * 1000f).ToString("F1") + " g/mol";

        float stall = StallSpeed();
        rStall.text = stall < 0f ? "-" : (stall > 999f ? ">999 m/s" : stall.ToString("F0") + " m/s");
    
}

    // ---------------------------------------------------------------- per-frame
    void Update()
    {
        var kb = Keyboard.current;
        bool inPlanet = view == null || view.Current == ViewManager.View.Planet;
        if (kb != null && inPlanet && kb[togglePanelKey].wasPressedThisFrame)
        {
            panelHidden = !panelHidden;
            leftPanel.SetActive(!panelHidden);
        }

        // stop sliders and buttons keeping keyboard focus (Space would "click" a selected button)
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (Time.unscaledTime >= nextHud)
        {
            nextHud = Time.unscaledTime + 0.05f;
            UpdateFlightHud();
        }
    }

    void UpdateFlightHud()
    {
        if (flightPanel == null || !flightPanel.activeSelf) return;
        if (fd != null)
        {
            fSpeed.text = fd.airspeedKmh.ToString("F0") + " km/h  " + fd.airspeedMS.ToString("F0") + " m/s";
            fMach.text = fd.mach.ToString("F2");
            fAlt.text = fd.altitudeM.ToString("F0") + " m";
            fVs.text = fd.verticalSpeedMS.ToString("F1") + " m/s";
            fAoa.text = fd.angleOfAttackDeg.ToString("F1") + "°";
            fG.text = fd.gLoad.ToString("F2") + " g";
            fRho.text = fd.airDensity.ToString("F3") + " kg/m³";
            fQ.text = fd.dynamicPressurePa.ToString("F0") + " Pa";
        }
        if (eng != null)
        {
            fThrottle.text = (eng.throttle * 100f).ToString("F0") + "%";
            throttleFill.anchorMax = new Vector2(Mathf.Clamp01(eng.throttle), 1f);
        }
        eGravity.text = atm.surfaceGravity.ToString("F2") + " m/s²";
        eDensity.text = atm.surfaceDensity.ToString("F3") + " kg/m³";

        bool stalling = pc != null && pc.leftWing != null && fd != null &&
                        fd.airspeedMS > 8f && pc.leftWing.effectiveAoaDeg > pc.leftWing.stallAngleDeg;
        stallText.enabled = stalling && Mathf.Sin(Time.unscaledTime * 12f) > 0f;
    }

    // ---------------------------------------------------------------- UI building helpers
    RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = Vector2.zero;   // the default is 100x100
        return rt;
    }

    void Stack(RectTransform rt)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(18, 18, 14, 14);
        v.spacing = 4f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
    }

    void AddSpace(Transform parent, float h)
    {
        var rt = MakeRect("Space", parent);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
    }

    void AddSection(Transform parent, string name)
    {
        AddSpace(parent, 6f);
        var line = MakeRect("Divider", parent);
        line.gameObject.AddComponent<Image>().color = Line;
        line.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
        AddText(parent, name, 13, Muted);
    }

    RectTransform AddRow(Transform parent, float height)
    {
        var rt = MakeRect("Row", parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = true;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return rt;
    }

    Text AddNote(Transform parent, string content, int size, Color color)
    {
        var t = AddText(parent, content, size, color);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.alignment = TextAnchor.UpperLeft;
        t.GetComponent<LayoutElement>().preferredHeight = -1f;   // let the text set its own height
        return t;
    }

    Text AddText(Transform parent, string content, int size, Color color,
                 TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal,
                 Font f = null, float width = -1f, float flex = 1f)
    {
        var rt = MakeRect("Text", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = f != null ? f : font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = size + 8;
        if (width >= 0f) le.preferredWidth = width;
        le.flexibleWidth = flex;
        return t;
    }

    Text AddInfoRow(Transform parent, string label)
    {
        var row = AddRow(parent, 22f);
        AddText(row, label, 14, Muted);
        return AddText(row, "-", 14, Ink, TextAnchor.MiddleRight, FontStyle.Normal, mono, 200f, 0f);
    }

    Button AddButton(Transform parent, string label, bool primary, UnityAction onClick, out Text labelText)
    {
        var rt = MakeRect("Button " + label, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.normalColor = primary ? Amber : new Color(0.20f, 0.20f, 0.21f);
        c.highlightedColor = primary ? new Color(1f, 0.72f, 0.36f) : new Color(0.29f, 0.29f, 0.30f);
        c.pressedColor = primary ? new Color(0.75f, 0.50f, 0.18f) : new Color(0.14f, 0.14f, 0.15f);
        c.selectedColor = c.normalColor;
        c.colorMultiplier = 1f;
        btn.colors = c;
        btn.onClick.AddListener(onClick);
        rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        labelText = AddText(rt, label, 14, primary ? new Color(0.10f, 0.08f, 0.05f) : Ink,
                            TextAnchor.MiddleCenter, primary ? FontStyle.Bold : FontStyle.Normal);
        var trt = labelText.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }

    Slider AddSlider(Transform parent, string label, Vector2 range, float initial, bool log,
                     System.Func<float, string> fmt, out Text valueText)
    {
        var row = AddRow(parent, 24f);
        AddText(row, label, 14, Muted, TextAnchor.MiddleLeft, FontStyle.Normal, null, 126f, 0f);

        var root = MakeRect("Slider", row);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f; le.preferredWidth = 100f; le.preferredHeight = 18f;
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // invisible, makes the whole row clickable

        var track = MakeRect("Track", root);
        track.anchorMin = new Vector2(0f, 0.5f); track.anchorMax = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(0f, -1f); track.offsetMax = new Vector2(0f, 1f);
        track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

        var fillArea = MakeRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(4f, -1f); fillArea.offsetMax = new Vector2(-4f, 1f);
        var fill = MakeRect("Fill", fillArea);
        fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = Amber;

        var handleArea = MakeRect("Handle Slide Area", root);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(4f, 0f); handleArea.offsetMax = new Vector2(-4f, 0f);
        var handle = MakeRect("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f); handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(8f, 0f);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = Ink;

        float clamped = Mathf.Clamp(initial, range.x, range.y);
        var vt = AddText(row, fmt(clamped), 14, Ink, TextAnchor.MiddleRight, FontStyle.Normal, mono, 76f, 0f);
        valueText = vt;

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill;
        s.handleRect = handle;
        s.targetGraphic = handleImg;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = log ? Mathf.Log10(range.x) : range.x;
        s.maxValue = log ? Mathf.Log10(range.y) : range.y;
        s.SetValueWithoutNotify(log ? Mathf.Log10(clamped) : clamped);
        if (log) logSliders.Add(s);

        s.onValueChanged.AddListener(v =>
        {
            vt.text = fmt(log ? Mathf.Pow(10f, v) : v);
            Apply();
        });
        return s;
    }

    void BuildKey(Transform p)
    {
        AddSection(p, "Elevation key");
        var bar = MakeRect("Key Bar", p);
        var ri = bar.gameObject.AddComponent<RawImage>();
        ri.raycastTarget = false;
        bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;

        keyTex = new Texture2D(64, 1, TextureFormat.RGBA32, false);
        keyTex.wrapMode = TextureWrapMode.Clamp;
        keyTex.filterMode = FilterMode.Bilinear;
        ri.texture = keyTex;

        var row = AddRow(p, 18f);
        keyLowText = AddText(row, "Low", 12, Muted, TextAnchor.MiddleLeft);
        keyHighText = AddText(row, "High", 12, Muted, TextAnchor.MiddleRight);
        AddNote(p, "Colour shows terrain height. Blue is the lowest ground and red is the highest.", 12, Muted);
        RefreshKey();
    }

    void RefreshKey()
    {
        if (keyTex == null) return;
        if (adapter == null) adapter = FindAnyObjectByType<PlanetAdapter>();

        for (int i = 0; i < 64; i++)
            keyTex.SetPixel(i, 0, elevationGradient.Evaluate(i / 63f));
        keyTex.Apply();

        float range = adapter != null ? adapter.terrainHeightWorldM : 0f;
        keyLowText.text = "Lower";
        keyHighText.text = range > 0.5f ? "Higher (+" + range.ToString("F0") + " m)" : "Higher";
    }

    // Approximates the planet shader's colour bands, using the material's own colours and thresholds
    Color PaletteAt(Material m, float h)
    {
        float sea = m.GetFloat("_SeaLevel");
        float beach = m.HasProperty("_BeachWidth") ? m.GetFloat("_BeachWidth") : 0.04f;
        float snow = m.HasProperty("_SnowLevel") ? m.GetFloat("_SnowLevel") : 0.8f;
        var stops = new (float pos, Color col)[]
        {
        (0f, m.GetColor("_DeepColor")),
        (sea, m.GetColor("_ShallowColor")),
        (sea + 0.002f, m.GetColor("_SandColor")),
        (sea + beach, m.GetColor("_GrassColor")),
        (Mathf.Lerp(sea, 1f, 0.5f), m.GetColor("_ForestColor")),
        (Mathf.Max(snow - 0.15f, sea + beach + 0.02f), m.GetColor("_RockColor")),
        (snow, m.GetColor("_SnowColor")),
        (1f, m.GetColor("_SnowColor")),
        };
        for (int i = 1; i < stops.Length; i++)
            if (h <= stops[i].pos)
            {
                float t = Mathf.InverseLerp(stops[i - 1].pos, stops[i].pos, h);
                return Color.Lerp(stops[i - 1].col, stops[i].col, t);
            }
        return stops[stops.Length - 1].col;
    }
}