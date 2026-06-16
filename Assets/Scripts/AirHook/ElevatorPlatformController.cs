using System.Collections;
using UnityEngine;

public class ElevatorPlatformController : MonoBehaviour
{
    [Header("Scene Refs")]
    public Transform platform;
    public Transform xrRigRoot;
    public Transform headTransform;
    public Transform groundStop;
    public float startDelay = 0.5f;

    [Header("Audio")]
    public AudioSource elevatorAudio;
    public AudioSource ambientAudio;

    [Header("Motion")]
    public float travelDuration = 5f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Hook & Chain")]
    public GameObject hookChainRoot;
    public float hideBeforeArrival = 1f;

    [Header("Wind (via WindHub)")]
    public int windPriority = 5;

    [Header("Wind: Movement Pulse Link")]
    public MovementWindPulse movementWind;

    bool started = false;
    Vector3 startPos, endPos;
    int _elevHandle = 0;

    void Awake()
    {
        if (!platform) platform = transform;
        if (!movementWind) movementWind = FindObjectOfType<MovementWindPulse>();

        if (elevatorAudio) elevatorAudio.loop = true;
    }

    void Start()
    {
        float endY = groundStop ? groundStop.position.y : platform.position.y;
        startPos = platform.position;
        endPos = new Vector3(startPos.x, endY, startPos.z);
    }

    void OnTriggerEnter(Collider other)
    {
        if (started) return;
        if (IsPlayer(other))
        {
            started = true;
            StartCoroutine(Sequence());
        }
    }

    bool IsPlayer(Collider c)
    {
        if (headTransform && (c.transform == headTransform || c.transform.IsChildOf(headTransform))) return true;
        if (xrRigRoot && (c.transform == xrRigRoot || c.transform.IsChildOf(xrRigRoot))) return true;
        return false;
    }

    IEnumerator Sequence()
    {
        if (ambientAudio)
        {
            ambientAudio.Stop();
            ambientAudio.enabled = false;
        }
        if (elevatorAudio)
        {
            elevatorAudio.enabled = true;
            elevatorAudio.Play();
        }

        if (movementWind) movementWind.BeginElevator();

        Transform oldParent = null;
        if (xrRigRoot) oldParent = xrRigRoot.parent;

        yield return new WaitForSeconds(startDelay);

        if (xrRigRoot) xrRigRoot.SetParent(platform, true);

        var moveCo = StartCoroutine(RisePlatform());
        var windCo = StartCoroutine(ElevatorWindFlow());

        if (hookChainRoot && hookChainRoot.activeSelf)
        {
            float delay = Mathf.Clamp(travelDuration - hideBeforeArrival, 0f, travelDuration);
            StartCoroutine(HideHookChainAfter(delay));
        }

        yield return moveCo;

        yield return StartCoroutine(RampSustain(
            _elevHandle,
            new float[] { 0.8f, 0.8f, 0f, 0f },
            new float[] { 0.4f, 0f, 0f, 0.4f },
            new float[] { 0.8f, 0.8f, 0f, 0f },
            new float[] { 0.4f, 0f, 0f, 0.4f },
            1f
        ));

        if (elevatorAudio) elevatorAudio.Stop();

        if (xrRigRoot) xrRigRoot.SetParent(oldParent, true);
    }

    IEnumerator HideHookChainAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (hookChainRoot) hookChainRoot.SetActive(false);
    }

    IEnumerator RisePlatform()
    {
        if (travelDuration <= 1e-4f || platform.position == endPos) yield break;
        Vector3 s = startPos, e = endPos;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / travelDuration;
            platform.position = Vector3.LerpUnclamped(s, e, ease.Evaluate(Mathf.Clamp01(t)));
            yield return null;
        }
        platform.position = e;
    }

    IEnumerator ElevatorWindFlow()
    {
        _elevHandle = WindHub.Instance.BeginSustain(
            new float[] { 0f, 0f, 0f, 0f },
            new float[] { 0f, 0f, 0f, 0f },
            windPriority
        );

        yield return RampSustain(_elevHandle,
            new float[] { 0f, 0f, 0f, 0f }, new float[] { 1f, 1f, 0f, 0f },
            new float[] { 0f, 0f, 0f, 0f }, new float[] { 1f, 1f, 0f, 0f }, 1f);

        yield return RampSustain(_elevHandle,
            new float[] { 1f, 1f, 0f, 0f }, new float[] { 0.9f, 0.9f, 0f, 0f },
            new float[] { 1f, 1f, 0f, 0f }, new float[] { 0.9f, 0.9f, 0f, 0f }, 1f);
    }

    IEnumerator RampSustain(int handle, float[] fromL, float[] toL, float[] fromR, float[] toR, float seconds)
    {
        seconds = Mathf.Max(0.01f, seconds);
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
        return new float[] {
            Mathf.Lerp(a[0], b[0], k),
            Mathf.Lerp(a[1], b[1], k),
            Mathf.Lerp(a[2], b[2], k),
            Mathf.Lerp(a[3], b[3], k)
        };
    }
}