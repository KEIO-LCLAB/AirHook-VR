using UnityEngine;

[RequireComponent(typeof(Transform))]
public class SpeedAudioDriver : MonoBehaviour
{
    [Header("Speed source")]
    public SlopeSpeedControllerCustom speedCtrl;
    public float maxSpeed = 18f;

    [Header("Audio Sources")]
    public AudioSource wheels;
    public AudioClip wheelsLoop;
    public AudioSource wind;
    public AudioClip windLoop;

    [Header("Curves (x = speed/max)")]
    public AnimationCurve wheelsVol = AnimationCurve.Linear(0, 0.0f, 1, 1.0f);
    public AnimationCurve wheelsPitch = AnimationCurve.Linear(0, 0.85f, 1, 1.6f);
    public AnimationCurve windVol = AnimationCurve.Linear(0, 0.0f, 1, 1.0f);
    public AnimationCurve windPitch = AnimationCurve.Linear(0, 0.9f, 1, 1.8f);

    [Header("Pitch Boost")]
    public float wheelsPitchMul = 1.5f;
    public float windPitchMul = 1.5f;

    [Header("Smoothing (seconds)")]
    public float speedSmooth = 0.15f;
    public float volSmooth = 0.08f;
    public float pitchSmooth = 0.08f;

    [Header("Auto Play/Stop")]
    public float startAbove = 0.25f;
    public float stopBelow = 0.15f;
    public float stopAfterIdle = 0.8f;

    bool useLocalOffsets = true;
    Vector3 wheelsLocalOffset = new(0f, 0f, -0.10f);
    Vector3 windLocalOffset = new(0f, 0f, 0.60f);

    float _smSpeed, _wV, _wP, _wdV, _wdP, _idle;

    void Awake()
    {
        if (wheels != null) { wheels.loop = true; wheels.playOnAwake = false; wheels.spatialBlend = 1f; wheels.dopplerLevel = 0f; if (wheelsLoop) wheels.clip = wheelsLoop; }
        if (wind != null) { wind.loop = true; wind.playOnAwake = false; wind.spatialBlend = 1f; wind.dopplerLevel = 0f; if (windLoop) wind.clip = windLoop; }
    }

    void OnEnable()
    {
        _idle = 0f;
    }

    void Update()
    {
        if (speedCtrl == null) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        float v = Mathf.Max(0f, speedCtrl.CurrentSpeed);

        _smSpeed = Smooth(_smSpeed, v, speedSmooth, dt);

        bool moving = v >= startAbove;
        bool idleLow = v <= stopBelow;

        bool hardStop = (speedCtrl != null) && (!speedCtrl.enabled || speedCtrl.IsStopped);
        if (hardStop)
        {
            ForceStopAudio();
            return;
        }

        if (moving)
        {
            _idle = 0f;
            if (wheels && wheels.clip && !wheels.isPlaying) wheels.Play();
            if (wind && wind.clip && !wind.isPlaying) wind.Play();
        }
        else if (idleLow)
        {
            _idle += dt;
            if (_idle >= stopAfterIdle)
            {
                if (wheels && wheels.isPlaying) wheels.Stop();
                if (wind && wind.isPlaying) wind.Stop();
            }
        }

        float n = (maxSpeed > 1e-3f) ? Mathf.Clamp01(_smSpeed / maxSpeed) : 0f;

        if (wheels)
        {
            float tgtV = (_smSpeed <= stopBelow) ? 0f : Mathf.Clamp01(wheelsVol.Evaluate(n));
            float tgtP = Mathf.Clamp(wheelsPitch.Evaluate(n) * wheelsPitchMul, 0.1f, 3f);
            _wV = Smooth(_wV, tgtV, volSmooth, dt);
            _wP = Smooth(_wP, tgtP, pitchSmooth, dt);
            wheels.volume = _wV;
            wheels.pitch = _wP;
        }

        if (wind)
        {
            float tgtV = (_smSpeed <= stopBelow) ? 0f : Mathf.Clamp01(windVol.Evaluate(n));
            float tgtP = Mathf.Clamp(windPitch.Evaluate(n) * windPitchMul, 0.1f, 3f);
            _wdV = Smooth(_wdV, tgtV, volSmooth, dt);
            _wdP = Smooth(_wdP, tgtP, pitchSmooth, dt);
            wind.volume = _wdV;
            wind.pitch = _wdP;
        }
    }

    void LateUpdate()
    {
        if (useLocalOffsets)
        {
            if (wheels) wheels.transform.position = transform.TransformPoint(wheelsLocalOffset);
            if (wind) wind.transform.position = transform.TransformPoint(windLocalOffset);
        }
    }

    float Smooth(float cur, float target, float tau, float dt)
    {
        if (tau <= 1e-4f) return target;
        float k = 1f - Mathf.Exp(-dt / tau);
        return Mathf.Lerp(cur, target, k);
    }

    void ForceStopAudio()
    {
        if (wheels)
        {
            wheels.volume = 0f;
            if (wheels.isPlaying) wheels.Stop();
        }
        if (wind)
        {
            wind.volume = 0f;
            if (wind.isPlaying) wind.Stop();
        }
        _smSpeed = _wV = _wdV = 0f;
    }

}