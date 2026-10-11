using UnityEngine;

public class FluidGPU : MonoBehaviour
{
    private int kBuoy;
    public GasType[] speciesGas = new GasType[] { GasType.N2, GasType.O2, GasType.Ar, GasType.CO2 };
    [Range(0f, 1f)] public float densityVisualExponent = 0.5f;
    public float earthTotalDensity = 1.85f;
    public float debounceS = 0.25f;
    float baseGravity, baseScaleHeight, applyAt = -1f;
    bool started;

    public Vector4 rho04 = new Vector4(1f, 0.5f, 0.25f, 0.1f);
    public Vector4 molarMass = new Vector4(28f, 16f, 44f, 4f);
    public float scaleHeightBase = 0.05f; 
    public Color[] speciesColor = new Color[4] { Color.white, Color.cyan, Color.red, Color.yellow };
    public Vector4 absorb = new Vector4(1f, 1f, 1f, 1f);
    public float gravity = 0.5f;
    
    public float drag = 1f, denRelax = 0.5f;
    public float noiseFreq = 0.15f;
    public float initSpeedCalls = 0.1f;
    public float rho0 = 1f;
    public Vector3 noiseSeed = new Vector3(12.3f, 45.6f, 78.9f);
    private int kInitVel, kInitDen;
    private int kVort, kConfine;
    public float confine = 0.1f;
    
    public float forceFreq = 0.08f, forceAmp = 1f, forceSpeed = 0.2f;
    private int kForce;
    
    private RenderTexture vort;
    
    public ComputeShader cs;
    
    public float densityScale = 8f;
    public int steps = 128;

    [Tooltip("Do in multiples of 64 bc the threads are in 8s")]
    public int N = 64;

    public int diffuseIters = 20;
    public int pressureIters = 20;
    public float omega = 1.8f;
    public float dt = 0.1f, radius = 0.02f, forceScale = 5f, densityAmount = 1f;
    public float visc = 0, diff = 0f;
    private RenderTexture velRHS, denRHS;
    private RenderTexture velA, velB, denA, denB, P, Div;

    public Transform planet, volumeCube;
    public Material volumeMat;

    public float rPlanetWorld = 100f, rAtmWorld = 140f, rScale = 1.4f;
    public float rAtmUV = 0.45f;
    private float cubeSize;

    public Camera cam;

    private int kDiffVel, kDiffDen;
    private int kSplat, kAdvVel, kDiv, kPress, kGrad, kAdvDen;
    private int groups, groupsX;
    private Vector2 lastMouse;

    public float planetRadius = 100f;

