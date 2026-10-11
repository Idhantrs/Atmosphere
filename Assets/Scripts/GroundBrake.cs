using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class GroundBrake : MonoBehaviour
{
    public float brakeDecel = 6f;
    public float groundedGrace = 0.1f;

    Rigidbody rb;
    Engine engine;
    float lastContactTime = -10f;

    void Awake() 
    { 
        rb = GetComponent<Rigidbody>(); engine = GetComponent<Engine>(); 
    }
    void OnCollisionStay(Collision c) 
    { 
        lastContactTime = Time.time; 
    }

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.spaceKey.isPressed) return;
        if (Time.time - lastContactTime > groundedGrace) return; //only for ground

        if (engine != null) engine.throttle = 0f;

        var atm = Atmosphere.Instance;
        Vector3 up = atm != null ? atm.Up(transform.position) : Vector3.up;
        Vector3 h = Vector3.ProjectOnPlane(rb.linearVelocity, up);
        float speed = h.magnitude;
        if (speed < 0.05f) return;

        float decel = Mathf.Min(brakeDecel, speed / Time.fixedDeltaTime);
        rb.AddForce(-h / speed * decel, ForceMode.Acceleration);
    }
}