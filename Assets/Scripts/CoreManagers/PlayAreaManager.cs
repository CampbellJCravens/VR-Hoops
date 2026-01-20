using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
#if NORMCORE
using Normal.Realtime;
#endif

/// <summary>
/// Manages a single PlayArea, including its ShootingMachine and ball spawning.
/// Each PlayArea can have its own ball spawning logic for multiplayer support.
/// </summary>
#if NORMCORE
[RequireComponent(typeof(RealtimeView))]
public class PlayAreaManager : RealtimeComponent<RealtimePlayAreaModel>
#else
public class PlayAreaManager : MonoBehaviour
#endif
{
    /// <summary>
    /// Game state enum for tracking whether the game is active or over.
    /// </summary>
    public enum GameState
    {
        Pregame,
        Playing,
        GameOver
    }

    /// <summary>
    /// Event fired when game state changes. Allows other components (like ScoreManager) to react to state changes.
    /// </summary>
    public event System.Action<GameState> GameStateChanged;

    [Header("References")]
    [Tooltip("The ShootingMachine in this PlayArea. If not assigned, will search for it in children.")]
    [SerializeField] private ShootingMachineLauncher shootingMachine;
    
    [Tooltip("The PlayerShootingPoint in this PlayArea. If not assigned, will search for it in children.")]
    [SerializeField] private Transform playerShootingPoint;

    [Tooltip("The ScoreManager for this PlayArea. If not assigned, will search for it in children.")]
    [SerializeField] private ScoreManager scoreManager;
    
    [Tooltip("The HoopPositionsManager for this PlayArea. If not assigned, will search for it in children.")]
    [SerializeField] private HoopPositionsManager hoopPositionsManager;

    [Header("Input")]
    [Tooltip("Input action for ending the game. If not assigned, will try to find automatically.")]
    [SerializeField] private InputActionProperty endGameAction;

    [Header("Ball Spawning")]
    [Tooltip("The basketball prefab to spawn.")]
    [SerializeField] private GameObject basketballPrefab;

    [Header("Game State")]
    [Tooltip("Current game state. Pregame by default.")]
    [SerializeField] private GameState currentGameState = GameState.Pregame;

    [Header("Movement Control")]
    [Tooltip("If true, player movement will be disabled during gameplay. Re-enabled on game over.")]
    [SerializeField] private bool disableMovementDuringGameplay = true;

    [Header("Debug")]
    [Tooltip("Enable to see detailed logging for game state and hoop position syncing.")]
    [SerializeField] private bool debugLogs = false;

    // Ball spawn counter - increments when a ball is spawned, never resets
    // Used to determine if a ball is a money ball (every 3rd ball)
    private int m_BallSpawnCount = 0;
    
    // Shot counter - increments when a ball hits the ground, never resets
    // Used to determine when to move the hoop (every 3rd shot)
    private int m_ShotCount = 0;

    // Track if we're waiting for game over delay to transition to Pregame
    private bool m_IsWaitingForGameOverTransition = false;
    
    // Track if game over was triggered by EndGame button (vs natural game over)
    private bool m_GameOverFromEndGameButton = false;
    
    // Track if a money ball is currently in play (blocks spawning until it registers as a shot)
    private bool m_MoneyBallInPlay = false;
    
    // Track all balls spawned by this PlayAreaManager for cleanup
    private System.Collections.Generic.List<GameObject> m_SpawnedBalls = new System.Collections.Generic.List<GameObject>();
    
    // Cache of locomotion providers to enable/disable movement
    private LocomotionProvider[] m_LocomotionProviders;
    private bool m_WasMovementEnabled = true;
    
#if NORMCORE
    private Realtime m_Realtime;
#endif

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    public GameState GetGameState()
    {
        return currentGameState;
    }

    /// <summary>
    /// Sets the game state. Called by ScoreManager when game over conditions are met.
    /// </summary>
    /// <param name="newState">The new game state.</param>
    /// <param name="syncToModel">Whether to sync to model (default: true). Set to false when called from model callback to avoid loops.</param>
    public void SetGameState(GameState newState, bool syncToModel = true)
    {
        // Clean up balls when transitioning to GameOver or Playing
        if (newState == GameState.GameOver || newState == GameState.Playing)
        {
            DestroyAllSpawnedBalls();
        }
        
        currentGameState = newState;
        
        // Fire event to notify subscribers (e.g., ScoreManager) about state change
        GameStateChanged?.Invoke(newState);
        
#if NORMCORE
        // Sync game state to multiplayer model (unless we're syncing FROM the model)
        if (syncToModel)
        {
            if (debugLogs)
                Debug.Log($"[PlayAreaManager] SetGameState: Calling OnGameStateChanged to sync state {newState} to model. model={model != null}, isOwnedLocallySelf={isOwnedLocallySelf}", this);
            OnGameStateChanged(newState);
        }
#endif
        
        // Initialize game session when transitioning to Playing state
        if (newState == GameState.Playing)
        {
            // Explicitly disable spawn blocker to ensure balls can spawn
            m_MoneyBallInPlay = false;
            InitializeGameSession();
        }
        
        // Handle GameOver state transition
        if (newState == GameState.GameOver)
        {
            // Only re-enable movement if game over was triggered by EndGame button
            // Natural game over (lives = 0) should keep player locked in place
            if (m_GameOverFromEndGameButton)
            {
                OnGameEnded(); // Re-enable movement
                StartCoroutine(TransitionToPregameAfterDelay(3f));
            }
            // For natural game over, don't re-enable movement - player stays locked
            // ScoreManager's RestartGameAfterDelay will handle restarting to Playing state
            
            // Reset flag after handling
            m_GameOverFromEndGameButton = false;
        }
    }

    private void Awake()
    {
        // Auto-find references if not assigned
        if (shootingMachine == null)
        {
            shootingMachine = GetComponentInChildren<ShootingMachineLauncher>();
        }

        // Try to auto-find EndGame input action if not assigned
        if (endGameAction.action == null)
        {
            var actionMap = InputSystem.actions?.FindActionMap("XRI RightHand");
            if (actionMap != null)
            {
                var endGame = actionMap.FindAction("EndGame");
                if (endGame != null)
                {
                    endGameAction = new InputActionProperty(endGame);
                }
            }
        }

        if (playerShootingPoint == null)
        {
            Transform found = transform.Find("PlayerShootingPoint");
            if (found == null)
            {
                // Try alternative names
                found = transform.Find("Player Shooting Point");
            }
            if (found != null)
            {
                playerShootingPoint = found;
            }
        }

        // Auto-find basketball prefab if not assigned
        if (basketballPrefab == null)
        {
            // Try to load from Resources
            basketballPrefab = Resources.Load<GameObject>("Prefabs/Basketball");
        }

        // Auto-find ScoreManager if not assigned
        if (scoreManager == null)
        {
            scoreManager = GetComponentInChildren<ScoreManager>();
        }
        
        // Auto-find HoopPositionsManager if not assigned
        if (hoopPositionsManager == null)
        {
            hoopPositionsManager = GetComponentInChildren<HoopPositionsManager>();
        }
        
#if NORMCORE
        // Auto-find Realtime component for multiplayer
        if (m_Realtime == null && realtimeView != null)
        {
            m_Realtime = realtimeView.realtime;
            if (m_Realtime == null)
            {
                m_Realtime = FindFirstObjectByType<Realtime>();
                if (m_Realtime == null)
                {
                    Debug.LogWarning("[PlayAreaManager] Realtime component not found in scene. Multiplayer features will not work.", this);
                }
            }
        }
#endif
    }

    private void OnEnable()
    {
        if (endGameAction.action != null)
        {
            endGameAction.action.performed += OnEndGamePressed;
            endGameAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (endGameAction.action != null)
        {
            endGameAction.action.performed -= OnEndGamePressed;
            endGameAction.action.Disable();
        }
    }

    /// <summary>
    /// Gets the ScoreManager for this PlayArea.
    /// </summary>
    public ScoreManager GetScoreManager()
    {
        if (scoreManager == null)
        {
            // Try to find it again (in case it was added after Awake)
            scoreManager = GetComponentInChildren<ScoreManager>();
            if (scoreManager == null)
            {
                Debug.LogError($"[PlayAreaManager] ScoreManager not found for {gameObject.name}. Lives system will not work!", this);
            }
        }
        return scoreManager;
    }

    /// <summary>
    /// Spawns and launches a ball from this PlayArea's ShootingMachine.
    /// </summary>
    public void SpawnAndLaunchBall()
    {
        Debug.Log($"[PlayAreaManager] SpawnAndLaunchBall() called on {gameObject.name}. Current state: {currentGameState}", this);

        // Don't spawn balls if game is over or in pregame
        if (currentGameState == GameState.GameOver || currentGameState == GameState.Pregame)
        {
            Debug.LogWarning($"[PlayAreaManager] Cannot spawn ball - game state is {currentGameState}. Ball will not spawn.", this);
            return;
        }
        
        // Block spawning if a money ball is currently in play
        if (m_MoneyBallInPlay)
        {
            Debug.Log("[PlayAreaManager] Cannot spawn ball - money ball is currently in play. Wait for it to register as a shot.", this);
            return;
        }

        if (shootingMachine == null)
        {
            Debug.LogError($"[PlayAreaManager] Cannot spawn ball - ShootingMachine not found in {gameObject.name}.", this);
            return;
        }

        if (basketballPrefab == null)
        {
            Debug.LogError($"[PlayAreaManager] Cannot spawn ball - Basketball prefab not assigned in {gameObject.name}.", this);
            return;
        }

        Debug.Log($"[PlayAreaManager] Spawning ball. Prefab: {basketballPrefab.name}, ShootingMachine: {shootingMachine.gameObject.name}", this);

        // Get spawn position from shooting machine
        Vector3 spawnPosition = shootingMachine.transform.position;
        Quaternion spawnRotation = shootingMachine.transform.rotation;
        
        // Spawn the ball (use Realtime.Instantiate for multiplayer sync)
        GameObject newBall;
#if NORMCORE
        if (m_Realtime != null && m_Realtime.connected)
        {
            // Use Normcore's networked instantiation
            // Note: Basketball prefab must be in Resources/Prefabs/ folder and have RealtimeView component
            Debug.Log($"[PlayAreaManager] Normcore is connected. Using Realtime.Instantiate for ball spawning.", this);
            
            var options = new Realtime.InstantiateOptions
            {
                ownedByClient = true,
                preventOwnershipTakeover = false, // Allow ownership transfer for physics
                useInstance = m_Realtime
            };
            // Use the full path relative to Resources folder
            // The prefab is at Resources/Prefabs/Basketball.prefab, so use "Prefabs/Basketball"
            string prefabName = "Prefabs/Basketball";
            Debug.Log($"[PlayAreaManager] Attempting to instantiate networked ball with prefab name: '{prefabName}'", this);
            Debug.Log($"[PlayAreaManager] Spawn position: {spawnPosition}, Rotation: {spawnRotation}", this);
            
            newBall = Realtime.Instantiate(prefabName, spawnPosition, spawnRotation, options);
            
            if (newBall == null)
            {
                Debug.LogError($"[PlayAreaManager] Realtime.Instantiate returned null! Prefab name '{prefabName}' may not exist in Resources/Prefabs/ folder.", this);
                return; // Cannot continue without a ball
            }
            
            Debug.Log($"[PlayAreaManager] Successfully instantiated networked ball: {newBall.name}", this);
            
            // Verify RealtimeView component
            RealtimeView realtimeView = newBall.GetComponent<RealtimeView>();
            if (realtimeView != null)
            {
                Debug.Log($"[PlayAreaManager] Ball RealtimeView found. Owner ID: {realtimeView.ownerIDSelf}, Is owned locally: {realtimeView.isOwnedLocallySelf}", this);
            }
            else
            {
                Debug.LogWarning($"[PlayAreaManager] Ball does not have RealtimeView component! It will not sync across clients.", this);
            }
            
            // Verify RealtimeTransform component
            RealtimeTransform realtimeTransform = newBall.GetComponent<RealtimeTransform>();
            if (realtimeTransform != null)
            {
                Debug.Log($"[PlayAreaManager] Ball RealtimeTransform found. Sync position: {realtimeTransform.syncPosition}, Sync rotation: {realtimeTransform.syncRotation}", this);
            }
            else
            {
                Debug.LogWarning($"[PlayAreaManager] Ball does not have RealtimeTransform component! Position/rotation will not sync.", this);
            }
        }
        else
        {
            // Fallback to local instantiation if not connected
            if (m_Realtime == null)
            {
                Debug.LogWarning("[PlayAreaManager] m_Realtime is null. Using local instantiation (ball will not sync).", this);
            }
            else if (!m_Realtime.connected)
            {
                Debug.LogWarning("[PlayAreaManager] Normcore is not connected. Using local instantiation (ball will not sync).", this);
            }
            
            newBall = Instantiate(basketballPrefab, spawnPosition, spawnRotation);
            
            // Disable RealtimeView components to prevent errors when not using Realtime.Instantiate
            var realtimeViews = newBall.GetComponentsInChildren<RealtimeView>();
            foreach (var view in realtimeViews)
            {
                if (view != null)
                {
                    view.enabled = false;
                    Debug.Log($"[PlayAreaManager] Disabled RealtimeView on locally instantiated ball: {view.gameObject.name}", this);
                }
            }
        }
#else
        Debug.Log("[PlayAreaManager] NORMCORE not defined. Using local instantiation.", this);
        newBall = Instantiate(basketballPrefab, spawnPosition, spawnRotation);
#endif
        Debug.Log($"[PlayAreaManager] Ball instantiated: {newBall.name}", this);
        
        // Track this ball for cleanup
        m_SpawnedBalls.Add(newBall);
        
        // Get ball spawn count (server authoritative)
        // If we're the owner, use local counter as source of truth
        // If we're not the owner, read from model (but we shouldn't be spawning if not owner)
        int currentBallSpawnCount = m_BallSpawnCount;
#if NORMCORE
        if (model != null)
        {
            // Only read from model if we're NOT the owner (shouldn't happen, but safety check)
            // Owner uses local counter as source of truth
            if (!IsOwnedByLocalClient())
            {
                currentBallSpawnCount = model.ballSpawnCount;
            }
        }
#endif
        
        // Increment ball spawn counter BEFORE determining if it's a money ball
        currentBallSpawnCount++;
        m_BallSpawnCount = currentBallSpawnCount;
        
        // Sync to model (only owner can write)
#if NORMCORE
        OnBallSpawnCountChanged(currentBallSpawnCount);
#endif
        
        // Add BallStateTracker component if it doesn't exist
        if (newBall.GetComponent<BallStateTracker>() == null)
        {
            newBall.AddComponent<BallStateTracker>();
            Debug.Log("[PlayAreaManager] Added BallStateTracker component to ball", this);
        }
        
        // Determine if this is a money ball using synced count (every 3rd ball spawned)
        bool isMoneyBall = (currentBallSpawnCount % 3 == 0);
        
        // Determine the material type
        BasketballVisualController.BasketballMaterial materialToUse = 
            isMoneyBall
                ? BasketballVisualController.BasketballMaterial.RedWhiteBlue 
                : BasketballVisualController.BasketballMaterial.Orange;
        
        // Set the material - BasketballVisualController handles multiplayer sync internally
        BasketballVisualController visualController = newBall.GetComponent<BasketballVisualController>();
        if (visualController != null)
        {
            visualController.SetMaterial(materialToUse);
            Debug.Log($"[PlayAreaManager] Set material {materialToUse} on ball #{currentBallSpawnCount} via BasketballVisualController", this);
        }
        else
        {
            Debug.LogWarning("[PlayAreaManager] BasketballVisualController not found on ball prefab. Material will not be set.", this);
        }
        
        string ballType = isMoneyBall ? "Money Ball" : "Normal";
        Debug.Log($"[PlayAreaManager] Spawned ball #{currentBallSpawnCount}: {ballType} (material: {materialToUse})", this);
        
        // If this is a money ball, block spawning until it registers as a shot
        if (isMoneyBall)
        {
            m_MoneyBallInPlay = true;
            Debug.Log("[PlayAreaManager] Money ball spawned! Spawning is now blocked until it registers as a shot.", this);
        }

        // Initialize fire VFX on the ball if we're currently on fire
        // This ensures the VFX is properly set up even if there are timing issues with component initialization
        OnFireVFXTrigger[] vfxTriggers = newBall.GetComponentsInChildren<OnFireVFXTrigger>();
        foreach (OnFireVFXTrigger vfxTrigger in vfxTriggers)
        {
            if (vfxTrigger != null)
            {
                // Force initialization of VFX state
                vfxTrigger.InitializeVFX();
                Debug.Log($"[PlayAreaManager] Initialized fire VFX on ball: {newBall.name}", this);
            }
        }
        
        // Initialize fire sound on the ball if we're currently on fire
        BallFireSound[] fireSounds = newBall.GetComponentsInChildren<BallFireSound>();
        foreach (BallFireSound fireSound in fireSounds)
        {
            if (fireSound != null)
            {
                // Force initialization of fire sound state
                fireSound.InitializeFireSound();
                Debug.Log($"[PlayAreaManager] Initialized fire sound on ball: {newBall.name}", this);
            }
        }

        // Launch the ball
        Debug.Log($"[PlayAreaManager] Launching ball from ShootingMachine...", this);
        shootingMachine.LaunchBall(newBall);
        Debug.Log("[PlayAreaManager] Ball launch complete!", this);
    }

    /// <summary>
    /// Gets the ShootingMachine for this PlayArea.
    /// </summary>
    public ShootingMachineLauncher GetShootingMachine()
    {
        return shootingMachine;
    }

    /// <summary>
    /// Gets the PlayerShootingPoint for this PlayArea.
    /// </summary>
    public Transform GetPlayerShootingPoint()
    {
        return playerShootingPoint;
    }

    /// <summary>
    /// Increments the shot counter. Called by ShotCounter when a ball hits the ground.
    /// Returns the new shot count.
    /// </summary>
    public int IncrementShotCount()
    {
        m_ShotCount++;
        
#if NORMCORE
        // Sync shot count to multiplayer model
        OnShotCountChanged(m_ShotCount);
#endif
        
        return m_ShotCount;
    }
    
    /// <summary>
    /// Gets the current shot count.
    /// </summary>
    public int GetShotCount()
    {
        return m_ShotCount;
    }
    
#if NORMCORE
    /// <summary>
    /// Syncs shot count from multiplayer model. Called internally when model changes.
    /// </summary>
    private void SyncShotCountFromModel(int shotCount)
    {
        m_ShotCount = shotCount;
    }
    
    /// <summary>
    /// Syncs ball spawn count from multiplayer model. Called internally when model changes.
    /// Only syncs if we're NOT the owner (owner uses local counter as source of truth).
    /// </summary>
    private void SyncBallSpawnCountFromModel(int ballSpawnCount)
    {
        // Only sync from model if we're NOT the owner
        // Owner uses local counter as source of truth and writes to model
        if (!IsOwnedByLocalClient())
        {
            m_BallSpawnCount = ballSpawnCount;
        }
    }
    
    /// <summary>
    /// Called when the RealtimeModel is replaced. Handles initial state sync for late joiners.
    /// </summary>
    protected override void OnRealtimeModelReplaced(RealtimePlayAreaModel previousModel, RealtimePlayAreaModel currentModel)
    {
        if (previousModel != null)
        {
            // Unsubscribe from previous model
            previousModel.gameStateDidChange -= GameStateDidChange;
            previousModel.ownerClientIDDidChange -= OwnerClientIDDidChange;
            previousModel.shotCountDidChange -= ShotCountDidChange;
            previousModel.ballSpawnCountDidChange -= BallSpawnCountDidChange;
            previousModel.hoopCoordinateXDidChange -= HoopCoordinateXDidChange;
            previousModel.hoopCoordinateYDidChange -= HoopCoordinateYDidChange;
        }
        
        if (currentModel != null)
        {
            // Subscribe to new model changes
            currentModel.gameStateDidChange += GameStateDidChange;
            currentModel.ownerClientIDDidChange += OwnerClientIDDidChange;
            currentModel.shotCountDidChange += ShotCountDidChange;
            currentModel.ballSpawnCountDidChange += BallSpawnCountDidChange;
            currentModel.hoopCoordinateXDidChange += HoopCoordinateXDidChange;
            currentModel.hoopCoordinateYDidChange += HoopCoordinateYDidChange;
            
            // Sync ALL initial state from model (for late joiners)
            SyncAllInitialStateFromModel(currentModel);
            
            // If we're the owner and model has default coordinates (0,0), initialize them with actual hoop position
            // Check realtimeView.isOwnedLocallySelf directly for consistency with other write methods
            if (realtimeView != null && realtimeView.isOwnedLocallySelf && hoopPositionsManager != null)
            {
                Vector2Int modelCoord = new Vector2Int(currentModel.hoopCoordinateX, currentModel.hoopCoordinateY);
                if (modelCoord.x == 0 && modelCoord.y == 0)
                {
                    Vector2Int actualCoord = hoopPositionsManager.GetCurrentCoordinate();
                    currentModel.hoopCoordinateX = actualCoord.x;
                    currentModel.hoopCoordinateY = actualCoord.y;
                    if (debugLogs)
                        Debug.Log($"[PlayAreaManager] Initialized hoop coordinates in model from default (0,0) to actual position: ({actualCoord.x}, {actualCoord.y})", this);
                }
            }
        }
    }
    
    private void BallSpawnCountDidChange(RealtimePlayAreaModel model, int ballSpawnCount)
    {
        SyncBallSpawnCountFromModel(ballSpawnCount);
    }
    
    private void HoopCoordinateXDidChange(RealtimePlayAreaModel model, int x)
    {
        Debug.Log($"[PlayAreaManager] HoopCoordinateXDidChange callback FIRED: x={x}, model coords: ({model.hoopCoordinateX}, {model.hoopCoordinateY}), isOwner={IsOwnedByLocalClient()}", this);
        SyncHoopPositionFromModel(model, false);
    }
    
    private void HoopCoordinateYDidChange(RealtimePlayAreaModel model, int y)
    {
        Debug.Log($"[PlayAreaManager] HoopCoordinateYDidChange callback FIRED: y={y}, model coords: ({model.hoopCoordinateX}, {model.hoopCoordinateY}), isOwner={IsOwnedByLocalClient()}", this);
        SyncHoopPositionFromModel(model, false);
    }
    
    /// <summary>
    /// Syncs all initial state from the model. Called when model is first assigned (for late joiners).
    /// </summary>
    private void SyncAllInitialStateFromModel(RealtimePlayAreaModel model)
    {
        if (model == null) return;
        
        // Sync game state
        if (model.gameState != (int)currentGameState)
        {
            SetGameState((GameState)model.gameState, syncToModel: false);
        }
        
        // Sync shot count
        SyncShotCountFromModel(model.shotCount);
        
        // Sync ball spawn count
        SyncBallSpawnCountFromModel(model.ballSpawnCount);
        
        // Sync hoop position (always sync from model - server authoritative)
        SyncHoopPositionFromModel(model, true);
    }
    
    private void SyncHoopPositionFromModel(RealtimePlayAreaModel model, bool forceSync = false)
    {
        if (model == null) return;
        
        Vector2Int coordinate = new Vector2Int(model.hoopCoordinateX, model.hoopCoordinateY);
        
        // Only sync hoop position if this PlayArea is in Playing state
        // This prevents inactive PlayAreas with (0,0) coordinates from overwriting active PlayArea's hoop
        // Exception: forceSync during initial load - but only sync if coordinate is not (0,0) to avoid inactive PlayAreas
        bool shouldSync = false;
        if (forceSync)
        {
            // During initial sync, check MODEL's game state, not local state (for late joiners)
            // Also sync if coordinate is not (0,0) to handle any valid hoop position
            GameState modelGameState = (GameState)model.gameState;
            shouldSync = (coordinate.x != 0 || coordinate.y != 0) || modelGameState == GameState.Playing;
        }
        else
        {
            // Regular sync: only if not owner AND PlayArea is in Playing state
            shouldSync = !IsOwnedByLocalClient() && currentGameState == GameState.Playing;
        }
        
        if (debugLogs)
        {
            Debug.Log($"[PlayAreaManager] SyncHoopPositionFromModel: coordinate=({coordinate.x}, {coordinate.y}), forceSync={forceSync}, isOwner={IsOwnedByLocalClient()}, localGameState={currentGameState}, modelGameState={(GameState)model.gameState}, shouldSync={shouldSync}", this);
        }
        
        if (shouldSync && hoopPositionsManager != null)
        {
            // Don't sync back to model when we're syncing FROM the model (to avoid loops)
            hoopPositionsManager.SetCoordinate(coordinate, syncToModel: false);
            Debug.Log($"[PlayAreaManager] Synced hoop position from model: ({coordinate.x}, {coordinate.y}), forceSync={forceSync}, gameState={currentGameState}", this);
        }
        else if (debugLogs && !shouldSync)
        {
            Debug.Log($"[PlayAreaManager] SyncHoopPositionFromModel: Skipped sync (shouldSync=false). LocalGameState={currentGameState}, ModelGameState={(GameState)model.gameState}, IsOwner={IsOwnedByLocalClient()}", this);
        }
    }
    
    private void GameStateDidChange(RealtimePlayAreaModel model, int gameState)
    {
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] GameStateDidChange callback FIRED: gameState={gameState} ({(GameState)gameState}), model.gameState={model.gameState}, local currentGameState={currentGameState}, isOwnedLocallySelf={IsOwnedByLocalClient()}", this);
        
        // Sync game state from model (only if different to avoid loops)
        // Set syncToModel=false to prevent writing back to model
        if ((GameState)gameState != currentGameState)
        {
            if (debugLogs)
                Debug.Log($"[PlayAreaManager] GameStateDidChange: Syncing state from model: {currentGameState} → {(GameState)gameState}", this);
            SetGameState((GameState)gameState, syncToModel: false);
        }
        else if (debugLogs)
        {
            Debug.Log($"[PlayAreaManager] GameStateDidChange: Skipped sync (local state already matches model state: {currentGameState})", this);
        }
    }
    
    private void OwnerClientIDDidChange(RealtimePlayAreaModel model, int ownerClientID)
    {
        // Handle ownership changes if needed
        // For now, we just track it
    }
    
    private void ShotCountDidChange(RealtimePlayAreaModel model, int shotCount)
    {
        SyncShotCountFromModel(shotCount);
    }
    
    /// <summary>
    /// Called when game state changes. Syncs to model.
    /// </summary>
    private void OnGameStateChanged(GameState newState)
    {
        // Check realtimeView.isOwnedLocallySelf directly (not isOwnedLocallySelf property) 
        // because the property might not be updated immediately after SetOwnership()
        if (model != null && realtimeView != null && realtimeView.isOwnedLocallySelf)
        {
            int oldModelState = model.gameState;
            model.gameState = (int)newState;
            if (debugLogs)
                Debug.Log($"[PlayAreaManager] OnGameStateChanged: Wrote game state {oldModelState} → {(int)newState} ({newState}) to model. Model should broadcast to other clients.", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[PlayAreaManager] OnGameStateChanged: Cannot write game state to model. model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}", this);
        }
    }
    
    /// <summary>
    /// Called when shot count changes. Syncs to model.
    /// </summary>
    private void OnShotCountChanged(int newCount)
    {
        // Check realtimeView.isOwnedLocallySelf directly for consistency with other write methods
        if (model != null && realtimeView != null && realtimeView.isOwnedLocallySelf)
        {
            model.shotCount = newCount;
        }
    }
    
    /// <summary>
    /// Sets the owner of this play area (client ID using it).
    /// If the play area is available (unowned) and we're setting it to the local client,
    /// this will first acquire ownership of the RealtimeView so it can write to the model.
    /// </summary>
    public void SetOwner(int clientID)
    {
#if NORMCORE
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] SetOwner() called with clientID: {clientID}", this);
        
        if (model == null || realtimeView == null || realtime == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[PlayAreaManager] SetOwner() - Missing required components (model: {model != null}, realtimeView: {realtimeView != null}, realtime: {realtime != null})", this);
            return;
        }
        
        // If we're trying to set ownership to the local client, we need to acquire ownership of the RealtimeView first
        bool isSettingToLocalClient = clientID == realtime.clientID;
        bool isAvailable = IsAvailable(); // Use IsAvailable() which handles both 0 and -1
        
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] SetOwner() - isSettingToLocalClient: {isSettingToLocalClient}, isAvailable: {isAvailable}, realtimeView.isOwnedLocallySelf: {realtimeView.isOwnedLocallySelf}, currentViewOwner: {realtimeView.ownerIDSelf}", this);
        
        if (isSettingToLocalClient && !realtimeView.isOwnedLocallySelf)
        {
            // Try to acquire ownership of the RealtimeView (not the component/model)
            int currentOwner = realtimeView.ownerIDSelf;
            if (currentOwner == -1)
            {
                // View is unowned, we can directly set ownership of the RealtimeView
                realtimeView.SetOwnership(realtime.clientID);
                if (debugLogs)
                    Debug.Log($"[PlayAreaManager] Set RealtimeView ownership (view was unowned) to local client {clientID}", this);
            }
            else
            {
                // View is owned by another client, request ownership of the RealtimeView
                realtimeView.RequestOwnership();
                if (debugLogs)
                    Debug.Log($"[PlayAreaManager] Requested RealtimeView ownership (view owned by client {currentOwner}) to set play area owner to local client {clientID}", this);
            }
        }
        
        // Try to write to the model if we own the RealtimeView
        // If we just requested ownership above, this might fail, but EnsureOwnership() will handle it later
        if (realtimeView.isOwnedLocallySelf)
        {
            model.ownerClientID = clientID;
            if (debugLogs)
                Debug.Log($"[PlayAreaManager] Set play area owner to client ID: {clientID}", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning($"[PlayAreaManager] Cannot set owner immediately - local client does not own RealtimeView yet (owner: {realtimeView.ownerIDSelf}, local: {realtime.clientID}). Ownership transfer may be in progress.", this);
        }
#endif
    }
    
    /// <summary>
    /// Gets the owner client ID of this play area.
    /// </summary>
    public int GetOwner()
    {
#if NORMCORE
        if (model != null)
        {
            return model.ownerClientID;
        }
#endif
        return -1;
    }
    
    /// <summary>
    /// Checks if this play area is available (not owned by anyone).
    /// Treats both -1 (explicitly unowned) and 0 (uninitialized) as available.
    /// </summary>
    public bool IsAvailable()
    {
#if NORMCORE
        int owner = GetOwner();
        // Treat both -1 (explicitly unowned) and 0 (uninitialized) as available
        return owner == -1 || owner == 0;
#else
        // Without NORMCORE, assume play area is always available (single player)
        return true;
#endif
    }
    
    /// <summary>
    /// Checks if this play area is owned by the local client.
    /// </summary>
    public bool IsOwnedByLocalClient()
    {
#if NORMCORE
        if (realtime == null) return false;
        return GetOwner() == realtime.clientID;
#else
        // Without NORMCORE, assume local client always owns (single player)
        return true;
#endif
    }
    
    /// <summary>
    /// Ensures ownership of the RealtimeView if the play area is owned by the local client.
    /// This allows the owner to write to the model properties.
    /// Uses RequestOwnership() to handle cases where another client owns the RealtimeView.
    /// </summary>
    private void EnsureOwnership()
    {
#if NORMCORE
        if (realtime == null || realtimeView == null)
            return;
        
        bool playAreaOwned = IsOwnedByLocalClient();
        bool currentlyOwned = realtimeView.isOwnedLocallySelf;
        
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] EnsureOwnership: PlayArea owned: {playAreaOwned}, RealtimeView owned: {currentlyOwned}, Current owner ID: {realtimeView.ownerIDSelf}, Local client ID: {realtime.clientID}", this);
        
        if (playAreaOwned && !currentlyOwned)
        {
            // Try to acquire ownership of the RealtimeView (not the component/model)
            int currentOwner = realtimeView.ownerIDSelf;
            if (currentOwner == -1)
            {
                // View is unowned, we can directly set ownership of the RealtimeView
                realtimeView.SetOwnership(realtime.clientID);
                if (debugLogs)
                    Debug.Log($"[PlayAreaManager] Set RealtimeView ownership (view was unowned) for local client {realtime.clientID}", this);
            }
            else
            {
                // View is owned by another client, request ownership of the RealtimeView
                realtimeView.RequestOwnership();
                if (debugLogs)
                    Debug.Log($"[PlayAreaManager] Requested RealtimeView ownership (view owned by client {currentOwner}) for local client {realtime.clientID}", this);
            }
        }
