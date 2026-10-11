using UnityEngine;

public class SimpleWind : MonoBehaviour, IWindSource
{
    public float windSpeedMS = 0f;
    public Vector3 headingHint = Vector3.right;

    public float gustMS = 0f;
    public float gustSizeM = 150f;
    public float gustPeriodS = 6f;

    public Transform thermalCenter;
    public float thermalRadiusM = 80f;
    public float thermalUpdraftMS = 5f;
    public float thermalHeightM = 600f;

    public Transform probe;           
    public Vector3 windAtProbe;     

    void OnEnable() 
    { 
        WindField.Register(this); 
    }
    void OnDisable() 
    { 
        WindField.Unregister(this); 
    }

    void Update()
    {
        if (probe != null) windAtProbe = WindField.At(probe.position);
    }

    public Vector3 WindAt(Vector3 p)
    {
        var atm = Atmosphere.Instance;
        Vector3 up = atm != null ? atm.Up(p) : Vector3.up;
        Vector3 w = Vector3.zero;

        if (windSpeedMS != 0f)
        {
            Vector3 hint = headingHint.sqrMagnitude > 1e-6f ? headingHint.normalized : Vector3.right;
            Vector3 dir = Vector3.ProjectOnPlane(hint, up);
            if (dir.sqrMagnitude < 1e-3f) dir = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (dir.sqrMagnitude < 1e-3f) dir = Vector3.ProjectOnPlane(Vector3.right, up);
            w += dir.normalized * windSpeedMS;
        }

        if (gustMS > 0f)
        {
            float t = 1000f + Time.time / gustPeriodS;
            float s = 1f / gustSizeM;
            float nx = Mathf.PerlinNoise(1000f + p.x * s + t, 1000f + p.z * s) - 0.5f;
            float ny = Mathf.PerlinNoise(2000f + p.y * s, 1000f + p.x * s + t) - 0.5f;
            float nz = Mathf.PerlinNoise(1000f + p.z * s - t, 3000f + p.y * s) - 0.5f;
            w += new Vector3(nx, ny * 0.5f, nz) * (2f * gustMS);
        }

        if (thermalCenter != null && thermalUpdraftMS != 0f)
        {
            Vector3 d = p - thermalCenter.position;
            float h = Vector3.Dot(d, up);
            if (h > -20f && h < thermalHeightM)
            {
                float r = Vector3.ProjectOnPlane(d, up).magnitude / thermalRadiusM;
                float core = Mathf.Exp(-r * r);
                float fade = Mathf.Clamp01(Mathf.Min((h + 20f) / 50f, (thermalHeightM - h) / 100f));
                w += up * (thermalUpdraftMS * core * fade);
            }
        }
        return w;
    }

    void OnDrawGizmosSelected()
    {
        if (thermalCenter == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(thermalCenter.position, thermalRadiusM);
        Gizmos.DrawLine(thermalCenter.position, thermalCenter.position + Vector3.up * thermalHeightM);
    }
}