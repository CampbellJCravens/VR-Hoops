using UnityEngine;

/// <summary>
/// Manages the visibility of the StartGameCanvas based on player proximity.
/// Attached to the StartGame GameObject in the PlayArea prefab.
/// </summary>
public class StartGameManager : MonoBehaviour
{
    [Header("Canvas Reference")]
    [Tooltip("Reference to the StartGameCanvas GameObject. If not assigned, will search for child named 'StartGameCanvas'.")]
    [SerializeField] private GameObject startGameCanvas;

    [Header("PlayArea Reference")]
    [Tooltip("Reference to the PlayAreaManager. If not assigned, will search for it in parent hierarchy.")]
    [SerializeField] private PlayAreaManager playAreaManager;

    [Header("Distance Settings")]
    [Tooltip("Distance threshold in world units. Canvas appears when player is within this distance.")]
    [SerializeField] private float showDistance = 50f;

    [Header("Player Detection")]
    [Tooltip("Tag that identifies the player (XR Origin).")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [Tooltip("Enable to see detailed logging.")]
    [SerializeField] private bool debugLogs = false;

    private Transform m_PlayerRoot; // XR Origin transform
    private bool m_WasCanvasVisible = false;
    private Camera m_MainCamera;

    private void Awake()
    {
        // Find StartGameCanvas if not assigned
        if (startGameCanvas == null)
        {
            // Search in children
            Transform canvasTransform = transform.Find("StartGameCanvas");
            if (canvasTransform != null)
            {
                startGameCanvas = canvasTransform.gameObject;
                if (debugLogs)
                    Debug.Log($"[StartGameManager] Found StartGameCanvas as child: {startGameCanvas.name}", this);
            }
            else
            {
                Debug.LogError($"[StartGameManager] StartGameCanvas not found! Please assign it in the inspector or ensure it's a child named 'StartGameCanvas'.", this);
            }
        }

        // Initially hide the canvas
        if (startGameCanvas != null)
        {
            startGameCanvas.SetActive(false);
            m_WasCanvasVisible = false;
        }

        // Find PlayAreaManager if not assigned
        if (playAreaManager == null)
        {
            playAreaManager = GetComponentInParent<PlayAreaManager>();
            if (debugLogs && playAreaManager != null)
                Debug.Log($"[StartGameManager] Found PlayAreaManager: {playAreaManager.gameObject.name}", this);
        }

        // Subscribe to game state changes
        if (playAreaManager != null)
        {
            playAreaManager.GameStateChanged += OnGameStateChanged;
            if (debugLogs)
                Debug.Log("[StartGameManager] Subscribed to PlayAreaManager.GameStateChanged event", this);
        }
        else
        {
            Debug.LogWarning("[StartGameManager] PlayAreaManager not found! Cannot subscribe to game state changes.", this);
        }

        // Find player root and main camera
        FindPlayerRoot();
        FindMainCamera();
    }

    private void Update()
    {
        // Don't do anything if canvas reference is missing
        if (startGameCanvas == null)
            return;

        // Check if player exists
        if (m_PlayerRoot == null)
        {
            // Try to find player again (in case they spawned after Awake)
            FindPlayerRoot();
            if (m_PlayerRoot == null)
            {
                // Still no player, hide canvas
                if (m_WasCanvasVisible)
                {
                    SetCanvasVisible(false);
                }
                return;
            }
        }

        // Check if play area is occupied or game is playing - hide canvas in these cases
        if (playAreaManager != null)
        {
            bool isGamePlaying = playAreaManager.GetGameState() == PlayAreaManager.GameState.Playing;
#if NORMCORE
            bool isPlayAreaOccupied = !playAreaManager.IsAvailable(); // Play area is occupied if not available
#else
            bool isPlayAreaOccupied = false; // Without NORMCORE, assume not occupied (single player)
#endif
            
            if (isGamePlaying || isPlayAreaOccupied)
            {
                // Game is playing or play area is occupied, ensure canvas is hidden
                if (m_WasCanvasVisible)
                {
                    SetCanvasVisible(false);
                    
                    if (debugLogs)
                    {
                        Debug.Log($"[StartGameManager] Hiding canvas - GamePlaying: {isGamePlaying}, PlayAreaOccupied: {isPlayAreaOccupied}", this);
                    }
                }
                return;
            }
        }

        // Only show canvas if play area is in Pregame state and available (unoccupied)
        // This ensures all clients see the canvas correctly
        bool canShowCanvas = true;
        if (playAreaManager != null)
        {
            bool isPregame = playAreaManager.GetGameState() == PlayAreaManager.GameState.Pregame;
#if NORMCORE
            bool isAvailable = playAreaManager.IsAvailable();
#else
            bool isAvailable = true; // Without NORMCORE, assume always available (single player)
#endif
            canShowCanvas = isPregame && isAvailable;
            
            if (!canShowCanvas && debugLogs)
            {
                Debug.Log($"[StartGameManager] Cannot show canvas - IsPregame: {isPregame}, IsAvailable: {isAvailable}", this);
            }
        }

        // Calculate distance from player to this GameObject
        float distance = Vector3.Distance(transform.position, m_PlayerRoot.position);
        bool shouldBeVisible = canShowCanvas && distance <= showDistance;

        // Only update if visibility state changed
        if (shouldBeVisible != m_WasCanvasVisible)
        {
            SetCanvasVisible(shouldBeVisible);
            
            if (debugLogs)
            {
                Debug.Log($"[StartGameManager] Player distance: {distance:F2} / {showDistance:F2}. Canvas visibility: {shouldBeVisible}", this);
            }
        }

        // Rotate canvas towards camera when visible
        if (shouldBeVisible && startGameCanvas != null && startGameCanvas.activeSelf)
        {
            RotateCanvasTowardsCamera();
        }
    }

    /// <summary>
    /// Sets the canvas visibility state.
    /// </summary>
    private void SetCanvasVisible(bool visible)
    {
        if (startGameCanvas != null)
        {
            startGameCanvas.SetActive(visible);
            m_WasCanvasVisible = visible;
        }
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
            if (debugLogs && m_PlayerRoot != null)
                Debug.Log($"[StartGameManager] Found player by tag: {m_PlayerRoot.name}", this);
            return;
        }

        // Try to find XR Origin component
        var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            m_PlayerRoot = xrOrigin.transform;
            if (debugLogs)
                Debug.Log($"[StartGameManager] Found XR Origin: {m_PlayerRoot.name}", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[StartGameManager] Could not find player/XR Origin. Canvas will remain hidden.", this);
        }
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
    /// Finds the main camera (VR headset camera).
    /// </summary>
    private void FindMainCamera()
    {
        // Try to find main camera
        m_MainCamera = Camera.main;
        if (m_MainCamera == null)
        {
            // If no main camera tag, find the camera in the XR Origin
            if (m_PlayerRoot != null)
            {
                m_MainCamera = m_PlayerRoot.GetComponentInChildren<Camera>();
            }
            
            // Fallback: find any camera
            if (m_MainCamera == null)
            {
                m_MainCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (debugLogs && m_MainCamera != null)
        {
            Debug.Log($"[StartGameManager] Found main camera: {m_MainCamera.name}", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[StartGameManager] Could not find main camera. Canvas will not rotate towards camera.", this);
        }
    }

    /// <summary>
    /// Rotates the canvas to face the main camera.
    /// </summary>
    private void RotateCanvasTowardsCamera()
    {
        if (startGameCanvas == null || m_MainCamera == null)
        {
            // Try to find camera again if missing
            if (m_MainCamera == null)
            {
                FindMainCamera();
            }
            return;
        }

        Transform canvasTransform = startGameCanvas.transform;
        
        // Calculate direction from canvas to camera
        Vector3 directionToCamera = m_MainCamera.transform.position - canvasTransform.position;
        
        // Only rotate on Y axis (horizontal rotation) for billboard effect
        // This prevents the canvas from tilting up/down with the camera
        directionToCamera.y = 0f;
        
        // Only rotate if there's a valid direction
        if (directionToCamera.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera);
            canvasTransform.rotation = targetRotation;
        }
    }

    /// <summary>
    /// Handles game state changes from PlayAreaManager.
    /// Hides/shows canvas based on game state and play area availability.
    /// This ensures all clients see the canvas state correctly.
    /// </summary>
    private void OnGameStateChanged(PlayAreaManager.GameState newState)
    {
        if (playAreaManager == null)
            return;

#if NORMCORE
        bool isAvailable = playAreaManager.IsAvailable();
#else
        bool isAvailable = true; // Without NORMCORE, assume always available (single player)
#endif
        
        if (newState == PlayAreaManager.GameState.Playing || !isAvailable)
        {
            // Hide canvas when game starts or play area becomes occupied
            SetCanvasVisible(false);
            
            if (debugLogs)
            {
                Debug.Log($"[StartGameManager] Hiding canvas - State: {newState}, IsAvailable: {isAvailable}", this);
            }
        }
        else if (newState == PlayAreaManager.GameState.Pregame && isAvailable)
        {
            // Show canvas again when game returns to Pregame and play area is unoccupied
            // The Update() method will handle distance-based visibility
            if (debugLogs)
            {
                Debug.Log($"[StartGameManager] Play area is now Pregame and available. Canvas visibility will be controlled by distance check.", this);
            }
            // Don't force show here - let the distance check in Update() handle it
            // This ensures it only shows when player is within range
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from event to prevent memory leaks
        if (playAreaManager != null)
        {
            playAreaManager.GameStateChanged -= OnGameStateChanged;
            if (debugLogs)
                Debug.Log("[StartGameManager] Unsubscribed from PlayAreaManager.GameStateChanged event", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw distance threshold as a wire sphere in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, showDistance);
    }
}

