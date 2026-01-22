using UnityEngine;

/// <summary>
/// Manages the visibility of the StartGameCanvas based on player proximity.
/// Attached to the StartGame GameObject in the PlayArea prefab.
/// </summary>
public class StartGameManager : MonoBehaviour
{
    [Header("Canvas Reference")]
    [SerializeField] private GameObject startGameCanvas;

    [Header("PlayArea Reference")]
    [SerializeField] private PlayAreaManager playAreaManager;

    [Header("Distance Settings")]
    [Tooltip("Distance threshold in world units. Canvas appears when player is within this distance.")]
    [SerializeField] private float showDistance = 50f;

    [Header("Player Detection")]
    [Tooltip("Tag that identifies the player (XR Origin).")]
    [SerializeField] private string playerTag = "Player";


    private Transform m_PlayerRoot; // XR Origin transform
    private bool m_WasCanvasVisible = false;
    private Camera m_MainCamera;

    private void Awake()
    {
        // Initially hide the canvas
        startGameCanvas.SetActive(false);
        m_WasCanvasVisible = false;

        // Subscribe to game state changes
        playAreaManager.GameStateChanged += OnGameStateChanged;
    }

    private void Update()
    {
        // Check if play area is occupied or game is playing - hide canvas in these cases
        bool isGamePlaying = playAreaManager.GetGameState() == PlayAreaManager.GameState.Playing;
        bool isPlayAreaOccupied = !playAreaManager.IsAvailable(); // Play area is occupied if not available
        
        if (isGamePlaying || isPlayAreaOccupied)
        {
            // Game is playing or play area is occupied, ensure canvas is hidden
            if (m_WasCanvasVisible)
            {
                SetCanvasVisible(false);
            }
            return;
        }

        // Only show canvas if play area is in Pregame state and available (unoccupied)
        // This ensures all clients see the canvas correctly
        bool isPregame = playAreaManager.GetGameState() == PlayAreaManager.GameState.Pregame;
        bool isAvailable = playAreaManager.IsAvailable();
        bool canShowCanvas = isPregame && isAvailable;

        // Calculate distance from player to this GameObject
        if (GetPlayerRoot() == null) { return; }
        float distance = Vector3.Distance(transform.position, GetPlayerRoot().position);
        bool shouldBeVisible = canShowCanvas && distance <= showDistance;

        // Only update if visibility state changed
        if (shouldBeVisible != m_WasCanvasVisible)
        {
            SetCanvasVisible(shouldBeVisible);
        }

        // Rotate canvas towards camera when visible
        if (shouldBeVisible && startGameCanvas.activeSelf)
        {
            RotateCanvasTowardsCamera();
        }
    }

    /// <summary>
    /// Sets the canvas visibility state.
    /// </summary>
    private void SetCanvasVisible(bool visible)
    {
        startGameCanvas.SetActive(visible);
        m_WasCanvasVisible = visible;
    }

    /// <summary>
    /// Gets the player root transform. If not already cached, finds it by tag.
    /// </summary>
    private Transform GetPlayerRoot()
    {
        // Return cached value if valid
        if (m_PlayerRoot != null)
        {
            return m_PlayerRoot;
        }

        // Find player by tag and return its transform directly
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        m_PlayerRoot = player.transform;

        return m_PlayerRoot;
    }

    /// <summary>
    /// Gets the main camera (VR headset camera). If not already cached, finds it from the player root.
    /// </summary>
    private Camera GetMainCamera()
    {
        // Return cached value if valid
        if (m_MainCamera != null)
        {
            return m_MainCamera;
        }

        // Find camera in the XR Origin (player root)
        m_MainCamera = GetPlayerRoot().GetComponentInChildren<Camera>();

        return m_MainCamera;
    }

    /// <summary>
    /// Rotates the canvas to face the main camera.
    /// </summary>
    private void RotateCanvasTowardsCamera()
    {
        Transform canvasTransform = startGameCanvas.transform;
        
        // Calculate direction from canvas to camera
        Vector3 directionToCamera = GetMainCamera().transform.position - canvasTransform.position;
        
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

        bool isAvailable = playAreaManager.IsAvailable();
        
        if (newState == PlayAreaManager.GameState.Playing || !isAvailable)
        {
            // Hide canvas when game starts or play area becomes occupied
            SetCanvasVisible(false);
        }
        else if (newState == PlayAreaManager.GameState.Pregame && isAvailable)
        {
            // Show canvas again when game returns to Pregame and play area is unoccupied
            // The Update() method will handle distance-based visibility
            // Don't force show here - let the distance check in Update() handle it
            // This ensures it only shows when player is within range
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from event to prevent memory leaks
        playAreaManager.GameStateChanged -= OnGameStateChanged;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw distance threshold as a wire sphere in the editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, showDistance);
    }
}

