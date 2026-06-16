using UnityEngine;

public class RockCollapseTrigger : MonoBehaviour
{
    [Header("Collapse Settings")]
    public Collider triggerArea;
    public GameObject rockGroup;
    public float downForce = 5f;

    [Header("Audio Settings")]
    public AudioSource collapseSFX;

    private bool _hasTriggered = false;
    private Rigidbody[] _rockRbs;
    private Transform _playerHead;

    void Start()
    {
        _playerHead = Camera.main.transform;
        if (rockGroup != null)
        {
            _rockRbs = rockGroup.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in _rockRbs) rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (_hasTriggered || triggerArea == null || _playerHead == null) return;

        if (triggerArea.bounds.Contains(_playerHead.position))
        {
            TriggerCollapse();
        }
    }

    void TriggerCollapse()
    {
        _hasTriggered = true;

        if (collapseSFX) collapseSFX.Play();

        foreach (var rb in _rockRbs)
        {
            rb.isKinematic = false;
            rb.AddForce(Vector3.down * downForce + Random.insideUnitSphere, ForceMode.Impulse);
        }

        if (MineWindController.Instance != null && rockGroup != null)
        {
            MineWindController.Instance.TriggerCollapseWind(rockGroup.transform.position);
        }
    }
}