using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MaterialToggleSwitcher : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer = null;
    [SerializeField] private Material defaultMaterial = null;
    [SerializeField] private Material alternateMaterial = null;
    [SerializeField] private bool useAlternateMaterial;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        Apply();
    }

    [ContextMenu("Use Default Material")]
    public void UseDefaultMaterial()
    {
        useAlternateMaterial = false;
        Apply();
    }

    [ContextMenu("Use Alternate Material")]
    public void UseAlternateMaterial()
    {
        useAlternateMaterial = true;
        Apply();
    }

    [ContextMenu("Toggle Material")]
    public void ToggleMaterial()
    {
        useAlternateMaterial = !useAlternateMaterial;
        Apply();
    }

    private void Apply()
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material selectedMaterial = useAlternateMaterial ? alternateMaterial : defaultMaterial;
        if (selectedMaterial == null)
        {
            return;
        }

        targetRenderer.sharedMaterial = selectedMaterial;
    }
}
