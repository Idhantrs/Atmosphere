using UnityEngine;

public class AeroSurface : MonoBehaviour
{
    public float pitchMomentNm;

    public float area = 8f;

    public float liftSlope = 5f;
    public float cl0 = 0f;
    public float cd0 = 0.02f;
    public float inducedDragFactor = 0.05f;
    public float stallAngleDeg = 15f;
    public float incidenceDeg = 3f;

    [Range(-1, 1)] public float control;
    public float controlAuthorityDeg = 5f;

    public bool drawDebug = true; // green = lift, red = drag (Scene view)
    public float effectiveAoaDeg, cl, cd;

    Rigidbody rb;

    void Awake() { rb = GetComponentInParent<Rigidbody>(); }

    void FixedUpdate()
    {
        var atm = Atmosphere.Instance;
        if (atm == null || rb == null) return;

        Vector3 pos = transform.position;
        Vector3 v = rb.GetPointVelocity(pos) - WindField.At(pos);   // velocity relative to the AIR
        float speed = v.magnitude;
        if (speed < 0.1f) { cl = cd = 0f; return; }

        // Angle of attack: forward = toward the nose, up = surface normal
        float aoa = Mathf.Atan2(-Vector3.Dot(v, transform.up), Vector3.Dot(v, transform.forward));
        effectiveAoaDeg = aoa * Mathf.Rad2Deg + incidenceDeg + control * controlAuthorityDeg;
        float a = effectiveAoaDeg * Mathf.Deg2Rad;

        // Lift: linear until stall, then blends into a flat-plate model
        float stall = stallAngleDeg * Mathf.Deg2Rad;
        float attached = liftSlope * a + cl0;
        float post = 1.1f * Mathf.Sin(2f * a);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stall, stall + 5f * Mathf.Deg2Rad, Mathf.Abs(a)));
        cl = Mathf.Lerp(attached, post, t);

        // Drag: parasitic + induced + post-stall flat-plate drag
        float sinA = Mathf.Sin(a);
        cd = cd0 + inducedDragFactor * cl * cl + t * 1.28f * sinA * sinA;

        // Simple compressibility drag rise near the speed of sound
        float mach = speed / atm.speedOfSound;
        if (mach > 0.8f) cd += 2f * Mathf.Pow(Mathf.Min(mach - 0.8f, 1f), 2f);

        float q = 0.5f * atm.DensityAt(pos) * speed * speed;
        Vector3 dragDir = -v / speed;
        Vector3 liftDir = Vector3.Cross(Vector3.Cross(dragDir, transform.up), dragDir).normalized;

        Vector3 lift = liftDir * (q * area * cl * atm.liftMultiplier);
        Vector3 drag = dragDir * (q * area * cd * atm.dragMultiplier);

        // Stability guard: stops dense atmospheres from reversing velocity in one step
        float maxForce = 0.2f * rb.mass * speed / Time.fixedDeltaTime;
        lift = Vector3.ClampMagnitude(lift, maxForce);
        drag = Vector3.ClampMagnitude(drag, maxForce);

        pitchMomentNm = Vector3.Dot(Vector3.Cross(pos - rb.worldCenterOfMass, lift + drag), rb.transform.right);

        rb.AddForceAtPosition(lift + drag, pos);

        if (drawDebug)
        {
            float weight = rb.mass * atm.surfaceGravity;
            Debug.DrawRay(pos, lift / weight * 3f, Color.green);
            Debug.DrawRay(pos, drag / weight * 3f, Color.red);
        }
    }
}