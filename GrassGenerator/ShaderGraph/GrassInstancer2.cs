using UnityEngine;
using System.Collections.Generic;

public class GrassInstancer2 : MonoBehaviour
{
    [Header("Grass Settings")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public bool usePlayerDistance = false;
    public Transform player;
    public float renderRange = 20f;
    public float density = 1.0f; // how dense grass is per vertex (1 = every vertex)

    private MeshFilter meshFilter;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter)
        {
            Debug.LogError("No MeshFilter found! Please attach this script to your terrain mesh object.");
            return;
        }
    }

    void Update()
    {
        RenderGrassOnMesh();
    }

    void RenderGrassOnMesh()
    {
        Mesh mesh = meshFilter.sharedMesh;
        if (!mesh) return;

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        List<List<Matrix4x4>> matrices = new List<List<Matrix4x4>>();
        int batch = 0;
        matrices.Add(new List<Matrix4x4>());

        for (int i = 0; i < vertices.Length; i++)
        {
            // Optional: skip some vertices for performance
            if (Random.value > density)
                continue;

            Vector3 worldPos = transform.TransformPoint(vertices[i]); // convert to world
            Vector3 normal = transform.TransformDirection(normals[i]); // also convert normal

            bool withinRange = true;
            if (usePlayerDistance)
            {
                withinRange = Vector2.Distance(
                    new Vector2(player.position.x, player.position.z),
                    new Vector2(worldPos.x, worldPos.z)
                ) < renderRange;
            }

            if (withinRange)
            {
                // Orient grass to match surface normal
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
                matrices[batch].Add(Matrix4x4.TRS(worldPos, rot, Vector3.one * 0.4f));

                // Unity allows max 1023 instances per draw
                if (matrices[batch].Count >= 1023)
                {
                    batch++;
                    matrices.Add(new List<Matrix4x4>());
                }
            }
        }

        // Draw all batches
        for (int i = 0; i <= batch; i++)
        {
            if (matrices[i].Count > 0)
                Graphics.DrawMeshInstanced(grassMesh, 0, grassMaterial, matrices[i]);
        }
    }
}
