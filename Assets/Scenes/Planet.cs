using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Planet : MonoBehaviour {

    [Range(2,256)]
    public int resolution = 10;
    public bool autoUpdate = true;
    public enum FaceRenderMask { All, Top, Bottom, Left, Right, Front, Back };
    public FaceRenderMask faceRenderMask;

    public ShapeSettings shapeSettings;
    public ColourSettings colourSettings;

    // Drag your PlanetUnlit material in here
    public Material planetMaterial;

    [HideInInspector]
    public bool shapeSettingsFoldout;
    [HideInInspector]
    public bool colourSettingsFoldout;

    ShapeGenerator shapeGenerator;

    [SerializeField, HideInInspector]
    MeshFilter[] meshFilters;
    TerrainFace[] terrainFaces;
     

	void Initialize()
    {
        shapeGenerator = new ShapeGenerator(shapeSettings);

        if (meshFilters == null || meshFilters.Length == 0)
        {
            meshFilters = new MeshFilter[6];
        }
        terrainFaces = new TerrainFace[6];

        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i] == null)
            {
                GameObject meshObj = new GameObject("mesh");
                meshObj.transform.parent = transform;

                meshObj.AddComponent<MeshRenderer>();
                meshFilters[i] = meshObj.AddComponent<MeshFilter>();
                meshFilters[i].sharedMesh = new Mesh();
            }

            // Use your material on every face (falls back to Standard if empty)
            MeshRenderer meshRenderer = meshFilters[i].GetComponent<MeshRenderer>();
            if (planetMaterial != null)
            {
                meshRenderer.sharedMaterial = planetMaterial;
            }
            else if (meshRenderer.sharedMaterial == null)
            {
                meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
            }

            terrainFaces[i] = new TerrainFace(shapeGenerator, meshFilters[i].sharedMesh, resolution, directions[i]);
            bool renderFace = faceRenderMask == FaceRenderMask.All || (int)faceRenderMask - 1 == i;
            meshFilters[i].gameObject.SetActive(renderFace);
        }
    }

    public void GeneratePlanet()
    {
        Initialize();
        GenerateMesh();
        GenerateColours();
    }

    public void OnShapeSettingsUpdated()
    {
        if (autoUpdate)
        {
            Initialize();
            GenerateMesh();
        }
    }

    public void OnColourSettingsUpdated()
    {
        if (autoUpdate)
        {
            Initialize();
            GenerateColours();
        }
    }

    void GenerateMesh()
    {
        for (int i = 0; i < 6; i++)
        {
            if (meshFilters[i].gameObject.activeSelf)
            {
                terrainFaces[i].ConstructMesh();
            }
        }

        // After the mesh changes, send the new heights to the shader
        UpdateShaderHeights();
    }

    void GenerateColours()
    {
        // Only used by the old Standard material. Skipped for your shader.
        foreach (MeshFilter m in meshFilters)
        {
            Material mat = m.GetComponent<MeshRenderer>().sharedMaterial;
            if (mat != null && mat.HasProperty("_Color"))
            {
                mat.color = colourSettings.planetColour;
            }
        }

        UpdateShaderHeights();
    }

    // Finds the lowest and highest point of the planet and gives them to the shader
    void UpdateShaderHeights()
    {
        if (planetMaterial == null) return;

        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] == null || !meshFilters[i].gameObject.activeSelf) continue;

            Vector3[] verts = meshFilters[i].sharedMesh.vertices;
            for (int v = 0; v < verts.Length; v++)
            {
                float d = verts[v].magnitude; // distance from planet center
                if (d < min) min = d;
                if (d > max) max = d;
            }
        }

        if (min > max) return; // no vertices yet

        // Avoids dividing by zero on a perfectly smooth sphere
        if (Mathf.Approximately(min, max)) max = min + 0.001f;

        // Names must match the shader's property names exactly
        planetMaterial.SetFloat("Radius", 0f);
        planetMaterial.SetFloat("MinHeight", min);
        planetMaterial.SetFloat("MaxHeight", max);
    }
}