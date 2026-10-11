using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetBuilder : MonoBehaviour
{
    [Min(100f)] public float radiusM = 10000f;
    [Range(16, 400)] public int resolution = 200;
    public float uvTiling = 50f;

    public float terrainHeightM = 0f;
    public float terrainFrequency = 6f;
    public int seed = 1;

    void Awake()
    {
        Mesh mesh = Build();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        var sphere = GetComponent<SphereCollider>();
        var meshCol = GetComponent<MeshCollider>();
        if (terrainHeightM > 0f)
        {
            if (sphere != null) sphere.enabled = false;
            if (meshCol == null) meshCol = gameObject.AddComponent<MeshCollider>();
            meshCol.enabled = true;
            meshCol.sharedMesh = mesh;
        }
        else
        {
            if (meshCol != null) meshCol.enabled = false;
            if (sphere == null) sphere = gameObject.AddComponent<SphereCollider>();
            sphere.enabled = true;
            sphere.center = Vector3.zero;
            sphere.radius = radiusM;
        }

        var link = GetComponent<PlanetLink>();
        if (link != null) link.SetManualRadius(radiusM);
    }

    float Height(Vector3 dir)
    {
        if (terrainHeightM <= 0f) return 0f;
        Vector3 p = dir * terrainFrequency + new Vector3(100f + seed * 17.3f, 100f + seed * 9.1f, 100f + seed * 5.7f);
        float n = (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y, p.z) + Mathf.PerlinNoise(p.z, p.x)) / 3f;
        return (n - 0.5f) * 2f * terrainHeightM;
    }

    Mesh Build()
    {
        // The mesh collider gets heavy, so cap the detail when terrain is on
        int res = terrainHeightM > 0f ? Mathf.Min(resolution, 150) : resolution;

        Vector3[] faces = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int n = res + 1;
        var verts = new Vector3[6 * n * n];
        var normals = new Vector3[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[6 * res * res * 6];
        int v = 0, t = 0;

        for (int f = 0; f < 6; f++)
        {
            Vector3 up = faces[f];
            Vector3 axisA = new Vector3(up.y, up.z, up.x);
            Vector3 axisB = Vector3.Cross(up, axisA);
            int baseIndex = v;

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float px = (float)x / res * 2f - 1f;
                    float py = (float)y / res * 2f - 1f;
                    Vector3 dir = (up + axisA * px + axisB * py).normalized;
                    verts[v] = dir * (radiusM + Height(dir));
                    normals[v] = dir;
                    uvs[v] = new Vector2((float)x / res, (float)y / res) * uvTiling;
                    v++;
                }

            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int i = baseIndex + x + y * n;
                    tris[t++] = i; tris[t++] = i + n + 1; tris[t++] = i + n;
                    tris[t++] = i; tris[t++] = i + 1; tris[t++] = i + n + 1;
                }
        }

        var mesh = new Mesh { name = "PlanetMesh", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        if (terrainHeightM > 0f) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}