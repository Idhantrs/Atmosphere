using UnityEngine;

[DefaultExecutionOrder(-150)]
public class PlanetAdapter : MonoBehaviour
{
    [Header("His planet")]
    public Planet planet;  // empty = the Planet on this object

    public FluidGPU fluid;
    public PlanetLink link;                // empty = the PlanetLink on this object
    [Tooltip("Extra objects that must scale with the planet, e.g. the atmosphere volume cube")]
    public Transform[] scaleWithPlanet;

    [Header("Sea level")]
    [Tooltip("Off: sea level = the lowest point of the mesh. On: the position of the shader's Sea Level slider")]
    public bool useShaderSeaLevel = false;

    [Header("Size (real km -> Unity metres)")]
    public bool resizeFromSlider = true;
    public float earthWorldRadiusM = 10000f;
    [Range(0.2f, 1f)] public float exponent = 0.4f;   // 1 = proportional, lower = compressed
    public Vector2 worldRadiusRangeM = new Vector2(3000f, 20000f);
    public float debounceS = 0.25f;

    [Header("Colliders")]
    public bool buildColliders = true;

    [Header("DISPLAY ONLY")]
    public float seaRadiusLocal, maxRadiusLocal;
    public float seaRadiusWorldM, maxRadiusWorldM;
    public float terrainHeightWorldM;

    float applyAt = -1f;

    void Awake()
    {
        if (planet == null) planet = GetComponent<Planet>();
        if (link == null) link = GetComponent<PlanetLink>();
        if (planet == null) { Debug.LogError("PlanetAdapter: no Planet found."); enabled = false; return; }
        Rebuild();
    }

    /// Call again if his code regenerates the planet at runtime.
    public void Rebuild()
    {
        var filters = planet.GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0 || filters[0].sharedMesh == null || filters[0].sharedMesh.vertexCount == 0)
        {
            planet.GeneratePlanet();
            filters = planet.GetComponentsInChildren<MeshFilter>();
        }
        Measure(filters);
        if (buildColliders) BuildColliders(filters);
        ApplyScale(TargetWorldRadius());
    }

    /// SimUI -> On Planet Changed -> PlanetAdapter.OnPlanetChanged
    public void OnPlanetChanged()
    {
        Debug.Log("PlanetAdapter: slider event received");
        applyAt = Time.unscaledTime + debounceS;
    }

    void Update()
    {
        if (applyAt > 0f && Time.unscaledTime >= applyAt) { applyAt = -1f; ApplyScale(TargetWorldRadius()); }
    }

    void Measure(MeshFilter[] filters)
    {
        float min = float.MaxValue, max = 0f;
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            var verts = mf.sharedMesh.vertices;      // local space of the face object
            for (int i = 0; i < verts.Length; i++)
            {
                float d = verts[i].magnitude;
                if (d < min) min = d;
                if (d > max) max = d;
            }
        }
        if (min == float.MaxValue) { min = 1f; max = 1f; }
        maxRadiusLocal = max;
        seaRadiusLocal = min;
        if (useShaderSeaLevel && planet.planetMaterial != null && planet.planetMaterial.HasProperty("_SeaLevel"))
            seaRadiusLocal = min + planet.planetMaterial.GetFloat("_SeaLevel") * (max - min);
    }

    void BuildColliders(MeshFilter[] filters)
    {
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = null;                    // force Unity to re-cook
            mc.sharedMesh = mf.sharedMesh;
        }
    }

    float Map(float km)
    {
        float r = earthWorldRadiusM * Mathf.Pow(km / 6371f, exponent);
        return Mathf.Clamp(r, worldRadiusRangeM.x, worldRadiusRangeM.y);
    }

    float TargetWorldRadius()
    {
        var atm = Atmosphere.Instance != null ? Atmosphere.Instance : FindAnyObjectByType<Atmosphere>();
        if (resizeFromSlider && atm != null) return Map(atm.planetRadiusKm);
        return seaRadiusLocal * planet.transform.lossyScale.x;
    }

    void ApplyScale(float targetWorldRadius)
    {
        float current = planet.transform.lossyScale.x;
        float wanted = targetWorldRadius / Mathf.Max(seaRadiusLocal, 1e-6f);   // scale that makes sea level = target
        float ratio = wanted / Mathf.Max(current, 1e-6f);
        if (!Mathf.Approximately(ratio, 1f))
        {
            planet.transform.localScale *= ratio;    // assumes the Planet's parent has scale 1
            foreach (var t in scaleWithPlanet) if (t != null) t.localScale *= ratio;
        }
        seaRadiusWorldM = seaRadiusLocal * wanted;
        maxRadiusWorldM = maxRadiusLocal * wanted;
        terrainHeightWorldM = maxRadiusWorldM - seaRadiusWorldM;
        if (link != null) link.SetManualRadius(seaRadiusWorldM);

        if (fluid != null) fluid.SetWorldRadius(seaRadiusWorldM);
    }
}