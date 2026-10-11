using System.Collections.Generic;
using UnityEngine;

/// Anything that can say "what is the air velocity at this world position?" implements this.
public interface IWindSource
{
    Vector3 WindAt(Vector3 worldPos);   // m/s, world space
}

public static class WindField
{
    static readonly List<IWindSource> sources = new List<IWindSource>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { sources.Clear(); }

    public static void Register(IWindSource s) { if (s != null && !sources.Contains(s)) sources.Add(s); }
    public static void Unregister(IWindSource s) { sources.Remove(s); }

    /// Sum of every registered source at this position (zero if none).
    public static Vector3 At(Vector3 worldPos)
    {
        Vector3 w = Vector3.zero;
        for (int i = 0; i < sources.Count; i++) w += sources[i].WindAt(worldPos);
        return w;
    }
}