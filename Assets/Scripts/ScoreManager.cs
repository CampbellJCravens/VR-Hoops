using UnityEngine;
using TMPro;
using System.Collections;
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

    [Header("UI References - Text Components")]
    [Tooltip("TextMeshProUGUI component that displays the score. Located in Shooting Machine Canvas > Results. Assign this in the Inspector.")]
    public TextMeshProUGUI scoreText;
    
    [Tooltip("TextMeshProUGUI component that displays the lives. Located in Shooting Machine Canvas > Results. Assign this in the Inspector.")]
    public TextMeshProUGUI livesText;
    
    [Header("UI References - Canvas Children (Visibility Control)")]
    [Tooltip("BG GameObject in Shooting Machine Canvas. Always visible. Assign this in the Inspector.")]
    public GameObject bgGameObject;
    
    [Tooltip("Pregame GameObject in Shooting Machine Canvas. Visible during Pregame state. Assign this in the Inspector.")]
    public GameObject pregameGameObject;
    
    [Tooltip("GameOver GameObject in Shooting Machine Canvas. Visible during GameOver state. Assign this in the Inspector.")]
    public GameObject gameOverGameObject;
    
    [Tooltip("Results GameObject in Shooting Machine Canvas. Visible during Playing or GameOver states. Assign this in the Inspector.")]
    public GameObject resultsGameObject;
    
    [Tooltip("SpawnInstruction GameObject (child of Results). Visible only during Playing state. Assign this in the Inspector.")]
    public GameObject spawnInstructionText;

    [Header("Screen Canvas UI References")]
    [Tooltip("TextMeshProUGUI component for score display on ScreenCanvas. Shows shot type and points earned.")]
    public TextMeshProUGUI screenScoreText;
    
    [Tooltip("TextMeshProUGUI component for money ball indicator on ScreenCanvas.")]
    public TextMeshProUGUI moneyText;
    
    [Tooltip("TextMeshProUGUI component for fire shot indicator on ScreenCanvas.")]
    public TextMeshProUGUI fireText;
    
    [Tooltip("Transform position for modifier text when only one is visible (ModifierPos1).")]
    public Transform modifierPos1;
    
    [Tooltip("Transform position for modifier text when both are visible (ModifierPos2).")]
    public Transform modifierPos2;
    
    [Tooltip("GameObject that displays when a life is changed (gained or lost). Should be on ScreenCanvas. Previously named 'Lost A Life', now 'LifeChangedUI'.")]
    public GameObject lifeChangedUIGameObject;
    
    [Tooltip("TextMeshProUGUI component for 'AirBallText' child of LifeChangedUI. Visible when life is lost, hidden when life is gained.")]
    public TextMeshProUGUI airBallText;
    
    [Tooltip("Animator component for the LifeHeart animation controller. Used to trigger entry animation when a life is lost.")]
    public Animator lifeHeartAnimator;
    
    [Tooltip("LifeHeartUI component on the ScreenCanvas. Used to trigger GainLife animation when a life is gained.")]
    public LifeHeartUI lifeHeartUI;

    [Header("Settings")]
    
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
    private bool m_IsGameOver = false;
    private bool m_HasInitialized = false;
    private int m_ConsecutiveScores = 0;
    private bool m_IsOnFire = false;

    // Event for On Fire state changes (for VFX, etc.)
    public event System.Action<bool> OnFireStateChanged;
    
    // Coroutine references for UI timers
    private Coroutine m_ScoreTextCoroutine;
    private Coroutine m_MoneyTextCoroutine;
    private Coroutine m_FireTextCoroutine;
    private Coroutine m_LifeChangedUICoroutine;
    
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
        
        // Initialize ScreenCanvas UI elements as hidden
        InitializeScreenCanvasUI();
    }
    
    /// <summary>
    /// Initializes ScreenCanvas UI elements to be hidden by default.
    /// </summary>
    private void InitializeScreenCanvasUI()
    {
        if (screenScoreText != null)
            screenScoreText.gameObject.SetActive(false);
        if (moneyText != null)
            moneyText.gameObject.SetActive(false);
        if (fireText != null)
            fireText.gameObject.SetActive(false);
        if (lifeChangedUIGameObject != null)
            lifeChangedUIGameObject.SetActive(false);
        if (airBallText != null)
            airBallText.gameObject.SetActive(false);
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
        
        // Subscribe to ownership changes (unsubscribe first to prevent duplicates)
        if (realtimeView != null)
        {
            realtimeView.ownerIDSelfDidChange -= OnOwnershipChanged; // Unsubscribe first to prevent duplicates
            realtimeView.ownerIDSelfDidChange += OnOwnershipChanged;
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] OnRealtimeModelReplaced called but realtimeView is null!", this);
        }
    }
    
    /// <summary>
    /// Called when ownership of the RealtimeView changes. Syncs from model when gaining ownership.
    /// The model is always the source of truth - we sync FROM it, not TO it, to avoid overwriting correct values.
    /// </summary>
    private void OnOwnershipChanged(RealtimeView view, int ownerID)
    {
        // ALWAYS log ownership changes - this is critical for debugging
        Debug.Log($"[ScoreManager] 🔑 OnOwnershipChanged: Ownership changed. Is owner now: {view.isOwnedLocallySelf}, Owner ID: {ownerID}, ClientID: {realtime?.clientID}", this);
        
        if (view.isOwnedLocallySelf && model != null)
        {
            Debug.Log($"[ScoreManager] 🔑 OnOwnershipChanged: Gained ownership! Local m_Score BEFORE sync: {m_Score}, model.score: {model.score}", this);
            // When gaining ownership, always sync FROM model (model is source of truth)
            // This ensures we have the correct state and don't overwrite model values with stale local state
            SyncFromModel();
            Debug.Log($"[ScoreManager] 🔑 OnOwnershipChanged: After SyncFromModel, Local m_Score: {m_Score}, model.score: {model.score}", this);
        }
    }
    
    private void ScoreDidChange(RealtimeScoreModel model, int score)
    {
        // ALWAYS log score changes - this is critical for debugging
        Debug.Log($"[ScoreManager] 🔔 ScoreDidChange callback FIRED! Callback param score: {score}, Model.score: {model.score}, Local m_Score BEFORE sync: {m_Score}, IsOwner: {isOwnedLocallySelf}, ClientID: {realtime?.clientID}", this);
        SyncFromModel();
        // Log AFTER sync too
        if (debugLogs)
            Debug.Log($"[ScoreManager] ScoreDidChange: After SyncFromModel, Local m_Score: {m_Score}", this);
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
        
        // ALWAYS log score sync - this is critical for debugging
        Debug.Log($"[ScoreManager] 📥 SyncFromModel ENTRY: model.score={model.score}, Local m_Score BEFORE: {m_Score}, IsOwner: {isOwnedLocallySelf}, ClientID: {realtime?.clientID}", this);
        
        m_Score = model.score;
        m_Lives = model.lives;
        m_IsOnFire = model.isOnFire;
        m_IsGameOver = model.isGameOver;
        
        bool scoreChanged = (oldScore != m_Score);
        bool livesChanged = (oldLives != m_Lives);
        bool gameOverChanged = (oldIsGameOver != m_IsGameOver);
        
        if (scoreChanged || livesChanged || gameOverChanged)
        {
            Debug.Log($"[ScoreManager] ✅ SYNC: Model values changed! score: {oldScore}→{m_Score} (model had {model.score}), lives: {oldLives}→{m_Lives}, isGameOver: {oldIsGameOver}→{m_IsGameOver}, IsOwner: {isOwnedLocallySelf}, ClientID: {realtime?.clientID}", this);
        }
        else
        {
            if (debugLogs)
                Debug.Log($"[ScoreManager] SyncFromModel: No changes (score={m_Score}, lives={m_Lives}, isGameOver={m_IsGameOver}, startingLives={startingLives})", this);
        }
        
        // Update UI displays
        UpdateScoreDisplay();
        UpdateLivesDisplay();
        
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
    /// Uses RequestOwnership() to handle cases where another client (like the host) owns the RealtimeView.
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
                    // Try to acquire ownership of the RealtimeView (not the component/model)
                    int currentOwner = realtimeView.ownerIDSelf;
                    if (currentOwner == -1)
                    {
                        // View is unowned, we can directly set ownership of the RealtimeView
                        realtimeView.SetOwnership(realtime.clientID);
                        if (debugLogs)
                            Debug.Log($"[ScoreManager] Set RealtimeView ownership (view was unowned) for local client {realtime.clientID}", this);
                    }
                    else
                    {
                        // View is owned by another client, request ownership of the RealtimeView
                        realtimeView.RequestOwnership();
                        if (debugLogs)
                            Debug.Log($"[ScoreManager] Requested RealtimeView ownership (view owned by client {currentOwner}) for local client {realtime.clientID}", this);
                    }
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
            // ALWAYS log score writes - this is critical for debugging
            Debug.Log($"[ScoreManager] ✍️ WRITE: Wrote score {score} to model (was {oldScore}). Local m_Score: {m_Score}, ClientID: {realtime?.clientID}, Model.score is now: {model.score}", this);
            if (debugLogs)
                Debug.Log($"[ScoreManager] ✓ CLIENT1 WRITE: Model should sync to other clients now.", this);
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
    /// Initializes UI visibility based on current game state.
    /// </summary>
    private void InitializeUIVisibility()
    {
        // Get current game state from PlayAreaManager
        PlayAreaManager.GameState currentState = PlayAreaManager.GameState.Pregame;
        if (playAreaManager != null)
        {
            currentState = playAreaManager.GetGameState();
        }
        
        // Update UI visibility based on current state
        UpdateUIVisibilityForState(currentState);
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
            return fallbackMgr;
        }

        // Final fallback: search all PlayAreaManagers in the scene and find the closest one in "Playing" state
        // This handles cases where balls are spawned without being parented to the PlayArea
        PlayAreaManager[] allPlayAreas = FindObjectsOfType<PlayAreaManager>();
        if (allPlayAreas != null && allPlayAreas.Length > 0)
        {
            // Prefer PlayAreas that are in "Playing" state
            PlayAreaManager playingPlayArea = null;
            PlayAreaManager closestPlayArea = null;
            float closestDistance = float.MaxValue;
            Vector3 objPosition = obj.transform.position;

            foreach (PlayAreaManager area in allPlayAreas)
            {
                if (area == null) continue;

                // Check if this PlayArea is in "Playing" state
                if (area.GetGameState() == PlayAreaManager.GameState.Playing)
                {
                    ScoreManager scoreMgr = area.GetScoreManager();
                    if (scoreMgr != null)
                    {
                        playingPlayArea = area;
                        Debug.Log($"[ScoreManager] FindScoreManagerFor found ScoreManager for {obj.name} via scene search (Playing PlayArea: {area.gameObject.name})", scoreMgr);
                        return scoreMgr;
                    }
                }

                // Also track the closest PlayArea as a fallback
                float distance = Vector3.Distance(objPosition, area.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayArea = area;
                }
            }

            // If no Playing PlayArea found, use the closest one
            if (closestPlayArea != null)
            {
                ScoreManager scoreMgr = closestPlayArea.GetScoreManager();
                if (scoreMgr != null)
                {
                    Debug.Log($"[ScoreManager] FindScoreManagerFor found ScoreManager for {obj.name} via scene search (Closest PlayArea: {closestPlayArea.gameObject.name}, distance: {closestDistance:F2})", scoreMgr);
                    return scoreMgr;
                }
            }
        }

        Debug.LogWarning($"[ScoreManager] FindScoreManagerFor could NOT find ScoreManager for {obj.name} (searched hierarchy and scene)", obj);
        return null;
    }


    private void Update()
    {
        // Flash timer logic removed - score text now always shows just the score number
    }

    /// <summary>
    /// Called when a basket is scored. Calculates score based on row and money ball status.
    /// </summary>
    /// <param name="row">The row index (0 = first row, 1 = second row, 2 = third row). Points = row + 1.</param>
    /// <param name="isMoneyBall">Whether this was a money ball (2x multiplier).</param>
    public void RegisterScore(int row, bool isMoneyBall)
    {
        // ALWAYS log RegisterScore - this is critical for debugging
        Debug.Log($"[ScoreManager] 🎯 RegisterScore CALLED! row={row}, isMoneyBall={isMoneyBall}, currentScore BEFORE: {m_Score}, isGameOver={m_IsGameOver}, IsOwner: {isOwnedLocallySelf}, ClientID: {realtime?.clientID}", this);
        
        // Don't register scores if game is over
        if (m_IsGameOver)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] RegisterScore ignored - game is over", this);
            return;
        }

        // Increment consecutive scores
        m_ConsecutiveScores++;
        Debug.Log($"[ScoreManager] Consecutive scores: {m_ConsecutiveScores}/{consecutiveScoresForOnFire} (need {consecutiveScoresForOnFire} for On Fire)", this);
        
        // Check if we should activate On Fire state
        // Only activate when we EXACTLY reach the threshold (not already on fire)
        if (m_ConsecutiveScores == consecutiveScoresForOnFire && !m_IsOnFire)
        {
            m_IsOnFire = true;
            
#if NORMCORE
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(m_IsOnFire);
#endif
            
            OnFireStateChanged?.Invoke(true); // Notify VFX systems immediately
            Debug.Log($"[ScoreManager] 🔥 ON FIRE ACTIVATED! (Consecutive scores: {m_ConsecutiveScores})", this);
        }
        else if (m_ConsecutiveScores >= consecutiveScoresForOnFire && !m_IsOnFire)
        {
            // This should not happen, but log it if it does
            Debug.LogWarning($"[ScoreManager] ⚠️ Consecutive scores ({m_ConsecutiveScores}) >= threshold ({consecutiveScoresForOnFire}) but fire state not activated! Already on fire: {m_IsOnFire}", this);
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
        
        int scoreBeforeIncrement = m_Score;
        m_Score += pointsEarned;
        
        Debug.Log($"[ScoreManager] RegisterScore: Calculated pointsEarned={pointsEarned}, score: {scoreBeforeIncrement} + {pointsEarned} = {m_Score}", this);
        
#if NORMCORE
        // Sync score to model (only if owner)
        Debug.Log($"[ScoreManager] RegisterScore: About to call WriteScoreToModel with score={m_Score}, model.score currently: {model?.score ?? -999}", this);
        WriteScoreToModel(m_Score);
#else
        if (debugLogs)
            Debug.LogWarning("[ScoreManager] RegisterScore: NORMCORE is not defined! Score will not sync across clients!", this);
#endif
        
        // If money ball, gain a life
        if (isMoneyBall)
        {
            GainLife();
        }
        
        // Update score display (no flash message)
        UpdateScoreDisplay();
        
        // Update ScreenCanvas UI
        UpdateScreenCanvasUI(row, pointsEarned, isMoneyBall);
    }

    /// <summary>
    /// Called when a shot is missed (ball hits ground but didn't score).
    /// Resets the consecutive scores counter and deactivates fire state if active.
    /// Does not lose a life.
    /// This should be called whenever a ball hits the ground without scoring, regardless of whether it hit the rim.
    /// </summary>
    public void RegisterMiss()
    {
        if (m_IsGameOver)
        {
            if (debugLogs)
                Debug.Log("[ScoreManager] RegisterMiss ignored - game is over", this);
            return;
        }

        // Reset consecutive scores and On Fire state when a shot is missed
        bool wasOnFire = m_IsOnFire;
        int oldConsecutiveScores = m_ConsecutiveScores;
        
        if (m_ConsecutiveScores > 0 || m_IsOnFire)
        {
            m_ConsecutiveScores = 0;
            
            // Deactivate fire state if it was active
            if (m_IsOnFire)
            {
                m_IsOnFire = false;
                
#if NORMCORE
                // Sync on fire state to model if it changed (only if owner)
                WriteOnFireToModel(false);
#endif
                
                // Notify VFX systems immediately if On Fire was deactivated
                OnFireStateChanged?.Invoke(false);
                Debug.Log($"[ScoreManager] 🔥 ON FIRE DEACTIVATED (Shot Missed)! Consecutive scores reset: {oldConsecutiveScores} → 0", this);
            }
            else
            {
                Debug.Log($"[ScoreManager] Shot missed! Resetting consecutive scores: {oldConsecutiveScores} → 0", this);
            }
        }
        else if (debugLogs)
        {
            Debug.Log("[ScoreManager] RegisterMiss called but consecutive scores already at 0 and not on fire.", this);
        }
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
            
            // Reset consecutive scores and On Fire state when a life is lost
            bool wasOnFire = m_IsOnFire;
            int oldConsecutiveScores = m_ConsecutiveScores;
            m_ConsecutiveScores = 0;
            m_IsOnFire = false;
            Debug.Log($"[ScoreManager] Life lost! Resetting consecutive scores: {oldConsecutiveScores} → 0. Was on fire: {wasOnFire}", this);
            
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
            
            // Play lose life sound effect from SoundManager (3D spatial audio)
            AudioClip loseLifeSound = SoundManager.GetLoseLife();
            float loseLifeSoundVolume = SoundManager.GetLoseLifeVolume();
            if (loseLifeSound != null)
            {
                SoundManager.PlayClipAtPoint3D(loseLifeSound, transform.position, loseLifeSoundVolume);
                if (debugLogs)
                    Debug.Log($"[ScoreManager] Playing lose life sound at volume {loseLifeSoundVolume:F2} (3D spatial).", this);
            }
            else if (debugLogs)
            {
                Debug.LogWarning("[ScoreManager] Lose life sound not found in SoundManager! Make sure SoundManager exists in scene and has Lose Life audio clip assigned.", this);
            }
            
            // Show LifeChangedUI with AirBallText visible (life lost) and trigger animation (only for local client)
            ShowLifeChangedUI(showAirBallText: true);
            
            // Check for game over
            if (m_Lives <= 0)
            {
                SetGameOver();
            }
        }
    }
    
    /// <summary>
    /// Shows the LifeChangedUI for 3 seconds with optional AirBallText visibility.
    /// Only runs for the local client's play area.
    /// </summary>
    /// <param name="showAirBallText">If true, shows AirBallText (for life lost). If false, hides AirBallText (for life gained).</param>
    private void ShowLifeChangedUI(bool showAirBallText)
    {
#if NORMCORE
        // Only show LifeChangedUI for the local client's play area
        if (playAreaManager != null && !playAreaManager.IsOwnedByLocalClient())
        {
            if (debugLogs)
                Debug.Log("[ScoreManager] Skipping LifeChangedUI update (not local client's PlayArea).", this);
            return;
        }
#endif
        
        if (lifeChangedUIGameObject != null)
        {
            // Stop any existing coroutine
            if (m_LifeChangedUICoroutine != null)
            {
                StopCoroutine(m_LifeChangedUICoroutine);
            }
            
            // Show the GameObject
            lifeChangedUIGameObject.SetActive(true);
            
            // Show or hide AirBallText based on whether it's a life lost or gained
            if (airBallText != null)
            {
                airBallText.gameObject.SetActive(showAirBallText);
                if (debugLogs)
                    Debug.Log($"[ScoreManager] AirBallText visibility set to: {showAirBallText} (showAirBallText={showAirBallText})", this);
            }
            
            // Only trigger the entry animation when a life is lost (showAirBallText = true)
            if (showAirBallText && lifeHeartAnimator != null)
            {
                // Look for trigger parameters in the animator
                bool triggerFound = false;
                if (lifeHeartAnimator.parameters.Length > 0)
                {
                    // Look for a trigger parameter (prioritize common names)
                    string[] preferredTriggers = { "Entry", "Play", "Show", "Activate", "LostLife", "LifeLost" };
                    
                    // First, try preferred trigger names
                    foreach (string preferredName in preferredTriggers)
                    {
                        foreach (AnimatorControllerParameter param in lifeHeartAnimator.parameters)
                        {
                            if (param.type == AnimatorControllerParameterType.Trigger && param.name == preferredName)
                            {
                                lifeHeartAnimator.SetTrigger(preferredName);
                                triggerFound = true;
                                if (debugLogs)
                                    Debug.Log($"[ScoreManager] Triggered animation '{preferredName}' on LifeHeart Animator.", this);
                                break;
                            }
                        }
                        if (triggerFound) break;
                    }
                    
                    // If no preferred trigger found, use the first trigger parameter
                    if (!triggerFound)
                    {
                        foreach (AnimatorControllerParameter param in lifeHeartAnimator.parameters)
                        {
                            if (param.type == AnimatorControllerParameterType.Trigger)
                            {
                                lifeHeartAnimator.SetTrigger(param.name);
                                triggerFound = true;
                                if (debugLogs)
                                    Debug.Log($"[ScoreManager] Triggered animation '{param.name}' on LifeHeart Animator (first available trigger).", this);
                                break;
                            }
                        }
                    }
                }
                
                if (!triggerFound && debugLogs)
                {
                    Debug.LogWarning("[ScoreManager] No trigger parameters found in LifeHeart Animator! Please ensure the animator has a trigger parameter for the entry animation.", this);
                }
            }
            else if (showAirBallText && debugLogs)
            {
                Debug.LogWarning("[ScoreManager] LifeHeart Animator not assigned! Animation will not play.", this);
            }
            
            // Start coroutine to hide after 3 seconds
            m_LifeChangedUICoroutine = StartCoroutine(HideLifeChangedUIAfterDelay());
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[ScoreManager] LifeChangedUI GameObject not assigned!", this);
        }
    }
    
    /// <summary>
    /// Coroutine that hides the LifeChangedUI after 3 seconds.
    /// </summary>
    private IEnumerator HideLifeChangedUIAfterDelay()
    {
        yield return new WaitForSeconds(3.0f);
        if (lifeChangedUIGameObject != null)
        {
            lifeChangedUIGameObject.SetActive(false);
        }
        if (airBallText != null)
        {
            airBallText.gameObject.SetActive(false);
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
        
        // Play gain life sound effect from SoundManager
        AudioClip gainLifeSound = SoundManager.GetGainLife();
        float gainLifeSoundVolume = SoundManager.GetGainLifeVolume();
        if (gainLifeSound != null)
        {
            SoundManager.PlayClipAtPoint3D(gainLifeSound, transform.position, gainLifeSoundVolume);
            if (debugLogs)
                Debug.Log($"[ScoreManager] Playing gain life sound at volume {gainLifeSoundVolume:F2} (3D spatial).", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[ScoreManager] Gain life sound not found in SoundManager! Make sure SoundManager exists in scene and has Gain Life audio clip assigned.", this);
        }
        
        // Show LifeChangedUI with AirBallText hidden (life gained) (only for local client)
        ShowLifeChangedUI(showAirBallText: false);
        
        // Play GainLife animation on LifeHeart (only for local client)
        PlayGainLifeAnimation();
    }
    
    /// <summary>
    /// Plays the GainLife animation on the LifeHeart UI.
    /// Only runs for the local client's play area.
    /// </summary>
    private void PlayGainLifeAnimation()
    {
#if NORMCORE
        // Only play animation for the local client's play area
        if (playAreaManager != null && !playAreaManager.IsOwnedByLocalClient())
        {
            if (debugLogs)
                Debug.Log("[ScoreManager] Skipping GainLife animation (not local client's PlayArea).", this);
            return;
        }
#endif
        
        if (lifeHeartUI != null)
        {
            lifeHeartUI.PlayGainLifeAnimation();
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[ScoreManager] LifeHeartUI not assigned! GainLife animation will not play.", this);
        }
    }

    /// <summary>
    /// Updates the score display to show just the score (no flash message).
    /// </summary>
    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            // ALWAYS log when score display is updated - this is critical for debugging
            Debug.Log($"[ScoreManager] 🖥️ UpdateScoreDisplay: Setting UI text to '{m_Score}', IsOwner: {isOwnedLocallySelf}, ClientID: {realtime?.clientID}, model.score: {model?.score ?? -999}", this);
            scoreText.text = $"{m_Score}";
        }
    }
    

    /// <summary>
    /// Updates the lives display.
    /// </summary>
    private void UpdateLivesDisplay()
    {
        if (livesText != null)
        {
            livesText.text = $"{m_Lives}";
        }
    }

    /// <summary>
    /// Updates the game over display.
    /// Note: GameOver is now a GameObject, so we only control visibility, not text content.
    /// </summary>
    private void UpdateGameOverDisplay()
    {
        // GameOver is now just a GameObject, so we only control visibility
        // Text content (if any) is set in the prefab/Inspector
        // Visibility is controlled via SetActive() calls elsewhere in this script
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
        int oldConsecutiveScores = m_ConsecutiveScores;
        m_ConsecutiveScores = 0;
        Debug.Log($"[ScoreManager] Game Over! Resetting consecutive scores: {oldConsecutiveScores} → 0", this);
        
        // Update game state in this play area's PlayAreaManager
        if (playAreaManager != null)
        {
            playAreaManager.SetGameState(PlayAreaManager.GameState.GameOver);
        }
        
        // Update UI visibility - will be handled by UpdateUIVisibilityForState when game state changes
        // But we can call it directly here to ensure immediate update
        if (playAreaManager != null)
        {
            UpdateUIVisibilityForState(PlayAreaManager.GameState.GameOver);
        }
        
        // Play game over sound effect from SoundManager
        AudioClip gameOverSound = SoundManager.GetGameOver();
        float gameOverSoundVolume = SoundManager.GetGameOverVolume();
        if (gameOverSound != null)
        {
            SoundManager.PlayClipAtPoint3D(gameOverSound, transform.position, gameOverSoundVolume);
            if (debugLogs)
                Debug.Log($"[ScoreManager] Playing game over sound at volume {gameOverSoundVolume:F2} (3D spatial).", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[ScoreManager] Game over sound not found in SoundManager! Make sure SoundManager exists in scene and has Game Over audio clip assigned.", this);
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
        int oldConsecutiveScores = m_ConsecutiveScores;
        m_ConsecutiveScores = 0;
        Debug.Log($"[ScoreManager] New game started! Resetting consecutive scores: {oldConsecutiveScores} → 0", this);
        
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
        
        // Update UI visibility - will be handled by UpdateUIVisibilityForState when game state changes
        // But we can call it directly here to ensure immediate update
        if (playAreaManager != null)
        {
            UpdateUIVisibilityForState(PlayAreaManager.GameState.Playing);
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
        // BG is always visible
        if (bgGameObject != null)
        {
            bgGameObject.SetActive(true);
        }
        
        if (gameState == PlayAreaManager.GameState.Pregame)
        {
            // Pregame: Show Pregame, hide GameOver and Results
            if (pregameGameObject != null)
                pregameGameObject.SetActive(true);
            if (gameOverGameObject != null)
                gameOverGameObject.SetActive(false);
            if (resultsGameObject != null)
                resultsGameObject.SetActive(false);
            // SpawnInstruction is hidden when Results is hidden
        }
        else if (gameState == PlayAreaManager.GameState.Playing)
        {
            // Playing: Hide Pregame and GameOver, show Results and SpawnInstruction
            if (pregameGameObject != null)
                pregameGameObject.SetActive(false);
            if (gameOverGameObject != null)
                gameOverGameObject.SetActive(false);
            if (resultsGameObject != null)
                resultsGameObject.SetActive(true);
            if (spawnInstructionText != null)
                spawnInstructionText.SetActive(true);
        }
        else if (gameState == PlayAreaManager.GameState.GameOver)
        {
            // GameOver: Hide Pregame, show GameOver and Results, hide SpawnInstruction
            if (pregameGameObject != null)
                pregameGameObject.SetActive(false);
            if (gameOverGameObject != null)
                gameOverGameObject.SetActive(true);
            if (resultsGameObject != null)
                resultsGameObject.SetActive(true);
            if (spawnInstructionText != null)
                spawnInstructionText.SetActive(false);
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
    
    /// <summary>
    /// Updates the ScreenCanvas UI when a basket is scored.
    /// Only updates if this is the local client's play area.
    /// </summary>
    private void UpdateScreenCanvasUI(int row, int pointsEarned, bool isMoneyBall)
    {
        // Only update ScreenCanvas UI if this is the local client's play area
        // This prevents other players' scores from updating our local UI
#if NORMCORE
        if (playAreaManager != null && !playAreaManager.IsOwnedByLocalClient())
        {
            if (debugLogs)
                Debug.Log("[ScoreManager] ScreenCanvas UI update skipped - not local client's play area.", this);
            return;
        }
#else
        // In single-player mode, always update (no ownership check needed)
#endif
        
        // Determine shot type based on row (Y coordinate)
        string shotType = "";
        switch (row)
        {
            case 0:
                shotType = "LAYUP";
                break;
            case 1:
                shotType = "MID RANGE";
                break;
            case 2:
                shotType = "3 POINTER";
                break;
            default:
                shotType = "SHOT";
                break;
        }
        
        // Show score text
        if (screenScoreText != null)
        {
            screenScoreText.text = $"{shotType}! +{pointsEarned}";
            ShowScoreText();
        }
        
        // Handle money and fire text visibility and positioning
        bool showMoney = isMoneyBall;
        bool showFire = m_IsOnFire;
        
        if (showMoney)
        {
            ShowMoneyText();
        }
        
        if (showFire)
        {
            ShowFireText();
        }
        
        // Update positions based on visibility
        UpdateModifierTextPositions(showMoney, showFire);
    }
    
    /// <summary>
    /// Shows the score text for 3 seconds.
    /// </summary>
    private void ShowScoreText()
    {
        if (screenScoreText == null)
            return;
            
        // Stop any existing coroutine
        if (m_ScoreTextCoroutine != null)
        {
            StopCoroutine(m_ScoreTextCoroutine);
        }
        
        // Show the text
        screenScoreText.gameObject.SetActive(true);
        
        // Start coroutine to hide after 3 seconds
        m_ScoreTextCoroutine = StartCoroutine(HideScoreTextAfterDelay(3f));
    }
    
    /// <summary>
    /// Coroutine to hide score text after delay.
    /// </summary>
    private IEnumerator HideScoreTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (screenScoreText != null)
        {
            screenScoreText.gameObject.SetActive(false);
        }
        
        m_ScoreTextCoroutine = null;
    }
    
    /// <summary>
    /// Shows the money text for 3 seconds.
    /// </summary>
    private void ShowMoneyText()
    {
        if (moneyText == null)
            return;
            
        // Stop any existing coroutine
        if (m_MoneyTextCoroutine != null)
        {
            StopCoroutine(m_MoneyTextCoroutine);
        }
        
        // Show the text
        moneyText.gameObject.SetActive(true);
        
        // Start coroutine to hide after 3 seconds
        m_MoneyTextCoroutine = StartCoroutine(HideMoneyTextAfterDelay(3f));
    }
    
    /// <summary>
    /// Coroutine to hide money text after delay.
    /// </summary>
    private IEnumerator HideMoneyTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (moneyText != null)
        {
            moneyText.gameObject.SetActive(false);
        }
        
        m_MoneyTextCoroutine = null;
    }
    
    /// <summary>
    /// Shows the fire text for 3 seconds.
    /// </summary>
    private void ShowFireText()
    {
        if (fireText == null)
            return;
            
        // Stop any existing coroutine
        if (m_FireTextCoroutine != null)
        {
            StopCoroutine(m_FireTextCoroutine);
        }
        
        // Show the text
        fireText.gameObject.SetActive(true);
        
        // Start coroutine to hide after 3 seconds
        m_FireTextCoroutine = StartCoroutine(HideFireTextAfterDelay(3f));
    }
    
    /// <summary>
    /// Coroutine to hide fire text after delay.
    /// </summary>
    private IEnumerator HideFireTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (fireText != null)
        {
            fireText.gameObject.SetActive(false);
        }
        
        m_FireTextCoroutine = null;
    }
    
    /// <summary>
    /// Updates the positions of modifier texts based on visibility.
    /// </summary>
    private void UpdateModifierTextPositions(bool showMoney, bool showFire)
    {
        if (modifierPos1 == null || modifierPos2 == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ScoreManager] Modifier positions not assigned. Cannot position modifier texts.", this);
            return;
        }
        
        // If both are visible: MoneyText at Pos1, FireText at Pos2
        if (showMoney && showFire)
        {
            if (moneyText != null)
            {
                moneyText.transform.position = modifierPos1.position;
            }
            if (fireText != null)
            {
                fireText.transform.position = modifierPos2.position;
            }
        }
        // If only money is visible: MoneyText at Pos1
        else if (showMoney)
        {
            if (moneyText != null)
            {
                moneyText.transform.position = modifierPos1.position;
            }
        }
        // If only fire is visible: FireText at Pos1
        else if (showFire)
        {
            if (fireText != null)
            {
                fireText.transform.position = modifierPos1.position;
            }
        }
    }

}

