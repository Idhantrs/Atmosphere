using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlanetGravity : MonoBehaviour
{
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        var atm = Atmosphere.Instance;
        if (atm == null) return;
        rb.AddForce(atm.GravityAt(rb.worldCenterOfMass), ForceMode.Acceleration);
    }
}