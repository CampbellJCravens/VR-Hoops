using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Helper component that automatically adds the BasketballPalmTransformer to the XRGrabInteractable.
/// This allows you to configure the palm positioning settings directly on the basketball prefab.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class BasketballGrabAdjuster : MonoBehaviour
{
    [Header("Palm Positioning")]
    [Tooltip("Distance to offset the ball into the palm (perpendicular to hand forward). Positive values move the ball toward the palm.")]
    [SerializeField] private float palmOffsetDistance = 0.13f;
    
    [Tooltip("Additional depth offset to push the ball deeper into the hand (along hand forward). Negative values move deeper.")]
    [SerializeField] private float depthOffset = -0.05f;

    private XRGrabInteractable m_GrabInteractable;
    private BasketballPalmTransformer m_Transformer;

    private void Awake()
    {
        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        
        // Get or add the transformer component
        // The XRGrabInteractable will automatically find and use any XRBaseGrabTransformer components
        m_Transformer = GetComponent<BasketballPalmTransformer>();
        if (m_Transformer == null)
        {
            m_Transformer = gameObject.AddComponent<BasketballPalmTransformer>();
        }
        
        // Apply settings to the transformer
        ApplySettingsToTransformer();
    }

    private void OnValidate()
    {
        // Update transformer settings when values change in inspector
        if (m_Transformer != null)
        {
            ApplySettingsToTransformer();
        }
    }

    private void ApplySettingsToTransformer()
    {
        if (m_Transformer != null)
        {
            // Use reflection to set private fields, or make them public/protected
            // For now, we'll need to make the fields accessible
            m_Transformer.SetPalmOffsetDistance(palmOffsetDistance);
            m_Transformer.SetDepthOffset(depthOffset);
        }
    }
}

