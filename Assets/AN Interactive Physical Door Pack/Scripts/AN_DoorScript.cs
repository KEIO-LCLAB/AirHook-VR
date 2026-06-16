using UnityEngine;

public class AN_DoorScript : MonoBehaviour
{
    [Header("General")]
    [Tooltip("为 false 时不可用")]
    public bool Locked = false;

    [Tooltip("true 表示仅允许远程调用（例如拉杆触发），本地键盘 E 不生效")]
    public bool Remote = true;

    [Tooltip("允许开门")]
    public bool CanOpen = true;

    [Tooltip("红/蓝钥匙锁（如不用就都设为 false）")]
    public bool RedLocked = false, BlueLocked = false;

    [Tooltip("当前是否已开门（运行时只读）")]
    public bool isOpened = false;

    [Header("Motor Settings")]
    [Tooltip("目标开门角度（度）")]
    public float OpenAngle = 120f;

    [Tooltip("电机角速度（度/秒）。方向由门开启方向决定，常用正值；若门需向负角度开，可在脚本中切换为负值。")]
    public float MotorSpeedDegPerSec = 90f;

    [Tooltip("电机最大力，不够就增大")]
    public float MotorForce = 1200f;

    [Header("Audio")]
    public AudioSource audioSource;          // 拖有 AudioSource 的物体
    public AudioClip doorOpenClip;           // 拖 DoorOpen 音效

    // 依赖组件
    [HideInInspector] public Rigidbody rbDoor;
    [HideInInspector] public HingeJoint hinge;


    AN_HeroInteractive heroInteractive;

    void Start()
    {
        rbDoor = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();
        heroInteractive = FindObjectOfType<AN_HeroInteractive>();

        if (hinge == null || rbDoor == null)
        {
            Debug.LogError("AN_DoorScript 需要挂在带 Rigidbody + HingeJoint 的门物体上。");
            enabled = false;
            return;
        }

        // 建议的 Rigidbody 设置
        rbDoor.isKinematic = false;
        rbDoor.angularDrag = Mathf.Max(0.05f, rbDoor.angularDrag); // 太大易“动一下就停”

        // 基础 Hinge 设置：由脚本接管 Limits 和 Motor
        hinge.useLimits = true;
        hinge.useMotor = false;   // 运行时再开启
        hinge.enableCollision = false;

        // 关门状态：把限位收紧为 0°（注意：以 Hinge 的 0° 为关门位置）
        SetLimitsClosed();
    }

    void Update()
    {
        // 本地测试键盘（仅当 Remote=false 时）
        if (!Remote && Input.GetKeyDown(KeyCode.E))
            Action();
    }

    /// <summary>
    /// 仅“开门一次”：把限位放到 OpenAngle，开启电机让门自动转到位并停住
    /// </summary>
    public void Action()
    {
        if (Locked || isOpened || !CanOpen) return;

        // 钥匙判定（如果你的项目没用到钥匙，这段也不会影响）
        if (heroInteractive != null)
        {
            if (RedLocked && heroInteractive.RedKey) { RedLocked = false; heroInteractive.RedKey = false; }
            if (BlueLocked && heroInteractive.BlueKey) { BlueLocked = false; heroInteractive.BlueKey = false; }
        }
        if (RedLocked || BlueLocked) return;

        isOpened = true;

        // 播放开门音效
        if (audioSource && doorOpenClip)
            audioSource.PlayOneShot(doorOpenClip);

        // 设置“开门”限位
        // —— 若门应向“负”角度开启，请改用：SetLimitsNegative(OpenAngle);
        SetLimitsPositive(OpenAngle);

        // 开启电机
        JointMotor m = hinge.motor;
        m.freeSpin = false;
        m.force = MotorForce;

        // 正向开门：角速度为正；若用负向开门，改成负值（见下方注释）
        m.targetVelocity = Mathf.Abs(MotorSpeedDegPerSec);
        hinge.motor = m;
        hinge.useMotor = true;
    }

    void FixedUpdate()
    {
        if (!hinge) return;

        if (isOpened)
        {
            // 到达角度后，不关闭 useMotor，而是将目标速度设为很小的一个正值
            // 这样电机就像一只手，一直把门顶在限位器上，防止它回弹
            if (hinge.angle >= OpenAngle - 1f)
            {
                JointMotor m = hinge.motor;
                m.targetVelocity = 1f; // 极小的维持速度
                m.force = MotorForce * 0.1f; // 较小的维持力
                hinge.motor = m;
            }
        }
    }

    // ===== 工具：设置限位 =====

    // 关门（0°）
    void SetLimitsClosed()
    {
        var lim = hinge.limits;
        lim.min = 0f;
        lim.max = 0f;
        hinge.limits = lim;
    }

    // 正向开门：0 -> +OpenAngle
    void SetLimitsPositive(float angle)
    {
        var lim = hinge.limits;
        lim.min = 0f;
        lim.max = Mathf.Abs(angle);
        hinge.limits = lim;

        // 电机角速度朝正向（确保 Action() 里 targetVelocity 为正）
        // 若你发现门是向反方向开，请改用 SetLimitsNegative(angle) 并把 targetVelocity 设为负值
    }

    // 反向开门：-OpenAngle -> 0
    void SetLimitsNegative(float angle)
    {
        var lim = hinge.limits;
        lim.min = -Mathf.Abs(angle);
        lim.max = 0f;
        hinge.limits = lim;

        // 使用该限位方案时，请把 Action() 里的 m.targetVelocity 改为负：
        // m.targetVelocity = -Mathf.Abs(MotorSpeedDegPerSec);
    }
}
