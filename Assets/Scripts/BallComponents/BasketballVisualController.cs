using UnityEngine;
#if NORMCORE
using Normal.Realtime;
#endif

/// <summary>
/// Controls the visual appearance of the basketball by switching between different materials.
/// Supports multiplayer sync via Normcore when available.
/// </summary>
[RequireComponent(typeof(Transform))]
[ExecuteAlways]
#if NORMCORE
[RequireComponent(typeof(RealtimeView))]
public class BasketballVisualController : RealtimeComponent<RealtimeBasketballModel>
#else
public class BasketballVisualController : MonoBehaviour
#endif
{
    /// <summary>
    /// Enum representing the different basketball material options.
    /// </summary>
    public enum BasketballMaterial
    {
        Orange,
        Black,
        RedWhiteBlue
    }

    [Header("Visual Settings")]
    [Tooltip("The child GameObject containing the mesh renderer. If not assigned, will search for a child named 'Visuals'.")]
    [SerializeField] private GameObject visualsObject;
    
    [Tooltip("The current material style for the basketball.")]
    [SerializeField] private BasketballMaterial currentMaterial = BasketballMaterial.Orange;

    [Header("Materials")]
    [Tooltip("The orange basketball material.")]
    [SerializeField] private Material orangeMaterial;
    
    [Tooltip("The black basketball material.")]
    [SerializeField] private Material blackMaterial;
    
    [Tooltip("The red, white, and blue basketball material.")]
    [SerializeField] private Material redWhiteBlueMaterial;

    private MeshRenderer m_MeshRenderer;
    private BasketballMaterial m_LastMaterial;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Start()
    {
#if NORMCORE
        // Subscribe to ownership changes
        realtimeView.ownerIDSelfDidChange += OnOwnershipChanged;
        
        // Sync from model if it's already available (for late joiners)
        SyncFromModel();
#endif
    }
    
    private void OnDestroy()
    {
#if NORMCORE
        // Unsubscribe from ownership changes
        realtimeView.ownerIDSelfDidChange -= OnOwnershipChanged;
#endif
    }

    private void Initialize()
    {
        FindVisualsTransform();
        FindMeshRenderer();
        // Don't sync to model during initialization - that happens in Start() after multiplayer is set up
        ApplyMaterial(currentMaterial, false);
        m_LastMaterial = currentMaterial;
    }

#if NORMCORE
    /// <summary>
    /// Called when the RealtimeModel is replaced. Handles initial state sync for late joiners.
    /// </summary>
    protected override void OnRealtimeModelReplaced(RealtimeBasketballModel previousModel, RealtimeBasketballModel currentModel)
    {
        // Unsubscribe from previous model events (if it exists)
        if (previousModel != null)
        {
            previousModel.materialTypeDidChange -= MaterialTypeDidChange;
        }
        
        // Subscribe to model change events
        currentModel.materialTypeDidChange += MaterialTypeDidChange;
        
        // Apply initial state from model (important for late joiners)
        SyncFromModel();
    }
    
    /// <summary>
    /// Called when ownership of the RealtimeView changes.
    /// </summary>
    private void OnOwnershipChanged(RealtimeView view, int ownerID)
    {
        // If we just gained ownership, sync our local state to the model
        if (view.isOwnedLocallySelf)
        {
            // Sync current local state to model (in case we had pending writes)
            model.materialType = (int)currentMaterial;
        }
    }
    
    private void MaterialTypeDidChange(RealtimeBasketballModel model, int materialType)
    {
        SyncFromModel();
    }
    
    /// <summary>
    /// Syncs local state from the RealtimeModel. Called when model changes (for non-owners or initial sync).
    /// </summary>
    private void SyncFromModel()
    {
        int materialType = model.materialType;
        
        if (materialType < 0 || materialType > 2)
        {
            return;
        }
        
        BasketballMaterial material = (BasketballMaterial)materialType;
        
        // Update local state from model
        if (currentMaterial != material)
        {
            // Don't sync back to model (we're receiving this from the model)
            ApplyMaterial(material, false);
        }
    }
    
    /// <summary>
    /// Writes material type to the model. Only the owner should call this.
    /// </summary>
    private void WriteMaterialTypeToModel(int materialType)
    {
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            model.materialType = materialType;
        }
    }
