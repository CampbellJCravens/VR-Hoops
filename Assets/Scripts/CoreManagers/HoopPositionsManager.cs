using UnityEngine;
using System;
#if NORMCORE
using Normal.Realtime;
#endif

/// <summary>
/// Manages a grid of possible hoop positions and tracks the current active position.
/// Positions are defined by a 2D coordinate system (row, column) that maps to 3D world positions.
/// </summary>
public class HoopPositionsManager : MonoBehaviour
{
    [System.Serializable]
    public class HoopPosition
    {
        [Tooltip("Row index (affects X and Y world position).")]
        public int row;
        
        [Tooltip("Column index (affects Z world position).")]
        public int column;
        
        [Tooltip("World position calculated from row/column and spacing values.")]
        public Vector3 worldPosition;

        public HoopPosition(int r, int c)
        {
            row = r;
            column = c;
        }
    }

    [Header("Grid Spacing")]
    [Tooltip("X spacing between rows (increments of 1.5: 0, 1.5, 3, etc.).")]
    [SerializeField] private float xSpacing = 1.5f;
    
    [Tooltip("Y spacing between rows (vertical offset per row).")]
    [SerializeField] private float ySpacing = 0.5f;
    
    [Tooltip("Z spacing between columns (increments of 1: -2, -1, 0, 1, 2).")]
    [SerializeField] private float zSpacing = 1f;

    [Header("Grid Bounds")]
    [Tooltip("Starting Z position (typically -2 for first column).")]
    [SerializeField] private float zStart = -2f;
    
    [Tooltip("Number of rows in the grid.")]
    [SerializeField] private int numRows = 3;
    
    [Tooltip("Number of columns in the grid (typically 5: -2 to 2).")]
    [SerializeField] private int numColumns = 5;

    [Header("Current Position")]
    [Tooltip("Starting coordinate (column, row) for the hoop. (0,0) is bottom-left. Example: (1,0) means column 1, row 0.")]
    [SerializeField] private Vector2Int startCoordinate = new Vector2Int(1, 0);

    [Header("Settings")]
    [Tooltip("If true, the hoop will never change position after its initial position. Position changes will be ignored.")]
    [SerializeField] private bool disablePositionChanges = false;
    
    [Header("Debug")]
    [Tooltip("Enable to see detailed logging for hoop position syncing.")]
    [SerializeField] private bool debugLogs = false;

    [Header("Invalid Coordinates")]
    [Tooltip("Coordinates that should be skipped when moving to the next position.")]
    [SerializeField] private Vector2Int[] invalidCoordinates = new Vector2Int[] { new Vector2Int(2, 0), new Vector2Int(2, 1) };

    [Header("Hoop Reference")]
    [Tooltip("The hoop GameObject to move. If not assigned, will search for BasketballHoop in children.")]
    [SerializeField] private Transform hoopTransform;

    private Vector2Int m_CurrentCoordinate;
    private Vector3 m_BasePosition; // Base position of this manager (relative to PlayArea)
    
#if NORMCORE
    private PlayAreaManager m_PlayAreaManager;
    private RealtimePlayAreaModel m_Model;
#endif

    private void Awake()
    {
        m_BasePosition = transform.localPosition;
        m_CurrentCoordinate = startCoordinate;
        
        // Auto-find hoop if not assigned
        if (hoopTransform == null)
        {
            Transform found = transform.parent.Find("BasketballHoop");
            if (found == null)
            {
                // Try searching in children
                found = GetComponentInChildren<Transform>();
                foreach (Transform child in transform.parent)
                {
                    if (child.name.Contains("Hoop") || child.name.Contains("hoop"))
                    {
                        found = child;
                        break;
                    }
                }
            }
            if (found != null)
            {
                hoopTransform = found;
            }
        }
    }

