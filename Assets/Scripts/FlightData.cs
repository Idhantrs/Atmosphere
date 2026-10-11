using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlightData : MonoBehaviour
{
    [Header("Live data (DISPLAY ONLY)")]
    public float airspeedMS;      // relative to the AIR
    public float airspeedKmh;
    public float groundSpeedMS;   // relative to the ground
    public float windSpeedMS;
    public float mach;
    public float altitudeM;
    public float verticalSpeedMS;
    public float airDensity;
    public float dynamicPressurePa;
    public float angleOfAttackDeg;
    public float gLoad;

    Rigidbody rb;
    Vector3 prevVelocity;

    void Awake() { rb = GetComponent<Rigidbody>(); prevVelocity = rb.linearVelocity; }

    void FixedUpdate()
    {
        var atm = Atmosphere.Instance;
        if (atm == null) return;

        Vector3 pos = transform.position;
        Vector3 v = rb.linearVelocity;
        Vector3 wind = WindField.At(pos);
        Vector3 vAir = v - wind;

        groundSpeedMS = v.magnitude;
        windSpeedMS = wind.magnitude;
        airspeedMS = vAir.magnitude;
        airspeedKmh = airspeedMS * 3.6f;
        mach = airspeedMS / atm.speedOfSound;
        altitudeM = atm.AltitudeAt(pos);
        verticalSpeedMS = Vector3.Dot(v, atm.Up(pos));
        airDensity = atm.DensityAt(pos);
        dynamicPressurePa = 0.5f * airDensity * airspeedMS * airspeedMS;

        if (airspeedMS > 0.5f)
            angleOfAttackDeg = Mathf.Atan2(-Vector3.Dot(vAir, transform.up),
                                            Vector3.Dot(vAir, transform.forward)) * Mathf.Rad2Deg;

        Vector3 accel = (v - prevVelocity) / Time.fixedDeltaTime;
        gLoad = Vector3.Dot(accel - atm.GravityAt(pos), transform.up) / atm.surfaceGravity;
        prevVelocity = v;
    }
}