    RenderTexture Make3D(RenderTextureFormat f)
    {
        var rt = new RenderTexture(N, N, 0, f);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.volumeDepth = N;
        rt.enableRandomWrite = true;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    void Diffuse(int kernel, RenderTexture field, RenderTexture rhs, string fieldName, float rate)
    {
        Graphics.CopyTexture(field, rhs);
        cs.SetTexture(kernel, fieldName, field);
        cs.SetTexture(kernel, fieldName + "RHS", rhs);
        cs.SetFloat("diffA", dt * rate * N * N);

        for (int k = 0; k < diffuseIters; k++)
        for (int color = 0; color < 2; color++)
        {
            cs.SetInt("color", color);
            cs.Dispatch(kernel, groupsX, groups, groups);
        }
    }

    void Project()
    {
        cs.SetTexture(kDiv, "Vel", velA);
        cs.Dispatch(kDiv, groups, groups, groups);
        
        for (int k = 0; k < pressureIters; k++)
        for (int color = 0; color < 2; color++)
        {
            cs.SetInt("color", color);
            cs.Dispatch(kPress, groupsX, groups, groups);
        }

        cs.SetTexture(kGrad, "Vel", velA);
        cs.Dispatch(kGrad, groups, groups, groups);
    }

    void Start()
    {
        baseGravity = gravity;
        baseScaleHeight = scaleHeightBase;

        cs.SetFloat("omega", omega);
        cs.SetFloat("dt", dt);
        
        var adapter = FindAnyObjectByType<PlanetAdapter>();
        ApplyGeometry(adapter != null ? adapter.seaRadiusWorldM : planet.localScale.x * 0.5f);

        vort = Make3D(RenderTextureFormat.ARGBFloat);
        velA = Make3D(RenderTextureFormat.ARGBFloat);
        velB = Make3D(RenderTextureFormat.ARGBFloat);
        velRHS = Make3D(RenderTextureFormat.ARGBFloat);
        denA = Make3D(RenderTextureFormat.ARGBFloat);
        denB = Make3D(RenderTextureFormat.ARGBFloat);
        denRHS = Make3D(RenderTextureFormat.ARGBFloat);
        P = Make3D(RenderTextureFormat.RFloat);
        Div = Make3D(RenderTextureFormat.RFloat);
        
        kAdvVel = cs.FindKernel("AdvVel");
        kAdvDen = cs.FindKernel("AdvDen");
        kDiffVel = cs.FindKernel("DiffuseVel");
        kDiffDen = cs.FindKernel("DiffuseDen");
        kPress = cs.FindKernel("Pressure");
        kGrad = cs.FindKernel("SubtractGrad");
        kDiv = cs.FindKernel("Divergence");
        kInitVel = cs.FindKernel("InitVel");
        kInitDen = cs.FindKernel("InitDen");
        kConfine = cs.FindKernel("Confine");
        kVort = cs.FindKernel("Vorticity");
        kForce = cs.FindKernel("Force");
        kBuoy = cs.FindKernel("Buoyancy");
        
        groups = Mathf.CeilToInt(N / 8f);
        groupsX = Mathf.CeilToInt(N / 8f / 2f);
        cs.SetInts("size", N, N, N);
        
        cs.SetTexture(kDiffVel, "VelRHS", velRHS);
        cs.SetTexture(kDiffDen, "DenRHS", denRHS);
        cs.SetTexture(kDiv, "Div", Div);
        cs.SetTexture(kPress, "P", P);
        cs.SetTexture(kPress, "Div", Div);
        cs.SetTexture(kGrad, "P", P);
        cs.SetTexture(kVort, "Vort", vort);
        cs.SetTexture(kConfine, "Vort", vort);
        
        float rPlanetUV = rPlanetWorld / cubeSize;

        Vector4 H4 = new Vector4(
            scaleHeightBase * 28f / molarMass.x, scaleHeightBase * 28f / molarMass.y,
            scaleHeightBase * 28f / molarMass.z, scaleHeightBase * 28f / molarMass.w);
        
        cs.SetVector("rho04", rho04);
        cs.SetVector("H4", H4);
        cs.SetVector("mass4", molarMass);
        cs.SetFloat("freq", noiseFreq);
        cs.SetFloat("amplitude", initSpeedCalls / (dt * N * 2f));
        cs.SetVector("seed", noiseSeed);

        cs.SetTexture(kInitVel, "Vel", velA);
        cs.SetTexture(kInitDen, "Den", denA);
        cs.Dispatch(kInitVel, groups, groups, groups);
        cs.Dispatch(kInitDen, groups, groups, groups);
        
        for (int i = 0; i < 3; i++) Project();

        started = true;
        ApplyAtmosphere(Atmosphere.Instance);
    }

    void Update()
    {
        if (applyAt > 0f && Time.unscaledTime >= applyAt)
        {
            applyAt = -1f;
            ApplyAtmosphere(Atmosphere.Instance);
        }


        cs.SetFloat("omega", omega);
        cs.SetFloat("dt", dt);

        cs.SetTexture(kAdvVel, "VelIn", velA);
        cs.SetTexture(kAdvVel, "VelOut", velB);
        cs.Dispatch(kAdvVel, groups, groups, groups);
        (velA, velB) = (velB, velA);
        
        cs.SetFloat("time", Time.time);
        cs.SetFloat("forceFreq", forceFreq);
        cs.SetFloat("forceAmp", forceAmp);
        cs.SetFloat("forceSpeed", forceSpeed);
        cs.SetFloat("drag", drag);
        cs.SetFloat("denRelax", denRelax);
        cs.SetTexture(kForce, "Vel", velA);
        cs.Dispatch(kForce, groups, groups, groups);

        cs.SetFloat("gravity", gravity);

        cs.SetTexture(kBuoy, "Vel", velA);
        cs.SetTexture(kBuoy, "Den", denA);
        cs.Dispatch(kBuoy, groups, groups, groups);
        
        cs.SetFloat("confine", confine);
        cs.SetTexture(kVort, "Vel", velA);
        cs.SetTexture(kConfine, "Vel", velA);
        cs.Dispatch(kVort, groups, groups, groups);
        cs.Dispatch(kConfine, groups, groups, groups);

        Project();
        
        if (diff > 0) Diffuse(kDiffDen, denA, denRHS, "Den", diff);
        
        cs.SetTexture(kAdvDen, "VelIn", velA);
        cs.SetTexture(kAdvDen, "DenIn", denA);
        cs.SetTexture(kAdvDen, "DenOut", denB);
        cs.Dispatch(kAdvDen, groups, groups, groups);
        (denA, denB) = (denB, denA);

        for (int i = 0; i < 4; i++) volumeMat.SetColor("_Col" + i, speciesColor[i]);
        volumeMat.SetVector("_Absorb", absorb);
        volumeMat.SetTexture("_Density", denA);
        volumeMat.SetFloat("_PlanetRadius", rPlanetWorld / cubeSize);
        volumeMat.SetFloat("_DensityScale", densityScale);
        volumeMat.SetInt("_Steps", steps);
    }
    
    void OnDestroy()
    {
        velA.Release(); velB.Release(); velRHS.Release();
        denA.Release(); denB.Release(); denRHS.Release();
        P.Release(); Div.Release(); vort.Release();
    }

    static float MolarGPerMol(GasType g)
    {
        switch (g)
        {
            case GasType.N2: return 28.014f;
            case GasType.O2: return 31.998f;
            case GasType.Ar: return 39.948f;
            case GasType.CO2: return 44.010f;
            default: return 28f;
        }
    }

    void ApplyGeometry(float planetR)
    {
        rPlanetWorld = planetR;
        rAtmWorld = rPlanetWorld * rScale;
        cubeSize = rAtmWorld / rAtmUV;
        volumeCube.position = planet.position;
        volumeCube.localScale = Vector3.one * cubeSize;
        cs.SetFloat("rAtm", rAtmUV);
        cs.SetFloat("rPlanet", rPlanetWorld / cubeSize);
    }

    /// Called by PlanetAdapter whenever the planet is resized.
    public void SetWorldRadius(float r)
    {
        if (!started) return;      // Start() reads the radius from PlanetAdapter itself
        ApplyGeometry(r);
    }

    /// SimUI -> On Planet Changed -> FluidGPU.OnPlanetChanged
    public void OnPlanetChanged() { applyAt = Time.unscaledTime + debounceS; }

    public void ApplyAtmosphere(Atmosphere atm)
    {
        if (!started || atm == null) return;

        // composition: each channel's share of the mix
        float[] f = new float[4];
        float total = 0f;
        foreach (var e in atm.gases)
        {
            total += e.fraction;
            for (int i = 0; i < 4; i++) if (speciesGas[i] == e.gas) f[i] += e.fraction;
        }
        if (total <= 0f) return;

        // overall density (compressed so Mars and Venus both show up)
        float vis = Mathf.Clamp(Mathf.Pow(atm.surfaceDensity / 1.225f, densityVisualExponent), 0.03f, 4f);
        for (int i = 0; i < 4; i++) rho04[i] = f[i] / total * vis * earthTotalDensity;
        for (int i = 0; i < 4; i++) molarMass[i] = MolarGPerMol(speciesGas[i]);

        // gravity in the sim's own units
        gravity = baseGravity * atm.surfaceGravity / 9.81f;

        // scale height relative to the planet size, compared with Earth (each gas still scales as 28/M)
        float hRel = (atm.temperatureK / 288f) * (9.81f / atm.surfaceGravity) * (6371f / atm.planetRadiusKm);
        scaleHeightBase = Mathf.Clamp(baseScaleHeight * hRel, 0.01f, 0.12f);
        Vector4 H4 = new Vector4(
            scaleHeightBase * 28f / molarMass.x, scaleHeightBase * 28f / molarMass.y,
            scaleHeightBase * 28f / molarMass.z, scaleHeightBase * 28f / molarMass.w);

        cs.SetVector("rho04", rho04);
        cs.SetVector("H4", H4);
        cs.SetVector("mass4", molarMass);

        // rebuild the density field from the new settings (velocity keeps flowing)
        cs.SetTexture(kInitDen, "Den", denA);
        cs.Dispatch(kInitDen, groups, groups, groups);

        Debug.Log($"FluidGPU: rho04 {rho04}, scale height {scaleHeightBase:F3}, gravity {gravity:F2}");
    }

}
