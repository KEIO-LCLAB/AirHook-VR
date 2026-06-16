using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RailExtruder : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;

    [Header("Sampling")]
    public float segmentsPerUnit = 6f;
    public int coarseSamples = 256;

    [Header("Rail Shape")]
    public float railWidth = 0.06f;
    public float railHeight = 0.06f;

    [Header("Placement")]
    public float halfGauge = 0.35f;
    public bool generateLeft = true;
    public bool generateRight = true;

    [Header("Rebuild")]
    public bool autoRebuild = true;
    public bool closeLoop = true;

    MeshFilter _mf;
    Mesh _mesh;

    void OnEnable()
    {
        EnsureMesh();
        Rebuild();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        EnsureMesh();
        if (autoRebuild) Rebuild();
    }

    void EnsureMesh()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "RailExtrudedMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _mf.sharedMesh = _mesh;
        }
    }

    Spline GetSpline()
    {
        if (splineContainer == null) return null;
        if (splineIndex < 0 || splineIndex >= splineContainer.Splines.Count) splineIndex = 0;
        return splineContainer.Splines[splineIndex];
    }

    float EstimateWorldLength(Spline spline, Transform tf, int samples)
    {
        if (samples < 2) samples = 2;
        float len = 0f;

        Vector3 PrevPoint()
        {
            float3 p0 = SplineUtility.EvaluatePosition(spline, 0f);
            return tf ? tf.TransformPoint((Vector3)p0) : (Vector3)p0;
        }

        Vector3 prev = PrevPoint();
        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            float3 p = SplineUtility.EvaluatePosition(spline, t);
            Vector3 wp = tf ? tf.TransformPoint((Vector3)p) : (Vector3)p;
            len += Vector3.Distance(prev, wp);
            prev = wp;
        }
        return len;
    }

    public void Rebuild()
    {
        var spline = GetSpline();
        if (spline == null) { _mesh.Clear(); return; }

        var tf = splineContainer ? splineContainer.transform : null;

        float length = Mathf.Max(0.01f, EstimateWorldLength(spline, tf, Mathf.Max(32, coarseSamples)));

        int rings = Mathf.Max(2, Mathf.CeilToInt(length * Mathf.Max(0.5f, segmentsPerUnit)) + 1);

        Vector3[] centers = new Vector3[rings];
        Vector3[] tangents = new Vector3[rings];
        Vector3[] ups = new Vector3[rings];
        Vector3[] rights = new Vector3[rings];

        for (int i = 0; i < rings; i++)
        {
            float t = (float)i / (rings - 1);

            float3 pLocal = SplineUtility.EvaluatePosition(spline, t);
            float3 tanLocal = SplineUtility.EvaluateTangent(spline, t);
            float3 upLocal = SplineUtility.EvaluateUpVector(spline, t);

            Vector3 p = tf ? tf.TransformPoint((Vector3)pLocal) : (Vector3)pLocal;
            Vector3 tan = tf ? tf.TransformDirection((Vector3)tanLocal) : (Vector3)tanLocal;
            Vector3 up = tf ? tf.TransformDirection((Vector3)upLocal) : (Vector3)upLocal;

            if (tan.sqrMagnitude < 1e-8f) tan = Vector3.forward;
            if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
            tan.Normalize();
            up.Normalize();

            Vector3 right = Vector3.Cross(up, tan).normalized;

            centers[i] = p;
            tangents[i] = tan;
            ups[i] = up;
            rights[i] = right;
        }

        var lateralOffsets = new List<float>(2);
        if (generateLeft) lateralOffsets.Add(-halfGauge);
        if (generateRight) lateralOffsets.Add(+halfGauge);

        int railsCount = lateralOffsets.Count;
        int vertsPerRingPerRail = 4;

        var vertices = new List<Vector3>(rings * vertsPerRingPerRail * railsCount);
        var normalsVS = new List<Vector3>(rings * vertsPerRingPerRail * railsCount);
        var uvs = new List<Vector2>(rings * vertsPerRingPerRail * railsCount);
        var indices = new List<int>();

        float halfW = railWidth * 0.5f;
        float halfH = railHeight * 0.5f;

        for (int r = 0; r < railsCount; r++)
        {
            float lateral = lateralOffsets[r];

            for (int i = 0; i < rings; i++)
            {
                Vector3 c = centers[i] + rights[i] * lateral;
                Vector3 N = rights[i];
                Vector3 U = ups[i];

                Vector3 v0 = c - N * halfW - U * halfH;
                Vector3 v1 = c - N * halfW + U * halfH;
                Vector3 v2 = c + N * halfW + U * halfH;
                Vector3 v3 = c + N * halfW - U * halfH;

                vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);

                normalsVS.Add(U); normalsVS.Add(U); normalsVS.Add(U); normalsVS.Add(U);

                float uCoord = (float)i / (rings - 1) * length;
                uvs.Add(new Vector2(uCoord, 0));
                uvs.Add(new Vector2(uCoord, 1));
                uvs.Add(new Vector2(uCoord, 2));
                uvs.Add(new Vector2(uCoord, 3));
            }
        }

        int ringStride = vertsPerRingPerRail;
        int railStride = rings * ringStride;
        int maxI = closeLoop ? rings : (rings - 1);

        for (int r = 0; r < railsCount; r++)
        {
            int railBase = r * railStride;

            for (int i = 0; i < maxI - 1; i++)
            {
                int i0 = railBase + i * ringStride;
                int i1 = railBase + ((i + 1) % rings) * ringStride;

                AddQuad(i0 + 0, i0 + 1, i1 + 1, i1 + 0, indices);
                AddQuad(i0 + 1, i0 + 2, i1 + 2, i1 + 1, indices);
                AddQuad(i0 + 2, i0 + 3, i1 + 3, i1 + 2, indices);
                AddQuad(i0 + 3, i0 + 0, i1 + 0, i1 + 3, indices);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetNormals(normalsVS);
        _mesh.SetUVs(0, uvs);
        _mesh.SetTriangles(indices, 0, true);
        _mesh.RecalculateBounds();
    }

    static void AddQuad(int a, int b, int c, int d, List<int> list)
    {
        list.Add(a); list.Add(b); list.Add(c);
        list.Add(a); list.Add(c); list.Add(d);
    }
}