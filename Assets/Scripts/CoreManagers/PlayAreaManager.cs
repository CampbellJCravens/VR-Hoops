using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using Normal.Realtime;

/// <summary>
/// Manages a single PlayArea, including its ShootingMachine and ball spawning.
/// Each PlayArea can have its own ball spawning logic for multiplayer support.
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public class PlayAreaManager : RealtimeComponent<RealtimePlayAreaModel>
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
    [SerializeField] private ShootingMachineLauncher shootingMachine;
    
    [SerializeField] private Transform playerShootingPoint;

    [SerializeField] private ScoreManager scoreManager;
    
    [SerializeField] private HoopPositionsManager hoopPositionsManager;


    [Header("Ball Spawning")]
    [Tooltip("The basketball prefab to spawn.")]
    [SerializeField] private GameObject basketballPrefab;

    [Header("Game State")]
    [Tooltip("Current game state. Pregame by default.")]
    [SerializeField] private GameState currentGameState = GameState.Pregame;

    [Header("Movement Control")]
    [Tooltip("If true, player movement will be disabled during gameplay. Re-enabled on game over.")]
    [SerializeField] private bool disableMovementDuringGameplay = true;


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
        
        // Sync game state to multiplayer model (unless we're syncing FROM the model)
        if (syncToModel)
        {
            OnGameStateChanged(newState);
        }
        
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

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
    }

    /// <summary>
    /// Gets the ScoreManager for this PlayArea.
    /// </summary>
    public ScoreManager GetScoreManager()
    {
        return scoreManager;
    }

    /// <summary>
    /// Spawns and launches a ball from this PlayArea's ShootingMachine.
    /// </summary>
    public void SpawnAndLaunchBall()
    {
        // Don't spawn balls if game is over or in pregame
        if (currentGameState == GameState.GameOver || currentGameState == GameState.Pregame)
        {
            return;
        }
        
        // Block spawning if a money ball is currently in play
        if (m_MoneyBallInPlay)
        {
            return;
        }

        // Get spawn position from shooting machine
        Vector3 spawnPosition = shootingMachine.transform.position;
        Quaternion spawnRotation = shootingMachine.transform.rotation;
        
        // Use Normcore's networked instantiation
        // Note: Basketball prefab must be in Resources/Prefabs/ folder and have RealtimeView component
        var options = new Realtime.InstantiateOptions
        {
            ownedByClient = true,
            preventOwnershipTakeover = false, // Allow ownership transfer for physics
            useInstance = realtimeView.realtime
        };
        // Use the full path relative to Resources folder
        // The prefab is at Resources/Prefabs/Basketball.prefab, so use "Prefabs/Basketball"
        string prefabName = "Prefabs/Basketball";
        
        // Spawn the ball (use Realtime.Instantiate for multiplayer sync)
        GameObject newBall = Realtime.Instantiate(prefabName, spawnPosition, spawnRotation, options);
        newBall.GetComponent<BallFireSound>().Initialize(scoreManager);
        
        // Parent the ball to this PlayAreaManager so GetComponentInParent works
        newBall.transform.SetParent(transform);
            
        // Track this ball for cleanup
        m_SpawnedBalls.Add(newBall);
        
        // Get ball spawn count (server authoritative)
        // If we're the owner, use local counter as source of truth
        // If we're not the owner, read from model (but we shouldn't be spawning if not owner)
        int currentBallSpawnCount = m_BallSpawnCount;
        // Only read from model if we're NOT the owner (shouldn't happen, but safety check)
        // Owner uses local counter as source of truth
        if (!IsOwnedByLocalClient())
        {
            currentBallSpawnCount = model.ballSpawnCount;
        }
        
        // Increment ball spawn counter BEFORE determining if it's a money ball
        currentBallSpawnCount++;
        m_BallSpawnCount = currentBallSpawnCount;
        
        // Sync to model (only owner can write)
        OnBallSpawnCountChanged(currentBallSpawnCount);
        
        // Add BallStateTracker component if it doesn't exist
        if (newBall.GetComponent<BallStateTracker>() == null)
        {
            newBall.AddComponent<BallStateTracker>();
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
        visualController.SetMaterial(materialToUse);
        
        // If this is a money ball, block spawning until it registers as a shot
        if (isMoneyBall)
        {
            m_MoneyBallInPlay = true;
        }

        // Initialize fire VFX on the ball if we're currently on fire
        // This ensures the VFX is properly set up even if there are timing issues with component initialization
        OnFireVFXTrigger[] vfxTriggers = newBall.GetComponentsInChildren<OnFireVFXTrigger>();
        foreach (OnFireVFXTrigger vfxTrigger in vfxTriggers)
        {
            // Force initialization of VFX state
            vfxTrigger.InitializeVFX();
        }
        
        // Initialize fire sound on the ball if we're currently on fire
        BallFireSound[] fireSounds = newBall.GetComponentsInChildren<BallFireSound>();
        foreach (BallFireSound fireSound in fireSounds)
        {
            // Force initialization of fire sound state
            fireSound.InitializeFireSound();
        }

        // Launch the ball
        shootingMachine.LaunchBall(newBall);
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
        
        // Sync shot count to multiplayer model
        OnShotCountChanged(m_ShotCount);
        
        return m_ShotCount;
    }
    
    /// <summary>
    /// Gets the current shot count.
    /// </summary>
    public int GetShotCount()
    {
        return m_ShotCount;
    }
    
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
        // Unsubscribe from previous model (if it exists)
        if (previousModel != null)
        {
            previousModel.gameStateDidChange -= GameStateDidChange;
            previousModel.ownerClientIDDidChange -= OwnerClientIDDidChange;
            previousModel.shotCountDidChange -= ShotCountDidChange;
            previousModel.ballSpawnCountDidChange -= BallSpawnCountDidChange;
            previousModel.hoopCoordinateXDidChange -= HoopCoordinateXDidChange;
            previousModel.hoopCoordinateYDidChange -= HoopCoordinateYDidChange;
        }
        
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
        if (realtimeView.isOwnedLocallySelf)
        {
            Vector2Int modelCoord = new Vector2Int(currentModel.hoopCoordinateX, currentModel.hoopCoordinateY);
            if (modelCoord.x == 0 && modelCoord.y == 0)
            {
                Vector2Int actualCoord = hoopPositionsManager.GetCurrentCoordinate();
                currentModel.hoopCoordinateX = actualCoord.x;
                currentModel.hoopCoordinateY = actualCoord.y;
            }
        }
    }
    
    private void BallSpawnCountDidChange(RealtimePlayAreaModel model, int ballSpawnCount)
    {
        SyncBallSpawnCountFromModel(ballSpawnCount);
    }
    
    private void HoopCoordinateXDidChange(RealtimePlayAreaModel model, int x)
    {
        SyncHoopPositionFromModel(model, false);
    }
    
    private void HoopCoordinateYDidChange(RealtimePlayAreaModel model, int y)
    {
        SyncHoopPositionFromModel(model, false);
    }
    
    /// <summary>
    /// Syncs all initial state from the model. Called when model is first assigned (for late joiners).
    /// </summary>
    private void SyncAllInitialStateFromModel(RealtimePlayAreaModel model)
    {
        
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
        
        if (shouldSync)
        {
            // Don't sync back to model when we're syncing FROM the model (to avoid loops)
            hoopPositionsManager.SetCoordinate(coordinate, syncToModel: false);
        }
    }
    
    private void GameStateDidChange(RealtimePlayAreaModel model, int gameState)
    {
        // Sync game state from model (only if different to avoid loops)
        // Set syncToModel=false to prevent writing back to model
        if ((GameState)gameState != currentGameState)
        {
            SetGameState((GameState)gameState, syncToModel: false);
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
        if (realtimeView.isOwnedLocallySelf)
        {
            model.gameState = (int)newState;
        }
    }
    
    /// <summary>
    /// Called when shot count changes. Syncs to model.
    /// </summary>
    private void OnShotCountChanged(int newCount)
    {
        // Check realtimeView.isOwnedLocallySelf directly for consistency with other write methods
        if (realtimeView.isOwnedLocallySelf)
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
        // If we're trying to set ownership to the local client, we need to acquire ownership of the RealtimeView first
        bool isSettingToLocalClient = clientID == realtime.clientID;
        bool isAvailable = IsAvailable(); // Use IsAvailable() which handles both 0 and -1
        
        if (isSettingToLocalClient && !realtimeView.isOwnedLocallySelf)
        {
            // Try to acquire ownership of the RealtimeView (not the component/model)
            int currentOwner = realtimeView.ownerIDSelf;
            if (currentOwner == -1)
            {
                // View is unowned, we can directly set ownership of the RealtimeView
                realtimeView.SetOwnership(realtime.clientID);
            }
            else
            {
                // View is owned by another client, request ownership of the RealtimeView
                realtimeView.RequestOwnership();
            }
        }
        
        // Try to write to the model if we own the RealtimeView
        // If we just requested ownership above, this might fail, but EnsureOwnership() will handle it later
        if (realtimeView.isOwnedLocallySelf)
        {
            model.ownerClientID = clientID;
        }
    }
    
    /// <summary>
    /// Gets the owner client ID of this play area.
    /// </summary>
    public int GetOwner()
    {
        return model.ownerClientID;
    }
    
    /// <summary>
    /// Checks if this play area is available (not owned by anyone).
    /// Treats both -1 (explicitly unowned) and 0 (uninitialized) as available.
    /// </summary>
    public bool IsAvailable()
    {
        int owner = GetOwner();
        // Treat both -1 (explicitly unowned) and 0 (uninitialized) as available
        return owner == -1 || owner == 0;
    }
    
    /// <summary>
    /// Checks if this play area is owned by the local client.
    /// </summary>
    public bool IsOwnedByLocalClient()
    {
        return GetOwner() == realtime.clientID;
    }
    
    /// <summary>
    /// Ensures ownership of the RealtimeView if the play area is owned by the local client.
    /// This allows the owner to write to the model properties.
    /// Uses RequestOwnership() to handle cases where another client owns the RealtimeView.
    /// </summary>
    private void EnsureOwnership()
    {
        bool playAreaOwned = IsOwnedByLocalClient();
        bool currentlyOwned = realtimeView.isOwnedLocallySelf;
        
        if (playAreaOwned && !currentlyOwned)
        {
            // Try to acquire ownership of the RealtimeView (not the component/model)
            int currentOwner = realtimeView.ownerIDSelf;
            if (currentOwner == -1)
            {
                // View is unowned, we can directly set ownership of the RealtimeView
                realtimeView.SetOwnership(realtime.clientID);
            }
            else
            {
                // View is owned by another client, request ownership of the RealtimeView
                realtimeView.RequestOwnership();
            }
        }
    }
    
    /// <summary>
    /// Called when hoop position changes. Syncs to model.
    /// </summary>
    public void OnHoopPositionChanged(Vector2Int coordinate)
    {
        // Try to ensure ownership before writing (like ScoreManager does)
        if (!realtimeView.isOwnedLocallySelf)
        {
            EnsureOwnership();
            
            // Check again after attempting to acquire ownership
            if (!realtimeView.isOwnedLocallySelf)
            {
                return;
            }
        }
        
        // Write to model (we're the owner now)
        model.hoopCoordinateX = coordinate.x;
        model.hoopCoordinateY = coordinate.y;
    }
    
    /// <summary>
    /// Called when ball spawn count changes. Syncs to model.
    /// </summary>
    private void OnBallSpawnCountChanged(int newCount)
    {
        // Check realtimeView.isOwnedLocallySelf directly for consistency with other write methods
        if (realtimeView.isOwnedLocallySelf)
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
    
    /// <summary>
    /// Called when a money ball registers as a shot.
    /// Unblocks spawning and moves the hoop.
    /// </summary>
    public void OnMoneyBallShotRegistered()
    {
        if (!m_MoneyBallInPlay)
        {
            return;
        }
        
        m_MoneyBallInPlay = false;
        
        // Move the hoop when money ball registers as a shot
        hoopPositionsManager.MoveToNextPosition();
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
        if (currentGameState != GameState.Pregame)
        {
            return;
        }

        // Snap player to shooting position and rotate towards ShootingMachine
        SnapPlayerToShootingPoint(playerRoot);

        // Disable movement
        if (disableMovementDuringGameplay)
        {
            DisablePlayerMovement(playerRoot);
        }

        // Ensure ownership BEFORE setting game state (so we can write to model)
        EnsureOwnership();

        // Transition to Playing state FIRST (before any ScoreManager calls)
        // Use SetGameState() to sync to model so other clients receive the state change
        SetGameState(GameState.Playing);

        // Note: InitializeGameSession() is called automatically by SetGameState() when transitioning to Playing

        // Handle ScoreManager initialization for this play area
        // Always call TransitionToPlaying() to ensure UI visibility is updated correctly
        scoreManager.TransitionToPlaying();
    }

    /// <summary>
    /// Snaps the player's XR Origin to the PlayerShootingPoint position.
    /// Positions the player so the bottom of the CharacterController (feet) is at the shooting point.
    /// </summary>
    private void SnapPlayerToShootingPoint(Transform playerRoot)
    {
        // Get the XR Origin component
        XROrigin xrOrigin = playerRoot.GetComponent<XROrigin>();
        // Fallback: just move the root transform
        playerRoot.position = playerShootingPoint.position;
        playerRoot.rotation = playerShootingPoint.rotation;

        // Get CharacterController to calculate bottom position (feet)
        CharacterController characterController = xrOrigin.Origin.GetComponent<CharacterController>();
        
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
        
        // Rotate player to face the ShootingMachine
        Transform shootingMachineTransform = shootingMachine.transform;
        Vector3 directionToMachine = shootingMachineTransform.position - xrOrigin.Origin.transform.position;
        directionToMachine.y = 0; // Keep rotation on horizontal plane only
        
        if (directionToMachine.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToMachine.normalized);
            xrOrigin.Origin.transform.rotation = targetRotation;
        }
        else
        {
            // Fallback: use shooting point rotation if no ShootingMachine found
            xrOrigin.Origin.transform.rotation = playerShootingPoint.rotation;
        }
    }

    /// <summary>
    /// Disables player movement by disabling movement-related locomotion providers.
    /// Keeps rotation providers (SnapTurnProvider, ContinuousTurnProvider) enabled.
    /// </summary>
    private void DisablePlayerMovement(Transform playerRoot)
    {
        // Find all locomotion providers on the XR Origin
        m_LocomotionProviders = playerRoot.GetComponentsInChildren<LocomotionProvider>();

        int disabledCount = 0;
        // Disable only movement-related locomotion providers (not rotation providers)
        foreach (var provider in m_LocomotionProviders)
        {
            if (provider.enabled)
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
    }

    /// <summary>
    /// Re-enables player movement by enabling all locomotion providers.
    /// </summary>
    private void EnablePlayerMovement()
    {
        // Re-enable all locomotion providers
        foreach (var provider in m_LocomotionProviders)
        {
            provider.enabled = true;
        }
    }

    /// <summary>
    /// Initializes a new game session for this play area (resets counters, positions, etc.).
    /// </summary>
    private void InitializeGameSession()
    {
        // Reset counters to 0 when starting a new game
        m_BallSpawnCount = 0;
        m_ShotCount = 0;
        
        // Sync counter resets to multiplayer model (only owner can write)
        if (IsOwnedByLocalClient())
        {
            OnShotCountChanged(m_ShotCount);
            OnBallSpawnCountChanged(m_BallSpawnCount);
        }
        
        // Reset money ball blocking state
        m_MoneyBallInPlay = false;

        // Reset hoop position to start
        if (hoopPositionsManager != null)
        {
            hoopPositionsManager.ResetToStartPosition();
            
            // Sync the reset position to multiplayer model (only owner can write)
            if (IsOwnedByLocalClient())
            {
                Vector2Int resetCoord = hoopPositionsManager.GetCurrentCoordinate();
                OnHoopPositionChanged(resetCoord);
            }
        }
        else
        {
        }

        // Reset shot counter (clears the counted balls set)
        // Note: ShotCounterManager should be assigned via PlayAreaManager or found through proper references
        // This is a fallback that requires proper assignment
        {
            // ShotCounterManager reset is handled through ScoreManager if needed
        }

        // Note: Score and lives are managed globally by ScoreManager
        // They will be reset when the first game starts or when transitioning from GameOver
    }
    
    /// <summary>
    /// Destroys all balls that were spawned by this PlayAreaManager.
    /// </summary>
    private void DestroyAllSpawnedBalls()
    {
        if (m_SpawnedBalls.Count == 0)
            return;
        
        // Filter out null entries (balls that were already destroyed)
        m_SpawnedBalls.RemoveAll(ball => ball == null);
        
        if (m_SpawnedBalls.Count == 0)
            return;
        
        // Create a copy of the list to avoid modification during iteration
        var ballsToDestroy = new System.Collections.Generic.List<GameObject>(m_SpawnedBalls);
        
        foreach (GameObject ball in ballsToDestroy)
        {
            // Stop VFX if present
            OnFireVFXTrigger[] vfxTriggers = ball.GetComponentsInChildren<OnFireVFXTrigger>();
            foreach (OnFireVFXTrigger vfxTrigger in vfxTriggers)
            {
                vfxTrigger.StopVFX();
            }
            
            // Destroy the ball
            Destroy(ball);
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
    /// Ends the game. Called by PlayAreaInputManager when end game input is pressed.
    /// Transitions to GameOver state.
    /// </summary>
    public void EndGame()
    {
        // Only allow ending game if we're currently playing and we own this play area
        if (currentGameState == GameState.Playing)
        {
            if (IsOwnedByLocalClient())
            {
                // Set flag to indicate this is from EndGame button (not natural game over)
                m_GameOverFromEndGameButton = true;
                SetGameState(GameState.GameOver);
            }
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
            if (IsOwnedByLocalClient())
            {
                // Clear the owner to make play area available
                SetOwner(-1);
                
                // Transition to Pregame state
                SetGameState(GameState.Pregame);
            }
        }

        m_IsWaitingForGameOverTransition = false;
    }
}

