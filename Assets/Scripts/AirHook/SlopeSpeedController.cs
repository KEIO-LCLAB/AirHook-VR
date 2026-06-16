using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineAnimate))]
public class SlopeSpeedControllerCustom : MonoBehaviour
{
    [Header("Path")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;
    public bool closedLoop = false;

    [Header("Base Speeds (m/s) by Phase")]
    public float baseStart = 6f;
    public float baseAfterFirstDown = 7.5f;
    public float baseAfterSecondDown = 6f;
    public float baseAfterFirstUphillFlat = 5.5f;

    [Header("Slope ¡ú Speed Multiplier")]
    public AnimationCurve speedBySlope = AnimationCurve.EaseInOut(-1, 0.7f, 1, 1.55f);
    public Vector3 gravityDir = Vector3.down;

    public float slopeEnterDown = 0.08f;
    public float slopeExitDown = 0.04f;
    public float slopeEnterUp = -0.08f;
    public float slopeExitUp = -0.04f;

    [Header("Acceleration model (m/s^2)")]
    public float accelBase = 6f;
    public float accelDownhillK = 18f;
    public float decelBase = 8f;
    public float decelUphillK = 10f;

    [Header("Downhill burst at entry")]
    public bool downhillBurst = true;
    public float downhillBurstAccel = 12f;
    public float downhillBurstTime = 0.6f;

    [Header("Corner slowdown (Left only)")]
    public float lookAheadT = 0.015f;
    public float leftTurnTriggerAngle = 6f;
    [Range(0f, 0.9f)] public float leftTurnAccelReduceMax = 0.35f;
    public float leftTurnMaxAngle = 40f;

    [Header("Start/End Ease")]
    public bool easeOutAtEnd = true;
    [Range(0f, 0.2f)]
    public float endEaseZone = 0.06f;

    [Header("Runtime Length Recalc")]
    public bool recalcLengthRuntime = true;
    public float recalcInterval = 1.0f;

    [Header("Limits")]
    public float minSpeed = 0.5f;
    public float maxSpeed = 18f;

    private SplineAnimate anim;
    private Spline spline;
    private Transform tf;
    private float pathLength = 1f;
    private float t;
    private float curSpeed;
    public float CurrentSpeed => curSpeed;
    public bool IsStopped { get; private set; }

    private int downhillCount = 0;
    private bool wasDownhillState = false;
    private bool wasUphillState = false;

    private bool isDownhillState = false;
    private bool isUphillState = false;

    private bool armedSlowFlat = false;
    private bool slowFlatActive = false;
    private bool firstUphillAfterFirstDownDone = false;

    private bool burstActive = false;
    private float burstTimer = 0f;

    private float recalcTimer = 0f;

    void Awake()
    {
        anim = GetComponent<SplineAnimate>();
        if (splineContainer == null) splineContainer = anim.Container;
        tf = splineContainer ? splineContainer.transform : null;
        spline = (splineContainer && splineIndex >= 0 && splineIndex < splineContainer.Splines.Count)
            ? splineContainer.Splines[splineIndex] : null;

        anim.PlayOnAwake = false;
        anim.Pause();

        t = anim.NormalizedTime;
        curSpeed = 0f;
        pathLength = Mathf.Max(EstimateWorldLength(768), 0.01f);
        IsStopped = false;
    }

    void Update()
    {
        if (spline == null) return;

        if (recalcLengthRuntime)
        {
            recalcTimer += Time.deltaTime;
            if (recalcTimer >= recalcInterval)
            {
                pathLength = Mathf.Max(EstimateWorldLength(768), 0.01f);
                recalcTimer = 0f;
            }
        }

        Vector3 tan = GetWorldTangent(t).normalized;
        Vector3 g = gravityDir.sqrMagnitude > 0 ? gravityDir.normalized : Vector3.down;
        float slopeFactor = Vector3.Dot(tan, g);
        float slopePos = Mathf.Clamp01(slopeFactor);
        float slopeNeg = Mathf.Clamp01(-slopeFactor);

        if (!isDownhillState && slopeFactor > slopeEnterDown) isDownhillState = true;
        if (isDownhillState && slopeFactor < slopeExitDown) isDownhillState = false;

        if (!isUphillState && slopeFactor < slopeEnterUp) isUphillState = true;
        if (isUphillState && slopeFactor > slopeExitUp) isUphillState = false;

        bool isDown = isDownhillState;
        bool isUp = isUphillState;
        bool isFlat = !isDown && !isUp;

        if (isDown && !wasDownhillState)
        {
            downhillCount = Mathf.Min(downhillCount + 1, 2);
            slowFlatActive = false;

            if (downhillBurst)
            {
                burstActive = true;
                burstTimer = 0f;
            }
        }
        wasDownhillState = isDown;

        if (isUp && !wasUphillState && downhillCount >= 1 && !firstUphillAfterFirstDownDone)
            armedSlowFlat = true;

        if (isFlat && armedSlowFlat && !firstUphillAfterFirstDownDone)
        {
            slowFlatActive = true;
            armedSlowFlat = false;
            firstUphillAfterFirstDownDone = true;
        }
        wasUphillState = isUp;

        float baseV = (slowFlatActive && isFlat)
            ? baseAfterFirstUphillFlat
            : (downhillCount == 0) ? baseStart
              : (downhillCount == 1) ? baseAfterFirstDown
              : baseAfterSecondDown;

        float slopeMul = Mathf.Clamp(speedBySlope.Evaluate(Mathf.Clamp(slopeFactor, -1f, 1f)), 0.1f, 5f);

        float cornerAccelMul = 1f;
        float signedTurn = GetSignedTurnAngleClamped(t, lookAheadT);
        if (signedTurn > leftTurnTriggerAngle)
        {
            float a = Mathf.Clamp01(signedTurn / Mathf.Max(1e-3f, leftTurnMaxAngle));
            float reduce = leftTurnAccelReduceMax * a;
            cornerAccelMul = Mathf.Clamp01(1f - reduce);
        }

        float target = Mathf.Clamp(baseV * slopeMul, minSpeed, maxSpeed);

        if (!closedLoop && easeOutAtEnd && !isDown)
        {
            float zoneStart = Mathf.Clamp01(1f - Mathf.Max(0.0001f, endEaseZone));
            float k = Mathf.InverseLerp(zoneStart, 1f, t);
            if (k > 0f)
            {
                float easeMul = 1f - k;
                target = Mathf.Max(minSpeed * 0.5f, target * Mathf.Clamp01(easeMul));
            }
        }

        float accelLimit = (accelBase + accelDownhillK * slopePos) * cornerAccelMul;
        float decelLimit = (decelBase + decelUphillK * slopeNeg);

        if (burstActive)
        {
            accelLimit += downhillBurstAccel;
            burstTimer += Time.deltaTime;
            if (burstTimer >= downhillBurstTime || !isDown) burstActive = false;
        }

        if (isDown && target < curSpeed) target = curSpeed;

        if (target > curSpeed)
            curSpeed = Mathf.Min(curSpeed + accelLimit * Time.deltaTime, target);
        else
            curSpeed = Mathf.Max(curSpeed - decelLimit * Time.deltaTime, target);

        t = AdvanceT(t, curSpeed, pathLength, Time.deltaTime);
        anim.NormalizedTime = t;
    }

    float AdvanceT(float tNow, float speed, float length, float dt)
    {
        if (length < 1e-3f) return tNow;
        float add = (speed / length) * dt;
        float tNext = tNow + add;

        if (closedLoop)
        {
            tNext -= Mathf.Floor(tNext);
        }
        else
        {
            if (tNext >= 1f)
            {
                tNext = 1f;
                curSpeed = 0f;
                IsStopped = true;
                anim.NormalizedTime = tNext;
                anim.Pause();
                enabled = false;
                return tNext;
            }
            else
            {
                tNext = Mathf.Clamp01(tNext);
            }
        }
        return tNext;
    }

    float EstimateWorldLength(int samples)
    {
        if (spline == null) return 0f;
        samples = Mathf.Max(64, samples);
        Vector3 prev = GetWorldPos(0f);
        float len = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float tt = (float)i / samples;
            Vector3 p = GetWorldPos(tt);
            len += Vector3.Distance(prev, p);
            prev = p;
        }
        return len;
    }

    Vector3 GetWorldPos(float tt)
    {
        var pLocal = SplineUtility.EvaluatePosition(spline, tt);
        return tf ? tf.TransformPoint((Vector3)pLocal) : (Vector3)pLocal;
    }

    Vector3 GetWorldTangent(float tt)
    {
        var tLocal = SplineUtility.EvaluateTangent(spline, tt);
        Vector3 v = tf ? tf.TransformDirection((Vector3)tLocal) : (Vector3)tLocal;
        return (v.sqrMagnitude < 1e-8f) ? Vector3.forward : v.normalized;
    }

    float GetSignedTurnAngleClamped(float tt, float lookAhead)
    {
        float t1 = tt;
        float maxAhead = closedLoop ? lookAhead : Mathf.Max(0f, Mathf.Min(lookAhead, 1f - tt));
        float t2 = closedLoop ? tt + lookAhead : tt + maxAhead;
        Vector3 upRef = Vector3.up;
        Vector3 a = GetWorldTangent(t1);
        Vector3 b = GetWorldTangent(t2);
        return Vector3.SignedAngle(a, b, upRef);
    }

    void OnDisable()
    {
        curSpeed = 0f;
        IsStopped = true;
        if (anim != null) anim.Pause();
    }

}