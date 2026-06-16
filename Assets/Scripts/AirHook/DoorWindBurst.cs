using System.Collections;
using UnityEngine;

public class DoorWindBurst : MonoBehaviour
{
    public AN_DoorScript door;
    public bool onlyOnce = true;

    [Header("Ramp Profile")]
    [Range(0, 1)] public float targetStrength = 0.8f;
    public float rampIn = 0.5f;
    public float hold = 0.0f;
    [Range(0, 1)] public float endStrength = 0.2f;
    public float fade = 3.0f;
    public bool keepAfterFade = false;
    public int windPriority = 6;

    [Header("Wind Mapping")]
    public Vector3 localWindDirection = new Vector3(0, 0, 1);
    public Transform windSpace;
    public Transform headTransform;

    bool _firedOnce = false;
    bool _lastOpened = false;
    Coroutine _co;

    void Start()
    {
        if (!door) door = GetComponentInParent<AN_DoorScript>();
        if (door) _lastOpened = door.isOpened;
    }

    void Update()
    {
        if (!door) return;
        if (!_lastOpened && door.isOpened)
        {
            if (!onlyOnce || (onlyOnce && !_firedOnce))
            {
                TriggerNow();
                _firedOnce = true;
            }
        }
        _lastOpened = door.isOpened;
    }

    public void TriggerNow()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RampedBurst());
    }

    public void OnDoorOpenedEvent() => TriggerNow();

    IEnumerator RampedBurst()
    {
        int handle = WindHub.Instance.BeginSustain(Zeros(), Zeros(), windPriority);

        float es = Mathf.Clamp01(endStrength);

        yield return Ramp(
            handle,
            Zeros(), FR_L(targetStrength),
            Zeros(), FR_R(targetStrength),
            Mathf.Max(0.01f, rampIn)
        );

        if (hold > 0f) yield return new WaitForSeconds(hold);

        yield return Ramp(
            handle,
            FR_L(targetStrength), FR_L(es),
            FR_R(targetStrength), FR_R(es),
            Mathf.Max(0.01f, fade)
        );

        if (!keepAfterFade)
        {
            WindHub.Instance.EndSustain(handle);
        }
    }

    float[] Zeros() => new float[] { 0f, 0f, 0f, 0f };

    float[] FR_L(float s)
    {
        ComputeNozzlePercents(s, out var left4, out var _);
        return left4;
    }

    float[] FR_R(float s)
    {
        ComputeNozzlePercents(s, out var _, out var right4);
        return right4;
    }

    IEnumerator Ramp(int handle, float[] fromL, float[] toL, float[] fromR, float[] toR, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            float k = t / seconds;
            WindHub.Instance.UpdateSustain(handle, Lerp4(fromL, toL, k), Lerp4(fromR, toR, k));
            t += Time.deltaTime;
            yield return null;
        }
        WindHub.Instance.UpdateSustain(handle, toL, toR);
    }

    float[] Lerp4(float[] a, float[] b, float k)
    {
        return new float[]
        {
            Mathf.Lerp(a[0], b[0], k),
            Mathf.Lerp(a[1], b[1], k),
            Mathf.Lerp(a[2], b[2], k),
            Mathf.Lerp(a[3], b[3], k),
        };
    }

    void ComputeNozzlePercents(float strength01, out float[] left4, out float[] right4)
    {
        strength01 = Mathf.Clamp01(strength01);
        left4 = new float[4] { strength01, 0f, 0f, strength01 };
        right4 = new float[4] { strength01, 0f, 0f, strength01 };
    }
}