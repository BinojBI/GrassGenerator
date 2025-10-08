using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Unity.VisualScripting;

public class GrassInstancer : MonoBehaviour
{
    [SerializeField] Mesh grassMesh;
    [SerializeField] Material grassMaterial;
    [SerializeField] GameObject grassPrefab;

    private TerrainGenerator terrainGenerator;
    private MeshFilter meshFilter;
    [SerializeField]  private Transform player;
    [SerializeField]  private float renderRange = 10;

    [SerializeField] private bool useGpuInstantiation = true;
    [SerializeField] private bool usePlayerDistance = true;

    private float[,] heights;

    void Start()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();
        meshFilter = GetComponent<MeshFilter>();
    }

    void Update()
    {
        if (useGpuInstantiation) {
            RenderGrassGPUInstantiated();
        }
        else
        {
            IRenderGrassNonInstantiated();
        }
    }

    void RenderGrassGPUInstantiated()
    {
        heights = terrainGenerator.heights;
        Mesh mesh = meshFilter.sharedMesh;

        List<List<Matrix4x4>> matrices = new List<List<Matrix4x4>>();
        int batch = 0;
        matrices.Add(new List<Matrix4x4>());

        for (float x = 0; x < terrainGenerator.XSize; x += 0.4f)
        {
            for (float z = 0; z < terrainGenerator.ZSize; z += 0.4f)
            {
                int xi = Mathf.Clamp(Mathf.RoundToInt(x), 0, heights.GetLength(0) - 1);
                int zi = Mathf.Clamp(Mathf.RoundToInt(z), 0, heights.GetLength(1) - 1);
                float y = heights[xi, zi];

                Vector3 localPos = new Vector3(x, y, z);
                Vector3 worldPos = terrainGenerator.transform.TransformPoint(localPos);

                bool withinRange = !usePlayerDistance ||
                    Vector2.Distance(
                        new Vector2(player.position.x, player.position.z),
                        new Vector2(worldPos.x, worldPos.z)
                    ) < renderRange;

                if (withinRange)
                {
                    matrices[batch].Add(Matrix4x4.TRS(worldPos, Quaternion.identity, new Vector3(0.2f,0.5f,0.2f)));

                    if (matrices[batch].Count >= 1023)
                    {
                        batch++;
                        matrices.Add(new List<Matrix4x4>());
                    }
                }
            }
        }

        for (int i = 0; i <= batch; i++)
        {
            if (matrices[i].Count > 0)
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, matrices[i]);
        }
    }


    void IRenderGrassNonInstantiated()
    {

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }


        heights = terrainGenerator.heights;

        Mesh mesh = meshFilter.sharedMesh;

        Bounds bounds = mesh.bounds;

        for (float x = bounds.center.x - bounds.size.x / 2; x < bounds.center.x + bounds.size.x / 2; x += 0.4f)
        {
            for (float z = bounds.center.z - bounds.size.z / 2; z < bounds.center.z + bounds.size.z / 2; z += 0.4f)
            {
                if (Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z)) < renderRange)
                {
                    Instantiate(grassPrefab, new Vector3(x, heights[(int)x, (int)z], z), Quaternion.identity, transform);
                }
            }
        }
    }

}
