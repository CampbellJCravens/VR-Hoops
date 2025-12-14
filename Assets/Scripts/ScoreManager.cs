using UnityEngine;
using TMPro;
#if NORMCORE
using Normal.Realtime;
#endif

/// <summary>
/// Manages score and lives for a single play area in the basketball game.
/// Each PlayArea should have its own ScoreManager instance.
/// </summary>
#if NORMCORE
[RequireComponent(typeof(RealtimeView))]
public class ScoreManager : RealtimeComponent<RealtimeScoreModel>
#else
public class ScoreManager : MonoBehaviour
#endif
{
    [Header("PlayArea Reference")]
    [Tooltip("The PlayAreaManager this ScoreManager belongs to. If not assigned, will search parent hierarchy.")]
    [SerializeField] private PlayAreaManager playAreaManager;

    [Header("UI References")]
    [Tooltip("TextMeshProUGUI component that displays the score. Assign this in the Inspector.")]
    public TextMeshProUGUI scoreText;
    
    [Tooltip("TextMeshProUGUI component that displays the lives. Assign this in the Inspector.")]
    public TextMeshProUGUI livesText;
    
    [Tooltip("TextMeshProUGUI component that displays the game over message. Assign this in the Inspector.")]
    public TextMeshProUGUI gameOverText;
    
    [Tooltip("TextMeshProUGUI component that displays waiting for player message. Assign this in the Inspector.")]
    public TextMeshProUGUI waitingForPlayerText;

    [Header("Settings")]
    [Tooltip("Duration (in seconds) to show the 'SCORE!' flash message.")]
    public float flashDuration = 0.5f;
    
    [Tooltip("Starting number of lives.")]
    public int startingLives = 5;
    
    [Tooltip("Multiplier for money ball scores (e.g., 2 = double points).")]
    public float moneyBallMultiplier = 2f;
    
    [Tooltip("Number of consecutive scores needed to activate On Fire state.")]
    public int consecutiveScoresForOnFire = 3;
    
    [Tooltip("Multiplier applied to all scores while On Fire (e.g., 2 = double points).")]
    public float onFireMultiplier = 2f;
    
    [Header("Debug")]
    [Tooltip("Enable to see detailed logging for score/lives syncing.")]
    [SerializeField] private bool debugLogs = false;

    private int m_Score = 0;
    private int m_Lives = 5;
    private float m_FlashTimer = 0f;
    private bool m_IsGameOver = false;
    private bool m_HasInitialized = false;
    private int m_ConsecutiveScores = 0;
    private bool m_IsOnFire = false;

    // Event for On Fire state changes (for VFX, etc.)
    public event System.Action<bool> OnFireStateChanged;
    
    private void Awake()
    {
        // Auto-find PlayAreaManager if not assigned
        if (playAreaManager == null)
        {
            playAreaManager = GetComponentInParent<PlayAreaManager>();
            if (playAreaManager == null)
            {
                // Try searching up the hierarchy
                Transform parent = transform.parent;
                while (parent != null && playAreaManager == null)
                {
                    playAreaManager = parent.GetComponent<PlayAreaManager>();
                    parent = parent.parent;
                }
            }
        }

        if (playAreaManager == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[ScoreManager] No PlayAreaManager found for {gameObject.name}. ScoreManager should be a child of a PlayArea or have PlayAreaManager assigned.", this);
        }

        // Initialize UI visibility on Awake (before Start) to ensure correct state on scene load
        InitializeUIVisibility();
    }
    

#if NORMCORE
    /// <summary>
    /// Called when the RealtimeModel is replaced. Handles initial state sync for late joiners.
    /// </summary>
    protected override void OnRealtimeModelReplaced(RealtimeScoreModel previousModel, RealtimeScoreModel currentModel)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] OnRealtimeModelReplaced called. Previous model: {previousModel != null}, Current model: {currentModel != null}", this);
        
        if (previousModel != null)
        {
            // Unsubscribe from previous model events
            previousModel.scoreDidChange -= ScoreDidChange;
            previousModel.livesDidChange -= LivesDidChange;
            previousModel.isOnFireDidChange -= IsOnFireDidChange;
            previousModel.isGameOverDidChange -= IsGameOverDidChange;
            previousModel.flashPointsEarnedDidChange -= FlashPointsEarnedDidChange;
        }
        
        if (currentModel != null)
        {
            if (debugLogs)
                Debug.Log($"[ScoreManager] Model available. Initial values: score={currentModel.score}, lives={currentModel.lives}, isOnFire={currentModel.isOnFire}, isGameOver={currentModel.isGameOver}, IsOwner: {isOwnedLocallySelf}, OwnerID: {realtimeView?.ownerIDSelf}", this);
            
            // Subscribe to model change events
            currentModel.scoreDidChange += ScoreDidChange;
            currentModel.livesDidChange += LivesDidChange;
            currentModel.isOnFireDidChange += IsOnFireDidChange;
            currentModel.isGameOverDidChange += IsGameOverDidChange;
            currentModel.flashPointsEarnedDidChange += FlashPointsEarnedDidChange;
            if (debugLogs)
                Debug.Log($"[ScoreManager] Subscribed to model change events (scoreDidChange, livesDidChange, etc.)", this);
            
            // Apply initial state from model (important for late joiners)
            SyncFromModel();
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] OnRealtimeModelReplaced called but currentModel is null!", this);
        }
        
        // Subscribe to ownership changes
        if (realtimeView != null)
        {
            realtimeView.ownerIDSelfDidChange += OnOwnershipChanged;
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] OnRealtimeModelReplaced called but realtimeView is null!", this);
        }
    }
    
    /// <summary>
    /// Called when ownership of the RealtimeView changes. Retries pending writes if we gained ownership.
    /// </summary>
    private void OnOwnershipChanged(RealtimeView view, int ownerID)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] Ownership changed. Is owner now: {view.isOwnedLocallySelf}, Owner ID: {ownerID}", this);
        
        // If we just gained ownership, sync our local state to the model
        if (view.isOwnedLocallySelf && model != null)
        {
            // Sync current local state to model (in case we had pending writes)
            model.score = m_Score;
            model.lives = m_Lives;
            model.isOnFire = m_IsOnFire;
            model.isGameOver = m_IsGameOver;
            if (debugLogs)
                Debug.Log($"[ScoreManager] Synced local state to model after gaining ownership: score={m_Score}, lives={m_Lives}", this);
        }
    }
    
    private void ScoreDidChange(RealtimeScoreModel model, int score)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] ScoreDidChange callback FIRED! Callback param score: {score}, Model.score: {model.score}, IsOwner: {isOwnedLocallySelf}", this);
        SyncFromModel();
    }
    
    private void LivesDidChange(RealtimeScoreModel model, int lives)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] LivesDidChange callback FIRED! Callback param lives: {lives}, Model.lives: {model.lives}, IsOwner: {isOwnedLocallySelf}", this);
        SyncFromModel();
    }
    
    private void IsOnFireDidChange(RealtimeScoreModel model, bool isOnFire)
    {
        SyncFromModel();
    }
    
    private void IsGameOverDidChange(RealtimeScoreModel model, bool isGameOver)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] IsGameOverDidChange callback FIRED! Callback param isGameOver: {isGameOver}, Model.isGameOver: {model.isGameOver}, IsOwner: {isOwnedLocallySelf}", this);
        SyncFromModel();
    }
    
    private void FlashPointsEarnedDidChange(RealtimeScoreModel model, int flashPointsEarned)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] FlashPointsEarnedDidChange callback FIRED! flashPointsEarned: {flashPointsEarned}, IsOwner: {isOwnedLocallySelf}", this);
        // When flash points change, show the flash message (only if >= 0, which means there's a flash to show)
        // Only show if we're not the owner (owner already showed it in RegisterScore)
        if (flashPointsEarned >= 0 && model != null && !isOwnedLocallySelf)
        {
            ShowFlashMessage(flashPointsEarned, model.flashIsMoneyBall, model.flashIsOnFire);
            // Clear the flash message after showing it (set to -1 to indicate no flash)
            // We need to clear it, but we can't write to the model since we're not the owner
            // The owner will clear it after the flash duration expires
        }
    }
    
    /// <summary>
    /// Syncs local state from the RealtimeModel. Called when model changes (for non-owners or initial sync).
    /// </summary>
    private void SyncFromModel()
    {
        if (model == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] SyncFromModel called but model is null!", this);
            return;
        }
        
        bool onFireChanged = (m_IsOnFire != model.isOnFire);
        
        // Update local state from model
        int oldScore = m_Score;
        int oldLives = m_Lives;
        bool oldIsGameOver = m_IsGameOver;
        m_Score = model.score;
        m_Lives = model.lives;
        m_IsOnFire = model.isOnFire;
        m_IsGameOver = model.isGameOver;
        
        bool scoreChanged = (oldScore != m_Score);
        bool livesChanged = (oldLives != m_Lives);
        bool gameOverChanged = (oldIsGameOver != m_IsGameOver);
        
        if (scoreChanged || livesChanged || gameOverChanged)
        {
            if (debugLogs)
                Debug.Log($"[ScoreManager] ✓ CLIENT2 RECEIVED: Model values changed! score: {oldScore}→{m_Score}, lives: {oldLives}→{m_Lives}, isGameOver: {oldIsGameOver}→{m_IsGameOver}, IsOwner: {isOwnedLocallySelf}", this);
        }
        else
        {
            if (debugLogs)
                Debug.Log($"[ScoreManager] SyncFromModel: No changes (score={m_Score}, lives={m_Lives}, isGameOver={m_IsGameOver}, startingLives={startingLives})", this);
        }
        
        // Update UI displays
        UpdateScoreDisplay();
        UpdateLivesDisplay();
        
        // Don't show flash message here - only show it when flashPointsEarned actually changes (via FlashPointsEarnedDidChange callback)
        
        // Fire On Fire state change event if needed
        if (onFireChanged)
        {
            OnFireStateChanged?.Invoke(model.isOnFire);
        }
        
        // Update UI visibility based on game state
        // For late joiners, ensure UI reflects the correct state
        // IMPORTANT: Check m_IsGameOver first (from model) - it's the source of truth for GameOver UI
        if (m_IsGameOver)
        {
            // Game is over - show GameOver UI regardless of PlayAreaManager state
            UpdateUIVisibilityForState(PlayAreaManager.GameState.GameOver);
            UpdateGameOverDisplay(); // Ensure game over text is updated with current score
            if (debugLogs)
                Debug.Log($"[ScoreManager] SyncFromModel: isGameOver=true, showing GameOver UI. Score: {m_Score}", this);
        }
        else if (playAreaManager != null)
        {
            PlayAreaManager.GameState currentState = playAreaManager.GetGameState();
            
            // Show Playing UI if we have actual game data:
            // - Score > 0 (obviously playing)
            // - Lives > 0 and <= startingLives (game has started - lives=startingLives means game started, just no lives lost yet)
            // Don't treat score=0, lives=0 as game data - that's just uninitialized state
            bool hasGameData = (m_Score > 0 || (m_Lives > 0 && m_Lives <= startingLives));
            
            if (debugLogs)
                Debug.Log($"[ScoreManager] SyncFromModel: currentState={currentState}, hasGameData={hasGameData} (score={m_Score}, lives={m_Lives}, startingLives={startingLives})", this);
            
            // If we have game data but state is still Pregame, the game state might not have synced yet.
            // In this case, we should show Playing UI (since we have score/lives data)
            if (currentState == PlayAreaManager.GameState.Pregame && hasGameData)
            {
                // Assume we're in Playing state if we have game data
                UpdateUIVisibilityForState(PlayAreaManager.GameState.Playing);
                if (debugLogs)
                    Debug.Log($"[ScoreManager] Model has game data (score={m_Score}, lives={m_Lives}) but state is Pregame. Showing Playing UI until state syncs.", this);
            }
            else
            {
                UpdateUIVisibilityForState(currentState);
                if (debugLogs)
                    Debug.Log($"[ScoreManager] SyncFromModel: Setting UI visibility for state {currentState}", this);
            }
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] SyncFromModel: playAreaManager is null!", this);
        }
    }
    
    /// <summary>
    /// Sets ownership of this ScoreManager's RealtimeView if the PlayArea is owned by the local client.
    /// Called when the game starts to ensure the owner can write to the model.
    /// Uses SetOwnership() instead of RequestOwnership() for scene-placed views.
    /// </summary>
    private void EnsureOwnership()
    {
        if (playAreaManager != null)
        {
#if NORMCORE
            // Check if the PlayArea is owned by the local client
            if (playAreaManager != null && realtime != null)
            {
                bool playAreaOwned = playAreaManager.IsOwnedByLocalClient();
                bool currentlyOwned = realtimeView != null && realtimeView.isOwnedLocallySelf;
                
                if (debugLogs)
                    Debug.Log($"[ScoreManager] EnsureOwnership: PlayArea owned: {playAreaOwned}, ScoreManager owned: {currentlyOwned}, Current owner ID: {realtimeView?.ownerIDSelf}, Local client ID: {realtime.clientID}", this);
                
                if (playAreaOwned && !currentlyOwned && realtimeView != null)
                {
                    // Use SetOwnership() for scene-placed views - sets ownership to local client
                    realtimeView.SetOwnership(realtime.clientID);
                    if (debugLogs)
                        Debug.Log($"[ScoreManager] Set ownership of RealtimeView to local client {realtime.clientID}. Is owner now: {realtimeView.isOwnedLocallySelf}", this);
                }
                else if (currentlyOwned)
                {
                    if (debugLogs)
                        Debug.Log($"[ScoreManager] Already owner of RealtimeView", this);
                }
            }
            else
            {
                if (playAreaManager == null)
                {
                    if (debugLogs)
                        Debug.LogWarning("[ScoreManager] PlayAreaManager not found - cannot check ownership", this);
                }
                if (realtime == null)
                {
                    if (debugLogs)
                        Debug.LogWarning("[ScoreManager] Realtime instance not available - cannot set ownership", this);
                }
            }
#endif
        }
    }
    
    /// <summary>
    /// Writes score to the model. Only the owner should call this.
    /// </summary>
    private void WriteScoreToModel(int score)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] WriteScoreToModel ENTRY: score={score}, model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}, isOwnedLocallySelf={isOwnedLocallySelf}", this);
        
        if (model == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write score - model is null", this);
            return;
        }
        
        if (realtimeView == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write score - realtimeView is null", this);
            return;
        }
        
        // Ensure we have ownership before writing
        if (!realtimeView.isOwnedLocallySelf)
        {
            if (debugLogs)
                Debug.Log($"[ScoreManager] WriteScoreToModel: Not owner. Calling EnsureOwnership(). Current owner: {realtimeView.ownerIDSelf}, Local client: {realtime?.clientID}", this);
            EnsureOwnership();
            // Check again - SetOwnership() might be synchronous
            if (!realtimeView.isOwnedLocallySelf)
            {
                if (debugLogs)
                    Debug.LogWarning($"[ScoreManager] Attempted to write score {score} but not owner yet. Requested ownership. Owner: {realtimeView.ownerIDSelf}. Will write when ownership is confirmed.", this);
                return; // Will write when ownership is confirmed via OnOwnershipChanged
            }
            // Ownership was granted immediately, continue with write below
            if (debugLogs)
                Debug.Log($"[ScoreManager] Ownership granted immediately after EnsureOwnership(). Proceeding with score write.", this);
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            int oldScore = model.score;
            model.score = score;
            if (debugLogs)
                Debug.Log($"[ScoreManager] ✓ CLIENT1 WRITE: Wrote score {score} to model (was {oldScore}). Model should sync to other clients now.", this);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[ScoreManager] ✗ WRITE FAILED: Cannot write score {score} to model - not owner (owner: {realtimeView.ownerIDSelf}, local client: {realtime?.clientID})", this);
        }
    }
    
    /// <summary>
    /// Writes lives to the model. Only the owner should call this.
    /// </summary>
    private void WriteLivesToModel(int lives)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] WriteLivesToModel ENTRY: lives={lives}, model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}, isOwnedLocallySelf={isOwnedLocallySelf}", this);
        
        if (model == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write lives - model is null", this);
            return;
        }
        
        if (realtimeView == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write lives - realtimeView is null", this);
            return;
        }
        
        // Ensure we have ownership before writing
        if (!realtimeView.isOwnedLocallySelf)
        {
            if (debugLogs)
                Debug.Log($"[ScoreManager] WriteLivesToModel: Not owner. Calling EnsureOwnership(). Current owner: {realtimeView.ownerIDSelf}, Local client: {realtime?.clientID}", this);
            EnsureOwnership();
            // Check again - SetOwnership() might be synchronous
            if (!realtimeView.isOwnedLocallySelf)
            {
                if (debugLogs)
                    Debug.LogWarning($"[ScoreManager] Attempted to write lives {lives} but not owner yet. Requested ownership. Owner: {realtimeView.ownerIDSelf}. Will write when ownership is confirmed.", this);
                return; // Will write when ownership is confirmed via OnOwnershipChanged
            }
            // Ownership was granted immediately, continue with write below
            if (debugLogs)
                Debug.Log($"[ScoreManager] Ownership granted immediately after EnsureOwnership(). Proceeding with lives write.", this);
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            int oldLives = model.lives;
            model.lives = lives;
            if (debugLogs)
                Debug.Log($"[ScoreManager] ✓ CLIENT1 WRITE: Wrote lives {lives} to model (was {oldLives}). Model should sync to other clients now.", this);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[ScoreManager] ✗ WRITE FAILED: Cannot write lives {lives} to model - not owner (owner: {realtimeView.ownerIDSelf}, local client: {realtime?.clientID})", this);
        }
    }
    
    /// <summary>
    /// Writes on fire state to the model. Only the owner should call this.
    /// </summary>
    private void WriteOnFireToModel(bool isOnFire)
    {
        if (model != null && realtimeView != null && realtimeView.isOwnedLocallySelf)
        {
            model.isOnFire = isOnFire;
        }
    }
    
    /// <summary>
    /// Writes flash message data to the model. Only the owner should call this.
    /// </summary>
    private void WriteFlashMessageToModel(int pointsEarned, bool isMoneyBall, bool isOnFire)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] WriteFlashMessageToModel ENTRY: pointsEarned={pointsEarned}, isMoneyBall={isMoneyBall}, isOnFire={isOnFire}, model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}", this);
        
        if (model == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write flash message - model is null", this);
            return;
        }
        
        if (realtimeView == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write flash message - realtimeView is null", this);
            return;
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            model.flashPointsEarned = pointsEarned;
            model.flashIsMoneyBall = isMoneyBall;
            model.flashIsOnFire = isOnFire;
            if (debugLogs)
                Debug.Log($"[ScoreManager] ✓ CLIENT1 WRITE: Wrote flash message to model (pointsEarned={pointsEarned}, isMoneyBall={isMoneyBall}, isOnFire={isOnFire}). Model should sync to other clients now.", this);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[ScoreManager] ✗ WRITE FAILED: Cannot write flash message to model - not owner (owner: {realtimeView.ownerIDSelf}, local client: {realtime?.clientID})", this);
        }
    }
    
    /// <summary>
    /// Writes game over state to the model. Only the owner should call this.
    /// </summary>
    private void WriteGameOverToModel(bool isGameOver)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] WriteGameOverToModel ENTRY: isGameOver={isGameOver}, model={model != null}, realtimeView={realtimeView != null}, realtimeView.isOwnedLocallySelf={realtimeView?.isOwnedLocallySelf}", this);
        
        if (model == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write game over state - model is null", this);
            return;
        }
        
        if (realtimeView == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Cannot write game over state - realtimeView is null", this);
            return;
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            bool oldIsGameOver = model.isGameOver;
            model.isGameOver = isGameOver;
            if (debugLogs)
                Debug.Log($"[ScoreManager] ✓ CLIENT1 WRITE: Wrote isGameOver {isGameOver} to model (was {oldIsGameOver}). Model should sync to other clients now.", this);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[ScoreManager] ✗ WRITE FAILED: Cannot write game over state {isGameOver} to model - not owner (owner: {realtimeView.ownerIDSelf}, local client: {realtime?.clientID})", this);
        }
    }
