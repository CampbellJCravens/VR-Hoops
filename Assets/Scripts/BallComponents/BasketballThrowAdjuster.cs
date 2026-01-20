using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Adjusts the throw velocity of the basketball to make shots feel better.
/// Increases throw force and caps maximum velocity to prevent unrealistic shots.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class BasketballThrowAdjuster : MonoBehaviour
{
    [Header("Throw Settings")]
    [Tooltip("Multiplier for throw velocity. Higher = ball goes farther with same controller movement. Default: 1.5")]
    [SerializeField] private float throwVelocityMultiplier = 1.5f;
    
    [Tooltip("Maximum velocity magnitude (m/s) to prevent unrealistic rocket shots.")]
    [SerializeField] private float maxThrowVelocity = 15f;

    [Header("Ball Type Velocity Override")]
    [Tooltip("Enable to use different velocity multipliers based on ball material type.")]
    [SerializeField] private bool ballVelocityOverride = false;
    
    [Tooltip("Velocity multiplier for Orange basketballs (only used if Ball Velocity Override is enabled).")]
    [SerializeField] private float orangeBallMultiplier = 1.5f;
    
    [Tooltip("Velocity multiplier for Black basketballs (only used if Ball Velocity Override is enabled).")]
    [SerializeField] private float blackBallMultiplier = 1.5f;
    
    [Tooltip("Velocity multiplier for RedWhiteBlue basketballs (only used if Ball Velocity Override is enabled).")]
    [SerializeField] private float redWhiteBlueBallMultiplier = 1.5f;

    [Header("Debug")]
    [Tooltip("Enable to log throw velocities.")]
    [SerializeField] private bool debugLogs = false;

    private XRGrabInteractable m_GrabInteractable;
    private Rigidbody m_Rigidbody;
    private BasketballVisualController m_VisualController;
    private Vector3 m_PendingVelocity;
    private bool m_HasPendingVelocity;

    private void Awake()
    {
        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_VisualController = GetComponent<BasketballVisualController>();
    }

    private void OnEnable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDisable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // Get the current velocity that XR Grab Interactable just set
        Vector3 currentVelocity = m_Rigidbody.linearVelocity;
        
        // Determine which multiplier to use
        float multiplierToUse = throwVelocityMultiplier;
        
        if (ballVelocityOverride && m_VisualController != null)
        {
            BasketballVisualController.BasketballMaterial ballType = m_VisualController.GetCurrentMaterial();
            multiplierToUse = ballType switch
            {
                BasketballVisualController.BasketballMaterial.Orange => orangeBallMultiplier,
                BasketballVisualController.BasketballMaterial.Black => blackBallMultiplier,
                BasketballVisualController.BasketballMaterial.RedWhiteBlue => redWhiteBlueBallMultiplier,
                _ => throwVelocityMultiplier
            };
            
            if (debugLogs)
            {
                Debug.Log($"[BasketballThrowAdjuster] Using {ballType} ball multiplier: {multiplierToUse}", this);
            }
        }
        
        // Apply multiplier
        Vector3 adjustedVelocity = currentVelocity * multiplierToUse;
        
        // Cap to max velocity
        if (adjustedVelocity.magnitude > maxThrowVelocity)
        {
            adjustedVelocity = adjustedVelocity.normalized * maxThrowVelocity;
        }
        
        // Store to apply after XR's detach (similar to ball spin system)
        m_PendingVelocity = adjustedVelocity;
        m_HasPendingVelocity = true;
        
        if (debugLogs)
        {
            Debug.Log($"[BasketballThrowAdjuster] Original velocity: {currentVelocity.magnitude:F2} m/s, Adjusted: {adjustedVelocity.magnitude:F2} m/s", this);
        }
        
        // Start coroutine to apply velocity after XR's detach
        StartCoroutine(ApplyVelocityAtEndOfFrame());
    }

    private System.Collections.IEnumerator ApplyVelocityAtEndOfFrame()
    {
        // Wait for end of frame to ensure XR's Detach() has run
        yield return new WaitForEndOfFrame();
        
        // Wait for the next FixedUpdate to apply the velocity
        yield return new WaitForFixedUpdate();
        
        if (m_HasPendingVelocity)
        {
            // Apply the adjusted velocity
            m_Rigidbody.linearVelocity = m_PendingVelocity;
            
            m_HasPendingVelocity = false;
            
            if (debugLogs)
            {
                Debug.Log($"[BasketballThrowAdjuster] Applied adjusted velocity: {m_PendingVelocity.magnitude:F2} m/s", this);
            }
        }
    }
}

