using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class GrassLOD_GPU : MonoBehaviour
{
    [Header("References")]
    public ComputeShader lodComputeShader;
    public Mesh highMesh;
    public Mesh lowMesh;
    public Material grassMaterial;

    [Header("Settings")]
    public Transform player;
    public float lodDistance = 20f;
    public int randomSeed = 12345;
    [Range(0f, 1f)] public float density = 1f;    
    public float offsetRadius = 0.2f;
    public bool alignToNormal = true;
    public float yLift = 0.02f;

    private ComputeBuffer allMatricesBuffer;
    private ComputeBuffer highLODAppendBuffer;
    private ComputeBuffer lowLODAppendBuffer;
    private ComputeBuffer argsBufferHigh;
    private ComputeBuffer argsBufferLow;

    private int kernel;
    private uint threadGroupSize;
    private int grassCount;
    private Bounds bounds;
    private Matrix4x4[] cpuMatrices;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Random.InitState(randomSeed);
        //cpuMatrices = GenerateMatricesFromMesh(mf.sharedMesh, mf.transform);
        // Example grass data (replace with your generated matrices)
        Matrix4x4[] allMatrices = GenerateGrassMatrices(mf.sharedMesh, mf.transform);

        grassCount = allMatrices.Length;
        allMatricesBuffer = new ComputeBuffer(grassCount, Marshal.SizeOf(typeof(Matrix4x4)));
        allMatricesBuffer.SetData(allMatrices);
        Debug.Log($"First matrix pos: {allMatrices[0].GetColumn(3)}");


        highLODAppendBuffer = new ComputeBuffer(grassCount, Marshal.SizeOf(typeof(Matrix4x4)), ComputeBufferType.Append);
        lowLODAppendBuffer = new ComputeBuffer(grassCount, Marshal.SizeOf(typeof(Matrix4x4)), ComputeBufferType.Append);

        kernel = lodComputeShader.FindKernel("CSMainn");
        lodComputeShader.GetKernelThreadGroupSizes(kernel, out threadGroupSize, out _, out _);

        // Setup indirect draw args buffers
        argsBufferHigh = CreateArgsBuffer(highMesh);
        argsBufferLow = CreateArgsBuffer(lowMesh);

        bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }

    void Update()
    {
        // Reset counters
        highLODAppendBuffer.SetCounterValue(0);
        lowLODAppendBuffer.SetCounterValue(0);

        // Set compute shader parameters
        lodComputeShader.SetVector("_PlayerPos", player.position);
        lodComputeShader.SetFloat("_LodDistanceSqr", lodDistance * lodDistance);
        Debug.Log($"LOD distance squared: {lodDistance * lodDistance}");
        lodComputeShader.SetInt("_GrassCount", grassCount);
        Debug.Log($"Grass count: {grassCount}");

        lodComputeShader.SetBuffer(kernel, "_AllGrassMatrices", allMatricesBuffer);
        lodComputeShader.SetBuffer(kernel, "_HighLODTransforms", highLODAppendBuffer);
        lodComputeShader.SetBuffer(kernel, "_LowLODTransforms", lowLODAppendBuffer);

        // Dispatch compute shader
        int groups = Mathf.CeilToInt(grassCount / (float)threadGroupSize);
        lodComputeShader.Dispatch(kernel, groups, 1, 1);
        Debug.Log($"Dispatching {groups} thread groups for {grassCount} grass instances");

        // Get instance counts for DrawMeshInstancedIndirect
        uint[] argsData = new uint[5];
        argsBufferHigh.GetData(argsData);
        Debug.Log($"High LOD Args → indexCount: {argsData[0]}, instanceCount: {argsData[1]}");

        argsBufferLow.GetData(argsData);
        Debug.Log($"Low LOD Args → indexCount: {argsData[0]}, instanceCount: {argsData[1]}");


        // Draw both LODs
        grassMaterial.SetBuffer("_TransformMatrices", highLODAppendBuffer);
        Graphics.DrawMeshInstancedIndirect(highMesh, 0, grassMaterial, bounds, argsBufferHigh);

        grassMaterial.SetBuffer("_TransformMatrices", lowLODAppendBuffer);
        Graphics.DrawMeshInstancedIndirect(lowMesh, 0, grassMaterial, bounds, argsBufferLow);
    }

    private ComputeBuffer CreateArgsBuffer(Mesh mesh)
    {
        uint[] args = new uint[5];
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = 0; // instance count (set via counter later)
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        args[4] = 0;
        return new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private Matrix4x4[] GenerateGrassMatrices(Mesh mesh, Transform meshTransform)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals.Length == verts.Length ? mesh.normals : null;

        // estimate size (density)
        System.Collections.Generic.List<Matrix4x4> mats = new System.Collections.Generic.List<Matrix4x4>(verts.Length);

        for (int i = 0; i < verts.Length; i++)
        {
            if (Random.value > density) continue;

            Vector3 localPos = verts[i];

            // random offset around vertex in tangent plane
            Vector3 normal = normals != null ? normals[i] : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(normal, Vector3.right);
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            float r = Random.Range(0f, offsetRadius);
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offsetLocal = (Mathf.Cos(a) * tangent + Mathf.Sin(a) * bitangent).normalized * r;

            Vector3 worldPos = meshTransform.TransformPoint(localPos + offsetLocal);
            worldPos.y += yLift;

            // orientation
            Quaternion rot = Quaternion.identity;
            if (alignToNormal)
            {
                Vector3 worldNormal = meshTransform.TransformDirection(normal);
                float randomY = Random.Range(0f, 360f);
                rot = Quaternion.FromToRotation(Vector3.up, worldNormal) * Quaternion.Euler(0f, randomY, 0f);
            }
            else
            {
                rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            float s = Random.Range(0.8f, 1.2f);
            Vector3 scale = Vector3.one * s * 0.4f; // base scale multiplier

            mats.Add(Matrix4x4.TRS(worldPos, rot, scale));
        }

        return mats.ToArray();
    }

    void OnDestroy()
    {
        allMatricesBuffer?.Release();
        highLODAppendBuffer?.Release();
        lowLODAppendBuffer?.Release();
        argsBufferHigh?.Release();
        argsBufferLow?.Release();
    }

}
