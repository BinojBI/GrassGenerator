using UnityEngine;
using System.Collections.Generic;

public class GrassInstancer2 : MonoBehaviour
{
    [Header("Grass Settings")]
    public Mesh highPolyMesh;
    public Mesh lowPolyMesh;
    public Material grassMaterial;

    [Range(0f, 1f)] public float density = 1.0f;
    public float offsetRadius = 0.3f;

    [Header("LOD Settings")]
    public bool useLOD = true;
    public Transform player;
    public float lodDistance = 15f; // threshold for highpoly switch
    [SerializeField] float lodUpdateInterval = 0.5f;
    private float lodTimer = 0f;

    private MeshFilter meshFilter;

    // store grass transforms permanently
    private List<Vector3> grassPositions = new List<Vector3>();
    private List<Quaternion> grassRotations = new List<Quaternion>();
    private List<Vector3> grassScales = new List<Vector3>();

    // reused each frame
    private List<List<Matrix4x4>> highPolyBatches = new List<List<Matrix4x4>>();
    private List<List<Matrix4x4>> lowPolyBatches = new List<List<Matrix4x4>>();

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter)
        {
            Debug.LogError("No MeshFilter found on this GameObject!");
            return;
        }

        GenerateGrassDataOnce();
    }

    void Update()
    {
        lodTimer += Time.deltaTime;
        if (lodTimer >= lodUpdateInterval)
        {
            UpdateGrassLOD();
            lodTimer = 0f;
        }

        RenderGrass();
    }

    // --------------------------------------------------------------------------
    // Generate base grass transform data once (positions, rotations, scales)
    // --------------------------------------------------------------------------
    void GenerateGrassDataOnce()
    {
        Mesh mesh = meshFilter.sharedMesh;
        if (!mesh) return;

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        grassPositions.Clear();
        grassRotations.Clear();
        grassScales.Clear();

        for (int i = 0; i < vertices.Length; i++)
        {
            if (Random.value > density)
                continue;

            Vector3 vertex = vertices[i];
            Vector3 normal = normals[i];

            // --- random offset around vertex ---
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            float radius = Random.Range(0f, offsetRadius);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = (Mathf.Cos(angle) * tangent + Mathf.Sin(angle) * bitangent).normalized * radius;

            Vector3 localPos = vertex + offset;
            Vector3 worldPos = transform.TransformPoint(localPos);
            Vector3 worldNormal = transform.TransformDirection(normal);

            // --- random rotation & scale ---
            float randomYRot = Random.Range(0f, 360f);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, worldNormal) * Quaternion.Euler(0, randomYRot, 0);
            float randomScale = Random.Range(0.8f, 1.2f);
            Vector3 scale = Vector3.one * randomScale * 0.4f;

            grassPositions.Add(worldPos);
            grassRotations.Add(rotation);
            grassScales.Add(scale);
        }

        Debug.Log($"Generated {grassPositions.Count} grass blades.");
    }

    // --------------------------------------------------------------------------
    // Each frame: decide which grass is high or low poly
    // --------------------------------------------------------------------------
    void UpdateGrassLOD()
    {
        highPolyBatches.Clear();
        lowPolyBatches.Clear();

        if (!useLOD || player == null)
        {
            // all lowpoly
            CreateBatches(grassPositions, grassRotations, grassScales, lowPolyBatches);
            return;
        }

        // dynamic distance check
        List<Matrix4x4> highMatrices = new List<Matrix4x4>();
        List<Matrix4x4> lowMatrices = new List<Matrix4x4>();

        for (int i = 0; i < grassPositions.Count; i++)
        {
            float dist = Vector3.Distance(player.position, grassPositions[i]);
            Matrix4x4 mat = Matrix4x4.TRS(grassPositions[i], grassRotations[i], grassScales[i]);

            if (dist < lodDistance)
                highMatrices.Add(mat);
            else
                lowMatrices.Add(mat);
        }

        CreateBatches(highMatrices, highPolyBatches);
        CreateBatches(lowMatrices, lowPolyBatches);
    }

    // --------------------------------------------------------------------------
    void CreateBatches(List<Vector3> pos, List<Quaternion> rot, List<Vector3> scale, List<List<Matrix4x4>> dst)
    {
        dst.Clear();
        List<Matrix4x4> current = new List<Matrix4x4>();
        for (int i = 0; i < pos.Count; i++)
        {
            current.Add(Matrix4x4.TRS(pos[i], rot[i], scale[i]));
            if (current.Count >= 1023)
            {
                dst.Add(current);
                current = new List<Matrix4x4>();
            }
        }
        if (current.Count > 0) dst.Add(current);
    }

    void CreateBatches(List<Matrix4x4> src, List<List<Matrix4x4>> dst)
    {
        dst.Clear();
        for (int i = 0; i < src.Count; i += 1023)
        {
            int count = Mathf.Min(1023, src.Count - i);
            dst.Add(src.GetRange(i, count));
        }
    }

    // --------------------------------------------------------------------------
    void RenderGrass()
    {
        foreach (var batch in highPolyBatches)
            Graphics.DrawMeshInstanced(highPolyMesh, 0, grassMaterial, batch);

        foreach (var batch in lowPolyBatches)
            Graphics.DrawMeshInstanced(lowPolyMesh, 0, grassMaterial, batch);
    }
}
