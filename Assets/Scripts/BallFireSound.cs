using UnityEngine;

/// <summary>
/// Plays a persistent looping sound on the ball while in fire state.
/// Attach this to the Basketball prefab (same GameObject that has OnFireVFXTrigger).
/// </summary>
public class BallFireSound : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Enable to see detailed logging for fire state changes.")]
    [SerializeField] private bool debugLogs = false;

    private AudioSource m_AudioSource;
    private ScoreManager m_ScoreManager;
    private bool m_WasOnFire = false;
    private bool m_Initialized = false;

    private void Awake()
    {
        // Get or add AudioSource component for 3D spatial audio
        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null)
        {
            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = true; // Loop the fire sound while on fire
            m_AudioSource.spatialBlend = 1.0f; // 3D sound (full spatial blend)
            m_AudioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Realistic distance falloff
            m_AudioSource.minDistance = 1f;
            m_AudioSource.maxDistance = 50f;
        }
    }

    private void OnEnable()
    {
        // Find ScoreManager through PlayArea hierarchy
        FindScoreManager();
        
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
            if (debugLogs)
                Debug.Log("[BallFireSound] Subscribed to OnFireStateChanged event.", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[BallFireSound] ScoreManager not found in OnEnable. Will retry in Start().", this);
        }
    }

    private void OnDisable()
    {
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        }
        
        // Stop sound when disabled
        if (m_AudioSource != null && m_AudioSource.isPlaying)
        {
            m_AudioSource.Stop();
        }
    }

    private void Start()
    {
        // Retry finding ScoreManager if not found in OnEnable
        if (m_ScoreManager == null)
        {
            FindScoreManager();
        }
        
        // Initialize sound state
        InitializeFireSound();
    }

    /// <summary>
    /// Public method to initialize the fire sound state. Can be called after ball is spawned.
    /// </summary>
    public void InitializeFireSound()
    {
        if (m_Initialized)
            return;
        
        if (m_ScoreManager == null)
        {
            FindScoreManager();
        }
        
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler; // Remove first to avoid duplicates
            m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
            
            bool currentFireState = m_ScoreManager.IsOnFire();
            m_WasOnFire = currentFireState;
            
            // Update sound state based on current fire state
            UpdateFireSoundState(currentFireState);
            
            m_Initialized = true;
            
            if (debugLogs)
                Debug.Log($"[BallFireSound] InitializeFireSound complete. Current fire state: {currentFireState}, Sound playing: {m_AudioSource.isPlaying}", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[BallFireSound] ScoreManager not found in InitializeFireSound. Fire sound will remain off.", this);
        }
    }

    private void FindScoreManager()
    {
        // Try to find ScoreManager through PlayArea hierarchy
        // First, try to find PlayAreaManager
        PlayAreaManager playAreaManager = GetComponentInParent<PlayAreaManager>();
        if (playAreaManager == null)
        {
            // Search up the hierarchy
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains("PlayArea"))
                {
                    playAreaManager = parent.GetComponent<PlayAreaManager>();
                    if (playAreaManager != null)
                        break;
                }
                parent = parent.parent;
            }
        }
        
        // Then find ScoreManager in the PlayArea
        if (playAreaManager != null)
        {
            m_ScoreManager = playAreaManager.GetComponentInChildren<ScoreManager>();
        }
        
        // Fallback: search scene for ScoreManager
        if (m_ScoreManager == null)
        {
            ScoreManager[] allScoreManagers = FindObjectsByType<ScoreManager>(FindObjectsSortMode.None);
            if (allScoreManagers.Length > 0)
            {
                // Prefer one in Playing state, or just use the first one
                foreach (var sm in allScoreManagers)
                {
                    PlayAreaManager pam = sm.GetComponentInParent<PlayAreaManager>();
                    if (pam != null && pam.GetGameState() == PlayAreaManager.GameState.Playing)
                    {
                        m_ScoreManager = sm;
                        break;
                    }
                }
                
                if (m_ScoreManager == null)
                {
                    m_ScoreManager = allScoreManagers[0];
                }
            }
        }
        
        if (m_ScoreManager == null && debugLogs)
        {
            Debug.LogWarning("[BallFireSound] ScoreManager not found. Ball fire sound will not play.", this);
        }
    }

    private void OnFireStateChangedHandler(bool isOnFire)
    {
        if (debugLogs)
            Debug.Log($"[BallFireSound] OnFireStateChangedHandler called: isOnFire={isOnFire}, m_WasOnFire={m_WasOnFire}", this);
        
        UpdateFireSoundState(isOnFire);
        m_WasOnFire = isOnFire;
    }

    private void UpdateFireSoundState(bool isOnFire)
    {
        if (m_AudioSource == null)
        {
            if (debugLogs)
                Debug.LogWarning("[BallFireSound] Cannot update fire sound state - AudioSource is null.", this);
            return;
        }
        
        AudioClip fireLoopSound = SoundManager.GetBallFireLoop();
        float fireVolume = SoundManager.GetBallFireLoopVolume();
        
        if (isOnFire)
        {
            // Start playing the fire loop sound
            if (fireLoopSound != null)
            {
                if (!m_AudioSource.isPlaying || m_AudioSource.clip != fireLoopSound)
                {
                    m_AudioSource.clip = fireLoopSound;
                    m_AudioSource.volume = fireVolume;
                    m_AudioSource.Play();
                    
                    if (debugLogs)
                        Debug.Log($"[BallFireSound] 🔥 Started playing ball fire loop sound at volume {fireVolume:F2}.", this);
                }
                else
                {
                    // Already playing the correct sound, just update volume
                    m_AudioSource.volume = fireVolume;
                }
            }
            else if (debugLogs)
            {
                Debug.LogWarning("[BallFireSound] Ball fire loop sound not found in SoundManager! Make sure SoundManager exists in scene and has Ball Fire Loop audio clip assigned.", this);
            }
        }
        else
        {
            // Stop playing the fire loop sound
            if (m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
                
                if (debugLogs)
                    Debug.Log("[BallFireSound] ❄️ Stopped ball fire loop sound (fire state deactivated).", this);
            }
        }
    }
}