#endif
    }
    
    /// <summary>
    /// Called when hoop position changes. Syncs to model.
    /// </summary>
    public void OnHoopPositionChanged(Vector2Int coordinate)
    {
        // Add entry debug log to see if this is being called
        Debug.Log($"[PlayAreaManager] OnHoopPositionChanged ENTRY: coordinate=({coordinate.x}, {coordinate.y}), model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}, isOwnedLocallySelf={isOwnedLocallySelf}", this);
        
        if (model == null)
        {
            Debug.LogWarning("[PlayAreaManager] OnHoopPositionChanged: Cannot write - model is null", this);
            return;
        }
        
        if (realtimeView == null)
        {
            Debug.LogWarning("[PlayAreaManager] OnHoopPositionChanged: Cannot write - realtimeView is null", this);
            return;
        }
        
        // Try to ensure ownership before writing (like ScoreManager does)
        if (!realtimeView.isOwnedLocallySelf)
        {
            Debug.Log($"[PlayAreaManager] OnHoopPositionChanged: Not owner. Calling EnsureOwnership(). Current owner: {realtimeView.ownerIDSelf}, Local client: {realtime?.clientID}", this);
            EnsureOwnership();
            
            // Check again after attempting to acquire ownership
            if (!realtimeView.isOwnedLocallySelf)
            {
                Debug.LogWarning($"[PlayAreaManager] OnHoopPositionChanged: Cannot write - not owner (owner: {realtimeView.ownerIDSelf}, local client: {realtime?.clientID})", this);
                return;
            }
            Debug.Log($"[PlayAreaManager] Ownership granted. Proceeding with hoop coordinate write.", this);
        }
        
        // Write to model (we're the owner now)
        int oldX = model.hoopCoordinateX;
        int oldY = model.hoopCoordinateY;
        model.hoopCoordinateX = coordinate.x;
        model.hoopCoordinateY = coordinate.y;
        Debug.Log($"[PlayAreaManager] OnHoopPositionChanged: Wrote hoop coordinates ({oldX}, {oldY}) → ({coordinate.x}, {coordinate.y}) to model. Model should broadcast to other clients.", this);
    }
    
    /// <summary>
    /// Called when ball spawn count changes. Syncs to model.
    /// </summary>
    private void OnBallSpawnCountChanged(int newCount)
    {
        // Check realtimeView.isOwnedLocallySelf directly for consistency with other write methods
        if (model != null && realtimeView != null && realtimeView.isOwnedLocallySelf)
        {
            model.ballSpawnCount = newCount;
        }
    }
    
    /// <summary>
    /// Gets the RealtimePlayAreaModel. Used by HoopPositionsManager.
    /// </summary>
    public RealtimePlayAreaModel GetModel()
    {
        return model;
    }
