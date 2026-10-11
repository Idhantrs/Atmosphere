using NUnit.Framework.Constraints;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BodyDrag: MonoBehaviour
{
    public Vector3 cdA = new Vector3(2f, 4f, 0.15f);

    Rigidbody rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        var atm = Atmosphere.Instance;
        if (atm == null) return;

        float rho = atm.DensityAt(transform.position);
        Vector3 vl = transform.InverseTransformDirection(rb.linearVelocity - WindField.At(transform.position)); //vl is local velocity

        Vector3 fl = new Vector3( //fl is local drag
            -0.5f * rho * cdA.x * vl.x * Mathf.Abs(vl.x),
            -0.5f * rho * cdA.y * vl.y * Mathf.Abs(vl.y),
            -0.5f * rho * cdA.z * vl.z * Mathf.Abs(vl.z)) * atm.dragMultiplier;

        rb.AddForce(transform.TransformDirection(fl));


        //uses standard drag formula ½ρv²·CdA
    }
}
