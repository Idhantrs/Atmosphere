using System;
using System.Collections.Generic;
using UnityEngine;

public enum GasType { N2, O2, Ar, CO2 }

[Serializable]
public class GasEntry
{
    public GasType gas = GasType.N2;
    [Min(0f)] public float fraction = 1f;
}

public class Atmosphere : MonoBehaviour
{
    public static Atmosphere Instance { get; private set; }
    const float R = 8.314462f;

    [Min(1f)] public float planetRadiusKm = 6371f;
    [Min(0.001f)] public float planetMassEarths = 1f;
    public bool overrideGravity = false;
    public float gravityOverride = 9.81f;

    [Min(0f)] public float surfacePressureKPa = 101.325f;
    [Min(1f)] public float temperatureK = 288f;

    public bool derivePressureFromColumn = true;
    [Min(0.0001f)] public float atmosphereAmount = 1f;
    public List<GasEntry> gases = new List<GasEntry>
    {
        new GasEntry { gas = GasType.N2, fraction = 0.78f },
        new GasEntry { gas = GasType.O2, fraction = 0.21f },
        new GasEntry { gas = GasType.Ar, fraction = 0.01f },
    };

    public Transform planetCenter;
    public float worldRadiusM = 10000f;

    public float dragMultiplier = 1f;
    public float liftMultiplier = 1f;
    public float thrustMultiplier = 1f;

    public float meanMolarMass;
    public float gamma;
    public float surfaceDensity;
    public float surfaceGravity;
    public float scaleHeight; 
    public float speedOfSound;

    void Awake() 
    {
        Instance = this; Recalculate(); 
    }
    void OnEnable() 
    { 
        Instance = this; 
    }
    void OnValidate() 
    { 
        Recalculate(); 
    }

    static void GasData(GasType g, out float molarMass, out float gam)
    {
        switch (g)
        {
            case GasType.N2: molarMass = 0.028014f; gam = 1.40f; break;
            case GasType.O2: molarMass = 0.031998f; gam = 1.40f; break;
            case GasType.Ar: molarMass = 0.039948f; gam = 1.667f; break;
            case GasType.CO2: molarMass = 0.044010f; gam = 1.30f; break;
            default: molarMass = 0.020180f; gam = 1.667f; break;
        }
    }

    public void Recalculate()
    {
        float sumM = 0f, cv = 0f, total = 0f;
        foreach (var e in gases)
        {
            GasData(e.gas, out float m, out float g);
            sumM += e.fraction * m;
            cv += e.fraction * R / (g - 1f);
            total += e.fraction;
        }
        meanMolarMass = total > 0f ? sumM / total : 0.029f;
        gamma = total > 0f ? 1f + R / (cv / total) : 1.4f;

        double radius = planetRadiusKm * 1000.0;
        double mass = planetMassEarths * 5.972e24;
        float g0 = overrideGravity ? gravityOverride : (float)(6.6743e-11 * mass / (radius * radius));
        surfaceGravity = Mathf.Max(0.1f, g0);

        if (derivePressureFromColumn) surfacePressureKPa = atmosphereAmount * 10332f * surfaceGravity / 1000f; 

        surfaceDensity = surfacePressureKPa * 1000f * meanMolarMass / (R * temperatureK);
        scaleHeight = R * temperatureK / (meanMolarMass * surfaceGravity);
        speedOfSound = Mathf.Sqrt(gamma * R * temperatureK / meanMolarMass);

        if (Application.isPlaying) Physics.gravity = Vector3.zero;
    }

    public Vector3 Center => planetCenter != null ? planetCenter.position : new Vector3(0f, -worldRadiusM, 0f);

    public Vector3 Up(Vector3 pos)
    {
        Vector3 d = pos - Center;
        float m = d.magnitude;
        return m > 1e-3f ? d / m : Vector3.up;
    }

    public float AltitudeAt(Vector3 pos) => (pos - Center).magnitude - worldRadiusM;

    public float DensityAt(Vector3 pos) => DensityAtAltitude(AltitudeAt(pos));

    public float DensityAtAltitude(float h) =>
        surfaceDensity * Mathf.Exp(-Mathf.Max(0f, h) / scaleHeight);

    public Vector3 GravityAt(Vector3 pos)
    {
        float h = Mathf.Max(0f, AltitudeAt(pos));
        float r = planetRadiusKm * 1000f;
        float k = r / (r + h);
        return -Up(pos) * surfaceGravity * k * k;
    }

    //Uses surface gravity: g = GM/r² and surface pressure: P = atmosphereAmount × Earth's air column mass × g and surface density: ρ = PM/(RT)


}