using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Rendering;

/// <summary>
/// Automatically fixes the MaterialPropertyBlockHelper renderer reference on the Highlight Interaction Affordance.
/// This script finds the Visuals child object's MeshRenderer and assigns it to the affordance system.
/// </summary>
[RequireComponent(typeof(Transform))]
public class BasketballAffordanceFixer : MonoBehaviour
{
    [Header("Auto-Fix Settings")]
    [Tooltip("The name of the child object containing the MeshRenderer (usually 'Visuals').")]
    [SerializeField] private string visualsChildName = "Visuals";
    
    [Tooltip("The name of the child object containing the affordance system (usually 'Highlight Interaction Affordance').")]
    [SerializeField] private string affordanceChildName = "Highlight Interaction Affordance";

    private void Awake()
    {
        FixAffordanceRenderer();
    }

    private void OnEnable()
    {
        // Fix on enable to catch any initialization issues
        FixAffordanceRenderer();
    }

    private void Start()
    {
        // Also fix in Start in case the affordance system initializes after Awake
        FixAffordanceRenderer();
    }

    private void OnValidate()
    {
        // Only fix in editor, not at runtime
        if (Application.isPlaying)
            return;
            
        FixAffordanceRenderer();
    }

    private void FixAffordanceRenderer()
    {
        // Find the Visuals child object
        Transform visualsTransform = transform.Find(visualsChildName);
        if (visualsTransform == null)
        {
            Debug.LogWarning($"[BasketballAffordanceFixer] Could not find '{visualsChildName}' child object on {gameObject.name}.", this);
            return;
        }

        // Get the MeshRenderer from Visuals
        MeshRenderer visualsRenderer = visualsTransform.GetComponent<MeshRenderer>();
        if (visualsRenderer == null)
        {
            Debug.LogWarning($"[BasketballAffordanceFixer] Could not find MeshRenderer on '{visualsChildName}' child object.", this);
            return;
        }

        // Find the Highlight Interaction Affordance child object
        Transform affordanceTransform = transform.Find(affordanceChildName);
        if (affordanceTransform == null)
        {
            // Try to find it by searching for MaterialPropertyBlockHelper
            MaterialPropertyBlockHelper helper = GetComponentInChildren<MaterialPropertyBlockHelper>();
            if (helper != null)
            {
                // Found it, assign the renderer
                helper.rendererTarget = visualsRenderer;
                Debug.Log($"[BasketballAffordanceFixer] Fixed MaterialPropertyBlockHelper renderer reference on {gameObject.name}.", this);
            }
            return;
        }

        // Get all MaterialPropertyBlockHelper components in the affordance child
        MaterialPropertyBlockHelper[] helpers = affordanceTransform.GetComponentsInChildren<MaterialPropertyBlockHelper>();
        foreach (var helper in helpers)
        {
            if (helper.rendererTarget == null)
            {
                helper.rendererTarget = visualsRenderer;
                Debug.Log($"[BasketballAffordanceFixer] Fixed MaterialPropertyBlockHelper renderer reference on {gameObject.name}.", this);
            }
        }

        // Disable affordance receivers that use shader properties not available in URP Lit
        // URP Lit doesn't have _RimColor and _RimPower properties
        // Do this for all MaterialPropertyBlockHelpers found (not just in affordanceTransform)
        MaterialPropertyBlockHelper[] allHelpers = GetComponentsInChildren<MaterialPropertyBlockHelper>(true);
        foreach (var helper in allHelpers)
        {
            // Fix renderer reference if needed
            if (helper.rendererTarget == null && visualsRenderer != null)
            {
                helper.rendererTarget = visualsRenderer;
            }

            // Disable problematic receivers on this helper's GameObject
            FloatMaterialPropertyAffordanceReceiver[] floatReceivers = helper.GetComponents<FloatMaterialPropertyAffordanceReceiver>();
            foreach (var receiver in floatReceivers)
            {
                string propName = receiver.floatPropertyName;
                if (propName == "_RimPower" || propName == "_RimColor")
                {
                    receiver.enabled = false;
                    Debug.Log($"[BasketballAffordanceFixer] Disabled FloatMaterialPropertyAffordanceReceiver using unsupported property '{propName}' on {helper.gameObject.name}.", this);
                }
            }

            ColorMaterialPropertyAffordanceReceiver[] colorReceivers = helper.GetComponents<ColorMaterialPropertyAffordanceReceiver>();
            foreach (var receiver in colorReceivers)
            {
                string propName = receiver.colorPropertyName;
                if (propName == "_RimColor" || propName == "_RimPower")
                {
                    receiver.enabled = false;
                    Debug.Log($"[BasketballAffordanceFixer] Disabled ColorMaterialPropertyAffordanceReceiver using unsupported property '{propName}' on {helper.gameObject.name}.", this);
                }
            }
        }
    }
}

