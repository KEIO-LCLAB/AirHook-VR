using System.Collections;
using UnityEngine;

public class CeilingRain : MonoBehaviour
{
    [Header("Area & Player")]
    public Collider area;
    public Transform headTransform;
    public bool drawGizmos = false;
    public bool debugLogs = false;

    [Header("Wind (via WindHub)")]
    public int windPriority = 1;
    public Vector2 intervalRange = new Vector2(0.35f, 0.9f);
    [Range(0, 1)] public float burstStrength = 0.7f;
    public float hold = 0.05f;
    public float fade = 0.35f;

    Coroutine _loop;
    bool _insideLast;

    void Awake()
    {
        if (!headTransform && Camera.main) headTransform = Camera.main.transform;
    }

    void OnEnable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator Loop()
    {
        int sustainHandle = 0;

        while (WindHub.Instance == null || area == null || headTransform == null) yield return null;

        while (true)
        {
            bool inside = IsInside(area, headTransform.position);

            if (WindHub.Instance.omniDirectional == false)
            {
                if (inside)
                {
                    if (sustainHandle == 0)
                    {
                        float[] strength = { burstStrength, 0, 0, 0 };
                        sustainHandle = WindHub.Instance.BeginSustain(strength, strength, windPriority);
                    }
                }
                else
                {
                    if (sustainHandle != 0)
                    {
                        WindHub.Instance.EndSustain(sustainHandle);
                        sustainHandle = 0;
                    }
                }
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                if (sustainHandle != 0) { WindHub.Instance.EndSustain(sustainHandle); sustainHandle = 0; }

                if (inside)
                {
                    FireOneDrop();
                    yield return new WaitForSeconds(Random.Range(intervalRange.x, intervalRange.y));
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
    }

    bool IsInside(Collider col, Vector3 pos)
    {
        Vector3 cp = col.ClosestPoint(pos);
        return (cp - pos).sqrMagnitude < 1e-6f;
    }

    void FireOneDrop()
    {
        float[] L = new float[4];
        float[] R = new float[4];

        switch (Random.Range(0, 4))
        {
            case 0: L[0] = burstStrength; break;
            case 1: R[0] = burstStrength; break;
            case 2: L[1] = burstStrength; break;
            default: R[1] = burstStrength; break;
        }

        const float tilt = 0.18f;
        if (L[0] > 0f) L[3] = Mathf.Max(L[3], L[0] * tilt);
        if (R[0] > 0f) R[3] = Mathf.Max(R[3], R[0] * tilt);
        if (L[1] > 0f) L[2] = Mathf.Max(L[2], L[1] * tilt);
        if (R[1] > 0f) R[2] = Mathf.Max(R[2], R[1] * tilt);

        WindHub.Instance.PostBurst(L, R, hold, fade, windPriority);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || area == null) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        var b = area.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}