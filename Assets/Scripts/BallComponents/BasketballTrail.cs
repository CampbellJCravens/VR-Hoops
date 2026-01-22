using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Manages a trail renderer that follows the basketball's path after it's released from the hand.
/// Creates a smooth, golf-style trail that shows the ball's arc.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class BasketballTrail : MonoBehaviour
{
    
    private TrailRenderer trailRenderer;
    
    [Header("Trail Settings")]
    [Tooltip("How long (in seconds) the trail stays visible. Longer = longer trail behind the ball.")]
    [SerializeField] private float trailTime = 2f;
    
    [Tooltip("How long (in seconds) the trail continues after the ball stops moving (velocity below threshold).")]
    [SerializeField] private float trailFadeOutTime = 0.5f;
    
    [Tooltip("Minimum velocity (m/s) required to keep the trail visible. Below this, trail will fade out.")]
    [SerializeField] private float minVelocityForTrail = 0.1f;

    [Header("Trail Appearance")]
    [Tooltip("Color of the trail.")]
    [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.8f);
    
    [Tooltip("Width of the trail at the start (near the ball).")]
    [SerializeField] private float startWidth = 0.05f;
    
    [Tooltip("Width of the trail at the end (farthest from the ball).")]
    [SerializeField] private float endWidth = 0.02f;
    
    [Tooltip("Material for the trail. If not assigned, will create a default material.")]
    [SerializeField] private Material trailMaterial;

    private XRGrabInteractable m_GrabInteractable;
    private Rigidbody m_Rigidbody;
    private bool m_IsGrabbed;
    private bool m_TrailActive;
    private float m_TimeSinceLastMovement;

    private void Awake()
    {
        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        m_Rigidbody = GetComponent<Rigidbody>();
        
        CreateTrailRenderer();
        
        ConfigureTrailRenderer();
    }

    private void OnEnable()
    {
        m_GrabInteractable.selectEntered.AddListener(OnGrab);
        m_GrabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        m_GrabInteractable.selectEntered.RemoveListener(OnGrab);
        m_GrabInteractable.selectExited.RemoveListener(OnRelease);
        
        // Clear trail when disabled
        trailRenderer.enabled = false;
        trailRenderer.Clear();
    }

    private void Update()
    {
        if (!m_IsGrabbed && m_TrailActive)
        {
            // Check if ball is still moving
            float velocity = m_Rigidbody.linearVelocity.magnitude;
            
            if (velocity < minVelocityForTrail)
            {
                m_TimeSinceLastMovement += Time.deltaTime;
                
                // Fade out trail after ball stops moving
                if (m_TimeSinceLastMovement >= trailFadeOutTime)
                {
                    StopTrail();
                }
            }
            else
            {
                m_TimeSinceLastMovement = 0f;
            }
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        m_IsGrabbed = true;
        trailRenderer.enabled = false;
        trailRenderer.Clear(); // Clear the trail immediately when grabbed
        m_TrailActive = false;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        m_IsGrabbed = false;
        StartTrail();
    }

    private void StartTrail()
    {
        m_TrailActive = true;
        m_TimeSinceLastMovement = 0f;
        trailRenderer.enabled = true;
        trailRenderer.Clear(); // Start fresh
    }

    private void StopTrail()
    {
        m_TrailActive = false;
        // Don't disable immediately - let the trail fade out naturally
        // It will be cleared when grabbed
    }

    private void CreateTrailRenderer()
    {
        Debug.Log("Creating trailRenderer");
        // Create a child GameObject for the trail renderer
        GameObject trailObj = new GameObject("BallTrail");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = Vector3.zero;
        trailObj.transform.localRotation = Quaternion.identity;
        trailObj.transform.localScale = Vector3.one;
        
        // Add TrailRenderer component
        trailRenderer = trailObj.AddComponent<TrailRenderer>();
    }

    private void ConfigureTrailRenderer()
    {
        if (trailRenderer == null)
            return;
            
        // Basic trail settings
        trailRenderer.time = trailTime;
        trailRenderer.startWidth = startWidth;
        trailRenderer.endWidth = endWidth;
        trailRenderer.material = trailMaterial != null ? trailMaterial : CreateDefaultTrailMaterial();
        
        // Color gradient (fade from full to transparent)
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trailColor, 0.0f), new GradientColorKey(trailColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(trailColor.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        trailRenderer.colorGradient = gradient;
        
        // Trail settings
        trailRenderer.minVertexDistance = 0.01f; // How close points need to be to add a new vertex
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.alignment = LineAlignment.View; // Trail faces the camera
        trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        
        // Start disabled
        trailRenderer.enabled = false;
    }

    private Material CreateDefaultTrailMaterial()
    {
        // Create a simple unlit material for the trail
        // Try URP shader first, fallback to built-in
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        
        Material mat = new Material(shader);
        mat.color = trailColor;
        return mat;
    }

    private void OnValidate()
    {
        // Update trail renderer settings when values change in editor
        ConfigureTrailRenderer();
    }

    /// <summary>
    /// Manually start the trail (useful for testing or special cases).
    /// </summary>
    public void EnableTrail()
    {
        StartTrail();
    }

    /// <summary>
    /// Manually stop the trail (useful for testing or special cases).
    /// </summary>
    public void DisableTrail()
    {
        StopTrail();
    }
}

