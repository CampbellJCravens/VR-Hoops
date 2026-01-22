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
        // Auto-finding is disabled. Visuals and Affordance transforms should be assigned via reference.
        // For now, try transform.Find as fallback but log error
        Transform visualsTransform = transform.Find(visualsChildName);

        // Get the MeshRenderer from Visuals
        MeshRenderer visualsRenderer = visualsTransform.GetComponent<MeshRenderer>();

        // Find the Highlight Interaction Affordance child object
        Transform affordanceTransform = transform.Find(affordanceChildName);
        MaterialPropertyBlockHelper helper = GetComponentInChildren<MaterialPropertyBlockHelper>();
        helper.rendererTarget = visualsRenderer;

        // Get all MaterialPropertyBlockHelper components in the affordance child
        MaterialPropertyBlockHelper[] helpers = affordanceTransform.GetComponentsInChildren<MaterialPropertyBlockHelper>();
        foreach (var h in helpers)
        {
            if (h.rendererTarget == null)
            {
                h.rendererTarget = visualsRenderer;
            }
        }

        // Disable affordance receivers that use shader properties not available in URP Lit
        // URP Lit doesn't have _RimColor and _RimPower properties
        // Do this for all MaterialPropertyBlockHelpers found (not just in affordanceTransform)
        MaterialPropertyBlockHelper[] allHelpers = GetComponentsInChildren<MaterialPropertyBlockHelper>(true);
        foreach (var h in allHelpers)
        {
            // Fix renderer reference if needed
            if (h.rendererTarget == null)
            {
                h.rendererTarget = visualsRenderer;
            }

            // Disable problematic receivers on this helper's GameObject
            FloatMaterialPropertyAffordanceReceiver[] floatReceivers = h.GetComponents<FloatMaterialPropertyAffordanceReceiver>();
            foreach (var receiver in floatReceivers)
            {
                string propName = receiver.floatPropertyName;
                if (propName == "_RimPower" || propName == "_RimColor")
                {
                    receiver.enabled = false;
                }
            }

            ColorMaterialPropertyAffordanceReceiver[] colorReceivers = h.GetComponents<ColorMaterialPropertyAffordanceReceiver>();
            foreach (var receiver in colorReceivers)
            {
                string propName = receiver.colorPropertyName;
                if (propName == "_RimColor" || propName == "_RimPower")
                {
                    receiver.enabled = false;
                }
            }
        }
    }
}

