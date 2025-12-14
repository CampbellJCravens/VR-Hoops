using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
#if NORMCORE
using Normal.Realtime;
#endif

/// <summary>
/// Attached to the PlayerShootingPoint GameObject. Detects when the player enters the trigger
/// and listens for the "A" button press to start the game at this play area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerShootingPointTrigger : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Input action for the 'A' button (primary button on right controller). If not assigned, will try to find automatically.")]
    [SerializeField] private InputActionProperty startButtonAction;

    [Header("References")]
    [Tooltip("The PlayAreaManager for this play area. If not assigned, will search parent.")]
    [SerializeField] private PlayAreaManager playAreaManager;

    [Header("Settings")]
    [Tooltip("Tag that identifies the player (XR Origin).")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [Tooltip("Enable to see detailed logging.")]
    [SerializeField] private bool debugLogs = false;

    private bool m_PlayerInTrigger = false;
    private Transform m_PlayerRoot; // XR Origin transform

    private void Awake()
    {
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] Awake() called on {gameObject.name}", this);
        
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[PlayerShootingPointTrigger] No Collider found on {gameObject.name}! This component requires a Collider.", this);
        }
        else if (!col.isTrigger)
        {
            col.isTrigger = true;
            if (debugLogs)
                Debug.Log($"[PlayerShootingPointTrigger] Set collider to trigger on {gameObject.name}", this);
        }
        else if (debugLogs)
        {
            Debug.Log($"[PlayerShootingPointTrigger] Collider is already a trigger on {gameObject.name}", this);
        }

        // Auto-find PlayAreaManager if not assigned
        if (playAreaManager == null)
        {
            playAreaManager = GetComponentInParent<PlayAreaManager>();
            if (playAreaManager == null)
            {
                // Try searching in parent hierarchy
                Transform parent = transform.parent;
                while (parent != null && playAreaManager == null)
                {
                    playAreaManager = parent.GetComponent<PlayAreaManager>();
                    parent = parent.parent;
                }
            }
            
            if (debugLogs)
                Debug.Log($"[PlayerShootingPointTrigger] PlayAreaManager {(playAreaManager != null ? "found" : "NOT found")} on {gameObject.name}", this);
        }
        else if (debugLogs)
        {
            Debug.Log($"[PlayerShootingPointTrigger] PlayAreaManager already assigned: {playAreaManager.gameObject.name}", this);
        }

        // Try to find or create input action for primary button (A/X button)
        if (startButtonAction.action == null)
        {
            // Try to find a default input action
            var actionMap = InputSystem.actions?.FindActionMap("XRI RightHand");
            if (actionMap != null)
            {
                var primaryButton = actionMap.FindAction("PrimaryButton");
                if (primaryButton != null)
                {
                    startButtonAction = new InputActionProperty(primaryButton);
                    if (debugLogs)
                        Debug.Log("[PlayerShootingPointTrigger] Found default PrimaryButton action", this);
                }
            }

            // If still not found, try UI Press action as fallback
            if (startButtonAction.action == null)
            {
                var uiActionMap = InputSystem.actions?.FindActionMap("UI");
                if (uiActionMap != null)
                {
                    var clickAction = uiActionMap.FindAction("Click");
                    if (clickAction != null)
                    {
                        startButtonAction = new InputActionProperty(clickAction);
                        if (debugLogs)
                            Debug.Log("[PlayerShootingPointTrigger] Using UI Click as fallback for start button", this);
                    }
                }
            }
        }

        // Find XR Origin (player)
        FindPlayerRoot();
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] Player root after FindPlayerRoot(): {(m_PlayerRoot != null ? m_PlayerRoot.name : "NULL")}", this);
    }

    private void OnEnable()
    {
        if (startButtonAction.action != null)
        {
            startButtonAction.action.performed += OnStartButtonPressed;
            startButtonAction.action.Enable();
            if (debugLogs)
                Debug.Log($"[PlayerShootingPointTrigger] Enabled start button action: {startButtonAction.action.name} on {gameObject.name}", this);
        }
        else
        {
            Debug.LogWarning($"[PlayerShootingPointTrigger] Start button action is NULL on {gameObject.name}! Button press will not work.", this);
        }
    }

    private void OnDisable()
    {
        if (startButtonAction.action != null)
        {
            startButtonAction.action.performed -= OnStartButtonPressed;
            startButtonAction.action.Disable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] OnTriggerEnter: {other.gameObject.name} (tag: {other.tag}) entered trigger on {gameObject.name}", this);
        
        // Check if the player (XR Origin) entered
        if (IsPlayer(other.gameObject))
        {
            m_PlayerInTrigger = true;
            if (debugLogs)
                Debug.Log($"[PlayerShootingPointTrigger] ✓ Player detected! Player entered trigger at {gameObject.name}. m_PlayerInTrigger = true", this);
        }
        else if (debugLogs)
        {
            Debug.Log($"[PlayerShootingPointTrigger] Object {other.gameObject.name} entered trigger but is not recognized as player", this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] OnTriggerExit: {other.gameObject.name} exited trigger on {gameObject.name}", this);
        
        // Check if the player (XR Origin) exited
        if (IsPlayer(other.gameObject))
        {
            m_PlayerInTrigger = false;
            if (debugLogs)
                Debug.Log($"[PlayerShootingPointTrigger] ✓ Player exited trigger at {gameObject.name}. m_PlayerInTrigger = false", this);
        }
    }

    private void OnStartButtonPressed(InputAction.CallbackContext context)
    {
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] ⚡ BUTTON PRESSED! Action: {context.action.name}, Phase: {context.phase}", this);
        
        // Check basic conditions
        if (debugLogs)
        {
            Debug.Log($"[PlayerShootingPointTrigger] Conditions check - m_PlayerInTrigger: {m_PlayerInTrigger}, playAreaManager: {(playAreaManager != null ? playAreaManager.gameObject.name : "NULL")}, m_PlayerRoot: {(m_PlayerRoot != null ? m_PlayerRoot.name : "NULL")}", this);
        }
        
        // Only respond if player is in trigger and play area exists
        if (!m_PlayerInTrigger)
        {
            if (debugLogs)
                Debug.LogWarning($"[PlayerShootingPointTrigger] Button pressed but player is NOT in trigger! m_PlayerInTrigger = false", this);
            return;
        }
        
        if (playAreaManager == null)
        {
            Debug.LogError($"[PlayerShootingPointTrigger] Button pressed but playAreaManager is NULL on {gameObject.name}!", this);
            return;
        }
        
        // Check game state
        var currentState = playAreaManager.GetGameState();
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] Current game state: {currentState} (Pregame required)", this);
        
        if (currentState != PlayAreaManager.GameState.Pregame)
        {
            if (debugLogs)
                Debug.LogWarning($"[PlayerShootingPointTrigger] Start button pressed but play area is not in Pregame state (current: {currentState})", this);
            return;
        }
        
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] ✓ All conditions met! Proceeding to start game at {playAreaManager.gameObject.name}", this);

