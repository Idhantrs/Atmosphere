using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlaneController : MonoBehaviour
{
    public AeroSurface leftWing;
    public AeroSurface rightWing;
    public AeroSurface horizontalTail;
    public AeroSurface verticalFin;
    public Engine engine;

    public float controlResponse = 4f;
    public float throttleRate = 0.4f;
    public bool invertPitch, invertRoll, invertYaw;

    float pitch, roll, yaw;

    static float Axis(KeyControl positive, KeyControl negative) =>
        (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float pitchT = Axis(kb.sKey, kb.wKey);                    //s/w is pulll back/dip down
        float rollT = Axis(kb.dKey, kb.aKey);                    //d/a is bank right/left
        float yawT = Axis(kb.rightArrowKey, kb.leftArrowKey);   //right/left arrow is turn right/left
        if (invertPitch) pitchT = -pitchT;
        if (invertRoll) rollT = -rollT;
        if (invertYaw) yawT = -yawT; //inverses

        float k = 1f - Mathf.Exp(-controlResponse * Time.deltaTime);
        pitch = Mathf.Lerp(pitch, pitchT, k);
        roll = Mathf.Lerp(roll, rollT, k);
        yaw = Mathf.Lerp(yaw, yawT, k);

        horizontalTail.control = -pitch;   //tail down = nose up
        leftWing.control = roll;
        rightWing.control = -roll;
        verticalFin.control = -yaw;     //fin's up-axis must point to the plane's right

        if (engine != null)
        {
            if (kb.upArrowKey.isPressed)
                engine.throttle = Mathf.Clamp01(engine.throttle + throttleRate * Time.deltaTime);
            if (kb.downArrowKey.isPressed)
                engine.throttle = Mathf.Clamp01(engine.throttle - throttleRate * Time.deltaTime);
        }
    }
}