using UnityEngine;

/// <summary>
/// Plays a sound on the rim when the player enters the fire state.
/// Attach this to the rim GameObject (same GameObject that has RimCollisionTracker).
/// </summary>
public class RimFireSound : MonoBehaviour
{
    private AudioSource m_AudioSource;
    [SerializeField] private ScoreManager m_ScoreManager;
    private bool m_WasOnFire = false;

    private void Awake()
    {
        // Get or add AudioSource component for 3D spatial audio
        m_AudioSource = GetComponent<AudioSource>();
        m_AudioSource = gameObject.AddComponent<AudioSource>();
        m_AudioSource.playOnAwake = false;
        m_AudioSource.spatialBlend = 1.0f; // 3D sound (full spatial blend)
        m_AudioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Realistic distance falloff
        m_AudioSource.minDistance = 1f;
        m_AudioSource.maxDistance = 50f;
    }

    private void OnEnable()
    {
        m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
    }

    private void OnDisable()
    {
        m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
    }

    private void Start()
    {
        bool currentFireState = m_ScoreManager.IsOnFire();
        m_WasOnFire = currentFireState;
    }

    private void OnFireStateChangedHandler(bool isOnFire)
    {
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
        
        float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, fireVolume);
        m_AudioSource.PlayOneShot(fireSound, effectiveVolume);
    }
}

