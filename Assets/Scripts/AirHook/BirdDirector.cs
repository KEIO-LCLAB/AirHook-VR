using System.Collections;
using UnityEngine;

public class BirdDirector : MonoBehaviour
{
    [Header("References")]
    public Transform headTransform;        // 围绕的圆心（玩家头/相机）
    public Transform graphics;             // 可视/骨骼根（应为“robin”，带 Animator）
    public Animator animator;              // 绑定在 graphics 或其子节点上
    public AudioSource audioSource;        // 可选：3D 音频

    [Header("Path & Timing")]
    public float ringRadius = 1.2f;        // 围头半径（m）
    public float ringHeight = 0.4f;        // 围头时相对头的高度（m）
    public int laps = 3;                   // 转几圈
    public float approachDistance = 8f;    // 入场起点离头部距离（m）
    public float exitDistance = 8f;        // 离场终点离头部距离（m）
    public float approachSpeed = 5.0f;     // 入场线速度（m/s）
    public float orbitAngularSpeed = -180f; // 围圈角速度（deg/s，+逆时针，-顺时针）
    public float exitSpeed = 6.0f;         // 离场线速度（m/s）
    public float fadeOutTime = 0.8f;       // 离场淡出时间（s）

    [Header("Blend & Smoothing")]
    public float preArcDegrees = 45f;      // 入场到围圈的“预弧”角度
    public float lookAhead = 0.25f;        // 前瞻比例（用于朝向）
    public float exitLeadMeters = 2.5f;    // 离场先沿切线拉出的长度

    [Header("Orientation & Look")]
    public float bankTiltMax = 25f;          // 最大倾斜角（°）
    public Vector3 localLookUp = Vector3.up; // 模型“上”方向
    public float modelYawOffset = 0f;        // 模型机头不是 +Z 时填 90/180 等

    [Header("Animator Params (optional)")]
    public string flyStateName = "fly";     // 飞行动画状态名
    public string flySpeedParam = "Speed";  // Blend Tree 速度参数名（可留空）

    // —— 内部状态 ——
    Quaternion _baseGraphicsRot;
    float _lastBank = 0f;
    float _omega = 0f, _omegaVel = 0f; // 当前角速度(°/s)及其平滑速度

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (graphics == null && animator != null) graphics = animator.transform;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    void Start()
    {
        if (headTransform == null)
            Debug.LogWarning("BirdDirector: headTransform 未设置，将以场景原点为圆心。");

        if (animator != null && !string.IsNullOrEmpty(flyStateName))
        {
            animator.CrossFade(flyStateName, 0.1f, 0, 0f);
            if (!string.IsNullOrEmpty(flySpeedParam))
                animator.SetFloat(flySpeedParam, 1.0f);
        }

        StartCoroutine(RunShow());
    }

