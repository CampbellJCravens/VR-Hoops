using UnityEngine;

/// <summary>
/// Component that triggers VFX when the player enters On Fire state.
/// Add this to any GameObject that has VFX (ParticleSystem) that should play during On Fire.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class OnFireVFXTrigger : MonoBehaviour
{
    [Header("VFX Settings")]
    [Tooltip("If true, this VFX is a one-shot effect that plays once when On Fire activates. If false, the VFX will persist while On Fire is active.")]
    [SerializeField] private bool isOneShot = false;
    
    [Tooltip("If not one-shot: whether the VFX will loop while On Fire is active. If false, it will play once when On Fire activates.")]
    [SerializeField] private bool loopWhileOnFire = true;
    
    [Tooltip("If not one-shot: whether the VFX will stop immediately when On Fire deactivates. If false, it will continue until it naturally ends.")]
    [SerializeField] private bool stopWhenOffFire = true;

    private ParticleSystem m_ParticleSystem;
    private bool m_WasOnFire = false;
    private ScoreManager m_ScoreManager; // Cache the ScoreManager reference

    private void Awake()
    {
        m_ParticleSystem = GetComponent<ParticleSystem>();
        if (m_ParticleSystem == null)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] No ParticleSystem found on {gameObject.name}.", this);
        }
        else
        {
            // Ensure ParticleSystem is stopped by default
            m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnEnable()
    {
        // Find ScoreManager through PlayArea hierarchy
        m_ScoreManager = ScoreManager.FindScoreManagerFor(gameObject);
        
        // Subscribe to On Fire state changes for immediate updates
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
            m_ScoreManager = null;
        }
    }

    private void Start()
    {
        // Find ScoreManager if not already found (in case it wasn't available in OnEnable)
        if (m_ScoreManager == null)
        {
            m_ScoreManager = ScoreManager.FindScoreManagerFor(gameObject);
        }

        if (m_ScoreManager != null)
        {
            // Subscribe if not already subscribed
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler; // Remove first to avoid duplicates
            m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
            
            m_WasOnFire = m_ScoreManager.IsOnFire();
            UpdateVFXState(m_WasOnFire);
        }
        else
        {
            // If ScoreManager doesn't exist yet, ensure VFX is off
            if (m_ParticleSystem != null)
            {
                m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    /// <summary>
    /// Event handler for On Fire state changes. Called immediately when state changes.
    /// </summary>
    private void OnFireStateChangedHandler(bool isOnFire)
    {
        UpdateVFXState(isOnFire);
        m_WasOnFire = isOnFire;
    }

    private void Update()
    {
        // Find ScoreManager if not cached
        if (m_ScoreManager == null)
        {
            m_ScoreManager = ScoreManager.FindScoreManagerFor(gameObject);
            if (m_ScoreManager != null)
            {
                // Subscribe now that we found it
                m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
                m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
            }
        }

        // Fallback: Check if On Fire state has changed (in case event wasn't fired)
        // This ensures we catch any state changes even if event system fails
        if (m_ScoreManager != null)
        {
            bool isOnFire = m_ScoreManager.IsOnFire();
            
            if (isOnFire != m_WasOnFire)
            {
                UpdateVFXState(isOnFire);
                m_WasOnFire = isOnFire;
            }
        }
    }

    /// <summary>
    /// Updates the VFX state based on On Fire status.
    /// </summary>
    private void UpdateVFXState(bool isOnFire)
    {
        if (m_ParticleSystem == null)
            return;

        if (isOnFire)
        {
            // On Fire activated - start VFX
            var main = m_ParticleSystem.main;
            
            if (isOneShot)
            {
                // One-shot VFX: play once, no looping
                main.loop = false;
                m_ParticleSystem.Play();
            }
            else
            {
                // Persistent VFX: configure looping and play
                main.loop = loopWhileOnFire;
                
                if (!m_ParticleSystem.isPlaying)
                {
                    m_ParticleSystem.Play();
                }
            }
        }
        else
        {
            // On Fire deactivated - stop VFX if configured (only for persistent VFX)
            if (!isOneShot && stopWhenOffFire)
            {
                m_ParticleSystem.Stop();
            }
        }
    }

    /// <summary>
    /// Immediately stops the VFX. Called when the ball hits the ground or On Fire deactivates.
    /// </summary>
    public void StopVFX()
    {
        if (m_ParticleSystem != null && m_ParticleSystem.isPlaying)
        {
            m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}

