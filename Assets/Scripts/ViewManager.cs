using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ViewManager : MonoBehaviour
{
    public enum View { Planet, Flight }

    public GameObject plane;        
    public GameObject planetCamera;   
    public GameObject flightCamera; 

    public GameObject[] planetViewOnly;
    public GameObject[] flightViewOnly;

    public Key toggleKey = Key.V;
    public bool spawnUnderCamera = true;

    public View Current { get; private set; }
    public event Action<View> Changed;

    void Awake() { Apply(View.Planet); }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) Toggle();
    }

    public void Toggle() 
    { 
        SetView(Current == View.Planet ? View.Flight : View.Planet); 
    }

    public void SetView(View v)
    {
        if (v == Current) return;
        Vector3 camPos = planetCamera != null ? planetCamera.transform.position : Vector3.zero;
        Vector3 camRight = planetCamera != null ? planetCamera.transform.right : Vector3.right;

        Apply(v);
        if (v == View.Flight) EnterFlight(camPos, camRight);
        Changed?.Invoke(v);
    }

    void Apply(View v)
    {
        Current = v;
        bool flight = v == View.Flight;
        if (planetCamera != null) planetCamera.SetActive(!flight);
        if (flightCamera != null) flightCamera.SetActive(flight);
        if (plane != null) plane.SetActive(flight);
        foreach (var go in planetViewOnly) if (go != null) go.SetActive(!flight);
        foreach (var go in flightViewOnly) if (go != null) go.SetActive(flight);
    }

    void EnterFlight(Vector3 camPos, Vector3 camRight)
    {
        if (plane == null) { Debug.LogError("ViewManager: no plane assigned."); return; }

        var setup = plane.GetComponent<PlaneSetup>();
        var atm = Atmosphere.Instance;
        if (setup != null)
        {
            if (spawnUnderCamera && atm != null)
            {
                Vector3 d = camPos - atm.Center;
                if (d.sqrMagnitude > 1f) { setup.spawnDirection = d.normalized; setup.headingHint = camRight; }
            }
            setup.Spawn();
        }

        var fc = flightCamera != null ? flightCamera.GetComponent<FollowCamera>() : null;
        if (fc != null)
        {
            fc.target = plane.transform;
            flightCamera.transform.SetPositionAndRotation(
                plane.transform.TransformPoint(fc.offset), plane.transform.rotation);
        }
    }
}