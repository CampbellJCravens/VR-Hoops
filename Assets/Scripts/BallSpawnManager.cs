using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Central manager for ball spawning that routes requests to the appropriate PlayArea.
/// Spawns balls in the PlayArea that is currently in "Playing" state.
/// </summary>
public class BallSpawnManager : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Input action for spawning a ball. Should be bound to a button (e.g., SecondaryButton).")]
    [SerializeField] private InputActionProperty spawnBallAction;

    [Header("Debug")]
    [Tooltip("Enable to see detailed logging.")]
    [SerializeField] private bool debugLogs = false;

    private void OnEnable()
    {
        Debug.Log("[BallSpawnManager] OnEnable() called", this);

        // Try to auto-find the SpawnBall action if not assigned
        if (spawnBallAction.action == null)
        {
            Debug.Log("[BallSpawnManager] SpawnBall action is null, attempting auto-find...", this);
            
            // Try to find it in the XRI RightHand or XRI LeftHand action maps
            var rightHandMap = InputSystem.actions?.FindActionMap("XRI RightHand");
            if (rightHandMap != null)
            {
                var spawnBall = rightHandMap.FindAction("SpawnBall");
                if (spawnBall != null)
                {
                    spawnBallAction = new InputActionProperty(spawnBall);
                    Debug.Log("[BallSpawnManager] Auto-found SpawnBall action in XRI RightHand", this);
                }
                else
                {
                    Debug.Log("[BallSpawnManager] SpawnBall action not found in XRI RightHand", this);
                }
            }
            else
            {
                Debug.Log("[BallSpawnManager] XRI RightHand action map not found", this);
            }

            // Try left hand if not found
            if (spawnBallAction.action == null)
            {
                var leftHandMap = InputSystem.actions?.FindActionMap("XRI LeftHand");
                if (leftHandMap != null)
                {
                    var spawnBall = leftHandMap.FindAction("SpawnBall");
                    if (spawnBall != null)
                    {
                        spawnBallAction = new InputActionProperty(spawnBall);
                        Debug.Log("[BallSpawnManager] Auto-found SpawnBall action in XRI LeftHand", this);
                    }
                    else
                    {
                        Debug.Log("[BallSpawnManager] SpawnBall action not found in XRI LeftHand", this);
                    }
                }
                else
                {
                    Debug.Log("[BallSpawnManager] XRI LeftHand action map not found", this);
                }
            }
        }
        else
        {
            Debug.Log($"[BallSpawnManager] SpawnBall action already assigned: {spawnBallAction.action.name} (ID: {spawnBallAction.action.id})", this);
        }

        if (spawnBallAction.action != null)
        {
            spawnBallAction.action.performed += OnSpawnBallPerformed;
            spawnBallAction.action.Enable();
            
            Debug.Log($"[BallSpawnManager] SpawnBall input action enabled and subscribed: {spawnBallAction.action.name}", this);
        }
        else
        {
            Debug.LogError("[BallSpawnManager] SpawnBall input action not found! Please assign it in the Inspector or ensure it exists in your Input Actions asset.", this);
        }
    }

    private void OnDisable()
    {
        if (spawnBallAction.action != null)
        {
            spawnBallAction.action.performed -= OnSpawnBallPerformed;
            spawnBallAction.action.Disable();
        }
    }

    private void OnSpawnBallPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[BallSpawnManager] SpawnBall input action triggered!", this);
        SpawnBall();
    }

    private void SpawnBall()
    {
        Debug.Log("[BallSpawnManager] SpawnBall() called", this);

        // Find the PlayArea that is currently in "Playing" state
        PlayAreaManager activePlayArea = GetActivePlayArea();
        
        if (activePlayArea == null)
        {
            Debug.LogWarning("[BallSpawnManager] No PlayArea in Playing state found. Cannot spawn ball.", this);
            
            // Additional debugging info
            PlayAreaManager[] allPlayAreas = FindObjectsByType<PlayAreaManager>(FindObjectsSortMode.None);
            Debug.Log($"[BallSpawnManager] Found {allPlayAreas.Length} PlayArea(s) in scene:", this);
            foreach (var playArea in allPlayAreas)
            {
                Debug.Log($"  - {playArea.gameObject.name}: State = {playArea.GetGameState()}", this);
            }
            return;
        }

        Debug.Log($"[BallSpawnManager] Found active PlayArea: {activePlayArea.gameObject.name} (State: {activePlayArea.GetGameState()})", this);
        Debug.Log($"[BallSpawnManager] Calling SpawnAndLaunchBall() on {activePlayArea.gameObject.name}", this);

        activePlayArea.SpawnAndLaunchBall();
        
        Debug.Log("[BallSpawnManager] SpawnAndLaunchBall() call completed", this);
    }

    /// <summary>
    /// Gets the PlayArea that is currently in "Playing" state and owned by the local client.
    /// This is the play area where the local player started the game.
    /// </summary>
    private PlayAreaManager GetActivePlayArea()
    {
        Debug.Log("[BallSpawnManager] GetActivePlayArea() called", this);

        // Find all PlayAreas in the scene
        PlayAreaManager[] allPlayAreas = FindObjectsByType<PlayAreaManager>(FindObjectsSortMode.None);

        Debug.Log($"[BallSpawnManager] Found {allPlayAreas.Length} PlayAreaManager(s) in scene", this);

        if (allPlayAreas.Length == 0)
        {
            Debug.LogWarning("[BallSpawnManager] No PlayAreaManager components found in scene.", this);
            return null;
        }

#if NORMCORE
        // In multiplayer, find the PlayArea that is both in Playing state AND owned by the local client
        // This ensures each client spawns balls in their own PlayArea
        foreach (var playArea in allPlayAreas)
        {
            var state = playArea.GetGameState();
            bool isOwnedByLocal = playArea.IsOwnedByLocalClient();
            Debug.Log($"[BallSpawnManager] Checking PlayArea: {playArea.gameObject.name}, State: {state}, IsOwnedByLocal: {isOwnedByLocal}", this);
            
            if (state == PlayAreaManager.GameState.Playing && isOwnedByLocal)
            {
                Debug.Log($"[BallSpawnManager] Found active PlayArea (owned by local client): {playArea.gameObject.name}", this);
                return playArea;
            }
        }
        
        // If no play area is in Playing state and owned by local client, log a warning
        Debug.LogWarning("[BallSpawnManager] No PlayArea is currently in Playing state and owned by the local client. Make sure you've started a game first (press A at PlayerShootingPoint).", this);
#else
        // In single-player, just find the one that's in Playing state
        foreach (var playArea in allPlayAreas)
        {
            var state = playArea.GetGameState();
            Debug.Log($"[BallSpawnManager] Checking PlayArea: {playArea.gameObject.name}, State: {state}", this);
            
            if (state == PlayAreaManager.GameState.Playing)
            {
                Debug.Log($"[BallSpawnManager] Found active PlayArea: {playArea.gameObject.name}", this);
                return playArea;
            }
        }

        // If no play area is in Playing state, log a warning
        Debug.LogWarning("[BallSpawnManager] No PlayArea is currently in Playing state. Make sure you've started a game first (press A at PlayerShootingPoint).", this);
#endif

        return null;
    }
}
