using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlaneSetup : MonoBehaviour
{
    public float massKg = 340f;
    public Transform centerOfMassMarker;
    public bool useCustomInertia = true;
    public Vector3 inertiaKgM2 = new Vector3(168f, 246f, 118f);

    public bool spawnOnStart = true;
    public Vector3 spawnDirection = Vector3.up;
    public Vector3 headingHint = Vector3.forward;
    public bool startInAir = false;
    public float startAltitude = 300f;
    public float startSpeed = 45f;
    public float groundClearance = 0.9f;
    public float groundSearchHeight = 3000f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = massKg;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.automaticCenterOfMass = false;
        rb.centerOfMass = centerOfMassMarker != null
            ? transform.InverseTransformPoint(centerOfMassMarker.position)
            : Vector3.zero;
        if (useCustomInertia)
        {
            rb.automaticInertiaTensor = false;
            rb.inertiaTensor = inertiaKgM2;
            rb.inertiaTensorRotation = Quaternion.identity;
        }
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.sleepThreshold = 0f;
    }

    void Start() { if (spawnOnStart) Spawn(); }

    /// <summary>Places the plane on (or above) the planet. Also used by the R key.</summary>
    public void Spawn()
    {
        var link = FindAnyObjectByType<PlanetLink>();
        if (link != null) link.Push();
        var atm = Atmosphere.Instance != null ? Atmosphere.Instance : FindAnyObjectByType<Atmosphere>();
        if (atm == null) return;

        Vector3 up = spawnDirection.sqrMagnitude > 1e-4f ? spawnDirection.normalized : Vector3.up;
        Vector3 fwd = Vector3.ProjectOnPlane(headingHint, up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Vector3.right, up);
        fwd.Normalize();

        Vector3 c = atm.Center;
        float R = atm.worldRadiusM;
        Vector3 pos;

        if (startInAir)
        {
            pos = c + up * (R + startAltitude);
        }
        else
        {
            pos = c + up * (R + groundClearance); // fallback if no ground is found
            Physics.SyncTransforms();
            Vector3 origin = c + up * (R + groundSearchHeight);
            var hits = Physics.RaycastAll(origin, -up, groundSearchHeight * 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.attachedRigidbody == rb) continue; // skip the plane's own colliders
                if (h.distance < best) { best = h.distance; pos = h.point + up * groundClearance; found = true; }
            }
            if (!found) Debug.LogWarning("PlaneSetup: no ground found under the spawn point. Does the planet have colliders?");
        }

        Quaternion rot = Quaternion.LookRotation(fwd, up);
        transform.SetPositionAndRotation(pos, rot);
        rb.position = pos;
        rb.rotation = rot;
        rb.linearVelocity = startInAir ? fwd * startSpeed : Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void OnDrawGizmos()
    {
        if (centerOfMassMarker == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(centerOfMassMarker.position, 0.15f);
    }
}