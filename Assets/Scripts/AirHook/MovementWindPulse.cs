using UnityEngine;

public class MovementWindPulse : MonoBehaviour
{
    [Header("Refs")]
    public Transform trackedTransform;

    [Header("Wind Pulse")]
    [Range(0f, 1f)] public float strength = 0.7f;
    public float fadeSeconds = 0.5f;
    public int windPriority = 5;

    [Header("Debounce")]
    public float minInterval = 0.1f;
    public float minDelta = 0.001f;

    Vector3 _lastPos;
    bool _hasLast;
    float _lastFireTime = -999f;
    bool _disabledForever = false;

    void Reset()
    {
        trackedTransform = transform;
    }

    public void BeginElevator() { _disabledForever = true; }

    void LateUpdate()
    {
        if (_disabledForever) return;
        if (WindHub.Instance == null) return;

        var t = trackedTransform ? trackedTransform : transform;
        var cur = t.position;

        if (!_hasLast) { _lastPos = cur; _hasLast = true; return; }

        var delta = cur - _lastPos;
        if (delta.sqrMagnitude >= minDelta * minDelta &&
            (Time.time - _lastFireTime) >= minInterval)
        {
            FireFrontWind();
            _lastFireTime = Time.time;
        }

        _lastPos = cur;
    }

    void FireFrontWind()
    {

        var L = new float[4];
        var R = new float[4];

        L[0] = strength; L[3] = strength;
        R[0] = strength; R[3] = strength;

        WindHub.Instance.PostBurst(L, R, 0f, Mathf.Max(0.05f, fadeSeconds), windPriority);
    }
}