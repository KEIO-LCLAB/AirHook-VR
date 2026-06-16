using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class CoasterWindSequence : MonoBehaviour
{
    string portLeft = "COM3";
    string portRight = "COM4";
    int baudRate = 9600;

    [Header("Wind Mode")]
    public bool omniDirectional = false;
    [Range(0f, 1f)]
    public float omniScale = 0.5f;

    Dictionary<(byte, int), int> maxPwmPerNozzle = new Dictionary<(byte, int), int>()
    {
        {(0x01, 6), 204}, {(0x01, 9), 204}, {(0x01, 10), 204}, {(0x01, 11), 204},
        {(0x02, 6), 204}, {(0x02, 9), 204}, {(0x02, 10), 204}, {(0x02, 11), 204},
    };

    [Header("Timings (seconds)")]
    public float t1_flat_up_to_40 = 20.0f;
    public float hold1_after_flat40 = 0.5f;

    public float t2_uphill_40_to_30 = 1.2f;
    public float hold2_after_30 = 0.3f;

    public float t3a_flat_30_to_40_20 = 0.3f;
    public float t3b_flat_40_20_to_30 = 0.3f;
    public float hold3_after_30 = 0.5f;

    public float t4_down45_30_to_100 = 1.5f;
    public float hold4_after_100 = 0.6f;

    public float t5_flat_100_to_70 = 1.0f;
    public float hold5_after_70 = 0.4f;

    public float t6_leftturn_70_to_L0_R100 = 2.0f;
    public float hold6_after = 0.2f;

    public float t7_flat_L0R100_to_70 = 1.0f;

    public float t8_down30_70_to_90 = 1.2f;
    public float hold8_after_90 = 0.4f;

    public float t9_flat_90_to_70 = 0.8f;

    public float t10_leftturn_70_to_L0_R90 = 2.0f;
    public float hold10_after = 0.2f;

    public float t10recover_to_70 = 0.6f;
    public float hold10recover = 0f;

    public float t11_flat_L0RXX_to_zero = 1.2f;

    [Header("Playback")]
    public bool playOnStart = true;
    public bool loop = false;

    [Header("Smoothing")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    public bool logPWM = false;

    [Header("Wind Direction (coming-from vectors)")]
    public Vector3 worldWindDirection = new Vector3(0, 0, 1);
    public Vector3 worldWindDirection_RightFront = new Vector3(0.2079f, 0f, 0.9781f);
    public Transform windSpace;
    public Transform headTransform;

    static readonly float S0 = 0.00f;
    static readonly float S20 = 0.30f;
    static readonly float S30 = 0.45f;
    static readonly float S40 = 0.50f;
    static readonly float S70 = 0.80f;
    static readonly float S90 = 0.90f;
    static readonly float S100 = 1.00f;

    static readonly Vector4 ZERO = new(0, 0, 0, 0);
    static readonly Vector4 L40_0_0_40 = new(0.40f, 0f, 0f, 0.40f);
    static readonly Vector4 L30_0_0_30 = new(0.35f, 0f, 0f, 0.35f);
    static readonly Vector4 L40_0_0_20 = new(0.40f, 0f, 0f, 0.20f);
    static readonly Vector4 L100_0_0_100 = new(1.00f, 0f, 0f, 1.00f);
    static readonly Vector4 L70_0_0_70 = new(0.70f, 0f, 0f, 0.70f);
    static readonly Vector4 L50_0_0_50 = new(0.50f, 0f, 0.50f, 0f);
    static readonly Vector4 L90_0_0_90 = new(0.90f, 0f, 0f, 0.90f);

    SerialPort portL, portR;
    Coroutine seqCo;

    float curLeftS = 0f, curRightS = 0f;

    Vector3 _baseWindDir;

    void Start()
    {
        TryOpen(ref portL, portLeft, "L");
        TryOpen(ref portR, portRight, "R");
        if (playOnStart) StartSequence();
    }

    public void StartSequence()
    {
        if (seqCo != null) StopCoroutine(seqCo);
        _baseWindDir = worldWindDirection;
        curLeftS = 0f; curRightS = 0f;
        SendBothBoards(StrengthToPWM(true, curLeftS), StrengthToPWM(false, curRightS));
        seqCo = StartCoroutine(Sequence());
    }

    public void StopSequence()
    {
        if (seqCo != null) StopCoroutine(seqCo);
        curLeftS = 0f; curRightS = 0f;
        SendBothBoards(new int[4], new int[4]);
    }

    IEnumerator Sequence()
    {
        do
        {
            worldWindDirection = _baseWindDir;
            yield return LerpTo(t1_flat_up_to_40, S40, S40);
            yield return HoldCurrent(hold1_after_flat40);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t2_uphill_40_to_30, S30, S30);
            yield return HoldCurrent(hold2_after_30);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t3a_flat_30_to_40_20, S40, S40);
            yield return LerpTo(t3b_flat_40_20_to_30, S30, S30);
            yield return HoldCurrent(hold3_after_30);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t4_down45_30_to_100, S100, S100);
            yield return HoldCurrent(hold4_after_100);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t5_flat_100_to_70, S70, S70);
            yield return HoldCurrent(hold5_after_70);

            worldWindDirection = worldWindDirection_RightFront;
            yield return LerpTo(t6_leftturn_70_to_L0_R100, S100, S70);
            yield return HoldCurrent(hold6_after);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t7_flat_L0R100_to_70, S70, S70);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t8_down30_70_to_90, S90, S90);
            yield return HoldCurrent(hold8_after_90);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t9_flat_90_to_70, S70, S70);

            worldWindDirection = worldWindDirection_RightFront;
            yield return LerpTo(t10_leftturn_70_to_L0_R90, S90, S70);
            yield return HoldCurrent(hold10_after);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t10recover_to_70, S70, S70);
            yield return HoldCurrent(hold10recover);

            worldWindDirection = _baseWindDir;
            yield return LerpTo(t11_flat_L0RXX_to_zero, S0, S0);

        } while (loop);
    }

    IEnumerator LerpTo(float seconds, float leftTo, float rightTo)
    {
        if (!omniDirectional)
        {
            float maxStrength = Mathf.Max(leftTo, rightTo);
            leftTo = maxStrength;
            rightTo = maxStrength;
        }

        seconds = Mathf.Max(0f, seconds);
        if (seconds <= 1e-4f)
        {
            curLeftS = leftTo; curRightS = rightTo;
            SendBothBoards(StrengthToPWM(true, curLeftS), StrengthToPWM(false, curRightS));
            yield break;
        }

        float fromL = curLeftS, fromR = curRightS;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            float t = Mathf.Clamp01(elapsed / seconds);
            float s = ease != null ? ease.Evaluate(t) : t;
            float Ls = Mathf.Lerp(fromL, leftTo, s);
            float Rs = Mathf.Lerp(fromR, rightTo, s);

            SendBothBoards(StrengthToPWM(true, Ls),
                           StrengthToPWM(false, Rs));

            yield return null;
            elapsed += Time.deltaTime;
        }

        curLeftS = leftTo; curRightS = rightTo;
        SendBothBoards(StrengthToPWM(true, curLeftS), StrengthToPWM(false, curRightS));
    }

    IEnumerator HoldCurrent(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        if (seconds <= 1e-4f)
        {
            SendBothBoards(StrengthToPWM(true, curLeftS),
                           StrengthToPWM(false, curRightS));
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            SendBothBoards(StrengthToPWM(true, curLeftS),
                           StrengthToPWM(false, curRightS));
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    void SendBothBoards(int[] left, int[] right)
    {
        SendPWM(0x01, left);
        SendPWM(0x02, right);
    }

    void SendPWM(byte boardId, int[] pwm4)
    {
        byte[] data = new byte[] { boardId, (byte)pwm4[0], (byte)pwm4[1], (byte)pwm4[2], (byte)pwm4[3] };
        try
        {
            if (boardId == 0x01) { if (portL != null && portL.IsOpen) portL.Write(data, 0, data.Length); }
            else { if (portR != null && portR.IsOpen) { portR.Write(data, 0, data.Length); } }
        }
        catch (Exception e)
        {
        }
    }

    void TryOpen(ref SerialPort sp, string name, string tag)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        try { sp = new SerialPort(name, baudRate); sp.Open(); }
        catch (Exception e) { }
    }

    void OnDestroy() => CloseAndZero();
    void OnApplicationQuit() => CloseAndZero();
    void CloseAndZero()
    {
        try
        {
            byte[] zL = new byte[] { 0x01, 0, 0, 0, 0 };
            byte[] zR = new byte[] { 0x02, 0, 0, 0, 0 };
            if (portL != null && portL.IsOpen) { portL.Write(zL, 0, zL.Length); portL.Close(); }
            if (portR != null && portR.IsOpen) { portR.Write(zR, 0, zR.Length); portR.Close(); }
        }
        catch (Exception e) { }
    }

    int[] StrengthToPWM(bool leftBoard, float strength01)
    {
        strength01 = Mathf.Clamp01(strength01);
        byte boardId = leftBoard ? (byte)0x01 : (byte)0x02;
        int[] pins = { 6, 9, 10, 11 };
        int[] arr = new int[4];

        const float slope = 0.6985f;
        const float offset = -1.7344f;
        const float minPWM = 30f;

        if (!omniDirectional)
        {
            float finalS = strength01 * omniScale;
            if (finalS < 0.001f) return new int[4];

            for (int i = 0; i < 4; i++)
            {
                int maxP = maxPwmPerNozzle.TryGetValue((boardId, pins[i]), out var vMax) ? vMax : 255;

                float maxPercent = (maxP / 255f) * 100f;
                float maxV = slope * maxPercent + offset;
                float targetV = finalS * maxV;

                float targetPct = (Mathf.Max(targetV, 0.64f) - offset) / slope;
                float finalPwm = (targetPct / 100f) * 255f;

                arr[i] = Mathf.RoundToInt(Mathf.Clamp(finalPwm, minPWM, (float)maxP));
            }
        }
        else
        {
            float boost = 1.2f;
            Transform space = windSpace ? windSpace : transform;
            Vector3 authoredLocal = worldWindDirection.sqrMagnitude > 1e-10f ? worldWindDirection.normalized : new Vector3(0, 0, 1);
            Vector3 vWorld = space.TransformDirection(authoredLocal);
            Vector3 vHead = (headTransform != null) ? headTransform.InverseTransformDirection(vWorld).normalized : vWorld.normalized;

            float side = leftBoard ? 1f : -1f;
            Vector3[] n = new Vector3[4];
            n[0] = new Vector3(side, 1f, 1f).normalized;
            n[1] = new Vector3(side, 1f, -1f).normalized;
            n[2] = new Vector3(side, -1f, -1f).normalized;
            n[3] = new Vector3(side, -1f, 1f).normalized;

            for (int i = 0; i < 4; i++)
            {
                int maxP = maxPwmPerNozzle.TryGetValue((boardId, pins[i]), out var vMax) ? vMax : 255;

                float s_i = Mathf.Max(0f, Vector3.Dot(vHead, n[i]));
                float combinedRatio = s_i * strength01 * boost;

                if (combinedRatio < 0.001f)
                {
                    arr[i] = 0;
                    continue;
                }

                float maxPercent = (maxP / 255f) * 100f;
                float maxV = slope * maxPercent + offset;
                float targetV = combinedRatio * maxV;

                float targetPct = (Mathf.Max(targetV, 0.64f) - offset) / slope;
                float finalPwm = (targetPct / 100f) * 255f;

                arr[i] = Mathf.RoundToInt(Mathf.Clamp(finalPwm, minPWM, (float)maxP));
            }
        }
        return arr;
    }
}