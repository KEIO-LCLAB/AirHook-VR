using System;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class WindHub : MonoBehaviour
{
    [Header("Omni Mode")]
    public bool omniDirectional = false;
    [Range(0f, 1f)] public float omniScale = 0.5f;

    [Header("Head-relative Direction Update")]
    Transform headTransform;
    bool remapSustainsWithHeadRotation = true;
    bool remapBurstsWithHeadRotation = false;

    public static WindHub Instance { get; private set; }

    string portLeft = "COM3";
    string portRight = "COM4";
    int baud = 9600;

    float minSendInterval = 0.02f;

    const float SLOPE = 0.6985f;
    const float OFFSET = -1.7344f;
    const float MIN_PWM = 30f;
    const float MIN_V = 0.64f;

    Dictionary<(byte, int), int> maxPwmPerNozzle = new Dictionary<(byte, int), int>()
    {
        {(0x01, 6), 204}, {(0x01, 9), 204}, {(0x01, 10), 204}, {(0x01, 11), 204},
        {(0x02, 6), 204}, {(0x02, 9), 204}, {(0x02, 10), 204}, {(0x02, 11), 204},
    };

    Dictionary<(byte board, int pin), Vector3> nozzleDirections = new Dictionary<(byte, int), Vector3>
    {
        {(0x01, 6), new Vector3(-1, 1, -1)},
        {(0x01, 9), new Vector3(-1, 1,  1)},
        {(0x01, 10), new Vector3(-1, -1, 1)},
        {(0x01, 11), new Vector3(-1, -1, -1)},

        {(0x02, 6), new Vector3( 1, 1, -1)},
        {(0x02, 9), new Vector3( 1, 1,  1)},
        {(0x02, 10), new Vector3( 1,-1,  1)},
        {(0x02, 11), new Vector3( 1,-1, -1)},
    };

    private SerialPort _spL, _spR;
    private float _lastSendTime;
    private int[] _lastL = new int[4], _lastR = new int[4];

    abstract class Effect
    {
        public bool expired;
        public abstract void Evaluate(float now, float[] outL, float[] outR);
    }

    class Burst : Effect
    {
        readonly WindHub _hub;
        readonly float _t0, _hold, _fade;
        readonly float[] _L = new float[4], _R = new float[4];
        readonly bool _hasWorldDir;
        readonly Vector3 _worldDir;
        readonly float _intensity;

        public Burst(WindHub hub, float now, float[] L, float[] R, float hold, float fade)
        {
            _hub = hub;
            _t0 = now; _hold = hold; _fade = Mathf.Max(0.001f, fade);
            Array.Copy(L, _L, 4); Array.Copy(R, _R, 4);
            _hasWorldDir = _hub.CaptureWorldDirection(L, R, out _worldDir, out _intensity);
        }

        public override void Evaluate(float now, float[] outL, float[] outR)
        {
            float dt = now - _t0;
            float k = (dt <= _hold) ? 1f : (dt <= _hold + _fade) ? 1f - (dt - _hold) / _fade : 0f;
            if (dt > _hold + _fade) expired = true;

            if (_hub.remapBurstsWithHeadRotation && _hasWorldDir && _hub.BuildRatiosFromWorldDirection(_worldDir, _intensity * k, out float[] remapL, out float[] remapR))
            {
                for (int i = 0; i < 4; i++)
                {
                    outL[i] = Mathf.Max(outL[i], remapL[i]);
                    outR[i] = Mathf.Max(outR[i], remapR[i]);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    outL[i] = Mathf.Max(outL[i], _L[i] * k);
                    outR[i] = Mathf.Max(outR[i], _R[i] * k);
                }
            }
        }
    }

    class Sustain
    {
        readonly WindHub _hub;
        public readonly int handle;
        float[] _L = new float[4], _R = new float[4];
        bool _hasWorldDir;
        Vector3 _worldDir;
        float _intensity;

        public Sustain(WindHub hub, int h, float[] L, float[] R) { _hub = hub; handle = h; Update(L, R); }

        public void Update(float[] L, float[] R)
        {
            Array.Copy(L, _L, 4); Array.Copy(R, _R, 4);
            _hasWorldDir = _hub.CaptureWorldDirection(L, R, out _worldDir, out _intensity);
        }

        public void Evaluate(float now, float[] outL, float[] outR)
        {
            if (_hub.remapSustainsWithHeadRotation && _hasWorldDir && _hub.BuildRatiosFromWorldDirection(_worldDir, _intensity, out float[] remapL, out float[] remapR))
            {
                for (int i = 0; i < 4; i++)
                {
                    outL[i] = Mathf.Max(outL[i], remapL[i]);
                    outR[i] = Mathf.Max(outR[i], remapR[i]);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    outL[i] = Mathf.Max(outL[i], _L[i]);
                    outR[i] = Mathf.Max(outR[i], _R[i]);
                }
            }
        }
    }

    readonly List<Effect> _effects = new();
    readonly List<Sustain> _sustains = new();
    int _nextHandle = 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;
        try
        {
            _spL = new SerialPort(portLeft, baud); _spR = new SerialPort(portRight, baud);
            _spL.Open(); _spR.Open();
        }
        catch (Exception e) { }
    }

    void Update()
    {
        if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;

        _effects.RemoveAll(e => e.expired);

        float[] L = new float[4], R = new float[4];
        foreach (var s in _sustains) s.Evaluate(Time.time, L, R);
        foreach (var e in _effects) e.Evaluate(Time.time, L, R);

        if (!omniDirectional)
        {
            float maxIntensity = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (L[i] > maxIntensity) maxIntensity = L[i];
                if (R[i] > maxIntensity) maxIntensity = R[i];
            }

            float finalGlobalS = maxIntensity * omniScale;

            for (int i = 0; i < 4; i++)
            {
                L[i] = finalGlobalS;
                R[i] = finalGlobalS;
            }
        }

        int[] iL = RatiosToPwm(0x01, L);
        int[] iR = RatiosToPwm(0x02, R);

        if ((!Same(_lastL, iL) || !Same(_lastR, iR)) && (Time.time - _lastSendTime) >= minSendInterval)
        {
            SendBoth(iL, iR);
            Array.Copy(iL, _lastL, 4);
            Array.Copy(iR, _lastR, 4);
            _lastSendTime = Time.time;
        }
    }

    bool CaptureWorldDirection(float[] L, float[] R, out Vector3 worldDir, out float intensity)
    {
        worldDir = Vector3.zero;
        intensity = 0f;

        if (!omniDirectional || headTransform == null) return false;

        Vector3 localDir = Vector3.zero;
        int[] pins = { 6, 9, 10, 11 };

        for (int i = 0; i < 4; i++)
        {
            float l = Mathf.Clamp01(L[i]);
            float r = Mathf.Clamp01(R[i]);
            if (l > intensity) intensity = l;
            if (r > intensity) intensity = r;
            localDir += nozzleDirections[(0x01, pins[i])].normalized * l;
            localDir += nozzleDirections[(0x02, pins[i])].normalized * r;
        }

        if (localDir.sqrMagnitude < 1e-6f || intensity < 0.001f) return false;

        worldDir = headTransform.TransformDirection(localDir.normalized);
        return true;
    }

    bool BuildRatiosFromWorldDirection(Vector3 worldDir, float intensity, out float[] L, out float[] R)
    {
        L = new float[4];
        R = new float[4];

        if (!omniDirectional || headTransform == null || worldDir.sqrMagnitude < 1e-6f || intensity < 0.001f) return false;

        Vector3 vHead = headTransform.InverseTransformDirection(worldDir).normalized;

        foreach (var nozzle in nozzleDirections)
        {
            byte boardId = nozzle.Key.board;
            int pin = nozzle.Key.pin;
            Vector3 n_i = nozzle.Value.normalized;

            float s_i = Mathf.Max(0f, Vector3.Dot(vHead, n_i)) * Mathf.Clamp01(intensity);
            int index = pin == 6 ? 0 : pin == 9 ? 1 : pin == 10 ? 2 : 3;

            if (boardId == 0x01) L[index] = s_i;
            else R[index] = s_i;
        }

        return true;
    }

    int[] RatiosToPwm(byte boardId, float[] r)
    {
        int[] pins = { 6, 9, 10, 11 };
        int[] o = new int[4];

        float baseIntensity = 0f;

        if (!omniDirectional)
        {
            foreach (float val in r) if (val > baseIntensity) baseIntensity = val;
            baseIntensity *= omniScale;
        }

        for (int i = 0; i < 4; i++)
        {
            float inputRatio = !omniDirectional ? baseIntensity : Mathf.Clamp01(r[i]);

            inputRatio = Mathf.Clamp01(inputRatio * 1.2f);

            if (inputRatio < 0.001f)
            {
                o[i] = 0;
                continue;
            }

            int maxP = maxPwmPerNozzle.TryGetValue((boardId, pins[i]), out var m) ? m : 255;

            float maxPercent = (maxP / 255f) * 100f;
            float maxV = (SLOPE * maxPercent) + OFFSET;

            float targetV = inputRatio * maxV;

            float targetPct = (Mathf.Max(targetV, MIN_V) - OFFSET) / SLOPE;

            float finalPwm = (targetPct / 100f) * 255f;

            o[i] = Mathf.RoundToInt(Mathf.Clamp(finalPwm, MIN_PWM, (float)maxP));
        }
        return o;
    }

    void SendBoth(int[] L, int[] R)
    {
        byte[] dL = { 0x01, (byte)L[0], (byte)L[1], (byte)L[2], (byte)L[3] };
        byte[] dR = { 0x02, (byte)R[0], (byte)R[1], (byte)R[2], (byte)R[3] };
        try { if (_spL?.IsOpen == true) _spL.Write(dL, 0, 5); } catch { }
        try { if (_spR?.IsOpen == true) _spR.Write(dR, 0, 5); } catch { }
    }

    bool Same(int[] a, int[] b) { for (int i = 0; i < 4; i++) if (a[i] != b[i]) return false; return true; }

    public void PostBurst(float[] L, float[] R, float hold, float fade, int priority = 0) => _effects.Add(new Burst(this, Time.time, L, R, hold, fade));
    public int BeginSustain(float[] L, float[] R, int priority = 0)
    {
        int h = _nextHandle++;
        var s = new Sustain(this, h, L, R);
        _sustains.Add(s);
        return h;
    }
    public void UpdateSustain(int handle, float[] L, float[] R) => _sustains.Find(x => x.handle == handle)?.Update(L, R);
    public void EndSustain(int handle) { _sustains.RemoveAll(x => x.handle == handle); }

    void OnApplicationQuit()
    {
        try
        {
            byte[] zeroL = { 0x01, 0, 0, 0, 0 };
            byte[] zeroR = { 0x02, 0, 0, 0, 0 };

            if (_spL != null && _spL.IsOpen) { _spL.Write(zeroL, 0, 5); _spL.Close(); }
            if (_spR != null && _spR.IsOpen) { _spR.Write(zeroR, 0, 5); _spR.Close(); }
        }
        catch (System.Exception e)
        {
        }
    }
}
