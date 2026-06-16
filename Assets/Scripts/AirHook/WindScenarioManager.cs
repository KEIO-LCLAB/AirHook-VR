using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;

public class WindScenarioManager : MonoBehaviour
{
    [Header("Wind Mode")]
    public bool omniDirectional = false;
    public float omniScale = 0.5f;

    public bool demo = false;

    public Transform headTransform;
    public WindZone windZone;

    /*    public AudioSource birdAudioSource;*/
    public AudioSource windAudio;
    [Range(0f, 1f)] public float windVolumeMin = 0.40f;
    [Range(0f, 1f)] public float windVolumeMax = 1.00f;
    [Range(0f, 2f)] public float windStrengthMin = 0.20f;

    [Header("Blowing Leaves (particles)")]
    public Transform leavesRoot;
    public float leavesRateMax = 10f;
    public float leavesSpeedMax = 3f;
    public float leavesLifetimeStrong = 4f;
    public float leavesLifetimeCalm = 6f;
    public float leavesGravityStrong = 0.6f;
    public float leavesGravityCalm = 0.0f;
    public Vector2 leavesTurbulenceXZ = new Vector2(0.8f, 0.5f);

    readonly List<ParticleSystem> _leafPS = new();

    public bool syncWindZone = true;
    public float windMainRest = 0.1f;
    public float windMainMax = 1.0f;
    public float windTurbulence = 1.0f;

    SerialPort portL;
    SerialPort portR;

    int[] currentLeft = new int[4];
    int[] currentRight = new int[4];

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

    /*    [Header("Bird (BirdDirector)")]
        public GameObject birdPrefab;
        public Transform birdParent;
        [Header("Bird (in-scene optional)")]
        public BirdDirector sceneBird;
        BirdDirector _activeBird;*/

    void Awake()
    {
        if (leavesRoot != null)
        {
            _leafPS.Clear();
            leavesRoot.GetComponentsInChildren(true, _leafPS);
            LeavesDisableBursts();
            LeavesEnsurePlayingAtZero();
        }
    }

    void Start()
    {
        portL = new SerialPort("COM3", 9600);
        portR = new SerialPort("COM4", 9600);
        portL.Open();
        portR.Open();

        StartCoroutine(ScenarioSequence());
    }

    void Update()
    {
        UpdateLeavesByWind();
    }