#endif

#if NORMCORE
    /// <summary>
    /// Called when the component is enabled. Ensures we're subscribed to game state changes
    /// early, before model syncs happen.
    /// </summary>
    private void OnEnable()
    {
        // Subscribe to game state changes early (in OnEnable, before Start)
        // This ensures we catch state changes even if PlayAreaManager syncs before ScoreManager.Start()
        if (playAreaManager != null)
        {
            playAreaManager.GameStateChanged += OnGameStateChanged;
        }
    }
#endif

    private void Start()
    {
        // Subscribe to game state changes from PlayAreaManager (if not already subscribed in OnEnable)
#if NORMCORE
        // Ensure we sync from model on Start if it's already available (for late joiners)
        if (model != null)
        {
            Debug.Log($"[ScoreManager] Start() called - Model already available. Syncing from model. Score: {model.score}, Lives: {model.lives}", this);
            SyncFromModel();
        }
        else
        {
            if (debugLogs)
                Debug.Log("[ScoreManager] Start() called - Model not available yet", this);
        }
        
        // Already subscribed in OnEnable, just update UI
        if (playAreaManager != null)
        {
            UpdateUIVisibilityForState(playAreaManager.GetGameState());
        }
#else
        // Non-multiplayer: subscribe here
        if (playAreaManager != null)
        {
            playAreaManager.GameStateChanged += OnGameStateChanged;
            UpdateUIVisibilityForState(playAreaManager.GetGameState());
        }
#endif
    }

    private void OnDestroy()
    {
        // Unsubscribe from game state changes
        if (playAreaManager != null)
        {
            playAreaManager.GameStateChanged -= OnGameStateChanged;
        }
        
#if NORMCORE
        // Unsubscribe from ownership changes
        if (realtimeView != null)
        {
            realtimeView.ownerIDSelfDidChange -= OnOwnershipChanged;
        }
#endif
    }

