using UnityEngine;
using TMPro;
using System.Collections;
using Normal.Realtime;

/// <summary>
/// Manages score and lives for a single play area in the basketball game.
/// Each PlayArea should have its own ScoreManager instance.
/// </summary>
[RequireComponent(typeof(RealtimeView))]
public class ScoreManager : RealtimeComponent<RealtimeScoreModel>
{
    [Header("PlayArea Reference")]
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
        screenScoreText.gameObject.SetActive(false);
        moneyText.gameObject.SetActive(false);
        fireText.gameObject.SetActive(false);
        lifeChangedUIGameObject.SetActive(false);
        airBallText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when the RealtimeModel is replaced. Handles initial state sync for late joiners.
    /// </summary>
    protected override void OnRealtimeModelReplaced(RealtimeScoreModel previousModel, RealtimeScoreModel currentModel)
    {
        // Unsubscribe from previous model events (if it exists)
        if (previousModel != null)
        {
            previousModel.scoreDidChange -= ScoreDidChange;
            previousModel.livesDidChange -= LivesDidChange;
            previousModel.isOnFireDidChange -= IsOnFireDidChange;
            previousModel.isGameOverDidChange -= IsGameOverDidChange;
        }
        
        // Subscribe to model change events
        currentModel.scoreDidChange += ScoreDidChange;
        currentModel.livesDidChange += LivesDidChange;
        currentModel.isOnFireDidChange += IsOnFireDidChange;
        currentModel.isGameOverDidChange += IsGameOverDidChange;
        
        // Apply initial state from model (important for late joiners)
        SyncFromModel();
        
        // Subscribe to ownership changes (unsubscribe first to prevent duplicates)
        realtimeView.ownerIDSelfDidChange -= OnOwnershipChanged; // Unsubscribe first to prevent duplicates
        realtimeView.ownerIDSelfDidChange += OnOwnershipChanged;
    }
    
    /// <summary>
    /// Called when ownership of the RealtimeView changes. Syncs from model when gaining ownership.
    /// The model is always the source of truth - we sync FROM it, not TO it, to avoid overwriting correct values.
    /// </summary>
    private void OnOwnershipChanged(RealtimeView view, int ownerID)
    {
        if (view.isOwnedLocallySelf)
        {
            // When gaining ownership, always sync FROM model (model is source of truth)
            // This ensures we have the correct state and don't overwrite model values with stale local state
            SyncFromModel();
        }
    }
    
    private void ScoreDidChange(RealtimeScoreModel model, int score)
    {
        SyncFromModel();
    }
    
    private void LivesDidChange(RealtimeScoreModel model, int lives)
    {
        SyncFromModel();
    }
    
    private void IsOnFireDidChange(RealtimeScoreModel model, bool isOnFire)
    {
        SyncFromModel();
    }
    
