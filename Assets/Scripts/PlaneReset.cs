using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlaneSetup))]
public class PlaneReset : MonoBehaviour
{
    PlaneSetup setup;
    void Awake() 
    { 
        setup = GetComponent<PlaneSetup>(); 
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            setup.Spawn();
        }
    }
}