#if NORMCORE
    /// <summary>
    /// Called when the component is disabled. Clean up subscriptions.
    /// </summary>
    private void OnDisable()
    {
        // Unsubscribe to prevent duplicate subscriptions
        if (playAreaManager != null)
        {
            playAreaManager.GameStateChanged -= OnGameStateChanged;
        }
    }
#endif

    /// <summary>
    /// Handler for game state changes from PlayAreaManager.
    /// Updates UI visibility when game state changes.
    /// </summary>
    private void OnGameStateChanged(PlayAreaManager.GameState newState)
    {
        UpdateUIVisibilityForState(newState);
    }

    /// <summary>
    /// Initializes UI visibility to show only waiting for player text.
    /// </summary>
    private void InitializeUIVisibility()
    {
        // Show waiting for player text
        if (waitingForPlayerText != null)
        {
            waitingForPlayerText.gameObject.SetActive(true);
        }
        
        // Hide all game UI elements
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }
        
        if (livesText != null)
        {
            livesText.gameObject.SetActive(false);
        }
        
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Gets the PlayAreaManager this ScoreManager belongs to.
    /// </summary>
    public PlayAreaManager GetPlayAreaManager()
    {
        return playAreaManager;
    }

    /// <summary>
    /// Static helper method to find a ScoreManager from a given GameObject.
    /// Searches up the hierarchy to find the PlayArea, then gets its ScoreManager.
    /// </summary>
    public static ScoreManager FindScoreManagerFor(GameObject obj)
    {
        // Search up the hierarchy for PlayAreaManager
        PlayAreaManager playArea = obj.GetComponentInParent<PlayAreaManager>();
        if (playArea != null)
        {
            ScoreManager scoreMgr = playArea.GetScoreManager();
            if (scoreMgr == null)
            {
                Debug.LogError($"[ScoreManager] PlayAreaManager {playArea.gameObject.name} has NULL ScoreManager reference! Lives system will not work. Ensure ScoreManager is a child of PlayArea.", obj);
            }
            else
            {
                Debug.Log($"[ScoreManager] FindScoreManagerFor found ScoreManager for {obj.name} via PlayAreaManager {playArea.gameObject.name}", scoreMgr);
            }
            return scoreMgr;
        }

        // Fallback: search for ScoreManager in parent hierarchy
        ScoreManager fallbackMgr = obj.GetComponentInParent<ScoreManager>();
        if (fallbackMgr != null)
        {
            Debug.Log($"[ScoreManager] FindScoreManagerFor found ScoreManager for {obj.name} via fallback search", fallbackMgr);
        }
        else
        {
            Debug.LogWarning($"[ScoreManager] FindScoreManagerFor could NOT find ScoreManager for {obj.name}", obj);
        }
        return fallbackMgr;
    }


    private void Update()
    {
        // Count down flash timer
        if (m_FlashTimer > 0f)
        {
            m_FlashTimer -= Time.deltaTime;
            
            // When flash expires, revert to normal display and clear flash message in model
            if (m_FlashTimer <= 0f)
            {
                UpdateScoreDisplay();
                
#if NORMCORE
                // Clear flash message in model (only owner can write)
                if (model != null && realtimeView != null && realtimeView.isOwnedLocallySelf)
                {
                    // Only clear if it's currently set (>= 0)
                    if (model.flashPointsEarned >= 0)
                    {
                        model.flashPointsEarned = -1; // -1 indicates no flash message
                        if (debugLogs)
                            Debug.Log("[ScoreManager] Cleared flash message in model after timer expired", this);
                    }
                }
#endif
            }
        }
    }

    /// <summary>
    /// Called when a basket is scored. Calculates score based on row and money ball status.
    /// </summary>
    /// <param name="row">The row index (0 = first row, 1 = second row, 2 = third row). Points = row + 1.</param>
    /// <param name="isMoneyBall">Whether this was a money ball (2x multiplier).</param>
    public void RegisterScore(int row, bool isMoneyBall)
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] RegisterScore called! row={row}, isMoneyBall={isMoneyBall}, currentScore={m_Score}, isGameOver={m_IsGameOver}", this);
        
        // Don't register scores if game is over
        if (m_IsGameOver)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] RegisterScore ignored - game is over", this);
            return;
        }

        // Increment consecutive scores
        m_ConsecutiveScores++;
        
        // Check if we should activate On Fire state
        if (m_ConsecutiveScores >= consecutiveScoresForOnFire && !m_IsOnFire)
        {
            m_IsOnFire = true;
            
#if NORMCORE
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(m_IsOnFire);
#endif
            
            OnFireStateChanged?.Invoke(true); // Notify VFX systems immediately
            Debug.Log("[ScoreManager] ON FIRE ACTIVATED!", this);
        }

        // Calculate base points: row 0 = 1 point, row 1 = 2 points, row 2 = 3 points
        int basePoints = row + 1;
        
        // Apply money ball multiplier if applicable
        int pointsEarned = isMoneyBall ? Mathf.RoundToInt(basePoints * moneyBallMultiplier) : basePoints;
        
        // Apply On Fire multiplier if active
        if (m_IsOnFire)
        {
            pointsEarned = Mathf.RoundToInt(pointsEarned * onFireMultiplier);
        }
        
        m_Score += pointsEarned;
        m_FlashTimer = flashDuration;
        
