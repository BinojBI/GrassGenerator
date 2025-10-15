using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class GrassLOD_GPU : MonoBehaviour
{
    [Header("References")]
    public ComputeShader lodComputeShader;
    public Mesh highMesh;
    public Mesh lowMesh;
    public Material grassMaterial; // shader must read StructuredBuffer<float4x4> _TransformMatrices

    [Header("Settings")]
    public Transform player;
    public float lodDistance = 20f;
    [Range(0f, 1f)] public float density = 1f;      // 1 = every vertex, <1 = sample subset
    public float offsetRadius = 0.2f;               // random offset from vertex in tangent plane
    public bool alignToNormal = true;
    public float yLift = 0.02f;                     // tiny lift to avoid z-fighting
    public int randomSeed = 12345;

    // GPU buffers
    private ComputeBuffer allMatricesBuffer;
    private ComputeBuffer highLODAppendBuffer;
    private ComputeBuffer lowLODAppendBuffer;
    private ComputeBuffer argsBufferHigh;
    private ComputeBuffer argsBufferLow;

    // runtime data
    private int kernel;
    private uint threadGroupSize;
    private int grassCount;
    private Bounds drawBounds;

    // store generated transforms on CPU (stable)
    private Matrix4x4[] cpuMatrices;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("GrassLOD_GPU: attach to GameObject with MeshFilter and assign imported mesh.");
            enabled = false;
            return;
        }

        // deterministic randomness
        Random.InitState(randomSeed);

        // Generate matrices from imported mesh vertices (world space)
        cpuMatrices = GenerateMatricesFromMesh(mf.sharedMesh, mf.transform);
        grassCount = cpuMatrices.Length;
        Debug.Log($"Generated {grassCount} grass matrices.");

        // Upload allMatricesBuffer
        allMatricesBuffer = new ComputeBuffer(grassCount, Marshal.SizeOf(typeof(Matrix4x4)));
        allMatricesBuffer.SetData(cpuMatrices);

        // Create Append buffers (stride = Matrix4x4)
        int matStride = Marshal.SizeOf(typeof(Matrix4x4));
        highLODAppendBuffer = new ComputeBuffer(grassCount, matStride, ComputeBufferType.Append);
        lowLODAppendBuffer = new ComputeBuffer(grassCount, matStride, ComputeBufferType.Append);

        // Create args buffers and initialize args[0] = mesh index count
        argsBufferHigh = CreateArgsBuffer(highMesh);
        argsBufferLow = CreateArgsBuffer(lowMesh);

        // find kernel and thread size
        kernel = lodComputeShader.FindKernel("CSMain");
        lodComputeShader.GetKernelThreadGroupSizes(kernel, out threadGroupSize, out _, out _);

        // Big draw bounds (adjust if needed)
        drawBounds = new Bounds(transform.position + Vector3.up * 10f, Vector3.one * 10000f);
    }

    void Update()
    {
        if (grassCount == 0) return;

        // reset append counters
        highLODAppendBuffer.SetCounterValue(0);
        lowLODAppendBuffer.SetCounterValue(0);

        // set compute shader buffers & uniforms
        lodComputeShader.SetBuffer(kernel, "_AllGrassMatrices", allMatricesBuffer);
        lodComputeShader.SetBuffer(kernel, "_HighLODTransforms", highLODAppendBuffer);
        lodComputeShader.SetBuffer(kernel, "_LowLODTransforms", lowLODAppendBuffer);

        Vector3 playerPos = player != null ? player.position : Vector3.zero;
        lodComputeShader.SetVector("_PlayerPos", playerPos);
        lodComputeShader.SetFloat("_LodDistanceSqr", lodDistance * lodDistance);
        lodComputeShader.SetInt("_GrassCount", grassCount);

        // dispatch
        int groups = Mathf.Max(1, Mathf.CeilToInt(grassCount / (float)threadGroupSize));
        lodComputeShader.Dispatch(kernel, groups, 1, 1);

        // copy counts into args buffer (args[1] = instanceCount)
        ComputeBuffer.CopyCount(highLODAppendBuffer, argsBufferHigh, 4);
        ComputeBuffer.CopyCount(lowLODAppendBuffer, argsBufferLow, 4);

        // read args for debug
        uint[] argsData = new uint[5];
        argsBufferHigh.GetData(argsData);
        Debug.Log($"High LOD Args → indexCount: {argsData[0]}, instanceCount: {argsData[1]}");
        argsBufferLow.GetData(argsData);
        Debug.Log($"Low LOD Args  → indexCount: {argsData[0]}, instanceCount: {argsData[1]}");

        // Bind buffers to material and draw (only if >0 instances)
        grassMaterial.SetBuffer("_TransformMatrices", highLODAppendBuffer);
        if (argsData[1] > 0)
            Graphics.DrawMeshInstancedIndirect(highMesh, 0, grassMaterial, drawBounds, argsBufferHigh);

        // read low args count again (or reuse previous read if cached)
        argsBufferLow.GetData(argsData);
        grassMaterial.SetBuffer("_TransformMatrices", lowLODAppendBuffer);
        if (argsData[1] > 0)
            Graphics.DrawMeshInstancedIndirect(lowMesh, 0, grassMaterial, drawBounds, argsBufferLow);
    }

    private ComputeBuffer CreateArgsBuffer(Mesh mesh)
    {
        uint[] args = new uint[5];
        args[0] = (uint)mesh.GetIndexCount(0); // index count per instance
        args[1] = 0;                           // instance count (filled by CopyCount)
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        args[4] = 0;
        var cb = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        cb.SetData(args);
        return cb;
    }

    private Matrix4x4[] GenerateMatricesFromMesh(Mesh mesh, Transform meshTransform)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals.Length == verts.Length ? mesh.normals : null;

        List<Matrix4x4> mats = new List<Matrix4x4>(verts.Length);

        for (int i = 0; i < verts.Length; i++)
        {
            if (Random.value > density) continue;

            Vector3 localPos = verts[i];
            Vector3 normal = normals != null ? normals[i] : Vector3.up;

            // tangent plane for random spread
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            float r = Random.Range(0f, offsetRadius);
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offsetLocal = (Mathf.Cos(a) * tangent + Mathf.Sin(a) * bitangent).normalized * r;

            Vector3 worldPos = meshTransform.TransformPoint(localPos + offsetLocal);
            Vector3 worldNormal = meshTransform.TransformDirection(normal).normalized;

            // random rotation
            float randomY = Random.Range(0f, 360f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, worldNormal) * Quaternion.Euler(0f, randomY, 0f);

            // scale
            float s = Random.Range(0.8f, 1.2f);
            Vector3 scale = Vector3.one * s * 0.18f;

            // get grass height (approximation for grounding)
            float grassHeight = 1.0f * scale.y; // 1.0f = base mesh height in local units
                                                // move the pivot slightly downward along the surface normal
            worldPos -= worldNormal * (grassHeight * 0.5f);
            mats.Add(Matrix4x4.TRS(worldPos, rot, scale));
        }

        return mats.ToArray();
    }


    void OnDisable()
    {
        allMatricesBuffer?.Release();
        highLODAppendBuffer?.Release();
        lowLODAppendBuffer?.Release();
        argsBufferHigh?.Release();
        argsBufferLow?.Release();
    }
}
