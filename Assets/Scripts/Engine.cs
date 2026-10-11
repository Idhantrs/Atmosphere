using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Engine : MonoBehaviour
{
    [Range(0, 1)] public float throttle = 0.6f;

    public float maxThrustN = 3800f;
    public float zeroThrustSpeed = 100f;

    public float referenceDensity = 1.225f; 

    [Range(0f, 1f)] public float densityExponent = 0.5f; //density factor is (ρ / referenceDensity) ^ densityExponent
    Rigidbody rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var atm = Atmosphere.Instance;
        if (atm == null) return;

        float rhoRatio = atm.DensityAt(transform.position) / referenceDensity;
        float densityFactor = Mathf.Pow(Mathf.Max(0f, rhoRatio), densityExponent);

        float fwdSpeed = Vector3.Dot(rb.linearVelocity - WindField.At(transform.position), transform.forward);
        float speedFactor = Mathf.Clamp01(1f - fwdSpeed / zeroThrustSpeed);

        float thrust = maxThrustN * throttle * densityFactor * speedFactor * atm.thrustMultiplier;
        rb.AddForce(transform.forward * thrust);
    }
}