    IEnumerator RunShow()
    {
        // —— 入场到围圈 —— //
        float startDeg = -45f;
        float preStartDeg = startDeg - Mathf.Abs(preArcDegrees) * Mathf.Sign(orbitAngularSpeed);

        Vector3 center = headTransform ? headTransform.position : Vector3.zero;
        Vector3 approachDir = (Vector3.left + Vector3.back).normalized;
        Vector3 approachStart = center + approachDir * approachDistance + Vector3.up * ringHeight;

        Vector3 preArcPoint = center + YRot(preStartDeg) * ringRadius + Vector3.up * ringHeight;
        Vector3 preArcTangent = TangentAt(preStartDeg, orbitAngularSpeed);

        transform.position = approachStart;
        FaceTowards(preArcPoint - transform.position, true);
        ApplyBank(0f);

        yield return MoveLinearConstantSpeed(preArcPoint, approachSpeed,
                                             blendStartDir: false,
                                             blendEndDir: true, endDir: preArcTangent);

        _omega = Mathf.Sign(orbitAngularSpeed) * (approachSpeed / Mathf.Max(0.001f, ringRadius)) * Mathf.Rad2Deg;
        _omegaVel = 0f;

        float angle = preStartDeg;
        while (!Passed(angle, startDeg, orbitAngularSpeed))
        {
            float dt = Time.deltaTime;
            _omega = Mathf.SmoothDamp(_omega, orbitAngularSpeed, ref _omegaVel, 0.15f);
            angle += _omega * dt;

            center = headTransform ? headTransform.position : Vector3.zero;
            Vector3 onRing = center + YRot(angle) * ringRadius + Vector3.up * ringHeight;
            Vector3 nextOnRing = center + YRot(angle + _omega * lookAhead) * ringRadius + Vector3.up * ringHeight;

            transform.position = onRing;
            Vector3 dir = (nextOnRing - transform.position).normalized;
            FaceTowards(dir, false);
            ApplyBank(BankFromOmega(_omega));
            yield return null;
        }

        float targetDeg = startDeg + 360f * laps;
        while (!Passed(angle, targetDeg, orbitAngularSpeed))
        {
            float dt = Time.deltaTime;
            _omega = Mathf.SmoothDamp(_omega, orbitAngularSpeed, ref _omegaVel, 0.15f);
            angle += _omega * dt;

            center = headTransform ? headTransform.position : Vector3.zero;
            Vector3 onRing = center + YRot(angle) * ringRadius + Vector3.up * ringHeight;
            Vector3 nextOnRing = center + YRot(angle + _omega * lookAhead) * ringRadius + Vector3.up * ringHeight;

            transform.position = onRing;
            Vector3 dir = (nextOnRing - transform.position).normalized;
            FaceTowards(dir, false);
            ApplyBank(BankFromOmega(_omega));

            if (animator != null && !string.IsNullOrEmpty(flySpeedParam))
            {
                float approxLinearSpeed = Mathf.Abs(_omega) * Mathf.Deg2Rad * ringRadius;
                animator.SetFloat(flySpeedParam, approxLinearSpeed);
            }
            yield return null;
        }

        // —— 修复：离场不再“贴圆”，直接从当前位置沿切线引出 —— //
        Vector3 centerNow = headTransform ? headTransform.position : Vector3.zero;

        // 以“当前位置”计算径向与切线（不改变位置）
        Vector3 radial = transform.position - (centerNow + Vector3.up * ringHeight);
        radial.y = 0f;
        Vector3 tangentDir;
        if (radial.sqrMagnitude > 1e-6f)
        {
            radial.Normalize();
            Vector3 ccw = Vector3.Cross(Vector3.up, radial).normalized; // 逆时针切线
            tangentDir = (orbitAngularSpeed >= 0f) ? ccw : -ccw;
        }
        else
        {
            // 退化情形：半径太小就用角度法
            tangentDir = TangentAt(angle, orbitAngularSpeed);
        }

        // 第一段速度：用当前围圈线速度，保证速度连续
        float currentLinearSpeed = Mathf.Abs(_omega) * Mathf.Deg2Rad * ringRadius;
        float leadSpeed = Mathf.Max(currentLinearSpeed, 0.01f);

        Vector3 leadOutPoint = transform.position + tangentDir * Mathf.Max(0.01f, exitLeadMeters);

        // 目标点
        Vector3 exitBaseDir = (Vector3.forward).normalized;
        Vector3 exitTarget = centerNow + exitBaseDir * exitDistance + Vector3.up * ringHeight;

        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        // 1) 切线引出：起/终端朝向都贴切线；速度=当前线速（无“平移一下”）
        yield return MoveLinearConstantSpeed(leadOutPoint, leadSpeed,
                                             blendStartDir: true, startDir: transform.forward,
                                             blendEndDir: true, endDir: tangentDir);

        // 2) 引出点 → 目标：起点沿切线、末端对齐目标方向；速度用 exitSpeed
        Vector3 endFacing = (exitTarget - leadOutPoint).normalized;
        float runSpeed = Mathf.Max(exitSpeed, 0.01f);
        yield return MoveLinearConstantSpeed(exitTarget, runSpeed,
                                             blendStartDir: true, startDir: tangentDir,
                                             blendEndDir: true, endDir: endFacing);

        var fade = StartCoroutine(FadeOut(fadeOutTime));
        yield return fade;

        gameObject.SetActive(false);
    }

    // —— Helpers —— //

