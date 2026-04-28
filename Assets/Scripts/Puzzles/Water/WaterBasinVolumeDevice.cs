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

    [Header("Target")]
    [SerializeField] private WaterBasinTarget target;

    [Header("Operation")]
    [SerializeField] private VolumeOperation operation = VolumeOperation.Remove;
    [SerializeField] private float amount = 1.0f;

    [Tooltip("If enabled, this device keeps applying the operation while activated.")]
    [SerializeField] private bool continuous;

    [Tooltip("Volume applied per second while Continuous is enabled.")]
    [SerializeField] private float amountPerSecond = 1.0f;

    [Tooltip("If enabled, Activate can only run once.")]
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

    public void AddOnce()
    {
        ExecuteExternal(VolumeOperation.Add, amount);
    }

    [ContextMenu("Remove Once")]
    public void RemoveOnce()
    {
        ExecuteExternal(VolumeOperation.Remove, amount);
    }

    [ContextMenu("Add Once")]
    private void AddOnceFromContextMenu()
    {
        AddOnce();
    }

    public void AddCustom(float customAmount)
    {
        ExecuteExternal(VolumeOperation.Add, customAmount);
    }

    public void RemoveCustom(float customAmount)
    {
        ExecuteExternal(VolumeOperation.Remove, customAmount);
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

        target.RemoveAllWater();
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
                target.AddWater(requestedAmount);
                break;
            case VolumeOperation.Remove:
                target.RemoveWater(requestedAmount);
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