    private void Start()
    {
        // Ensure starting coordinate is not invalid, if it is, find the next valid one
        if (IsInvalidCoordinate(m_CurrentCoordinate))
        {
            if (debugLogs)
                Debug.LogWarning($"[HoopPositionsManager] Starting coordinate {m_CurrentCoordinate} is invalid. Finding next valid coordinate.", this);
            // Move to next position until we find a valid one
            int maxAttempts = numRows * numColumns;
            int attempts = 0;
            while (IsInvalidCoordinate(m_CurrentCoordinate) && attempts < maxAttempts)
            {
                m_CurrentCoordinate.y++;
                if (m_CurrentCoordinate.y >= numRows)
                {
                    m_CurrentCoordinate.y = 0;
                    m_CurrentCoordinate.x++;
                    if (m_CurrentCoordinate.x >= numColumns)
                    {
                        m_CurrentCoordinate.x = 0;
                    }
                }
                attempts++;
            }
        }
        
        // Set initial hoop position (don't sync yet - will sync when model is ready if we're owner)
        MoveHoopToCoordinate(m_CurrentCoordinate, syncToModel: false);
        
#if NORMCORE
        // Initialize multiplayer sync
        InitializeMultiplayerSync();
#endif
    }
    
#if NORMCORE
    /// <summary>
    /// Initializes multiplayer sync by finding the PlayAreaManager and subscribing to its model.
    /// </summary>
    private void InitializeMultiplayerSync()
    {
        // Find PlayAreaManager to get access to its RealtimePlayAreaModel
        m_PlayAreaManager = GetComponentInParent<PlayAreaManager>();
        if (m_PlayAreaManager == null)
        {
            if (debugLogs)
                Debug.LogWarning("[HoopPositionsManager] PlayAreaManager not found in parent. Hoop position sync may not work.", this);
            return;
        }
        
        if (debugLogs)
            Debug.Log("[HoopPositionsManager] InitializeMultiplayerSync: Found PlayAreaManager", this);
        
        // Subscribe to model changes after PlayAreaManager has initialized
        SubscribeToModel();
    }
    
    private void Update()
    {
        // Try to subscribe to model if we haven't yet (model might not be available in Start)
        if (m_Model == null && m_PlayAreaManager != null)
        {
            m_Model = m_PlayAreaManager.GetModel();
            if (m_Model != null)
            {
                SubscribeToModel();
            }
        }
    }
    
    private void OnDestroy()
    {
        if (m_Model != null)
        {
            m_Model.hoopCoordinateXDidChange -= HoopCoordinateXDidChange;
            m_Model.hoopCoordinateYDidChange -= HoopCoordinateYDidChange;
        }
    }
    
    private void SubscribeToModel()
    {
        // Get the model from PlayAreaManager which has direct access to it
        if (m_PlayAreaManager != null)
        {
            m_Model = m_PlayAreaManager.GetModel();
            if (m_Model != null)
            {
                // Unsubscribe first to avoid duplicate subscriptions
                m_Model.hoopCoordinateXDidChange -= HoopCoordinateXDidChange;
                m_Model.hoopCoordinateYDidChange -= HoopCoordinateYDidChange;
                
                // Subscribe to coordinate changes
                m_Model.hoopCoordinateXDidChange += HoopCoordinateXDidChange;
                m_Model.hoopCoordinateYDidChange += HoopCoordinateYDidChange;
                
                if (debugLogs)
                    Debug.Log($"[HoopPositionsManager] SubscribeToModel: Subscribed to coordinate change events. Current model coordinates: ({m_Model.hoopCoordinateX}, {m_Model.hoopCoordinateY}), Local coordinate: {m_CurrentCoordinate}, IsOwner: {m_PlayAreaManager.IsOwnedByLocalClient()}", this);
                
                // Apply initial state (force sync for late joiners)
                // The PlayAreaManager will handle initializing default coordinates, so we just sync from model
                SyncHoopPositionFromModel(m_Model, true);
            }
            else if (debugLogs)
            {
                Debug.Log("[HoopPositionsManager] SubscribeToModel: Model not available yet.", this);
            }
        }
    }
    
    private void HoopCoordinateXDidChange(RealtimePlayAreaModel model, int x)
    {
        if (debugLogs)
            Debug.Log($"[HoopPositionsManager] HoopCoordinateXDidChange callback: x={x}, current model coords: ({model.hoopCoordinateX}, {model.hoopCoordinateY}), local coord: {m_CurrentCoordinate}", this);
        SyncHoopPositionFromModel(model, false);
    }
    
    private void HoopCoordinateYDidChange(RealtimePlayAreaModel model, int y)
    {
        if (debugLogs)
            Debug.Log($"[HoopPositionsManager] HoopCoordinateYDidChange callback: y={y}, current model coords: ({model.hoopCoordinateX}, {model.hoopCoordinateY}), local coord: {m_CurrentCoordinate}", this);
        SyncHoopPositionFromModel(model, false);
    }
    
