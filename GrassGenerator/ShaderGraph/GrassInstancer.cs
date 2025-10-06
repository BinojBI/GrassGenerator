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

    [SerializeField]
    private bool useGpuInstantiation = false;

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

        Bounds bounds = mesh.bounds;

        List<List<Matrix4x4>> matrices = new List<List<Matrix4x4>>();

        int batch = 0;
        List<Matrix4x4> newMatrixList = new List<Matrix4x4>();
        matrices.Add(newMatrixList);

        for (float x = bounds.center.x - bounds.size.x / 2; x < bounds.center.x + bounds.size.x / 2; x += 0.4f)
        {
            for (float z = bounds.center.z - bounds.size.z / 2; z < bounds.center.z + bounds.size.z / 2; z += 0.4f)
            {
                if (Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(x, z)) < renderRange)
                {
                    matrices[batch].Add(Matrix4x4.TRS(new Vector3(x, heights[(int)x, (int)z], z), Quaternion.identity, Vector3.one));

                }

                if (matrices[batch].Count > 1000){ 


            batch++;

                    List<Matrix4x4> newMatrixList1 = new List<Matrix4x4>();
                    matrices.Add(newMatrixList1);
                }
            }
        }

        for (int i = 0; i <= batch; i++)
        {
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
