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

    public void Initialize(ScoreManager scoreManager)
    {
        m_ScoreManager = scoreManager;
        m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
        InitializeFireSound();
    }


    private void OnDisable()
    {
        m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        
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
        
        m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler; // Remove first to avoid duplicates
        m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
        
        bool currentFireState = m_ScoreManager.IsOnFire();
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