#endif
    
    /// <summary>
    /// Called when a money ball registers as a shot.
    /// Unblocks spawning and moves the hoop.
    /// </summary>
    public void OnMoneyBallShotRegistered()
    {
        if (!m_MoneyBallInPlay)
        {
            Debug.LogWarning("[PlayAreaManager] OnMoneyBallShotRegistered called but no money ball was in play.", this);
            return;
        }
        
        m_MoneyBallInPlay = false;
        Debug.Log("[PlayAreaManager] Money ball registered as shot! Spawning is now unblocked.", this);
        
        // Move the hoop when money ball registers as a shot
        if (hoopPositionsManager != null)
        {
            hoopPositionsManager.MoveToNextPosition();
        }
        else
        {
            Debug.LogWarning("[PlayAreaManager] Cannot move hoop - HoopPositionsManager not found!", this);
        }
    }
    
    /// <summary>
    /// Gets the current ball spawn count.
    /// </summary>
    public int GetBallSpawnCount()
    {
        return m_BallSpawnCount;
    }
    
    /// <summary>
    /// Checks if a money ball is currently in play (blocking spawning).
    /// </summary>
    public bool IsMoneyBallInPlay()
    {
        return m_MoneyBallInPlay;
    }
    
    // Note: Counters are no longer reset - they increment indefinitely

    /// <summary>
    /// Starts the game at this play area. Called when player presses A button at PlayerShootingPoint.
    /// Snaps player to shooting position and disables movement.
    /// </summary>
    /// <param name="playerRoot">The XR Origin transform (player root).</param>
    public void StartGame(Transform playerRoot)
    {
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] 🚀 StartGame() called on {gameObject.name} with playerRoot: {(playerRoot != null ? playerRoot.name : "NULL")}", this);
        
        if (currentGameState != GameState.Pregame)
        {
            if (debugLogs)
                Debug.LogWarning($"[PlayAreaManager] ❌ Cannot start game - play area is not in Pregame state (current: {currentGameState})", this);
            return;
        }

        if (playerRoot == null)
        {
            Debug.LogError("[PlayAreaManager] ❌ Cannot start game - player root is null!", this);
            return;
        }

        if (playerShootingPoint == null)
        {
            Debug.LogError("[PlayAreaManager] ❌ Cannot start game - PlayerShootingPoint is not assigned!", this);
            return;
        }

        if (debugLogs)
            Debug.Log($"[PlayAreaManager] ✓ Validation passed. Snapping player to shooting point...", this);

        // Snap player to shooting position and rotate towards ShootingMachine
        SnapPlayerToShootingPoint(playerRoot);

        // Disable movement
        if (disableMovementDuringGameplay)
        {
            if (debugLogs)
                Debug.Log($"[PlayAreaManager] Disabling player movement...", this);
            DisablePlayerMovement(playerRoot);
        }

        // Ensure ownership BEFORE setting game state (so we can write to model)