    IEnumerator ScenarioSequence()
    {
        yield return new WaitForSeconds(3);

        if (demo)
        {
            foreach (var ps in _leafPS)
            {
                var main = ps.main;
                main.simulationSpeed = 0.9f;
            }

            yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.zero, 0.1f, true));
            yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.back * 1f, 6f));
            yield return StartCoroutine(TransitionWind(Vector3.back * 1f, (Vector3.back + Vector3.left).normalized * 1f, 10f));
            yield return StartCoroutine(TransitionWind((Vector3.back + Vector3.left).normalized * 1f, (Vector3.back + Vector3.left).normalized * 1f, 7f));
            yield return StartCoroutine(TransitionWind((Vector3.back + Vector3.left).normalized * 1f, Vector3.back * 1f, 5f));
            yield return StartCoroutine(TransitionWind(Vector3.back * 1f, Vector3.right * 1f, 16f));
            yield return StartCoroutine(TransitionWind(Vector3.right * 1f, Vector3.forward, 16f));
            yield return StartCoroutine(TransitionWind(Vector3.forward * 1f, Vector3.zero, 5f));
        }
        else
        {
            foreach (var ps in _leafPS)
            {
                var main = ps.main;
                main.simulationSpeed = 0.9f;
            }

            yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.zero, 0.1f, true));
            yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.back * 1f, 30f));
            yield return StartCoroutine(TransitionWind(Vector3.back * 1f, (Vector3.back + Vector3.left).normalized * 1f, 20f));
            yield return StartCoroutine(TransitionWind((Vector3.back + Vector3.left).normalized * 1f, (Vector3.back + Vector3.left).normalized * 1f, 10f));
            yield return StartCoroutine(TransitionWind((Vector3.back + Vector3.left).normalized * 1f, Vector3.back * 1f, 15f));
            yield return StartCoroutine(TransitionWind(Vector3.back * 1f, (Vector3.back + Vector3.right).normalized * 1f, 15f));
            yield return StartCoroutine(TransitionWind((Vector3.back + Vector3.right).normalized * 1f, (Vector3.back + Vector3.right).normalized * 1f, 10f));
            yield return StartCoroutine(TransitionWind((Vector3.back + Vector3.right).normalized * 1f, Vector3.back * 0.7f, 10f));
            yield return StartCoroutine(TransitionWind(Vector3.back * 0.7f, Vector3.zero, 15f));
        }

        foreach (var ps in _leafPS)
        {
            var main = ps.main;
            main.simulationSpeed = 1.6f;
        }

        if (demo)
        {
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.forward * 1f, 1.5f));
                yield return StartCoroutine(TransitionWind(Vector3.forward * 1f, Vector3.forward * 1f, 3.5f));
                if (i == 2)
                {
                    yield return StartCoroutine(TransitionWind(Vector3.forward * 1f, Vector3.forward * 1f, 3.5f));
                }
                yield return StartCoroutine(TransitionWind(Vector3.forward * 1f, (Vector3.forward + Vector3.up * 0.7f).normalized * 0.8f, 3f));
                yield return StartCoroutine(TransitionWind((Vector3.forward + Vector3.up * 0.7f).normalized * 0.8f, Vector3.zero, 3f));
                yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.zero, 2f));
            }
        }

        Debug.Log("Complete");

        yield return StartCoroutine(TransitionWind(Vector3.zero, Vector3.zero, 0.1f, true));

        foreach (var ps in _leafPS)
        {
            var main = ps.main;
            main.simulationSpeed = 1.0f;
        }
    }

    IEnumerator TransitionWind(Vector3 fromDir, Vector3 toDir, float duration, bool ignoreHeadRotation = false)
    {
        bool toIsRest = toDir == Vector3.zero;
        BuildRestPWM(0.2f, out int[] lRest, out int[] rRest);

        float elapsed = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            Vector3 blended = Vector3.Slerp(fromDir, toDir, t);

            UpdateWindZoneVisual(blended, ignoreHeadRotation);

            if (ignoreHeadRotation)
            {
                SendBothBoards(lRest, rRest);
            }
            else
            {
                if (blended == Vector3.zero)
                {
                    SendBothBoards(lRest, rRest);
                }
                else
                {
                    ComputeDirectionalPWM(blended, out int[] lNow, out int[] rNow);
                    SendBothBoards(lNow, rNow);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        UpdateWindZoneVisual(toDir, ignoreHeadRotation);
        if (ignoreHeadRotation || toIsRest)
        {
            SendBothBoards(lRest, rRest);
        }
        else
        {
            ComputeDirectionalPWM(toDir, out int[] lFinal, out int[] rFinal);
            SendBothBoards(lFinal, rFinal);
        }
    }

    /*    IEnumerator BirdCircling()
        {
            const float segDuration = 1.3f;
            const int steps = 20;
            const int cycles = 2;

            BuildRestPWM(0.2f, out int[] lRest, out int[] rRest);

            if (!omniDirectional)
            {
                float finalStrength = omniScale;
                float[] allOpenRatios = { finalStrength, finalStrength, finalStrength, finalStrength };

                int[] omniL = RatiosToPwm(0x01, allOpenRatios);
                int[] omniR = RatiosToPwm(0x02, allOpenRatios);

                yield return StartCoroutine(LerpPwmAndSend(lRest, rRest, omniL, omniR, segDuration, steps));

                int totalSteps = (cycles * 4) + 2;
                for (int i = 0; i < totalSteps; i++)
                {
                    if (birdAudioSource != null && birdAudioSource.clip != null)
                    {
                        birdAudioSource.PlayOneShot(birdAudioSource.clip);
                    }
                    yield return new WaitForSeconds(segDuration);
                }

                yield return StartCoroutine(LerpPwmAndSend(omniL, omniR, lRest, rRest, 5.0f, steps));
            }
            else
            {
                float[] L_none = { 0, 0, 0, 0 };
                float[] R_none = { 0, 0, 0, 0 };
                float[] R_BU = { 0, 1, 0, 0 };
                float[] R_FU = { 1, 0, 0, 0 };
                float[] L_FU = { 1, 0, 0, 0 };
                float[] L_BU = { 0, 1, 0, 0 };

                var patterns = new List<(float[] L, float[] R, string tag)>();
                for (int c = 0; c < cycles; c++)
                {
                    patterns.Add((L_none, R_BU, "R_BU"));
                    patterns.Add((L_none, R_FU, "R_FU"));
                    patterns.Add((L_FU, R_none, "L_FU"));
                    patterns.Add((L_BU, R_none, "L_BU"));
                }
                patterns.Add((L_none, R_BU, "R_BU"));
                patterns.Add((L_none, R_FU, "R_FU"));

                int[] ToPwmL(float[] ratios) => RatiosToPwm(0x01, ratios);
                int[] ToPwmR(float[] ratios) => RatiosToPwm(0x02, ratios);

                int[] curL = lRest, curR = rRest;
                if (patterns.Count > 0)
                {
                    int[] tgtL = ToPwmL(patterns[0].L);
                    int[] tgtR = ToPwmR(patterns[0].R);
                    yield return StartCoroutine(LerpPwmAndSend(curL, curR, tgtL, tgtR, segDuration, steps));
                    curL = tgtL; curR = tgtR;
                }

                for (int i = 1; i < patterns.Count; i++)
                {
                    int[] tgtL = ToPwmL(patterns[i].L);
                    int[] tgtR = ToPwmR(patterns[i].R);

                    if ((patterns[i].tag == "L_FU" || patterns[i].tag == "R_BU") && birdAudioSource != null)
                    {
                        birdAudioSource.PlayOneShot(birdAudioSource.clip);
                    }

                    yield return StartCoroutine(LerpPwmAndSend(curL, curR, tgtL, tgtR, segDuration, steps));
                    curL = tgtL; curR = tgtR;
                }

                yield return StartCoroutine(LerpPwmAndSend(curL, curR, lRest, rRest, 5.0f, steps));
            }

            yield return new WaitForSeconds(5f);
        }*/

    IEnumerator LerpPwmAndSend(int[] fromL, int[] fromR, int[] toL, int[] toR, float duration, int steps)
    {
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;

            int[] outL = new int[4];
            int[] outR = new int[4];
            for (int k = 0; k < 4; k++)
            {
                outL[k] = Mathf.RoundToInt(Mathf.Lerp(fromL[k], toL[k], t));
                outR[k] = Mathf.RoundToInt(Mathf.Lerp(fromR[k], toR[k], t));
            }

            SendBothBoards(outL, outR);
            yield return new WaitForSeconds(duration / steps);
        }
    }

    void UpdateWindZoneVisual(Vector3 worldDir, bool isRest)
    {
        if (!syncWindZone || windZone == null) return;

        float mag = Mathf.Clamp01(worldDir.magnitude);
        float main = isRest ? windMainRest : Mathf.Lerp(windMainRest, windMainMax, mag);

        if (mag > 1e-3f)
        {
            windZone.transform.rotation = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
        }

        windZone.windMain = main;
        windZone.windTurbulence = windTurbulence;
        UpdateWindAudio(windZone.windMain);
    }

    void UpdateWindAudio(float currentWindMain)
    {
        if (windAudio == null) return;

        if (!windAudio.isPlaying && windAudio.clip != null) windAudio.Play();

        float k = Mathf.InverseLerp(windStrengthMin, 1f, Mathf.Clamp01(currentWindMain));
        float vol = Mathf.Lerp(windVolumeMin, windVolumeMax, k);

        windAudio.volume = vol;
    }

    /*    IEnumerator BirdCallsRoutine(float totalDuration, int birdCalls)
        {
            if (birdAudioSource == null || birdAudioSource.clip == null || birdCalls <= 0)
                yield break;

            float interval = totalDuration / birdCalls;
            float elapsed = 0f;
            while (elapsed + 1e-3f < totalDuration)
            {
                yield return new WaitForSeconds(interval);
                birdAudioSource.PlayOneShot(birdAudioSource.clip);
                elapsed += interval;
            }
        }*/

    public void ApplyDirectionalWind(Vector3 worldWindDir)
    {
        Vector3 localWind = Quaternion.Inverse(headTransform.rotation) * worldWindDir;
        ApplyToAllNozzles(localWind.normalized);
    }

    void ApplyToAllNozzles(Vector3 localDir)
    {
        Dictionary<byte, float[]> pwmDict = new()
        {
            { 0x01, new float[4] },
            { 0x02, new float[4] },
        };

        foreach (var nozzle in nozzleDirections)
        {
            byte boardId = nozzle.Key.Item1;
            int pin = nozzle.Key.Item2;
            Vector3 nozzleDir = nozzle.Value.normalized;

            float strength = Mathf.Clamp01(Vector3.Dot(nozzleDir, localDir));
            int index = pin == 6 ? 0 : pin == 9 ? 1 : pin == 10 ? 2 : 3;
            pwmDict[boardId][index] = strength;
        }

        foreach (var board in pwmDict)
        {
            SendPWMNormalized(board.Key, board.Value);
        }
    }

    void SendPWMNormalized(byte boardId, float[] ratios)
    {
        int[] pins = new int[] { 6, 9, 10, 11 };
        int[] pwm = new int[4];

        for (int i = 0; i < 4; i++)
        {
            int pin = pins[i];
            int max = maxPwmPerNozzle.TryGetValue((boardId, pin), out var v) ? v : 255;
            float r = Mathf.Clamp01(ratios[i]);
            pwm[i] = Mathf.RoundToInt(r * max);
        }

        SendPWM(boardId, pwm[0], pwm[1], pwm[2], pwm[3]);
    }

    void SendPWM(byte boardId, int pwm1, int pwm2, int pwm3, int pwm4)
    {
        if (boardId == 0x01)
        {
            currentLeft[0] = pwm1; currentLeft[1] = pwm2; currentLeft[2] = pwm3; currentLeft[3] = pwm4;
        }
        else if (boardId == 0x02)
        {
            currentRight[0] = pwm1; currentRight[1] = pwm2; currentRight[2] = pwm3; currentRight[3] = pwm4;
        }

        byte[] dataL = new byte[] { 0x01, (byte)currentLeft[0], (byte)currentLeft[1], (byte)currentLeft[2], (byte)currentLeft[3] };
        byte[] dataR = new byte[] { 0x02, (byte)currentRight[0], (byte)currentRight[1], (byte)currentRight[2], (byte)currentRight[3] };

        if (portL != null && portL.IsOpen) portL.Write(dataL, 0, dataL.Length);
        if (portR != null && portR.IsOpen) portR.Write(dataR, 0, dataR.Length);
    }

    void OnApplicationQuit()
    {
        try
        {
            byte[] zeroL = new byte[] { 0x01, 0, 0, 0, 0 };
            byte[] zeroR = new byte[] { 0x02, 0, 0, 0, 0 };
            if (portL != null && portL.IsOpen) { portL.Write(zeroL, 0, zeroL.Length); portL.Close(); }
            if (portR != null && portR.IsOpen) { portR.Write(zeroR, 0, zeroR.Length); portR.Close(); }
        }
        catch (System.Exception e)
        {
        }
    }

    int[] RatiosToPwm(byte boardId, float[] ratios)
    {
        int[] pins = new int[] { 6, 9, 10, 11 };
        int[] pwm = new int[4];

        const float slope = 0.6985f;
        const float offset = -1.7344f;
        const float minPwmLimit = 30f;

        for (int i = 0; i < 4; i++)
        {
            float r = Mathf.Clamp01(ratios[i]);

            if (r < 0.001f) { pwm[i] = 0; continue; }

            int pin = pins[i];
            int maxPWM = maxPwmPerNozzle.TryGetValue((boardId, pin), out var v) ? v : 255;

            float maxPercent = (maxPWM / 255f) * 100f;
            float maxV = (slope * maxPercent) + offset;

            float targetV = r * maxV;

            float targetPct = (Mathf.Max(targetV, 0.64f) - offset) / slope;

            float finalPwm = (targetPct / 100f) * 255f;

            pwm[i] = Mathf.RoundToInt(Mathf.Clamp(finalPwm, minPwmLimit, (float)maxPWM));
        }
        return pwm;
    }

    void BuildRestPWM(float ratio, out int[] left, out int[] right)
    {
        left = new int[4];
        right = new int[4];
        ratio = Mathf.Clamp01(ratio);

        int L9 = maxPwmPerNozzle.TryGetValue((0x01, 9), out var vL9) ? vL9 : 255;
        int L10 = maxPwmPerNozzle.TryGetValue((0x01, 10), out var vL10) ? vL10 : 255;
        int R9 = maxPwmPerNozzle.TryGetValue((0x02, 9), out var vR9) ? vR9 : 255;
        int R10 = maxPwmPerNozzle.TryGetValue((0x02, 10), out var vR10) ? vR10 : 255;

        left[0] = 0;
        left[3] = 0;
        right[0] = 0;
        right[3] = 0;

        left[1] = Mathf.RoundToInt(L9 * ratio);
        left[2] = Mathf.RoundToInt(L10 * ratio);
        right[1] = Mathf.RoundToInt(R9 * ratio);
        right[2] = Mathf.RoundToInt(R10 * ratio);
    }

    void ComputeDirectionalPWM(Vector3 worldDir, out int[] left, out int[] right)
    {
        left = new int[4];
        right = new int[4];
        if (worldDir == Vector3.zero) return;

        float I = Mathf.Clamp01(worldDir.magnitude);

        if (!omniDirectional)
        {
            float scaledI = I * omniScale;

            float[] allOpen = { scaledI, scaledI, scaledI, scaledI };
            left = RatiosToPwm(0x01, allOpen);
            right = RatiosToPwm(0x02, allOpen);
        }
        else
        {
            Vector3 vHead = (headTransform ? Quaternion.Inverse(headTransform.rotation) * worldDir : worldDir).normalized;
            float[] ratiosL = new float[4];
            float[] ratiosR = new float[4];

            foreach (var nozzle in nozzleDirections)
            {
                byte boardId = nozzle.Key.board;
                int pin = nozzle.Key.pin;
                Vector3 n_i = nozzle.Value.normalized;

                float s_i = Mathf.Max(0, Vector3.Dot(vHead, n_i));
                int index = pin == 6 ? 0 : pin == 9 ? 1 : pin == 10 ? 2 : 3;

                if (boardId == 0x01) ratiosL[index] = s_i * I;
                else ratiosR[index] = s_i * I;
            }
            left = RatiosToPwm(0x01, ratiosL);
            right = RatiosToPwm(0x02, ratiosR);
        }
    }

    void SendBothBoards(int[] left, int[] right)
    {
        SendPWM(0x01, left[0], left[1], left[2], left[3]);
        SendPWM(0x02, right[0], right[1], right[2], right[3]);
    }

    void LeavesDisableBursts()
    {
        foreach (var ps in _leafPS)
        {
            var em = ps.emission;
            int count = em.burstCount;
            for (int i = count - 1; i >= 0; i--) em.SetBurst(i, new ParticleSystem.Burst(0, 0));
        }
    }

    void LeavesEnsurePlayingAtZero()
    {
        foreach (var ps in _leafPS)
        {
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;

            var fol = ps.forceOverLifetime;
            fol.enabled = true;
            fol.space = ParticleSystemSimulationSpace.World;
            fol.x = new ParticleSystem.MinMaxCurve(0f);
            fol.y = new ParticleSystem.MinMaxCurve(-0.4f);
            fol.z = new ParticleSystem.MinMaxCurve(0f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(-leavesTurbulenceXZ.x, leavesTurbulenceXZ.x);
            vol.z = new ParticleSystem.MinMaxCurve(-leavesTurbulenceXZ.y, leavesTurbulenceXZ.y);

            if (!ps.isPlaying) ps.Play();
        }
    }

    void UpdateLeavesByWind()
    {
        if (windZone == null || _leafPS.Count == 0) return;

        Vector3 targetDirection = windZone.transform.forward;

        float k = Mathf.InverseLerp(0.2f, 1f, windZone.windMain);
        k = Mathf.Clamp01(k);

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            leavesRoot.rotation = Quaternion.LookRotation(targetDirection);
        }

        foreach (var ps in _leafPS)
        {
            var main = ps.main;
            var em = ps.emission;

            em.rateOverTime = leavesRateMax * k;

            main.startSpeed = Mathf.Lerp(0f, leavesSpeedMax, k);

            var fol = ps.forceOverLifetime;
            fol.enabled = true;
            fol.space = ParticleSystemSimulationSpace.Local;

            fol.z = new ParticleSystem.MinMaxCurve(main.startSpeed.constant);

            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }
}