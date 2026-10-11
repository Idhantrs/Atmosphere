using UnityEngine;

[DefaultExecutionOrder(-200)]
public class PlanetLink : MonoBehaviour
{
    public enum RadiusMode { Manual, FromSphereCollider, FromRendererBounds }

    public Transform center;
    public RadiusMode radiusMode = RadiusMode.Manual;
    public float manualRadiusM = 10000f;
    public float radiusOffsetM = 0f;

    public float currentRadiusM;
    public int colliderCount;

    Atmosphere atm;
    float nextRefresh;

    /// <summary>Call this from the planet generator if the radius changes at runtime.</summary>
    public void SetManualRadius(float r)
    {
        radiusMode = RadiusMode.Manual;
        manualRadiusM = r;
        Push();
    }

    public float ComputeRadius()
    {
        float r = manualRadiusM;
        switch (radiusMode)
        {
            case RadiusMode.FromSphereCollider:
                var sc = GetComponentInChildren<SphereCollider>();
                if (sc != null)
                {
                    Vector3 s = sc.transform.lossyScale;
                    r = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                }
                break;
            case RadiusMode.FromRendererBounds:
                var rs = GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    r = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
                }
                break;
        }
        return Mathf.Max(1f, r + radiusOffsetM);
    }

    public void Push()
    {
        if (atm == null) atm = FindAnyObjectByType<Atmosphere>();
        currentRadiusM = ComputeRadius();
        if (atm == null) return;
        atm.planetCenter = center != null ? center : transform;
        atm.worldRadiusM = currentRadiusM;
    }

    void Awake() { Push(); }

    void Start()
    {
        Push();
        colliderCount = GetComponentsInChildren<Collider>().Length;
        if (colliderCount == 0)
            Debug.LogWarning("PlanetLink: no colliders found on the planet, so the plane will fall through it. " +
                             "(Ignore this if your planet creates its colliders later at runtime.)");
    }

    void FixedUpdate()
    {
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + 0.5f;
        Push();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 c = (center != null ? center : transform).position;
        Gizmos.DrawWireSphere(c, Application.isPlaying ? currentRadiusM : ComputeRadius());
    }
}