#if NORMCORE
        // Check if play area is available (not owned by another client)
        bool isAvailable = playAreaManager.IsAvailable();
        bool isOwnedByLocal = playAreaManager.IsOwnedByLocalClient();
        int currentOwner = playAreaManager.GetOwner();
        
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] Ownership check - IsAvailable: {isAvailable}, IsOwnedByLocal: {isOwnedByLocal}, CurrentOwner: {currentOwner}", this);
        
        // Block if play area is owned by someone else (not available AND not owned by local client)
        if (!isAvailable && !isOwnedByLocal)
        {
            if (debugLogs)
                Debug.LogWarning($"[PlayerShootingPointTrigger] ❌ BLOCKED: Play area is already occupied by another player! Current owner: {currentOwner}", this);
            return;
        }
        
        // Set ownership of this play area
        Realtime realtime = FindFirstObjectByType<Realtime>();
        if (realtime == null)
        {
            Debug.LogError("[PlayerShootingPointTrigger] ❌ CRITICAL: Realtime component not found! Cannot set ownership.", this);
            return;
        }
        
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] Setting owner to local client ID: {realtime.clientID}", this);
        playAreaManager.SetOwner(realtime.clientID);
        
        // Verify ownership was set
        int ownerAfterSet = playAreaManager.GetOwner();
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] Owner after SetOwner() call: {ownerAfterSet} (expected: {realtime.clientID})", this);
#else
        // Non-Normcore mode: proceed without ownership checks
        if (debugLogs)
            Debug.Log("[PlayerShootingPointTrigger] Non-Normcore mode: skipping ownership checks", this);
#endif
        
        // Start the game at this play area
        if (debugLogs)
            Debug.Log($"[PlayerShootingPointTrigger] 🚀 Calling playAreaManager.StartGame() with playerRoot: {(m_PlayerRoot != null ? m_PlayerRoot.name : "NULL")}", this);
        playAreaManager.StartGame(m_PlayerRoot);
    }

    /// <summary>
    /// Checks if the GameObject is the player (XR Origin).
    /// </summary>
    private bool IsPlayer(GameObject obj)
    {
        // Check tag first
        if (obj.CompareTag(playerTag))
        {
            return true;
        }

        // Check if it's the XR Origin or a child of it
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag) || 
                current.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null)
            {
                if (m_PlayerRoot == null)
                {
                    // Find the root XR Origin
                    m_PlayerRoot = FindXROriginRoot(current);
                }
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// Finds the XR Origin root transform.
    /// </summary>
    private Transform FindXROriginRoot(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null)
            {
                return current;
            }
            current = current.parent;
        }
        return start; // Fallback to starting transform
    }

    /// <summary>
    /// Finds the player root (XR Origin) in the scene.
    /// </summary>
    private void FindPlayerRoot()
    {
        // Try to find by tag
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            m_PlayerRoot = FindXROriginRoot(player.transform);
            return;
        }

        // Try to find XR Origin component
        var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            m_PlayerRoot = xrOrigin.transform;
        }
    }

    private void OnValidate()
    {
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[PlayerShootingPointTrigger] Collider on {gameObject.name} should be a trigger. Setting isTrigger = true.", this);
            col.isTrigger = true;
        }
    }
}