#if NORMCORE
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] Ensuring ownership before setting game state...", this);
        EnsureOwnership();
#endif

        // Transition to Playing state FIRST (before any ScoreManager calls)
        // Use SetGameState() to sync to model so other clients receive the state change
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] Setting game state to Playing...", this);
        SetGameState(GameState.Playing);
        if (debugLogs)
            Debug.Log($"[PlayAreaManager] ✓ Set game state to Playing for {gameObject.name}", this);

        // Note: InitializeGameSession() is called automatically by SetGameState() when transitioning to Playing

        // Handle ScoreManager initialization for this play area
        // Always call TransitionToPlaying() to ensure UI visibility is updated correctly
        if (scoreManager != null)
        {
            Debug.Log($"[PlayAreaManager] Calling TransitionToPlaying() on ScoreManager. Current state - Score: {scoreManager.GetScore()}, Lives: {scoreManager.GetLives()}, IsGameOver: {scoreManager.IsGameOver()}", this);
            scoreManager.TransitionToPlaying();
            Debug.Log($"[PlayAreaManager] TransitionToPlaying() completed", this);
        }
        else
        {
            Debug.LogWarning($"[PlayAreaManager] No ScoreManager found for {gameObject.name}. Score tracking will not work.", this);
        }

        // Verify final state
        Debug.Log($"[PlayAreaManager] Game started at {gameObject.name}. Final state: {currentGameState}", this);
    }

    /// <summary>
    /// Snaps the player's XR Origin to the PlayerShootingPoint position.
    /// Positions the player so the bottom of the CharacterController (feet) is at the shooting point.
    /// </summary>
    private void SnapPlayerToShootingPoint(Transform playerRoot)
    {
        if (playerRoot == null || playerShootingPoint == null)
            return;

        // Get the XR Origin component
        XROrigin xrOrigin = playerRoot.GetComponent<XROrigin>();
        if (xrOrigin == null)
        {
            // Try finding it in children
            xrOrigin = playerRoot.GetComponentInChildren<XROrigin>();
        }

        if (xrOrigin == null)
        {
            Debug.LogWarning("[PlayAreaManager] Could not find XROrigin component. Player position may not snap correctly.", this);
            // Fallback: just move the root transform
            playerRoot.position = playerShootingPoint.position;
            playerRoot.rotation = playerShootingPoint.rotation;
            return;
        }

        // Get CharacterController to calculate bottom position (feet)
        CharacterController characterController = xrOrigin.Origin.GetComponent<CharacterController>();
        
        if (characterController != null)
        {
            // Calculate the bottom of the CharacterController (feet position)
            // Bottom is at: center.y - (height / 2)
            float bottomOffset = characterController.center.y - (characterController.height * 0.5f);
            
            // Calculate the world position of the bottom of the CharacterController
            Vector3 currentBottomPosition = xrOrigin.Origin.transform.position + new Vector3(0, bottomOffset, 0);
            
            // Calculate how much we need to move the XR Origin so the bottom is at the shooting point
            Vector3 offsetToBottom = currentBottomPosition - xrOrigin.Origin.transform.position;
            Vector3 targetOriginPosition = playerShootingPoint.position - offsetToBottom;
            
            // Set XR Origin position so bottom of CharacterController is at shooting point
            xrOrigin.Origin.transform.position = targetOriginPosition;
        }
        else
        {
            // Fallback: if no CharacterController, use camera offset method
            Transform cameraTransform = xrOrigin.Camera.transform;
            Vector3 cameraOffset = cameraTransform.position - xrOrigin.Origin.transform.position;
            Vector3 targetOriginPosition = playerShootingPoint.position - cameraOffset;
            xrOrigin.Origin.transform.position = targetOriginPosition;
            
            Debug.LogWarning("[PlayAreaManager] No CharacterController found. Using camera offset method for positioning.", this);
        }
        
        // Rotate player to face the ShootingMachine
        if (shootingMachine != null)
        {
            Transform shootingMachineTransform = shootingMachine.transform;
            Vector3 directionToMachine = shootingMachineTransform.position - xrOrigin.Origin.transform.position;
            directionToMachine.y = 0; // Keep rotation on horizontal plane only
            
            if (directionToMachine.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToMachine.normalized);
                xrOrigin.Origin.transform.rotation = targetRotation;
            }
        }
        else
        {
            // Fallback: use shooting point rotation if no ShootingMachine found
            xrOrigin.Origin.transform.rotation = playerShootingPoint.rotation;
        }

        Debug.Log($"[PlayAreaManager] Snapped player to shooting point at {playerShootingPoint.position} (feet at shooting point)", this);
    }

    /// <summary>
    /// Disables player movement by disabling movement-related locomotion providers.
    /// Keeps rotation providers (SnapTurnProvider, ContinuousTurnProvider) enabled.
    /// </summary>
    private void DisablePlayerMovement(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        // Find all locomotion providers on the XR Origin
        m_LocomotionProviders = playerRoot.GetComponentsInChildren<LocomotionProvider>();

        if (m_LocomotionProviders == null || m_LocomotionProviders.Length == 0)
        {
            Debug.LogWarning("[PlayAreaManager] No LocomotionProvider components found. Movement may not be disabled.", this);
            return;
        }

        int disabledCount = 0;
        // Disable only movement-related locomotion providers (not rotation providers)
        foreach (var provider in m_LocomotionProviders)
        {
            if (provider != null && provider.enabled)
            {
                // Check the type name to determine if it's a rotation provider
                string providerTypeName = provider.GetType().Name;
                
                // Keep rotation providers enabled (SnapTurnProvider, ContinuousTurnProvider)
                if (providerTypeName.Contains("Turn") || providerTypeName.Contains("TurnProvider"))
                {
                    // Skip rotation providers - keep them enabled
                    continue;
                }
                
                // Disable movement providers (ContinuousMoveProvider, GrabMoveProvider, TeleportationProvider, etc.)
                provider.enabled = false;
                m_WasMovementEnabled = true;
                disabledCount++;
            }
        }

        Debug.Log($"[PlayAreaManager] Disabled {disabledCount} movement provider(s) (rotation providers remain enabled)", this);
    }

    /// <summary>
    /// Re-enables player movement by enabling all locomotion providers.
    /// </summary>
    private void EnablePlayerMovement()
    {
        if (m_LocomotionProviders == null || m_LocomotionProviders.Length == 0)
            return;

        // Re-enable all locomotion providers
        foreach (var provider in m_LocomotionProviders)
        {
            if (provider != null)
            {
                provider.enabled = true;
            }
        }

        Debug.Log("[PlayAreaManager] Re-enabled player movement", this);
    }

    /// <summary>
    /// Initializes a new game session for this play area (resets counters, positions, etc.).
    /// </summary>
    private void InitializeGameSession()
    {
        // Reset counters to 0 when starting a new game
        m_BallSpawnCount = 0;
        m_ShotCount = 0;
        
#if NORMCORE
        // Sync counter resets to multiplayer model (only owner can write)
        if (IsOwnedByLocalClient())
        {
            OnShotCountChanged(m_ShotCount);
            OnBallSpawnCountChanged(m_BallSpawnCount);
        }
#endif
        
        // Reset money ball blocking state
        m_MoneyBallInPlay = false;

        // Reset hoop position to start
        HoopPositionsManager hoopManager = GetComponentInChildren<HoopPositionsManager>();
        if (hoopManager != null)
        {
            hoopManager.ResetToStartPosition();
            
#if NORMCORE
            // Sync the reset position to multiplayer model (only owner can write)
            if (IsOwnedByLocalClient())
            {
                Vector2Int resetCoord = hoopManager.GetCurrentCoordinate();
                OnHoopPositionChanged(resetCoord);
            }
#endif
        }

        // Reset shot counter (clears the counted balls set)
        ShotCounterManager shotCounter = GetComponentInChildren<ShotCounterManager>();
        if (shotCounter != null)
        {
            shotCounter.ResetCounter();
        }

        // Note: Score and lives are managed globally by ScoreManager
        // They will be reset when the first game starts or when transitioning from GameOver
    }
    
    /// <summary>
    /// Destroys all balls that were spawned by this PlayAreaManager.
    /// </summary>
    private void DestroyAllSpawnedBalls()
    {
        if (m_SpawnedBalls == null || m_SpawnedBalls.Count == 0)
            return;
        
        // Filter out null entries (balls that were already destroyed)
        m_SpawnedBalls.RemoveAll(ball => ball == null);
        
        if (m_SpawnedBalls.Count == 0)
            return;
        
        Debug.Log($"[PlayAreaManager] Destroying {m_SpawnedBalls.Count} ball(s) for {gameObject.name}", this);
        
        // Create a copy of the list to avoid modification during iteration
        var ballsToDestroy = new System.Collections.Generic.List<GameObject>(m_SpawnedBalls);
        
        foreach (GameObject ball in ballsToDestroy)
        {
            if (ball != null)
            {
                // Stop VFX if present
                OnFireVFXTrigger[] vfxTriggers = ball.GetComponentsInChildren<OnFireVFXTrigger>();
                foreach (OnFireVFXTrigger vfxTrigger in vfxTriggers)
                {
                    if (vfxTrigger != null)
                    {
                        vfxTrigger.StopVFX();
                    }
                }
                
                // Destroy the ball
                Destroy(ball);
            }
        }
        
        // Clear the list
        m_SpawnedBalls.Clear();
    }

    /// <summary>
    /// Called when the game ends. Re-enables movement and resets state.
    /// </summary>
    public void OnGameEnded()
    {
        // Re-enable movement when game ends
        if (disableMovementDuringGameplay && m_WasMovementEnabled)
        {
            EnablePlayerMovement();
        }
    }

    /// <summary>
    /// Handles the EndGame input action press. Transitions to GameOver state.
    /// </summary>
    private void OnEndGamePressed(InputAction.CallbackContext context)
    {
        // Only allow ending game if we're currently playing and we own this play area
        if (currentGameState == GameState.Playing)
        {
#if NORMCORE
            if (IsOwnedByLocalClient())
            {
                // Set flag to indicate this is from EndGame button (not natural game over)
                m_GameOverFromEndGameButton = true;
                SetGameState(GameState.GameOver);
            }
#else
            // Set flag to indicate this is from EndGame button (not natural game over)
            m_GameOverFromEndGameButton = true;
            SetGameState(GameState.GameOver);
#endif
        }
    }

    /// <summary>
    /// Coroutine that waits for a delay after game over, then transitions to Pregame state
    /// and clears the owner, making the play area available again.
    /// </summary>
    private System.Collections.IEnumerator TransitionToPregameAfterDelay(float delay)
    {
        // Prevent multiple coroutines from running
        if (m_IsWaitingForGameOverTransition)
            yield break;

        m_IsWaitingForGameOverTransition = true;

        yield return new WaitForSeconds(delay);

        // Only transition if we're still in GameOver state and we own the play area
        if (currentGameState == GameState.GameOver)
        {
#if NORMCORE
            if (IsOwnedByLocalClient())
            {
                // Clear the owner to make play area available
                SetOwner(-1);
                
                // Transition to Pregame state
                SetGameState(GameState.Pregame);
            }
#else
            // Transition to Pregame state
            SetGameState(GameState.Pregame);
#endif
        }

        m_IsWaitingForGameOverTransition = false;
    }
}

