using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class WindAreaConfig
{
    public Collider area;
    public Vector3 worldWindDirection = Vector3.forward;
    public float targetIntensity = 0.3f;
}

public class MineWindController : MonoBehaviour
{
    public static MineWindController Instance;

    [Header("Player & Area Tracking")]
    public List<WindAreaConfig> windZones;
    public Transform headTransform;

    [Header("Wind Settings")]
    public float roadWindBase = 0.2f;
    public float smoothSpeed = 3f;
    public int sustainPriority = 1;

    [Header("Audio")]
    public AudioSource narrowWindAudio;
    public float maxAudioVolume = 0.8f;

    private readonly Dictionary<int, Vector3> nLeft = new Dictionary<int, Vector3>
    {
        { 6,  new Vector3(-1,  1, -1).normalized },
        { 9,  new Vector3(-1,  1,  1).normalized },
        { 10, new Vector3(-1, -1,  1).normalized },
        { 11, new Vector3(-1, -1, -1).normalized },
    };

    private readonly Dictionary<int, Vector3> nRight = new Dictionary<int, Vector3>
    {
        { 6,  new Vector3( 1,  1, -1).normalized },
        { 9,  new Vector3( 1,  1,  1).normalized },
        { 10, new Vector3( 1, -1,  1).normalized },
        { 11, new Vector3( 1, -1, -1).normalized },
    };

    private float _currentStrength;
    private int _sustainHandle = 0;
    private Vector3 _activeWorldDir = Vector3.forward;

    void Awake() { Instance = this; if (!headTransform) headTransform = Camera.main.transform; }

    void Start()
    {
        if (WindHub.Instance != null)
        {
            _sustainHandle = WindHub.Instance.BeginSustain(new float[4], new float[4], sustainPriority);
        }
    }

    void Update()
    {
        if (_sustainHandle == 0 && WindHub.Instance != null)
        {
            _sustainHandle = WindHub.Instance.BeginSustain(new float[4], new float[4], sustainPriority);
        }

        bool insideAny = false;
        float targetPower = roadWindBase;
        Vector3 selectedDir = Vector3.forward;

        foreach (var zone in windZones)
        {
            if (zone.area != null && zone.area.bounds.Contains(headTransform.position))
            {
                insideAny = true;
                targetPower = zone.targetIntensity;
                selectedDir = zone.worldWindDirection.normalized;
                break;
            }
        }

        _activeWorldDir = selectedDir;
        _currentStrength = Mathf.Lerp(_currentStrength, targetPower, Time.deltaTime * smoothSpeed);

        ComputeDirectionalRatios(_activeWorldDir, _currentStrength, out float[] L, out float[] R);

        if (WindHub.Instance != null && _sustainHandle != 0)
        {
            WindHub.Instance.UpdateSustain(_sustainHandle, L, R);
        }

        UpdateAudio(insideAny);
    }

    public void TriggerCollapseWind(Vector3 collapsePos)
    {
        TriggerFrontWindBurst();
    }

    public void TriggerTeleportWind()
    {
        TriggerFrontWindBurst();
    }

    public void TriggerFrontWindBurst()
    {
        WindHub.Instance?.PostBurst(FrontL(1f), FrontR(1f), 2f, 2f, 0);
    }

    private void ComputeDirectionalRatios(Vector3 worldDir, float strength01, out float[] L, out float[] R)
    {
        L = new float[4];
        R = new float[4];

        if (!headTransform || worldDir.sqrMagnitude < 1e-6f || strength01 < 0.001f) return;

        Vector3 vHead = headTransform.InverseTransformDirection(worldDir).normalized;
        int[] pins = { 6, 9, 10, 11 };

        for (int i = 0; i < pins.Length; i++)
        {
            int pin = pins[i];
            L[i] = Mathf.Clamp01(Mathf.Max(0f, Vector3.Dot(vHead, nLeft[pin])) * strength01);
            R[i] = Mathf.Clamp01(Mathf.Max(0f, Vector3.Dot(vHead, nRight[pin])) * strength01);
        }
    }

    private float[] FrontL(float s)
    {
        s = Mathf.Clamp01(s);
        return new float[] { s, 0f, 0f, s };
    }

    private float[] FrontR(float s)
    {
        s = Mathf.Clamp01(s);
        return new float[] { s, 0f, 0f, s };
    }

    private void UpdateAudio(bool inZone)
    {
        if (!narrowWindAudio) return;
        narrowWindAudio.volume = Mathf.Lerp(narrowWindAudio.volume, inZone ? maxAudioVolume : 0, Time.deltaTime * smoothSpeed);
        if (inZone)
        {
            narrowWindAudio.transform.position = headTransform.position + _activeWorldDir * 2f;
            if (!narrowWindAudio.isPlaying) narrowWindAudio.Play();
        }
    }
}