    private void SyncHoopPositionFromModel(RealtimePlayAreaModel model, bool forceSync = false)
    {
        if (model == null) return;
        
        // Always sync from model (server authoritative)
        // The owner sets the position, all clients receive it
        // Force sync on initial load to ensure late joiners get the correct position
        if (forceSync || (m_PlayAreaManager != null && !m_PlayAreaManager.IsOwnedByLocalClient()))
        {
            Vector2Int coordinate = new Vector2Int(model.hoopCoordinateX, model.hoopCoordinateY);
            if (debugLogs)
                Debug.Log($"[HoopPositionsManager] SyncHoopPositionFromModel: forceSync={forceSync}, isOwner={m_PlayAreaManager != null && m_PlayAreaManager.IsOwnedByLocalClient()}, modelCoord=({coordinate.x}, {coordinate.y}), localCoord={m_CurrentCoordinate}", this);
            
            // Don't sync back to model when we're syncing FROM the model (to avoid loops)
            MoveHoopToCoordinate(coordinate, syncToModel: false);
            
            if (debugLogs)
                Debug.Log($"[HoopPositionsManager] ✓ Synced hoop position from model: ({coordinate.x}, {coordinate.y}) → local position updated", this);
        }
        else if (debugLogs)
        {
            Debug.Log($"[HoopPositionsManager] SyncHoopPositionFromModel: Skipped sync (forceSync={forceSync}, isOwner={m_PlayAreaManager != null && m_PlayAreaManager.IsOwnedByLocalClient()})", this);
        }
    }
    
    /// <summary>
    /// Checks if the local client owns the PlayArea and can move the hoop.
    /// </summary>
    public bool CanMoveHoop()
    {
        if (m_PlayAreaManager == null) return false;
        return m_PlayAreaManager.IsOwnedByLocalClient();
    }
#endif

    /// <summary>
    /// Moves to the next position in the grid sequence.
    /// Sequence: (1,0) -> (1,1) -> (1,2) -> (2,0) -> (2,1) -> etc.
    /// Moves through rows first, then to next column.
    /// Skips any coordinates in the invalidCoordinates array.
    /// </summary>
    public void MoveToNextPosition()
    {
        // If position changes are disabled, do nothing
        if (disablePositionChanges)
        {
            if (debugLogs)
                Debug.Log("[HoopPositionsManager] Position changes are disabled. Hoop will remain at current position.", this);
            return;
        }
        
#if NORMCORE
        // Check if we can move the hoop (only owner of PlayArea can move it)
        if (!CanMoveHoop())
        {
            if (debugLogs)
                Debug.Log("[HoopPositionsManager] Cannot move hoop - local client does not own this PlayArea.", this);
            return;
        }
        
        if (debugLogs)
            Debug.Log($"[HoopPositionsManager] MoveToNextPosition: Current coordinate: {m_CurrentCoordinate}, IsOwner: {m_PlayAreaManager != null && m_PlayAreaManager.IsOwnedByLocalClient()}", this);
#endif

        int maxAttempts = numRows * numColumns; // Prevent infinite loop
        int attempts = 0;
        
        do
        {
            // Move to next row first (increment y, which is row)
            m_CurrentCoordinate.y++;
            
            // If we've exceeded the number of rows, move to next column and reset row
            if (m_CurrentCoordinate.y >= numRows)
            {
                m_CurrentCoordinate.y = 0;
                m_CurrentCoordinate.x++; // Increment column (x)
                
                // If we've exceeded columns, wrap around to first column
                if (m_CurrentCoordinate.x >= numColumns)
                {
                    m_CurrentCoordinate.x = 0;
                }
            }
            
            attempts++;
            
            // Continue if this coordinate is invalid, but don't loop forever
            if (attempts >= maxAttempts)
            {
                if (debugLogs)
                    Debug.LogWarning($"[HoopPositionsManager] Could not find valid coordinate after {maxAttempts} attempts. Using current coordinate.", this);
                break;
            }
        }
        while (IsInvalidCoordinate(m_CurrentCoordinate));
        
        MoveHoopToCoordinate(m_CurrentCoordinate);
    }

