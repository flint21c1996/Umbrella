using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class WaterBasinVolumeDevice : MonoBehaviour
{
    private enum VolumeOperation
    {
        AddAmount,
        RemoveAmount,
        FillAll,
        DrainAll,
        SetWaterDepth,
        SetSurfaceWorldY
    }

    private enum ControlScope
    {
        ConnectedGroup,
        TargetOnly
    }

    [Header("Target")]
    [Tooltip("이 장치가 물을 추가하거나 제거할 기준 WaterBasinTarget입니다. 비워두면 부모에서 자동으로 찾습니다.")]
    [SerializeField] private WaterBasinTarget target;

    [Header("Operation")]
    [Tooltip("물을 제어할 범위입니다. Connected Group은 연결된 물 그룹 전체를, Target Only는 지정한 타겟 하나만 제어합니다.")]
    [SerializeField] private ControlScope controlScope = ControlScope.ConnectedGroup;

    [Tooltip("장치가 실행될 때 수행할 물 조작입니다. 일정량 추가/제거, 전체 채우기/비우기, 지정 수위 맞추기를 선택합니다.")]
    [SerializeField] private VolumeOperation operation = VolumeOperation.RemoveAmount;

    [Tooltip("한 번 실행할 때 추가하거나 제거할 물의 양입니다. 연속 동작이 꺼져 있을 때 사용합니다.")]
    [SerializeField] private float amount = 1.0f;

    [Tooltip("Set Water Depth 동작에서 사용할 목표 물 깊이입니다. 기준 타겟의 바닥 높이에서 이 값만큼 위로 수면을 맞춥니다.")]
    [SerializeField] private float targetWaterDepth = 1.0f;

    [Tooltip("Set Surface World Y 동작에서 사용할 목표 수면 월드 Y 좌표입니다.")]
    [SerializeField] private float targetSurfaceWorldY;

    [Tooltip("활성화하면 장치가 켜져 있는 동안 물 추가/제거 동작을 계속 적용합니다.")]
    [SerializeField] private bool continuous;

    [Tooltip("연속 동작이 켜져 있을 때 초당 적용할 물의 양입니다.")]
    [SerializeField] private float amountPerSecond = 1.0f;

    [Tooltip("활성화하면 장치 활성화 동작이 한 번만 실행됩니다.")]
    [SerializeField] private bool oneShot;

    [Tooltip("장치가 한 번 실행된 뒤 다시 실행될 수 있을 때까지 기다리는 시간(초)입니다.")]
    [SerializeField] private float cooldown = 0.0f;

    [Header("Runtime")]
    [Tooltip("현재 장치가 활성화되어 있는지 나타냅니다. 연속 동작일 때 활성 상태 동안 계속 물을 조작합니다.")]
    [SerializeField] private bool activated;

    [Tooltip("One Shot 설정에서 이미 한 번 사용되었는지 나타냅니다. ResetOneShot으로 다시 사용할 수 있습니다.")]
    [SerializeField] private bool hasUsedOneShot;

    [Tooltip("쿨다운이 끝나고 다음 실행이 가능해지는 게임 시간입니다.")]
    [SerializeField] private float nextUseTime;

    [Header("Events")]
    [Tooltip("물이 실제로 추가되거나 제거되었을 때 호출됩니다.")]
    [SerializeField] private UnityEvent onVolumeChanged = new UnityEvent();

    [Tooltip("타겟이 없거나 One Shot/쿨다운 조건 때문에 장치를 실행할 수 없을 때 호출됩니다.")]
    [SerializeField] private UnityEvent onDeviceBlocked = new UnityEvent();

    public bool Activated => activated;
    public bool Continuous => continuous;
    public bool CanUse => CanExecute();
    public bool ControlsConnectedGroup => controlScope == ControlScope.ConnectedGroup;

    public void SetTarget(WaterBasinTarget newTarget)
    {
        target = newTarget;
    }

    public void SetAmount(float newAmount)
    {
        amount = Mathf.Max(0.0f, newAmount);
    }

    public void SetAmountPerSecond(float newAmountPerSecond)
    {
        amountPerSecond = Mathf.Max(0.0f, newAmountPerSecond);
    }

    public void SetTargetWaterDepth(float newTargetWaterDepth)
    {
        targetWaterDepth = Mathf.Max(0.0f, newTargetWaterDepth);
    }

    public void SetTargetSurfaceWorldY(float newTargetSurfaceWorldY)
    {
        targetSurfaceWorldY = newTargetSurfaceWorldY;
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
        targetWaterDepth = Mathf.Max(0.0f, targetWaterDepth);
        amountPerSecond = Mathf.Max(0.0f, amountPerSecond);
        cooldown = Mathf.Max(0.0f, cooldown);
    }

    private void Update()
    {
        if (!activated || !continuous || target == null || !SupportsContinuousOperation())
        {
            return;
        }

        float deltaAmount = amountPerSecond * Time.deltaTime;
        if (deltaAmount > 0.0f)
        {
            ExecuteOperation(operation, deltaAmount, false);
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

        if (!continuous || !SupportsContinuousOperation())
        {
            ExecuteOperation(operation, amount, true);
            activated = false;
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
        ExecuteExternal(VolumeOperation.AddAmount, amount);
    }

    public void SetAddOperation()
    {
        operation = VolumeOperation.AddAmount;
    }

    public void SetRemoveOperation()
    {
        operation = VolumeOperation.RemoveAmount;
    }

    [ContextMenu("Remove Once")]
    public void RemoveOnce()
    {
        ExecuteExternal(VolumeOperation.RemoveAmount, amount);
    }

    public void AddCustom(float customAmount)
    {
        ExecuteExternal(VolumeOperation.AddAmount, customAmount);
    }

    public void RemoveCustom(float customAmount)
    {
        ExecuteExternal(VolumeOperation.RemoveAmount, customAmount);
    }

    public void SetFillAllOperation()
    {
        operation = VolumeOperation.FillAll;
    }

    public void SetDrainAllOperation()
    {
        operation = VolumeOperation.DrainAll;
    }

    public void SetWaterDepthOperation()
    {
        operation = VolumeOperation.SetWaterDepth;
    }

    public void SetSurfaceWorldYOperation()
    {
        operation = VolumeOperation.SetSurfaceWorldY;
    }

    [ContextMenu("Fill All")]
    public void FillAll()
    {
        ExecuteExternal(VolumeOperation.FillAll, 0.0f);
    }

    [ContextMenu("Drain All")]
    public void DrainAll()
    {
        ExecuteExternal(VolumeOperation.DrainAll, 0.0f);
    }

    [ContextMenu("Set Water Depth")]
    public void SetWaterDepthOnce()
    {
        ExecuteExternal(VolumeOperation.SetWaterDepth, 0.0f);
    }

    public void SetWaterDepthCustom(float customWaterDepth)
    {
        ExecuteCustomWaterDepth(Mathf.Max(0.0f, customWaterDepth));
    }

    [ContextMenu("Set Surface World Y")]
    public void SetSurfaceWorldYOnce()
    {
        ExecuteExternal(VolumeOperation.SetSurfaceWorldY, 0.0f);
    }

    public void SetSurfaceWorldYCustom(float customSurfaceWorldY)
    {
        ExecuteCustomSurfaceWorldY(customSurfaceWorldY);
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

        ExecuteOperation(requestedOperation, requestedAmount, true);
    }

    private void ExecuteOperation(VolumeOperation requestedOperation, float requestedAmount, bool markUsed)
    {
        if (target == null)
        {
            return;
        }

        switch (requestedOperation)
        {
            case VolumeOperation.AddAmount:
                if (requestedAmount <= 0.0f)
                {
                    return;
                }

                if (controlScope == ControlScope.TargetOnly)
                {
                    target.AddWaterToThisTarget(requestedAmount);
                }
                else
                {
                    target.AddWater(requestedAmount);
                }
                break;
            case VolumeOperation.RemoveAmount:
                if (requestedAmount <= 0.0f)
                {
                    return;
                }

                if (controlScope == ControlScope.TargetOnly)
                {
                    target.RemoveWaterFromThisTarget(requestedAmount);
                }
                else
                {
                    target.RemoveWater(requestedAmount);
                }
                break;
            case VolumeOperation.FillAll:
                float remainingCapacity = controlScope == ControlScope.TargetOnly
                    ? target.Capacity - target.CurrentVolume
                    : target.GetConnectedGroupCapacity() - target.GetConnectedGroupVolume();
                if (remainingCapacity <= 0.0f)
                {
                    onDeviceBlocked.Invoke();
                    return;
                }

                if (controlScope == ControlScope.TargetOnly)
                {
                    target.FillThisTarget();
                }
                else
                {
                    target.AddWater(remainingCapacity);
                }
                break;
            case VolumeOperation.DrainAll:
                float currentVolume = controlScope == ControlScope.TargetOnly
                    ? target.CurrentVolume
                    : target.GetConnectedGroupVolume();
                if (currentVolume <= 0.0f)
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
                break;
            case VolumeOperation.SetWaterDepth:
                ApplyWaterDepth(targetWaterDepth);
                break;
            case VolumeOperation.SetSurfaceWorldY:
                ApplySurfaceWorldY(targetSurfaceWorldY);
                break;
        }

        onVolumeChanged.Invoke();

        if (markUsed)
        {
            MarkUsed();
        }
    }

    private void ExecuteCustomWaterDepth(float customWaterDepth)
    {
        if (!CanExecute())
        {
            onDeviceBlocked.Invoke();
            return;
        }

        ApplyWaterDepth(customWaterDepth);
        onVolumeChanged.Invoke();
        MarkUsed();
    }

    private void ExecuteCustomSurfaceWorldY(float customSurfaceWorldY)
    {
        if (!CanExecute())
        {
            onDeviceBlocked.Invoke();
            return;
        }

        ApplySurfaceWorldY(customSurfaceWorldY);
        onVolumeChanged.Invoke();
        MarkUsed();
    }

    private void ApplyWaterDepth(float waterDepth)
    {
        if (controlScope == ControlScope.TargetOnly)
        {
            target.SetThisTargetWaterDepth(waterDepth);
        }
        else
        {
            target.SetWaterDepth(waterDepth);
        }
    }

    private void ApplySurfaceWorldY(float surfaceWorldY)
    {
        if (controlScope == ControlScope.TargetOnly)
        {
            target.SetThisTargetWaterSurfaceWorldY(surfaceWorldY);
        }
        else
        {
            target.SetWaterSurfaceWorldY(surfaceWorldY);
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

    private bool SupportsContinuousOperation()
    {
        return operation == VolumeOperation.AddAmount
            || operation == VolumeOperation.RemoveAmount;
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