    IEnumerator MoveLinearConstantSpeed(Vector3 to, float speed,
                                        bool blendStartDir = false, Vector3 startDir = default,
                                        bool blendEndDir = false, Vector3 endDir = default)
    {
        Vector3 from = transform.position;
        float total = Vector3.Distance(from, to);
        if (total < 1e-4f) yield break;

        startDir = startDir.normalized;
        endDir = endDir.normalized;

        while (true)
        {
            Vector3 delta = to - transform.position;
            float remain = delta.magnitude;
            if (remain <= speed * Time.deltaTime) { transform.position = to; break; }

            Vector3 dir = delta / remain;
            transform.position += dir * speed * Time.deltaTime; // 恒速积分

            float progress = 1f - Mathf.Clamp01(remain / total);
            Vector3 lookDir = dir;

            if (blendStartDir && startDir != Vector3.zero)
            {
                float kStart = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.3f)));
                lookDir = Vector3.Slerp(startDir, lookDir, kStart).normalized;
            }
            if (blendEndDir && endDir != Vector3.zero)
            {
                float kEnd = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, progress));
                lookDir = Vector3.Slerp(lookDir, endDir, kEnd).normalized;
            }

            FaceTowards(lookDir, false);
            ApplyBank(0f); // 直线不滚转
            yield return null;
        }

        // 兜底
        FaceTowards((endDir != Vector3.zero ? endDir : (to - from)).normalized, false);
    }

    void FaceTowards(Vector3 dir, bool instant)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        Quaternion desiredRig = Quaternion.LookRotation(dir.normalized, localLookUp);
        float lerp = instant ? 1f : (1f - Mathf.Exp(-10f * Time.deltaTime)); // 转向稍快
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRig, lerp);

        if (graphics != null)
        {
            Quaternion yawFix = Quaternion.AngleAxis(modelYawOffset, Vector3.up);
            _baseGraphicsRot = yawFix * transform.rotation;
            UpdateGraphicsRotation();
        }
    }

    void ApplyBank(float degrees)
    {
        _lastBank = Mathf.Clamp(degrees, -bankTiltMax, bankTiltMax);
        UpdateGraphicsRotation();
    }

    void UpdateGraphicsRotation()
    {
        if (graphics == null) return;
        Vector3 rollAxis = _baseGraphicsRot * Vector3.forward;
        Quaternion roll = Quaternion.AngleAxis(_lastBank, rollAxis);
        graphics.rotation = roll * _baseGraphicsRot;
    }

    IEnumerator FadeOut(float time)
    {
        var rends = graphics ? graphics.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(1f - t / time);
            foreach (var r in rends)
            {
                foreach (var m in r.materials)
                {
                    if (m.HasProperty("_Color"))
                    {
                        var c = m.color; c.a = a; m.color = c;
                    }
                }
            }
            yield return null;
        }
    }

    static Vector3 YRot(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    static Vector3 TangentAt(float deg, float omegaDegPerSec)
    {
        Vector3 radial = YRot(deg).normalized;
        Vector3 t_ccw = Vector3.Cross(Vector3.up, radial).normalized; // 逆时针切线
        return (omegaDegPerSec >= 0f) ? t_ccw : -t_ccw;
    }

    static bool Passed(float angle, float target, float omegaSign)
        => omegaSign >= 0f ? angle >= target : angle <= target;

    float BankFromOmega(float omegaDegPerSec)
    {
        float k = Mathf.Clamp01(Mathf.Abs(omegaDegPerSec) / 180f);
        float signed = Mathf.Sign(omegaDegPerSec) * k * bankTiltMax;
        return -signed; // 左转正/右转负
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 center = headTransform ? headTransform.position : Vector3.zero;
        Vector3 ringCenter = center + Vector3.up * ringHeight;

        Gizmos.color = Color.cyan;
        const int steps = 64;
        Vector3 prev = ringCenter + YRot(0f) * ringRadius;
        for (int i = 1; i <= steps; i++)
        {
            float ang = i * 360f / steps;
            Vector3 p = ringCenter + YRot(ang) * ringRadius;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // 入/出方向参考
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(ringCenter, ringCenter + (Vector3.right + Vector3.back).normalized * approachDistance);
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(ringCenter, ringCenter + (Vector3.left + Vector3.back).normalized * exitDistance);
    }
#endif
}
