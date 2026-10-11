using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    public Transform target;   // planet center. Leave empty to use the Atmosphere's center

    [Header("Distance (Unity metres from the planet center, NOT planet radii)")]
    [Tooltip("Must clear the biggest planet plus its atmosphere cube (about 1.6 x 20000 = 32000 m)")]
    public float distance = 60000f;
    public bool lockDistance = true;                  // on = scroll wheel does nothing
    public float minDistance = 36000f, maxDistance = 150000f;
    [Tooltip("Zoom per scroll unit (multiplicative). A mouse wheel notch is often 120")]
    public float zoomSpeed = 0.001f;

    [Header("Orbit")]
    public float yaw = 30f, pitch = 20f;
    public float rotateSpeed = 0.2f;

    Camera cam;
    void Awake() { cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        var mouse = Mouse.current;
        var atm = Atmosphere.Instance;
        if (mouse == null || atm == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * rotateSpeed;
            pitch = Mathf.Clamp(pitch - d.y * rotateSpeed, -89f, 89f);
        }

        if (!lockDistance)
        {
            float sc = mouse.scroll.ReadValue().y;
            if (sc != 0f) distance = Mathf.Clamp(distance * Mathf.Exp(-sc * zoomSpeed), minDistance, maxDistance);
        }

        Vector3 c = target != null ? target.position : atm.Center;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = c - rot * Vector3.forward * distance;
        transform.rotation = rot;

        // clip planes follow the distance to the surface, not the planet size
        float R = Mathf.Max(1f, atm.worldRadiusM);
        float toSurface = Mathf.Max(100f, distance - R);
        cam.nearClipPlane = Mathf.Max(1f, toSurface * 0.1f);
        cam.farClipPlane = distance + R * 3f;
    }
}