using UnityEngine;

/// <summary>
/// Plays a sound on the rim when the player enters the fire state.
/// Attach this to the rim GameObject (same GameObject that has RimCollisionTracker).
/// </summary>
public class RimFireSound : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Enable to see detailed logging for fire state changes.")]
    [SerializeField] private bool debugLogs = false;

    private AudioSource m_AudioSource;
    private ScoreManager m_ScoreManager;
    private bool m_WasOnFire = false;

    private void Awake()
    {
        // Get or add AudioSource component for 3D spatial audio
        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null)
        {
            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.playOnAwake = false;
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
                Debug.Log("[RimFireSound] Subscribed to OnFireStateChanged event.", this);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[RimFireSound] ScoreManager not found in OnEnable. Will retry in Start().", this);
        }
    }

    private void OnDisable()
    {
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        }
    }

    private void Start()
    {
        // Retry finding ScoreManager if not found in OnEnable
        if (m_ScoreManager == null)
        {
            FindScoreManager();
            if (m_ScoreManager != null)
            {
                m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
                bool currentFireState = m_ScoreManager.IsOnFire();
                m_WasOnFire = currentFireState;
                
                if (debugLogs)
                    Debug.Log($"[RimFireSound] Found ScoreManager in Start. Current fire state: {currentFireState}", this);
            }
        }
    }

    private void FindScoreManager()
    {
        // Try to find ScoreManager in parent hierarchy (PlayArea structure)
        m_ScoreManager = GetComponentInParent<ScoreManager>();
        
        if (m_ScoreManager == null)
        {
            // Try searching in PlayArea
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains("PlayArea"))
                {
                    m_ScoreManager = parent.GetComponentInChildren<ScoreManager>();
                    if (m_ScoreManager != null)
                        break;
                }
                parent = parent.parent;
            }
        }
        
        if (m_ScoreManager == null && debugLogs)
        {
            Debug.LogWarning("[RimFireSound] ScoreManager not found. Rim fire sound will not play.", this);
        }
    }

    private void OnFireStateChangedHandler(bool isOnFire)
    {
        if (debugLogs)
            Debug.Log($"[RimFireSound] OnFireStateChangedHandler called: isOnFire={isOnFire}, m_WasOnFire={m_WasOnFire}", this);
        
        // Only play sound when entering fire state (transitioning from false to true)
        if (isOnFire && !m_WasOnFire)
        {
            PlayFireActivateSound();
        }
        
        m_WasOnFire = isOnFire;
    }

    private void PlayFireActivateSound()
    {
        AudioClip fireSound = SoundManager.GetRimFireActivate();
        float fireVolume = SoundManager.GetRimFireActivateVolume();
        
        if (fireSound != null && m_AudioSource != null)
        {
            float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, fireVolume);
            m_AudioSource.PlayOneShot(fireSound, effectiveVolume);
            
            if (debugLogs)
                Debug.Log($"[RimFireSound] Playing rim fire activate sound at volume {fireVolume:F2}.", this);
        }
        else if (fireSound == null && debugLogs)
        {
            Debug.LogWarning("[RimFireSound] Rim fire activate sound not found in SoundManager! Make sure SoundManager exists in scene and has Rim Fire Activate audio clip assigned.", this);
        }
    }
}