#endif

    private void OnValidate()
    {
        // Update material when enum changes in editor
        if (m_LastMaterial != currentMaterial)
        {
            // Only apply if we have the necessary components
            if (visualsObject == null)
                FindVisualsTransform();
            
            if (m_MeshRenderer == null)
                FindMeshRenderer();
            
            ApplyMaterial(currentMaterial);
            m_LastMaterial = currentMaterial;
        }
    }

    /// <summary>
    /// Finds the visuals GameObject if not already assigned.
    /// </summary>
    private void FindVisualsTransform()
    {
        if (visualsObject == null)
        {
            // Auto-finding is disabled. Visuals transform should be assigned via reference.
            // For now, try transform.Find as fallback but log error
            Transform visualsTransform = transform.Find("Visuals");
            if (visualsTransform != null)
            {
                visualsObject = visualsTransform.gameObject;
            }
        }
    }

    /// <summary>
    /// Finds the MeshRenderer component on the visuals GameObject.
    /// </summary>
    private void FindMeshRenderer()
    {
        m_MeshRenderer = visualsObject.GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Applies the specified material to the basketball.
    /// </summary>
    /// <param name="materialType">The type of material to apply.</param>
    /// <param name="syncToModel">Whether to sync to RealtimeModel if multiplayer is enabled (default: true).</param>
    public void ApplyMaterial(BasketballMaterial materialType, bool syncToModel = true)
    {
        if (m_MeshRenderer == null)
        {
            FindMeshRenderer();
        }

        Material materialToApply = GetMaterialForType(materialType);
        
        // Use sharedMaterial in edit mode to avoid creating instance materials
        if (Application.isPlaying)
        {
            m_MeshRenderer.material = materialToApply;
        }
        else
        {
            m_MeshRenderer.sharedMaterial = materialToApply;
        }
        
        currentMaterial = materialType;
        m_LastMaterial = materialType;

#if NORMCORE
        // Sync to RealtimeModel if we're the owner and syncToModel is true
        if (syncToModel)
        {
            WriteMaterialTypeToModel((int)materialType);
        }
#endif
    }

    /// <summary>
    /// Gets the Material object for the specified material type.
    /// </summary>
    /// <param name="materialType">The type of material to get.</param>
    /// <returns>The Material object, or null if not assigned.</returns>
    private Material GetMaterialForType(BasketballMaterial materialType)
    {
        return materialType switch
        {
            BasketballMaterial.Orange => orangeMaterial,
            BasketballMaterial.Black => blackMaterial,
            BasketballMaterial.RedWhiteBlue => redWhiteBlueMaterial,
            _ => null
        };
    }

    /// <summary>
    /// Sets the basketball material type. Can be called at runtime.
    /// In multiplayer, this will sync the material type to all clients if this client owns the ball.
    /// </summary>
    /// <param name="materialType">The material type to set.</param>
    public void SetMaterial(BasketballMaterial materialType)
    {
        currentMaterial = materialType;
        ApplyMaterial(materialType, true); // Sync to model when SetMaterial is called explicitly
    }

    /// <summary>
    /// Gets the current material type.
    /// </summary>
    /// <returns>The current BasketballMaterial enum value.</returns>
    public BasketballMaterial GetCurrentMaterial()
    {
        return currentMaterial;
    }

    /// <summary>
    /// Checks if this basketball is a money ball.
    /// RedWhiteBlue balls are always money balls.
    /// </summary>
    /// <returns>True if this is a money ball, false otherwise.</returns>
    public bool IsMoneyBall()
    {
        return currentMaterial == BasketballMaterial.RedWhiteBlue;
    }
}

