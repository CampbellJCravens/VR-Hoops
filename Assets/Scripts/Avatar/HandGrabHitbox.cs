using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Sets up a rectangular grab hitbox around the hand using XR Direct Interactor with a Box Collider.
/// Provides runtime debug visualization of the hitbox.
/// 
/// Attach this to the Left Hand or Right Hand GameObject in your XR Origin.
/// The script will automatically:
/// - Add XR Direct Interactor component
/// - Add Box Collider (configured as trigger)
/// - Set up interaction layers and handedness
/// - Provide debug visualization
/// </summary>
[RequireComponent(typeof(Transform))]
public class HandGrabHitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    [Tooltip("Size of the rectangular hitbox in meters (X, Y, Z).")]
    [SerializeField] private Vector3 hitboxSize = new Vector3(0.15f, 0.15f, 0.15f);
    
    [Tooltip("Center offset of the hitbox relative to the hand transform.")]
    [SerializeField] private Vector3 hitboxCenter = Vector3.zero;
    
    [Tooltip("Handedness for this hand. Set to Left or Right.")]
    [SerializeField] private InteractorHandedness handedness = InteractorHandedness.Right;
    
    [Header("Debug Visualization")]
    [Tooltip("Enable to show the hitbox outline at runtime.")]
    [SerializeField] private bool showDebugVisual = false;
    
    [Tooltip("Color of the debug visualization outline.")]
    [SerializeField] private Color debugColor = new Color(0f, 1f, 0f, 1f); // Green, fully opaque
    
    [Tooltip("Line width for the debug visualization.")]
    [SerializeField] private float lineWidth = 0.05f; // Increased default for better visibility
    
    
    [Header("Auto-Setup")]
    [Tooltip("If true, automatically configures components on Awake. If false, you must configure manually.")]
    [SerializeField] private bool autoSetup = true;
    
    private XRDirectInteractor m_DirectInteractor;
    private BoxCollider m_BoxCollider;
    private LineRenderer m_LineRenderer;
    private bool m_IsInitialized = false;
    
    private void Awake()
    {
        if (autoSetup)
        {
            SetupComponents();
        }
        
        SetupDebugVisual();
    }
    
    /// <summary>
    /// Automatically sets up the XR Direct Interactor and Box Collider.
    /// </summary>
    private void SetupComponents()
    {
        // Get or add XR Direct Interactor
        m_DirectInteractor = GetComponent<XRDirectInteractor>();
        if (m_DirectInteractor == null)
        {
            m_DirectInteractor = gameObject.AddComponent<XRDirectInteractor>();
        }
        
        // Configure XR Direct Interactor
        m_DirectInteractor.handedness = handedness;
        
        // Get or add Box Collider
        m_BoxCollider = GetComponent<BoxCollider>();
        if (m_BoxCollider == null)
        {
            m_BoxCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        // Configure Box Collider
        m_BoxCollider.isTrigger = true; // Required for XR Direct Interactor
        m_BoxCollider.size = hitboxSize;
        m_BoxCollider.center = hitboxCenter;
        
        m_IsInitialized = true;
    }
    
    /// <summary>
    /// Sets up the debug visualization LineRenderer.
    /// </summary>
    private void SetupDebugVisual()
    {
        // Get or add LineRenderer for debug visualization
        m_LineRenderer = GetComponent<LineRenderer>();
        if (m_LineRenderer == null)
        {
            m_LineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        // Create a simple unlit material that works in both Built-in and URP
        // Try multiple shader options
        Shader shader = null;
        string[] shaderNames = {
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Sprites/Default",
            "Legacy Shaders/Transparent/Diffuse",
            "Standard"
        };
        
        foreach (string shaderName in shaderNames)
        {
            shader = Shader.Find(shaderName);
            if (shader != null)
            {
                break;
            }
        }
        
        if (shader != null)
        {
            Material lineMaterial = new Material(shader);
            lineMaterial.color = debugColor;
            m_LineRenderer.material = lineMaterial;
        }
        
        // Configure LineRenderer with more visible defaults
        m_LineRenderer.startColor = debugColor;
        m_LineRenderer.endColor = debugColor;
        m_LineRenderer.startWidth = Mathf.Max(lineWidth, 0.01f); // Ensure minimum width
        m_LineRenderer.endWidth = Mathf.Max(lineWidth, 0.01f);
        m_LineRenderer.useWorldSpace = false; // Use local space relative to hand
        m_LineRenderer.loop = true; // Close the loop
        m_LineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_LineRenderer.receiveShadows = false;
        m_LineRenderer.positionCount = 16; // 4 corners × 4 edges = 16 points (with duplicates for line segments)
        m_LineRenderer.sortingOrder = 32767; // Render on top
        
        // Initially hide if debug is disabled
        m_LineRenderer.enabled = showDebugVisual;
    }
    
    private void Update()
    {
        // Update debug visualization if enabled
        if (showDebugVisual)
        {
            UpdateDebugVisual();
        }
        else
        {
            m_LineRenderer.enabled = false;
        }
    }
    
    /// <summary>
    /// Updates the debug visualization to draw the box outline.
    /// </summary>
    private void UpdateDebugVisual()
    {
        m_LineRenderer.enabled = true;
        
        // Get box bounds
        Vector3 center = m_BoxCollider.center;
        Vector3 size = m_BoxCollider.size;
        Vector3 halfSize = size * 0.5f;
        
        // Calculate the 8 corners of the box
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z); // Bottom-left-back
        corners[1] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);  // Bottom-right-back
        corners[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);   // Bottom-right-front
        corners[3] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z); // Bottom-left-front
        corners[4] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);  // Top-left-back
        corners[5] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);   // Top-right-back
        corners[6] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);     // Top-right-front
        corners[7] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);    // Top-left-front
        
        // Create line segments to draw the box outline
        // Bottom face
        Vector3[] positions = new Vector3[16];
        positions[0] = corners[0]; // Start at bottom-left-back
        positions[1] = corners[1]; // Bottom-right-back
        positions[2] = corners[2]; // Bottom-right-front
        positions[3] = corners[3]; // Bottom-left-front
        positions[4] = corners[0]; // Close bottom face
        
        // Top face
        positions[5] = corners[4]; // Top-left-back
        positions[6] = corners[5]; // Top-right-back
        positions[7] = corners[6]; // Top-right-front
        positions[8] = corners[7]; // Top-left-front
        positions[9] = corners[4]; // Close top face
        
        // Vertical edges
        positions[10] = corners[0]; // Bottom-left-back
        positions[11] = corners[4]; // Top-left-back
        positions[12] = corners[1]; // Bottom-right-back
        positions[13] = corners[5]; // Top-right-back
        positions[14] = corners[2]; // Bottom-right-front
        positions[15] = corners[6]; // Top-right-front
        
        m_LineRenderer.SetPositions(positions);
        
        // Verify the LineRenderer is actually set up correctly
        if (m_LineRenderer.positionCount != positions.Length)
        {
            m_LineRenderer.positionCount = positions.Length;
            m_LineRenderer.SetPositions(positions);
        }
    }
    
    /// <summary>
    /// Toggles the debug visualization on/off.
    /// </summary>
    public void ToggleDebugVisual()
    {
        showDebugVisual = !showDebugVisual;
        m_LineRenderer.enabled = showDebugVisual;
    }
    
    /// <summary>
    /// Sets the debug visualization visibility.
    /// </summary>
    /// <param name="visible">True to show, false to hide.</param>
    public void SetDebugVisualVisible(bool visible)
    {
        showDebugVisual = visible;
        m_LineRenderer.enabled = showDebugVisual;
    }
    
    /// <summary>
    /// Updates the hitbox size and center, then refreshes the collider and debug visual.
    /// </summary>
    public void UpdateHitbox(Vector3 newSize, Vector3 newCenter)
    {
        hitboxSize = newSize;
        hitboxCenter = newCenter;
        
        m_BoxCollider.size = hitboxSize;
        m_BoxCollider.center = hitboxCenter;
    }
    
    /// <summary>
    /// Gets the XR Direct Interactor component (for external configuration).
    /// </summary>
    public XRDirectInteractor GetDirectInteractor()
    {
        if (m_DirectInteractor == null)
        {
            m_DirectInteractor = GetComponent<XRDirectInteractor>();
        }
        return m_DirectInteractor;
    }
    
    /// <summary>
    /// Gets the Box Collider component (for external configuration).
    /// </summary>
    public BoxCollider GetBoxCollider()
    {
        if (m_BoxCollider == null)
        {
            m_BoxCollider = GetComponent<BoxCollider>();
        }
        return m_BoxCollider;
    }
    
    private void OnValidate()
    {
        // Update collider if it exists and values changed in inspector
        if (m_BoxCollider != null)
        {
            m_BoxCollider.size = hitboxSize;
            m_BoxCollider.center = hitboxCenter;
        }
        
        // Update line renderer color if it exists
        if (m_LineRenderer != null)
        {
            m_LineRenderer.startColor = debugColor;
            m_LineRenderer.endColor = debugColor;
            m_LineRenderer.startWidth = lineWidth;
            m_LineRenderer.endWidth = lineWidth;
            
            // Update material color if it exists
            m_LineRenderer.material.color = debugColor;
        }
        
        // Update handedness if direct interactor exists
        if (m_DirectInteractor != null)
        {
            m_DirectInteractor.handedness = handedness;
        }
    }
    
    /// <summary>
    /// Draws the box in the Scene view (editor only).
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugVisual)
            return;
        
        Gizmos.color = debugColor;
        
        // Get the box bounds
        Vector3 center = transform.position + transform.TransformDirection(hitboxCenter);
        Vector3 size = hitboxSize;
        Vector3 halfSize = size * 0.5f;
        
        // Get local axes
        Vector3 right = transform.right * halfSize.x;
        Vector3 up = transform.up * halfSize.y;
        Vector3 forward = transform.forward * halfSize.z;
        
        // Draw the 12 edges of the box
        // Bottom face
        Gizmos.DrawLine(center - right - forward - up, center + right - forward - up);
        Gizmos.DrawLine(center + right - forward - up, center + right + forward - up);
        Gizmos.DrawLine(center + right + forward - up, center - right + forward - up);
        Gizmos.DrawLine(center - right + forward - up, center - right - forward - up);
        
        // Top face
        Gizmos.DrawLine(center - right - forward + up, center + right - forward + up);
        Gizmos.DrawLine(center + right - forward + up, center + right + forward + up);
        Gizmos.DrawLine(center + right + forward + up, center - right + forward + up);
        Gizmos.DrawLine(center - right + forward + up, center - right - forward + up);
        
        // Vertical edges
        Gizmos.DrawLine(center - right - forward - up, center - right - forward + up);
        Gizmos.DrawLine(center + right - forward - up, center + right - forward + up);
        Gizmos.DrawLine(center + right + forward - up, center + right + forward + up);
        Gizmos.DrawLine(center - right + forward - up, center - right + forward + up);
    }
    
    /// <summary>
    /// Forces the debug visual to be visible and logs diagnostic information.
    /// Call this method to troubleshoot visualization issues.
    /// </summary>
    [ContextMenu("Force Enable Debug Visual")]
    public void ForceEnableDebugVisual()
    {
        showDebugVisual = true;
        
        if (m_LineRenderer == null)
        {
            SetupDebugVisual();
        }
        
        m_LineRenderer.enabled = true;
        UpdateDebugVisual();
    }
}

