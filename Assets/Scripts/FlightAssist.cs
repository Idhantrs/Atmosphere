using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class FlightAssist : MonoBehaviour
{
    public float rollDamping = 2.5f;
    public float pitchDamping = 2f;
    public float yawDamping = 1f;

    public float maxRollRate = 70f;
    public float maxPitchRate = 35f;
    public float maxYawRate = 25f;

    [Range(0f, 1.5f)] public float turnAssist = 0.6f;
    public float turnAssistGain = 3f;

    public bool autoLevel = true;
    public float levelStrength = 3f;

    Rigidbody rb;
    void Awake() { rb = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        var atm = Atmosphere.Instance;
        if (atm == null) return;

        Vector3 pos = transform.position;
        Vector3 up = atm.Up(pos);
        float speed = rb.linearVelocity.magnitude;
        float rhoRatio = Mathf.Clamp(atm.DensityAt(pos) / 1.225f, 0.1f, 1.5f);
        float airFactor = Mathf.Clamp01(speed / 20f) * rhoRatio;

        // Damping; x = pitch; y = yaw; z = roll
        Vector3 w = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 accel = new Vector3(-pitchDamping * w.x, -yawDamping * w.y, -rollDamping * w.z) * airFactor;
        rb.AddRelativeTorque(accel, ForceMode.Acceleration);

        // Turn assist: bank right => turn right
        if (turnAssist > 0f && speed > 15f)
        {
            float sinBank = Mathf.Clamp(-Vector3.Dot(transform.right, up), -0.85f, 0.85f);
            float targetYawRate = turnAssist * atm.surfaceGravity * sinBank / speed;
            float currentYawRate = Vector3.Dot(rb.angularVelocity, up);
            rb.AddTorque(up * ((targetYawRate - currentYawRate) * turnAssistGain), ForceMode.Acceleration);
        }

        // Auto-level
        if (autoLevel && speed > 15f && Vector3.Dot(transform.up, up) > 0f)
        {
            var kb = Keyboard.current;
            bool rolling = kb != null && (kb.aKey.isPressed || kb.dKey.isPressed);
            if (!rolling)
            {
                float sinBank = Mathf.Clamp(-Vector3.Dot(transform.right, up), -1f, 1f);
                rb.AddRelativeTorque(0f, 0f, levelStrength * sinBank, ForceMode.Acceleration);
            }
        }

        // Hard caps on rotation speed
        w = transform.InverseTransformDirection(rb.angularVelocity);
        w.x = Mathf.Clamp(w.x, -maxPitchRate * Mathf.Deg2Rad, maxPitchRate * Mathf.Deg2Rad);
        w.y = Mathf.Clamp(w.y, -maxYawRate * Mathf.Deg2Rad, maxYawRate * Mathf.Deg2Rad);
        w.z = Mathf.Clamp(w.z, -maxRollRate * Mathf.Deg2Rad, maxRollRate * Mathf.Deg2Rad);
        rb.angularVelocity = transform.TransformDirection(w);

        //Main formula: angular acceleration = −k × ω × airFactor
    }
}