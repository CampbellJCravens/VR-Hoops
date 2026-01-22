using UnityEngine;

/// <summary>
/// Plays a persistent looping sound on the ball while in fire state.
/// Attach this to the Basketball prefab (same GameObject that has OnFireVFXTrigger).
/// </summary>
public class BallFireSound : MonoBehaviour
{

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

    /// <summary>
    /// Gets the ScoreManager via lazy getter. Finds and caches it when first accessed.
    /// Works on all clients once the ball is parented (owner sets parent immediately, non-owners get it via network sync).
    /// </summary>
    private ScoreManager GetScoreManager()
    {
        // If already cached, return it
        if (m_ScoreManager != null)
        {
            return m_ScoreManager;
        }

        // Try to find it via parent hierarchy
        PlayAreaManager playAreaManager = GetComponentInParent<PlayAreaManager>();
        if (playAreaManager != null)
        {
            m_ScoreManager = playAreaManager.GetScoreManager();
        }

        return m_ScoreManager;
    }


    private void OnDisable()
    {
        // Unsubscribe from events if we have a reference
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        }
        
        // Stop sound when disabled
        if (m_AudioSource.isPlaying)
        {
            m_AudioSource.Stop();
        }
    }

    /// <summary>
    /// Public method to initialize the fire sound state. Can be called after ball is spawned.
    /// </summary>
    public void InitializeFireSound()
    {
        if (m_Initialized)
            return;
        
        // Get ScoreManager via lazy getter (will find it now that parent is set)
        ScoreManager scoreManager = GetScoreManager();
        if (scoreManager == null)
        {
            // Parent not set yet, can't initialize - will be called again later
            return;
        }

        // Unsubscribe first to avoid duplicates
        scoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        scoreManager.OnFireStateChanged += OnFireStateChangedHandler;
        
        bool currentFireState = scoreManager.IsOnFire();
        m_WasOnFire = currentFireState;
        
        // Update sound state based on current fire state
        UpdateFireSoundState(currentFireState);
        
        m_Initialized = true;
    }


    private void OnFireStateChangedHandler(bool isOnFire)
    {
        UpdateFireSoundState(isOnFire);
        m_WasOnFire = isOnFire;
    }

    private void UpdateFireSoundState(bool isOnFire)
    {
        AudioClip fireLoopSound = SoundManager.GetBallFireLoop();
        float fireVolume = SoundManager.GetBallFireLoopVolume();
        
        if (isOnFire)
        {
            // Start playing the fire loop sound
            if (!m_AudioSource.isPlaying || m_AudioSource.clip != fireLoopSound)
            {
                m_AudioSource.clip = fireLoopSound;
                float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, fireVolume);
                m_AudioSource.volume = effectiveVolume;
                m_AudioSource.Play();
            }
            else
            {
                // Already playing the correct sound, just update volume (accounting for mute settings)
                float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, fireVolume);
                m_AudioSource.volume = effectiveVolume;
            }
        }
        else
        {
            // Stop playing the fire loop sound
            if (m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
            }
        }
    }
}

