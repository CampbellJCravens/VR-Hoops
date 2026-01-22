using UnityEngine;
using System;
using Normal.Realtime;

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
    

    [Header("Invalid Coordinates")]
    [Tooltip("Coordinates that should be skipped when moving to the next position.")]
    [SerializeField] private Vector2Int[] invalidCoordinates = new Vector2Int[] { new Vector2Int(2, 0), new Vector2Int(2, 1) };

    [Header("Hoop Reference")]
    [Tooltip("The hoop GameObject to move.")]
    [SerializeField] private Transform hoopTransform;

    private Vector2Int m_CurrentCoordinate;
    private Vector3 m_BasePosition; // Base position of this manager (relative to PlayArea)
    
    private PlayAreaManager m_PlayAreaManager;
    private RealtimePlayAreaModel m_Model;

    private void Awake()
    {
        m_BasePosition = transform.localPosition;
        m_CurrentCoordinate = startCoordinate;
    }

    private void Start()
    {
        // // Ensure starting coordinate is not invalid, if it is, find the next valid one
        // if (IsInvalidCoordinate(m_CurrentCoordinate))
        // {
        //     // Move to next position until we find a valid one
        //     int maxAttempts = numRows * numColumns;
        //     int attempts = 0;
        //     while (IsInvalidCoordinate(m_CurrentCoordinate) && attempts < maxAttempts)
        //     {
        //         m_CurrentCoordinate.y++;
        //         if (m_CurrentCoordinate.y >= numRows)
        //         {
        //             m_CurrentCoordinate.y = 0;
        //             m_CurrentCoordinate.x++;
        //             if (m_CurrentCoordinate.x >= numColumns)
        //             {
        //                 m_CurrentCoordinate.x = 0;
        //             }
        //         }
        //         attempts++;
        //     }
        // }
        
        // // Set initial hoop position (don't sync yet - will sync when model is ready if we're owner)
        // MoveHoopToCoordinate(m_CurrentCoordinate, syncToModel: false);
        
        // Initialize multiplayer sync
        InitializeMultiplayerSync();
    }
    
    /// <summary>
    /// Initializes multiplayer sync by finding the PlayAreaManager and subscribing to its model.
    /// </summary>
    private void InitializeMultiplayerSync()
    {
        m_PlayAreaManager = GetComponentInParent<PlayAreaManager>();
        
        // Subscribe to game state changes to reset hoop position when game starts
        if (m_PlayAreaManager != null)
        {
            m_PlayAreaManager.GameStateChanged += OnGameStateChanged;
        }
        
        // Subscribe to model changes after PlayAreaManager has initialized
        SubscribeToModel();
    }
    
    private void Update()
    {
        // Try to subscribe to model if we haven't yet (model might not be available in Start)
        if (m_Model == null)
        {
            if (m_PlayAreaManager != null)
            {
                m_Model = m_PlayAreaManager.GetModel();
                if (m_Model != null)
                {
                    SubscribeToModel();
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        if (m_PlayAreaManager != null)
        {
            m_PlayAreaManager.GameStateChanged -= OnGameStateChanged;
        }
        
        if (m_Model != null)
        {
            m_Model.hoopCoordinateXDidChange -= HoopCoordinateXDidChange;
            m_Model.hoopCoordinateYDidChange -= HoopCoordinateYDidChange;
        }
    }
    
    private void SubscribeToModel()
    {
        if (m_PlayAreaManager == null)
        {
            return;
        }
        
        // Get the model from PlayAreaManager which has direct access to it
        m_Model = m_PlayAreaManager.GetModel();
        if (m_Model == null)
        {
            return;
        }
        
        Vector2Int modelCoord = new Vector2Int(m_Model.hoopCoordinateX, m_Model.hoopCoordinateY);
        
        // Unsubscribe first to avoid duplicate subscriptions
        m_Model.hoopCoordinateXDidChange -= HoopCoordinateXDidChange;
        m_Model.hoopCoordinateYDidChange -= HoopCoordinateYDidChange;
        
        // Subscribe to coordinate changes
        m_Model.hoopCoordinateXDidChange += HoopCoordinateXDidChange;
        m_Model.hoopCoordinateYDidChange += HoopCoordinateYDidChange;
        
        // Apply initial state (force sync for late joiners)
        // BUT: If model has default (0,0) coordinates, don't sync yet (for ANY client)
        // The PlayAreaManager owner will initialize the model with the correct starting coordinate
        // This prevents ALL clients from overwriting their correct Start() position with (0,0)
        if (modelCoord.x == 0 && modelCoord.y == 0)
        {
            return;
        }
        
        SyncHoopPositionFromModel(m_Model, true);
    }
    
    private void HoopCoordinateXDidChange(RealtimePlayAreaModel model, int x)
    {
        SyncHoopPositionFromModel(model, false);
    }
    
    private void HoopCoordinateYDidChange(RealtimePlayAreaModel model, int y)
    {
        SyncHoopPositionFromModel(model, false);
    }
    
    private void SyncHoopPositionFromModel(RealtimePlayAreaModel model, bool forceSync = false)
    {
        if (m_PlayAreaManager == null)
        {
            return;
        }
        
        bool isOwner = m_PlayAreaManager.IsOwnedByLocalClient();
        bool shouldSync = forceSync || !isOwner;
        Vector2Int coordinate = new Vector2Int(model.hoopCoordinateX, model.hoopCoordinateY);
        
        // Always sync from model (server authoritative)
        // The owner sets the position, all clients receive it
        // Force sync on initial load to ensure late joiners get the correct position
        if (shouldSync)
        {
            // Don't sync back to model when we're syncing FROM the model (to avoid loops)
            MoveHoopToCoordinate(coordinate, syncToModel: false);
        }
    }
    
    /// <summary>
    /// Called when the game state changes. Resets hoop to start position when entering Playing state.
    /// </summary>
    private void OnGameStateChanged(PlayAreaManager.GameState newState)
    {
        if (newState == PlayAreaManager.GameState.Playing)
        {
            ResetToStartPosition();
        }
    }
    
    /// <summary>
    /// Checks if the local client owns the PlayArea and can move the hoop.
    /// </summary>
    public bool CanMoveHoop()
    {
        return m_PlayAreaManager.IsOwnedByLocalClient();
    }

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
            return;
        }
        
        // Check if we can move the hoop (only owner of PlayArea can move it)
        if (!CanMoveHoop())
        {
            return;
        }

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
        if (invalidCoordinates.Length == 0)
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
        
        Vector3 targetPosition = GetWorldPositionForCoordinate(coordinate);
        hoopTransform.localPosition = targetPosition;
        
        // Notify PlayAreaManager to sync position to model (only if owner and syncToModel is true)
        if (syncToModel && CanMoveHoop())
        {
            m_PlayAreaManager.OnHoopPositionChanged(coordinate);
        }
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

