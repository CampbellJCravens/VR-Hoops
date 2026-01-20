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
        if (realtimeView != null)
        {
            realtimeView.ownerIDSelfDidChange += OnOwnershipChanged;
        }
        
        // Sync from model if it's already available (for late joiners)
        if (model != null)
        {
            SyncFromModel();
        }
#endif
    }
    
    private void OnDestroy()
    {
#if NORMCORE
        // Unsubscribe from ownership changes
        if (realtimeView != null)
        {
            realtimeView.ownerIDSelfDidChange -= OnOwnershipChanged;
        }
#endif
    }

    private void Initialize()
    {
        FindVisualsTransform();
        FindMeshRenderer();
        if (m_MeshRenderer != null)
        {
            // Don't sync to model during initialization - that happens in Start() after multiplayer is set up
            ApplyMaterial(currentMaterial, false);
            m_LastMaterial = currentMaterial;
        }
    }

#if NORMCORE
    /// <summary>
    /// Called when the RealtimeModel is replaced. Handles initial state sync for late joiners.
    /// </summary>
    protected override void OnRealtimeModelReplaced(RealtimeBasketballModel previousModel, RealtimeBasketballModel currentModel)
    {
        Debug.Log($"[BasketballVisualController] OnRealtimeModelReplaced called. Previous model: {previousModel != null}, Current model: {currentModel != null}", this);
        
        if (previousModel != null)
        {
            // Unsubscribe from previous model events
            previousModel.materialTypeDidChange -= MaterialTypeDidChange;
        }
        
        if (currentModel != null)
        {
            Debug.Log($"[BasketballVisualController] Model available. Initial material type: {currentModel.materialType}, IsOwner: {isOwnedLocallySelf}, OwnerID: {realtimeView?.ownerIDSelf}", this);
            
            // Subscribe to model change events
            currentModel.materialTypeDidChange += MaterialTypeDidChange;
            Debug.Log($"[BasketballVisualController] Subscribed to materialTypeDidChange event", this);
            
            // Apply initial state from model (important for late joiners)
            SyncFromModel();
        }
        else
        {
            Debug.LogWarning("[BasketballVisualController] OnRealtimeModelReplaced called but currentModel is null!", this);
        }
    }
    
    /// <summary>
    /// Called when ownership of the RealtimeView changes.
    /// </summary>
    private void OnOwnershipChanged(RealtimeView view, int ownerID)
    {
        Debug.Log($"[BasketballVisualController] Ownership changed. Is owner now: {view.isOwnedLocallySelf}, Owner ID: {ownerID}", this);
        
        // If we just gained ownership, sync our local state to the model
        if (view.isOwnedLocallySelf && model != null)
        {
            // Sync current local state to model (in case we had pending writes)
            model.materialType = (int)currentMaterial;
            Debug.Log($"[BasketballVisualController] Synced local material {currentMaterial} to model after gaining ownership", this);
        }
    }
    
    private void MaterialTypeDidChange(RealtimeBasketballModel model, int materialType)
    {
        Debug.Log($"[BasketballVisualController] MaterialTypeDidChange callback FIRED! Callback param materialType: {materialType}, Model.materialType: {model.materialType}, IsOwner: {isOwnedLocallySelf}", this);
        SyncFromModel();
    }
    
    /// <summary>
    /// Syncs local state from the RealtimeModel. Called when model changes (for non-owners or initial sync).
    /// </summary>
    private void SyncFromModel()
    {
        if (model == null)
        {
            Debug.LogWarning("[BasketballVisualController] SyncFromModel called but model is null!", this);
            return;
        }
        
        int materialType = model.materialType;
        
        if (materialType < 0 || materialType > 2)
        {
            Debug.LogWarning($"[BasketballVisualController] Invalid material type from model: {materialType}", this);
            return;
        }
        
        BasketballMaterial material = (BasketballMaterial)materialType;
        
        // Update local state from model
        if (currentMaterial != material)
        {
            Debug.Log($"[BasketballVisualController] ✓ CLIENT2 RECEIVED: Material type changed! {currentMaterial}→{material}, IsOwner: {isOwnedLocallySelf}", this);
            // Don't sync back to model (we're receiving this from the model)
            ApplyMaterial(material, false);
        }
        else
        {
            Debug.Log($"[BasketballVisualController] SyncFromModel: No changes (materialType={material})", this);
        }
    }
    
    /// <summary>
    /// Writes material type to the model. Only the owner should call this.
    /// </summary>
    private void WriteMaterialTypeToModel(int materialType)
    {
        Debug.Log($"[BasketballVisualController] WriteMaterialTypeToModel ENTRY: materialType={materialType}, model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}", this);
        
        if (model == null)
        {
            Debug.LogWarning("[BasketballVisualController] Cannot write material type - model is null", this);
            return;
        }
        
        if (realtimeView == null)
        {
            Debug.LogWarning("[BasketballVisualController] Cannot write material type - realtimeView is null", this);
            return;
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            int oldMaterialType = model.materialType;
            model.materialType = materialType;
            Debug.Log($"[BasketballVisualController] ✓ CLIENT1 WRITE: Wrote materialType {materialType} to model (was {oldMaterialType}). Model should sync to other clients now.", this);
        }
        else
        {
            Debug.LogWarning($"[BasketballVisualController] ✗ WRITE FAILED: Cannot write materialType {materialType} to model - not owner (owner: {realtimeView.ownerIDSelf}, local client: {realtime?.clientID})", this);
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
            
            if (m_MeshRenderer != null)
            {
                ApplyMaterial(currentMaterial);
                m_LastMaterial = currentMaterial;
            }
        }
    }

    /// <summary>
    /// Finds the visuals GameObject if not already assigned.
    /// </summary>
    private void FindVisualsTransform()
    {
        if (visualsObject == null)
        {
            // Search for a child named "Visuals"
            Transform visualsTransform = transform.Find("Visuals");
            if (visualsTransform != null)
            {
                visualsObject = visualsTransform.gameObject;
            }
            
            if (visualsObject == null)
            {
                Debug.LogWarning($"BasketballVisualController on {gameObject.name}: Could not find 'Visuals' child object. Please assign it manually.", this);
            }
        }
    }

    /// <summary>
    /// Finds the MeshRenderer component on the visuals GameObject.
    /// </summary>
    private void FindMeshRenderer()
    {
        if (visualsObject != null)
        {
            m_MeshRenderer = visualsObject.GetComponent<MeshRenderer>();
            
            if (m_MeshRenderer == null)
            {
                Debug.LogWarning($"BasketballVisualController on {gameObject.name}: Could not find MeshRenderer on Visuals object.", this);
            }
        }
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
            if (m_MeshRenderer == null)
            {
                Debug.LogError($"BasketballVisualController on {gameObject.name}: Cannot apply material - MeshRenderer not found.", this);
                return;
            }
        }

        Material materialToApply = GetMaterialForType(materialType);
        
        if (materialToApply != null)
        {
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
#else
            Debug.LogWarning("[BasketballVisualController] ApplyMaterial: NORMCORE is not defined! Material will not sync across clients!", this);
#endif
        }
        else
        {
            Debug.LogWarning($"BasketballVisualController on {gameObject.name}: Material for {materialType} is not assigned.", this);
        }
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