    /// <summary>
    /// Checks if a coordinate is in the invalid coordinates list.
    /// </summary>
    private bool IsInvalidCoordinate(Vector2Int coordinate)
    {
        if (invalidCoordinates == null || invalidCoordinates.Length == 0)
            return false;
        
        foreach (Vector2Int invalidCoord in invalidCoordinates)
        {
            if (invalidCoord.x == coordinate.x && invalidCoord.y == coordinate.y)
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Moves the hoop to the specified coordinate.
    /// </summary>
    /// <param name="coordinate">The coordinate to move to.</param>
    /// <param name="syncToModel">Whether to sync to model (default: true). Set to false when syncing from model to avoid loops.</param>
    public void MoveHoopToCoordinate(Vector2Int coordinate, bool syncToModel = true)
    {
        m_CurrentCoordinate = coordinate;
        
        if (hoopTransform == null)
        {
            if (debugLogs)
                Debug.LogWarning("[HoopPositionsManager] Cannot move hoop - hoop transform not assigned.", this);
            return;
        }

        Vector3 targetPosition = GetWorldPositionForCoordinate(coordinate);
        Vector3 oldPosition = hoopTransform.localPosition;
        hoopTransform.localPosition = targetPosition;
        
        if (debugLogs)
            Debug.Log($"[HoopPositionsManager] MoveHoopToCoordinate: coordinate=({coordinate.x}, {coordinate.y}), syncToModel={syncToModel}, oldPos={oldPosition}, newPos={targetPosition}", this);
        
        // Notify PlayAreaManager to sync position to model (only if owner and syncToModel is true)
#if NORMCORE
        if (syncToModel && m_PlayAreaManager != null && CanMoveHoop())
        {
            m_PlayAreaManager.OnHoopPositionChanged(coordinate);
            if (debugLogs)
                Debug.Log($"[HoopPositionsManager] ✓ Synced hoop position to model: ({coordinate.x}, {coordinate.y})", this);
        }
        else if (debugLogs && syncToModel)
        {
            Debug.Log($"[HoopPositionsManager] MoveHoopToCoordinate: Not syncing to model (syncToModel={syncToModel}, m_PlayAreaManager={m_PlayAreaManager != null}, CanMoveHoop={CanMoveHoop()})", this);
        }
#endif
    }

    /// <summary>
    /// Calculates the world position for a given (column, row) coordinate.
    /// Coordinate system: (column, row) where (0,0) is bottom-left.
    /// - Column (first value) determines Z position
    /// - Row (second value) determines X and Y positions
    /// </summary>
    public Vector3 GetWorldPositionForCoordinate(Vector2Int coordinate)
    {
        // Clamp coordinates to valid range
        // coordinate.x = column, coordinate.y = row
        int column = Mathf.Clamp(coordinate.x, 0, numColumns - 1);
        int row = Mathf.Clamp(coordinate.y, 0, numRows - 1);
        
        // Calculate position:
        // - X and Y are determined by row (row * xSpacing, row * ySpacing)
        // - Z is determined by column (zStart + column * zSpacing)
        // - Z is multiplied by -1 to flip the direction
        float x = row * xSpacing;
        float y = row * ySpacing;
        float z = (zStart + column * zSpacing) * -1f;
        
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Gets the current coordinate.
    /// </summary>
    public Vector2Int GetCurrentCoordinate()
    {
        return m_CurrentCoordinate;
    }

    /// <summary>
    /// Sets the current coordinate and moves the hoop.
    /// </summary>
    /// <param name="coordinate">The coordinate to set.</param>
    /// <param name="syncToModel">Whether to sync to model (default: true). Set to false when syncing from model.</param>
    public void SetCoordinate(Vector2Int coordinate, bool syncToModel = true)
    {
        MoveHoopToCoordinate(coordinate, syncToModel);
    }

    /// <summary>
    /// Resets the hoop to its starting position.
    /// </summary>
    public void ResetToStartPosition()
    {
        Vector2Int targetCoordinate = startCoordinate;
        
        // Ensure starting coordinate is not invalid, if it is, find the next valid one
        if (IsInvalidCoordinate(targetCoordinate))
        {
            if (debugLogs)
                Debug.LogWarning($"[HoopPositionsManager] Starting coordinate {targetCoordinate} is invalid. Finding next valid coordinate.", this);
            // Move to next position until we find a valid one
            int maxAttempts = numRows * numColumns;
            int attempts = 0;
            while (IsInvalidCoordinate(targetCoordinate) && attempts < maxAttempts)
            {
                targetCoordinate.y++;
                if (targetCoordinate.y >= numRows)
                {
                    targetCoordinate.y = 0;
                    targetCoordinate.x++;
                    if (targetCoordinate.x >= numColumns)
                    {
                        targetCoordinate.x = 0;
                    }
                }
                attempts++;
            }
        }
        
        // Move hoop to the starting coordinate
        if (debugLogs)
            Debug.Log($"[HoopPositionsManager] ResetToStartPosition: Resetting to coordinate {targetCoordinate}", this);
        MoveHoopToCoordinate(targetCoordinate);
    }

    private void OnValidate()
    {
        // Ensure valid values
        numRows = Mathf.Max(1, numRows);
        numColumns = Mathf.Max(1, numColumns);
        startCoordinate.x = Mathf.Clamp(startCoordinate.x, 0, numColumns - 1);
        startCoordinate.y = Mathf.Clamp(startCoordinate.y, 0, numRows - 1);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw grid positions in editor
        for (int row = 0; row < numRows; row++)
        {
            for (int col = 0; col < numColumns; col++)
            {
                Vector2Int coord = new Vector2Int(col, row);
                Vector3 pos = transform.TransformPoint(GetWorldPositionForCoordinate(coord));
                
                // Color invalid coordinates red
                if (IsInvalidCoordinate(coord))
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.yellow;
                }
                
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
        
        // Highlight current position
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Vector3 currentPos = transform.TransformPoint(GetWorldPositionForCoordinate(m_CurrentCoordinate));
            Gizmos.DrawWireSphere(currentPos, 0.15f);
        }
    }
}