    private void IsGameOverDidChange(RealtimeScoreModel model, bool isGameOver)
    {
        SyncFromModel();
    }
    
    
    /// <summary>
    /// Syncs local state from the RealtimeModel. Called when model changes (for non-owners or initial sync).
    /// </summary>
    private void SyncFromModel()
    {
        bool onFireChanged = (m_IsOnFire != model.isOnFire);
        
        // Update local state from model
        m_Score = model.score;
        m_Lives = model.lives;
        m_IsOnFire = model.isOnFire;
        m_IsGameOver = model.isGameOver;
        
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
        }
        else
        {
            PlayAreaManager.GameState currentState = playAreaManager.GetGameState();
            
            // Show Playing UI if we have actual game data:
            // - Score > 0 (obviously playing)
            // - Lives > 0 and <= startingLives (game has started - lives=startingLives means game started, just no lives lost yet)
            // Don't treat score=0, lives=0 as game data - that's just uninitialized state
            bool hasGameData = (m_Score > 0 || (m_Lives > 0 && m_Lives <= startingLives));
            
            // If we have game data but state is still Pregame, the game state might not have synced yet.
            // In this case, we should show Playing UI (since we have score/lives data)
            if (currentState == PlayAreaManager.GameState.Pregame && hasGameData)
            {
                // Assume we're in Playing state if we have game data
                UpdateUIVisibilityForState(PlayAreaManager.GameState.Playing);
            }
            else
            {
                UpdateUIVisibilityForState(currentState);
            }
        }
    }
    
    /// <summary>
    /// Sets ownership of this ScoreManager's RealtimeView if the PlayArea is owned by the local client.
    /// Called when the game starts to ensure the owner can write to the model.
    /// Uses RequestOwnership() to handle cases where another client (like the host) owns the RealtimeView.
    /// </summary>
    private void EnsureOwnership()
    {
        // Check if the PlayArea is owned by the local client
        bool playAreaOwned = playAreaManager.IsOwnedByLocalClient();
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
    /// Writes score to the model. Only the owner should call this.
    /// </summary>
    private void WriteScoreToModel(int score)
    {
        // Ensure we have ownership before writing
        if (!realtimeView.isOwnedLocallySelf)
        {
            EnsureOwnership();
            // Check again - SetOwnership() might be synchronous
            if (!realtimeView.isOwnedLocallySelf)
            {
                return; // Will write when ownership is confirmed via OnOwnershipChanged
            }
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            model.score = score;
        }
    }
    
    /// <summary>
    /// Writes lives to the model. Only the owner should call this.
    /// </summary>
    private void WriteLivesToModel(int lives)
    {
        // Ensure we have ownership before writing
        if (!realtimeView.isOwnedLocallySelf)
        {
            EnsureOwnership();
            // Check again - SetOwnership() might be synchronous
            if (!realtimeView.isOwnedLocallySelf)
            {
                return; // Will write when ownership is confirmed via OnOwnershipChanged
            }
        }
        
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            model.lives = lives;
        }
    }
    
    /// <summary>
    /// Writes on fire state to the model. Only the owner should call this.
    /// </summary>
    private void WriteOnFireToModel(bool isOnFire)
    {
        if (realtimeView.isOwnedLocallySelf)
        {
            model.isOnFire = isOnFire;
        }
    }
    
    
    /// <summary>
    /// Writes game over state to the model. Only the owner should call this.
    /// </summary>
    private void WriteGameOverToModel(bool isGameOver)
    {
        // Use realtimeView.isOwnedLocallySelf consistently
        if (realtimeView.isOwnedLocallySelf)
        {
            model.isGameOver = isGameOver;
        }
    }

    /// <summary>
    /// Called when the component is enabled. Ensures we're subscribed to game state changes
    /// early, before model syncs happen.
    /// </summary>
    private void OnEnable()
    {
        // Subscribe to game state changes early (in OnEnable, before Start)
        // This ensures we catch state changes even if PlayAreaManager syncs before ScoreManager.Start()
        playAreaManager.GameStateChanged += OnGameStateChanged;
    }

    private void Start()
    {
        // Subscribe to game state changes from PlayAreaManager (if not already subscribed in OnEnable)
        // Ensure we sync from model on Start if it's already available (for late joiners)
        SyncFromModel();
        
        // Already subscribed in OnEnable, just update UI
        UpdateUIVisibilityForState(playAreaManager.GetGameState());
    }

    private void OnDestroy()
    {
        // Unsubscribe from game state changes
        playAreaManager.GameStateChanged -= OnGameStateChanged;
        
        // Unsubscribe from ownership changes
        realtimeView.ownerIDSelfDidChange -= OnOwnershipChanged;
    }

    /// <summary>
    /// Called when the component is disabled. Clean up subscriptions.
    /// </summary>
    private void OnDisable()
    {
        // Unsubscribe to prevent duplicate subscriptions
        playAreaManager.GameStateChanged -= OnGameStateChanged;
    }

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
        PlayAreaManager.GameState currentState = playAreaManager.GetGameState();
        
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
    /// Requires proper assignment - no auto-finding.
    /// </summary>
    public static ScoreManager FindScoreManagerFor(GameObject obj)
    {
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
        // Don't register scores if game is over
        if (m_IsGameOver)
        {
            return;
        }

        // Increment consecutive scores
        m_ConsecutiveScores++;
        
        // Check if we should activate On Fire state
        // Only activate when we EXACTLY reach the threshold (not already on fire)
        if (m_ConsecutiveScores == consecutiveScoresForOnFire && !m_IsOnFire)
        {
            m_IsOnFire = true;
            
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(m_IsOnFire);
            
            OnFireStateChanged?.Invoke(true); // Notify VFX systems immediately
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
        
        // Sync score to model (only if owner)
        WriteScoreToModel(m_Score);
        
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
            return;
        }

        // Reset consecutive scores and On Fire state when a shot is missed
        bool wasOnFire = m_IsOnFire;
        
        if (m_ConsecutiveScores > 0 || m_IsOnFire)
        {
            m_ConsecutiveScores = 0;
            
            // Deactivate fire state if it was active
            if (m_IsOnFire)
            {
                m_IsOnFire = false;
                
                // Sync on fire state to model if it changed (only if owner)
                WriteOnFireToModel(false);
                
                // Notify VFX systems immediately if On Fire was deactivated
                OnFireStateChanged?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Called when a life should be lost.
    /// </summary>
    public void LoseLife()
    {
        if (m_Lives > 0 && !m_IsGameOver)
        {
            m_Lives--;
            UpdateLivesDisplay();
            
            // Sync lives to model (only if owner)
            WriteLivesToModel(m_Lives);
            
            // Reset consecutive scores and On Fire state when a life is lost
            bool wasOnFire = m_IsOnFire;
            m_ConsecutiveScores = 0;
            m_IsOnFire = false;
            
            // Sync on fire state to model if it changed (only if owner)
            if (wasOnFire)
            {
                WriteOnFireToModel(false);
            }
            
            // Notify VFX systems immediately if On Fire was deactivated
            if (wasOnFire)
            {
                OnFireStateChanged?.Invoke(false);
            }
            
            // Play lose life sound effect from SoundManager (3D spatial audio)
            AudioClip loseLifeSound = SoundManager.GetLoseLife();
            float loseLifeSoundVolume = SoundManager.GetLoseLifeVolume();
            SoundManager.PlayClipAtPoint3D(loseLifeSound, transform.position, loseLifeSoundVolume);
            
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
        // Only show LifeChangedUI for the local client's play area
        if (!playAreaManager.IsOwnedByLocalClient())
        {
            return;
        }
        
        // Stop any existing coroutine
        if (m_LifeChangedUICoroutine != null)
        {
            StopCoroutine(m_LifeChangedUICoroutine);
        }
        
        // Show the GameObject
        lifeChangedUIGameObject.SetActive(true);
        
        // Show or hide AirBallText based on whether it's a life lost or gained
        airBallText.gameObject.SetActive(showAirBallText);
        
        // Only trigger the entry animation when a life is lost (showAirBallText = true)
        if (showAirBallText)
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
                            break;
                        }
                    }
                }
            }
        }
        
        // Start coroutine to hide after 3 seconds
        m_LifeChangedUICoroutine = StartCoroutine(HideLifeChangedUIAfterDelay());
    }
    
    /// <summary>
    /// Coroutine that hides the LifeChangedUI after 3 seconds.
    /// </summary>
    private IEnumerator HideLifeChangedUIAfterDelay()
    {
        yield return new WaitForSeconds(3.0f);
        lifeChangedUIGameObject.SetActive(false);
        airBallText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when a life should be gained (e.g., from money ball).
    /// </summary>
    public void GainLife()
    {
        m_Lives++;
        UpdateLivesDisplay();
        
        // Sync lives to model (only if owner)
        WriteLivesToModel(m_Lives);
        
        // Play gain life sound effect from SoundManager
        AudioClip gainLifeSound = SoundManager.GetGainLife();
        float gainLifeSoundVolume = SoundManager.GetGainLifeVolume();
        SoundManager.PlayClipAtPoint3D(gainLifeSound, transform.position, gainLifeSoundVolume);
        
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
        // Only play animation for the local client's play area
        if (!playAreaManager.IsOwnedByLocalClient())
        {
            return;
        }
        
        lifeHeartUI.PlayGainLifeAnimation();
    }

    /// <summary>
    /// Updates the score display to show just the score (no flash message).
    /// </summary>
    private void UpdateScoreDisplay()
    {
        scoreText.text = $"{m_Score}";
    }
    

    /// <summary>
    /// Updates the lives display.
    /// </summary>
    private void UpdateLivesDisplay()
    {
        livesText.text = $"{m_Lives}";
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
        
        // Sync game over state to model (only if owner)
        WriteGameOverToModel(true);
        
        // Deactivate On Fire if it was active
        if (m_IsOnFire)
        {
            m_IsOnFire = false;
            
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(false);
            
            OnFireStateChanged?.Invoke(false);
        }
        m_ConsecutiveScores = 0;
        
        // Update game state in this play area's PlayAreaManager
        playAreaManager.SetGameState(PlayAreaManager.GameState.GameOver);
        
        // Update UI visibility - will be handled by UpdateUIVisibilityForState when game state changes
        // But we can call it directly here to ensure immediate update
        UpdateUIVisibilityForState(PlayAreaManager.GameState.GameOver);
        
        // Play game over sound effect from SoundManager
        AudioClip gameOverSound = SoundManager.GetGameOver();
        float gameOverSoundVolume = SoundManager.GetGameOverVolume();
        SoundManager.PlayClipAtPoint3D(gameOverSound, transform.position, gameOverSoundVolume);
        
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
        m_ConsecutiveScores = 0;
        
        // Ensure we have ownership if the PlayArea is owned by this client
        EnsureOwnership();
        
        // Sync reset state to model (only if owner)
        WriteScoreToModel(0);
        WriteLivesToModel(startingLives);
        WriteGameOverToModel(false);
        
        // Deactivate On Fire if it was active
        if (m_IsOnFire)
        {
            m_IsOnFire = false;
            
            // Sync on fire state to model (only if owner)
            WriteOnFireToModel(false);
            
            OnFireStateChanged?.Invoke(false);
        }
        
        // Reset this play area's game state and components
        // Set the play area state back to Playing
        playAreaManager.SetGameState(PlayAreaManager.GameState.Playing);
        
        // Note: Ball spawn and shot counters are not reset - they increment indefinitely
        
        // Reset hoop position to starting position for this play area
        // Note: HoopPositionsManager should be accessed through PlayAreaManager reference
        // This requires proper assignment in the Inspector
        
        // Reset shot counter for this play area
        // Note: ShotCounterManager should be accessed through proper references
        // This requires proper assignment in the Inspector
        
        // Update UI visibility - will be handled by UpdateUIVisibilityForState when game state changes
        // But we can call it directly here to ensure immediate update
        UpdateUIVisibilityForState(PlayAreaManager.GameState.Playing);
        
        // Update displays
        UpdateScoreDisplay();
        UpdateLivesDisplay();
    }
    
    /// <summary>
    /// Updates UI visibility based on game state. Called when syncing from model or when game state changes.
    /// </summary>
    public void UpdateUIVisibilityForState(PlayAreaManager.GameState gameState)
    {
        // BG is always visible
        bgGameObject.SetActive(true);
        
        if (gameState == PlayAreaManager.GameState.Pregame)
        {
            // Pregame: Show Pregame, hide GameOver and Results
            pregameGameObject.SetActive(true);
            gameOverGameObject.SetActive(false);
            resultsGameObject.SetActive(false);
            // SpawnInstruction is hidden when Results is hidden
        }
        else if (gameState == PlayAreaManager.GameState.Playing)
        {
            // Playing: Hide Pregame and GameOver, show Results and SpawnInstruction
            pregameGameObject.SetActive(false);
            gameOverGameObject.SetActive(false);
            resultsGameObject.SetActive(true);
            spawnInstructionText.SetActive(true);
        }
        else if (gameState == PlayAreaManager.GameState.GameOver)
        {
            // GameOver: Hide Pregame, show GameOver and Results, hide SpawnInstruction
            pregameGameObject.SetActive(false);
            gameOverGameObject.SetActive(true);
            resultsGameObject.SetActive(true);
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
        if (!playAreaManager.IsOwnedByLocalClient())
        {
            return;
        }
        
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
        screenScoreText.text = $"{shotType}! +{pointsEarned}";
        ShowScoreText();
        
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
        
        screenScoreText.gameObject.SetActive(false);
        
        m_ScoreTextCoroutine = null;
    }
    
    /// <summary>
    /// Shows the money text for 3 seconds.
    /// </summary>
    private void ShowMoneyText()
    {
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
        
        moneyText.gameObject.SetActive(false);
        
        m_MoneyTextCoroutine = null;
    }
    
    /// <summary>
    /// Shows the fire text for 3 seconds.
    /// </summary>
    private void ShowFireText()
    {
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
        
        fireText.gameObject.SetActive(false);
        
        m_FireTextCoroutine = null;
    }
    
    /// <summary>
    /// Updates the positions of modifier texts based on visibility.
    /// </summary>
    private void UpdateModifierTextPositions(bool showMoney, bool showFire)
    {
        
        // If both are visible: MoneyText at Pos1, FireText at Pos2
        if (showMoney && showFire)
        {
            moneyText.transform.position = modifierPos1.position;
            fireText.transform.position = modifierPos2.position;
        }
        // If only money is visible: MoneyText at Pos1
        else if (showMoney)
        {
            moneyText.transform.position = modifierPos1.position;
        }
        // If only fire is visible: FireText at Pos1
        else if (showFire)
        {
            fireText.transform.position = modifierPos1.position;
        }
    }

}

