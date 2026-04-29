using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class WaterBasinVolumeDevice : MonoBehaviour
{
    private enum VolumeOperation
    {
        Add,
        Remove
    }

    private enum ControlScope
    {
        ConnectedGroup,
        TargetOnly
    }

    [Header("Target")]
    [SerializeField] private WaterBasinTarget target;

    [Header("Operation")]
    [SerializeField] private ControlScope controlScope = ControlScope.ConnectedGroup;
    [SerializeField] private VolumeOperation operation = VolumeOperation.Remove;
    [SerializeField] private float amount = 1.0f;

    [Tooltip("활성화하면 장치가 켜져 있는 동안 물 추가/제거 동작을 계속 적용합니다.")]
    [SerializeField] private bool continuous;

    [Tooltip("연속 동작이 켜져 있을 때 초당 적용할 물의 양입니다.")]
    [SerializeField] private float amountPerSecond = 1.0f;

    [Tooltip("활성화하면 장치 활성화 동작이 한 번만 실행됩니다.")]
    [SerializeField] private bool oneShot;

    [SerializeField] private float cooldown = 0.0f;

    [Header("Runtime")]
    [SerializeField] private bool activated;
    [SerializeField] private bool hasUsedOneShot;
    [SerializeField] private float nextUseTime;

    [Header("Events")]
    [SerializeField] private UnityEvent onVolumeChanged = new UnityEvent();
    [SerializeField] private UnityEvent onDeviceBlocked = new UnityEvent();

    public bool Activated => activated;
    public bool Continuous => continuous;
    public bool CanUse => CanExecute();
    public bool ControlsConnectedGroup => controlScope == ControlScope.ConnectedGroup;

    public void SetTarget(WaterBasinTarget newTarget)
    {
        target = newTarget;
    }

    public void SetConnectedGroupScope()
    {
        controlScope = ControlScope.ConnectedGroup;
    }

    public void SetTargetOnlyScope()
    {
        controlScope = ControlScope.TargetOnly;
    }

    private void Reset()
    {
        target = GetComponentInParent<WaterBasinTarget>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponentInParent<WaterBasinTarget>();
        }
    }

    private void OnValidate()
    {
        amount = Mathf.Max(0.0f, amount);
        amountPerSecond = Mathf.Max(0.0f, amountPerSecond);
        cooldown = Mathf.Max(0.0f, cooldown);
    }

    private void Update()
    {
        if (!activated || !continuous || target == null)
        {
            return;
        }

        float deltaAmount = amountPerSecond * Time.deltaTime;
        if (deltaAmount > 0.0f)
        {
            ExecuteAmount(deltaAmount, false);
        }
    }

    public void Activate()
    {
        if (!CanExecute())
        {
            onDeviceBlocked.Invoke();
            return;
        }

        activated = true;

        if (!continuous)
        {
            ExecuteAmount(amount, true);
            return;
        }

        MarkUsed();
    }

    public void Deactivate()
    {
        activated = false;
    }

    public void Toggle()
    {
        if (activated)
        {
            Deactivate();
        }
        else
        {
            Activate();
        }
    }

    [ContextMenu("Add Once")]
    public void AddOnce()
    {
        ExecuteExternal(VolumeOperation.Add, amount);
    }

    public void SetAddOperation()
    {
        operation = VolumeOperation.Add;
    }

    public void SetRemoveOperation()
    {
        operation = VolumeOperation.Remove;
    }

    [ContextMenu("Remove Once")]
    public void RemoveOnce()
    {
        ExecuteExternal(VolumeOperation.Remove, amount);
    }

    public void AddCustom(float customAmount)
    {
        ExecuteExternal(VolumeOperation.Add, customAmount);
    }

    public void RemoveCustom(float customAmount)
    {
        ExecuteExternal(VolumeOperation.Remove, customAmount);
    }

    [ContextMenu("Fill All")]
    public void FillAll()
    {
        if (target == null)
        {
            onDeviceBlocked.Invoke();
            return;
        }

        if (!CanExecute())
        {
            onDeviceBlocked.Invoke();
            return;
        }

        float remainingCapacity = target.GetConnectedGroupCapacity() - target.GetConnectedGroupVolume();
        if (controlScope == ControlScope.TargetOnly)
        {
            remainingCapacity = target.Capacity - target.CurrentVolume;
        }

        if (remainingCapacity <= 0.0f)
        {
            onDeviceBlocked.Invoke();
            return;
        }

        if (controlScope == ControlScope.TargetOnly)
        {
            target.AddWaterToThisTarget(remainingCapacity);
        }
        else
        {
            target.AddWater(remainingCapacity);
        }

        onVolumeChanged.Invoke();
        MarkUsed();
    }

    [ContextMenu("Drain All")]
    public void DrainAll()
    {
        if (target == null)
        {
            onDeviceBlocked.Invoke();
            return;
        }

        if (!CanExecute())
        {
            onDeviceBlocked.Invoke();
            return;
        }

        if (controlScope == ControlScope.TargetOnly)
        {
            target.RemoveAllWaterFromThisTarget();
        }
        else
        {
            target.RemoveAllWater();
        }

        onVolumeChanged.Invoke();
        MarkUsed();
    }

    public void ResetOneShot()
    {
        hasUsedOneShot = false;
    }

    private void ExecuteExternal(VolumeOperation requestedOperation, float requestedAmount)
    {
        if (!CanExecute())
        {
            onDeviceBlocked.Invoke();
            return;
        }

        VolumeOperation previousOperation = operation;
        operation = requestedOperation;
        ExecuteAmount(requestedAmount, true);
        operation = previousOperation;
    }

    private void ExecuteAmount(float requestedAmount, bool markUsed)
    {
        if (target == null || requestedAmount <= 0.0f)
        {
            return;
        }

        switch (operation)
        {
            case VolumeOperation.Add:
                if (controlScope == ControlScope.TargetOnly)
                {
                    target.AddWaterToThisTarget(requestedAmount);
                }
                else
                {
                    target.AddWater(requestedAmount);
                }
                break;
            case VolumeOperation.Remove:
                if (controlScope == ControlScope.TargetOnly)
                {
                    target.RemoveWaterFromThisTarget(requestedAmount);
                }
                else
                {
                    target.RemoveWater(requestedAmount);
                }
                break;
        }

        onVolumeChanged.Invoke();

        if (markUsed)
        {
            MarkUsed();
        }
    }

    private bool CanExecute()
    {
        if (target == null)
        {
            return false;
        }

        if (oneShot && hasUsedOneShot)
        {
            return false;
        }

        return !Application.isPlaying || Time.time >= nextUseTime;
    }

    private void MarkUsed()
    {
        if (oneShot)
        {
            hasUsedOneShot = true;
        }

        if (Application.isPlaying && cooldown > 0.0f)
        {
            nextUseTime = Time.time + cooldown;
        }
    }
}
