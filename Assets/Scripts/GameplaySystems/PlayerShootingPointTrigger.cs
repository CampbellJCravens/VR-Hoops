using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;

/// <summary>
/// Attached to the PlayerShootingPoint GameObject. Detects when the player enters the trigger
/// and listens for the "A" button press to start the game at this play area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerShootingPointTrigger : MonoBehaviour
{

    [Header("Input")]
    [Tooltip("Input action for starting the game (A button on right controller).")]
    [SerializeField] private InputActionProperty startGameAction;

    [Header("References")]
    [Tooltip("The PlayAreaManager for this play area. If not assigned, will search parent.")]
    [SerializeField] private PlayAreaManager playAreaManager;

    [Header("Settings")]
    [Tooltip("Tag that identifies the player (XR Origin).")]
    [SerializeField] private string playerTag = "Player";

    private bool m_PlayerInTrigger = false;
    private Transform m_PlayerRoot; // XR Origin transform

    private void Awake()
    {
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        startGameAction.action.performed += OnStartGamePerformed;
        startGameAction.action.Enable();
    }

    private void OnDisable()
    {
        startGameAction.action.performed -= OnStartGamePerformed;
        startGameAction.action.Disable();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the player (XR Origin) entered
        if (IsPlayer(other.gameObject))
        {
            m_PlayerInTrigger = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if the player (XR Origin) exited
        if (IsPlayer(other.gameObject))
        {
            m_PlayerInTrigger = false;
        }
    }

    private void OnStartGamePerformed(InputAction.CallbackContext ctx)
    {
        HandleStartGameInput();
    }

    /// <summary>
    /// Handles the start game input logic.
    /// </summary>
    private void HandleStartGameInput()
    {
        // Only respond if player is in trigger and play area exists
        if (!m_PlayerInTrigger)
        {
            return;
        }
        
        // Check game state
        var currentState = playAreaManager.GetGameState();
        
        if (currentState != PlayAreaManager.GameState.Pregame)
        {
            return;
        }

        // Check if play area is available (not owned by another client)
        bool isAvailable = playAreaManager.IsAvailable();
        bool isOwnedByLocal = playAreaManager.IsOwnedByLocalClient();
        int currentOwner = playAreaManager.GetOwner();
        
        // Block if play area is owned by someone else (not available AND not owned by local client)
        if (!isAvailable && !isOwnedByLocal)
        {
            return;
        }
        
        // Set ownership of this play area
        Realtime realtime = FindFirstObjectByType<Realtime>();
        playAreaManager.SetOwner(realtime.clientID);
        
        // Verify ownership was set
        int ownerAfterSet = playAreaManager.GetOwner();
        
        // Start the game at this play area
        playAreaManager.StartGame(GetPlayerRoot());
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
        // Transform current = obj.transform;
        // while (current != null)
        // {
        //     if (current.CompareTag(playerTag) || 
        //         current.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null)
        //     {
        //         // Find the root XR Origin (GetPlayerRoot will cache it)
        //         GetPlayerRoot();
        //         return true;
        //     }
        //     current = current.parent;
        // }

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
    /// Gets the player root (XR Origin) transform. If not already cached, finds it in the scene.
    /// </summary>
    private Transform GetPlayerRoot()
    {
        // Return cached value if valid
        if (m_PlayerRoot != null)
        {
            return m_PlayerRoot;
        }

        // For now, try to find by tag as fallback
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        Debug.Log($"[PlayerShootingPointTrigger] GetPlayerRoot - playerTag: {playerTag}, player: {(player != null ? player.name : "NULL")}, this: {this.name}");
        m_PlayerRoot = FindXROriginRoot(player.transform);

        // Alternative if above fails
        // var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        // m_PlayerRoot = xrOrigin.transform;
            

        return m_PlayerRoot;
    }

    private void OnValidate()
    {
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }
}