#if NORMCORE
        // Sync score to model (only if owner)
        if (debugLogs)
            if (debugLogs)
            Debug.Log($"[ScoreManager] RegisterScore: About to call WriteScoreToModel with score={m_Score}", this);
        WriteScoreToModel(m_Score);
        
        // Sync flash message data to model (only if owner)
        WriteFlashMessageToModel(pointsEarned, isMoneyBall, m_IsOnFire);
#else
        if (debugLogs)
            Debug.LogWarning("[ScoreManager] RegisterScore: NORMCORE is not defined! Score will not sync across clients!", this);
#endif
        
        // If money ball, gain a life
        if (isMoneyBall)
        {
            GainLife();
        }
        
        // Show flash message
        ShowFlashMessage(pointsEarned, isMoneyBall, m_IsOnFire);
    }

    /// <summary>
    /// Called when a life should be lost.
    /// </summary>
    public void LoseLife()
    {
        if (debugLogs)
            Debug.Log($"[ScoreManager] LoseLife called! currentLives={m_Lives}, isGameOver={m_IsGameOver}", this);
        
        if (m_Lives > 0 && !m_IsGameOver)
        {
            m_Lives--;
            UpdateLivesDisplay();
            
#if NORMCORE
            // Sync lives to model (only if owner)
            if (debugLogs)
                Debug.Log($"[ScoreManager] LoseLife: About to call WriteLivesToModel with lives={m_Lives}", this);
            WriteLivesToModel(m_Lives);
#else
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] LoseLife: NORMCORE is not defined! Lives will not sync across clients!", this);
#endif
            
            // Reset consecutive scores and On Fire state when a shot misses
            bool wasOnFire = m_IsOnFire;
            m_ConsecutiveScores = 0;
            m_IsOnFire = false;
            
#if NORMCORE
            // Sync on fire state to model if it changed (only if owner)
            if (wasOnFire)
            {
                WriteOnFireToModel(false);
            }
#endif
            
            // Notify VFX systems immediately if On Fire was deactivated
            if (wasOnFire)
            {
                OnFireStateChanged?.Invoke(false);
            }
            
            // Check for game over
            if (m_Lives <= 0)
            {
                SetGameOver();
            }
        }
    }

    /// <summary>
    /// Called when a life should be gained (e.g., from money ball).
    /// </summary>
    public void GainLife()
    {
        m_Lives++;
        UpdateLivesDisplay();
        
#if NORMCORE
        // Sync lives to model (only if owner)
        WriteLivesToModel(m_Lives);
#endif
    }

    /// <summary>
    /// Updates the score display to show just the score (no flash message).
    /// </summary>
    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {m_Score}";
        }
    }
    
    /// <summary>
    /// Shows a flash message with points earned, money ball, and on fire text.
    /// </summary>
    private void ShowFlashMessage(int pointsEarned, bool isMoneyBall, bool isOnFire)
    {
        if (scoreText != null)
        {
            string moneyBallText = isMoneyBall ? " MONEY BALL!" : "";
            string onFireText = isOnFire ? " ON FIRE!" : "";
            scoreText.text = $"Score: {m_Score}   +{pointsEarned}{moneyBallText}{onFireText}";
            m_FlashTimer = flashDuration; // Reset flash timer
            if (debugLogs)
                Debug.Log($"[ScoreManager] Showing flash message: +{pointsEarned}{moneyBallText}{onFireText}", this);
        }
    }

    /// <summary>
    /// Updates the lives display.
    /// </summary>
    private void UpdateLivesDisplay()
    {
        if (livesText != null)
        {
            livesText.text = $"Lives: {m_Lives}";
        }
    }

    /// <summary>
    /// Updates the game over display.
    /// </summary>
    private void UpdateGameOverDisplay()
    {
        if (gameOverText != null)
        {
            gameOverText.text = $"Game Over - Score: {m_Score}";
        }
    }

    /// <summary>
    /// Sets the game to game over state.
    /// </summary>
    private void SetGameOver()
    {
        if (m_IsGameOver)
            return; // Already game over

        m_IsGameOver = true;
        
#if NORMCORE
        // Sync game over state to model (only if owner)
        WriteGameOverToModel(true);
#endif
        
        // Deactivate On Fire if it was active
        if (m_IsOnFire)
        {
            m_IsOnFire = false;
            
#if NORMCORE
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(false);
#endif
            
            OnFireStateChanged?.Invoke(false);
            Debug.Log("[ScoreManager] On Fire DEACTIVATED (Game Over)!", this);
        }
        m_ConsecutiveScores = 0;
        
        // Update game state in this play area's PlayAreaManager
        if (playAreaManager != null)
        {
            playAreaManager.SetGameState(PlayAreaManager.GameState.GameOver);
        }
        
        // Update UI visibility
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }
        
        if (livesText != null)
        {
            livesText.gameObject.SetActive(false);
        }
        
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            UpdateGameOverDisplay();
        }
        
        // Wait 3 seconds then restart the game
        StartCoroutine(RestartGameAfterDelay(3f));
    }

    /// <summary>
    /// Transitions the game to the Playing state, resetting score and lives.
    /// Called when a new game session starts (via PlayAreaManager.StartGame()).
    /// </summary>
    public void TransitionToPlaying()
    {
        // Reset score and lives
        m_Score = 0;
        m_Lives = startingLives;
        m_IsGameOver = false;
        m_FlashTimer = 0f;
        m_ConsecutiveScores = 0;
        
#if NORMCORE
        // Ensure we have ownership if the PlayArea is owned by this client
        EnsureOwnership();
        
        // Sync reset state to model (only if owner)
        WriteScoreToModel(0);
        WriteLivesToModel(startingLives);
        WriteGameOverToModel(false);
#endif
        
        // Deactivate On Fire if it was active
        if (m_IsOnFire)
        {
            m_IsOnFire = false;
            
#if NORMCORE
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(false);
#endif
            
            OnFireStateChanged?.Invoke(false);
            Debug.Log("[ScoreManager] On Fire DEACTIVATED (New Game)!", this);
        }
        
        // Reset this play area's game state and components
        if (playAreaManager != null)
        {
            // Set the play area state back to Playing
            playAreaManager.SetGameState(PlayAreaManager.GameState.Playing);
            
            // Note: Ball spawn and shot counters are not reset - they increment indefinitely
            
            // Reset hoop position to starting position for this play area
            HoopPositionsManager hoopManager = playAreaManager.GetComponentInChildren<HoopPositionsManager>();
            if (hoopManager != null)
            {
                hoopManager.ResetToStartPosition();
            }
            
            // Reset shot counter for this play area
            ShotCounterManager shotCounter = playAreaManager.GetComponentInChildren<ShotCounterManager>();
            if (shotCounter != null)
            {
                shotCounter.ResetCounter();
            }
        }
        
        // Update UI visibility - hide waiting text, show game UI
        if (waitingForPlayerText != null)
        {
            waitingForPlayerText.gameObject.SetActive(false);
            Debug.Log("[ScoreManager] Hiding waiting for player text, showing game UI", this);
        }
        
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            Debug.Log("[ScoreManager] Showing score text", this);
        }
        
        if (livesText != null)
        {
            livesText.gameObject.SetActive(true);
            Debug.Log("[ScoreManager] Showing lives text", this);
        }
        
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }
        
        // Update displays
        UpdateScoreDisplay();
        UpdateLivesDisplay();
        
        Debug.Log("[ScoreManager] TransitionToPlaying() completed. UI visibility updated.", this);
    }
    
    /// <summary>
    /// Updates UI visibility based on game state. Called when syncing from model or when game state changes.
    /// </summary>
    public void UpdateUIVisibilityForState(PlayAreaManager.GameState gameState)
    {
        if (gameState == PlayAreaManager.GameState.Pregame)
        {
            if (waitingForPlayerText != null)
                waitingForPlayerText.gameObject.SetActive(true);
            if (scoreText != null)
                scoreText.gameObject.SetActive(false);
            if (livesText != null)
                livesText.gameObject.SetActive(false);
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
        }
        else if (gameState == PlayAreaManager.GameState.Playing)
        {
            if (waitingForPlayerText != null)
                waitingForPlayerText.gameObject.SetActive(false);
            if (scoreText != null)
                scoreText.gameObject.SetActive(true);
            if (livesText != null)
                livesText.gameObject.SetActive(true);
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
        }
        else if (gameState == PlayAreaManager.GameState.GameOver)
        {
            if (waitingForPlayerText != null)
                waitingForPlayerText.gameObject.SetActive(false);
            if (scoreText != null)
                scoreText.gameObject.SetActive(false);
            if (livesText != null)
                livesText.gameObject.SetActive(false);
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Coroutine to wait a delay then restart the game.
    /// </summary>
    private System.Collections.IEnumerator RestartGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        TransitionToPlaying();
    }

    /// <summary>
    /// Resets the score to zero.
    /// </summary>
    public void ResetScore()
    {
        m_Score = 0;
        m_FlashTimer = 0f;
        UpdateScoreDisplay();
    }

    /// <summary>
    /// Gets the current score.
    /// </summary>
    public int GetScore()
    {
        return m_Score;
    }

    /// <summary>
    /// Gets the current number of lives.
    /// </summary>
    public int GetLives()
    {
        return m_Lives;
    }

    /// <summary>
    /// Gets whether the player is currently On Fire.
    /// </summary>
    public bool IsOnFire()
    {
        return m_IsOnFire;
    }

    /// <summary>
    /// Gets the current number of consecutive scores.
    /// </summary>
    public int GetConsecutiveScores()
    {
        return m_ConsecutiveScores;
    }

    /// <summary>
    /// Checks if the game is currently in Game Over state.
    /// </summary>
    public bool IsGameOver()
    {
        return m_IsGameOver;
    }

}

