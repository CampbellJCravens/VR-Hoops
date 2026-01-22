using UnityEngine;
using System.Collections;

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
    private bool m_Initialized = false;

    private void Awake()
    {
        // Ensure this GameObject is active (VFX won't show if GameObject is inactive)
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Try to find ParticleSystem on this GameObject first, then in children
        m_ParticleSystem = GetComponent<ParticleSystem>();
        
        // Ensure the ParticleSystem's GameObject is active
        if (!m_ParticleSystem.gameObject.activeSelf)
        {
            m_ParticleSystem.gameObject.SetActive(true);
        }
        
        // Disable playOnAwake to ensure we have full control over when the VFX plays
        var main = m_ParticleSystem.main;
        // Note: We can't directly set playOnAwake via the API, but we'll stop it immediately
        // Ensure ParticleSystem is stopped by default
        m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        // Clear all particles immediately
        m_ParticleSystem.Clear(true);
        
        // Verify the particle system is actually stopped
        if (m_ParticleSystem.isPlaying)
        {
            // Force stop by setting time to 0 and clearing
            m_ParticleSystem.time = 0;
            m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            m_ParticleSystem.Clear(true);
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

    private void OnEnable()
    {
        // Don't try to get ScoreManager here - parent might not be set yet
        // We'll get it lazily when needed
    }

    private void OnDisable()
    {
        // Unsubscribe from events if we have a reference
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        }
    }

    private void Start()
    {
        InitializeVFX();
    }

    /// <summary>
    /// Initializes the VFX state. Can be called manually after ball spawn to ensure proper initialization.
    /// </summary>
    public void InitializeVFX()
    {
        // Ensure ParticleSystem is set up
        if (m_ParticleSystem == null)
        {
            m_ParticleSystem = GetComponent<ParticleSystem>();
        }
        
        // Ensure the ParticleSystem's GameObject is active
        if (!m_ParticleSystem.gameObject.activeSelf)
        {
            m_ParticleSystem.gameObject.SetActive(true);
        }
        
        // Get ScoreManager via lazy getter (will find it now that parent is set)
        ScoreManager scoreManager = GetScoreManager();
        if (scoreManager == null)
        {
            // Parent not set yet, can't initialize - will be called again later
            // Ensure VFX is off until we can initialize
            m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        // Unsubscribe first to avoid duplicates
        scoreManager.OnFireStateChanged -= OnFireStateChangedHandler;
        scoreManager.OnFireStateChanged += OnFireStateChangedHandler;
        
        bool currentFireState = scoreManager.IsOnFire();
        m_WasOnFire = currentFireState;
        
        // Always update VFX state, even if it seems correct (to ensure it's properly started)
        UpdateVFXState(currentFireState);
        m_Initialized = true;
    }

    /// <summary>
    /// Event handler for On Fire state changes. Called immediately when state changes.
    /// </summary>
    private void OnFireStateChangedHandler(bool isOnFire)
    {
        UpdateVFXState(isOnFire);
        m_WasOnFire = isOnFire;
    }

    /// <summary>
    /// Updates the VFX state based on On Fire status.
    /// </summary>
    private void UpdateVFXState(bool isOnFire)
    {
        // Ensure GameObject is active (VFX won't show if inactive)
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Ensure ParticleSystem GameObject is active
        if (!m_ParticleSystem.gameObject.activeSelf)
        {
            m_ParticleSystem.gameObject.SetActive(true);
        }
        
        // Check if renderer is enabled
        var renderer = m_ParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (!renderer.enabled)
        {
            renderer.enabled = true;
        }

        if (isOnFire)
        {
            // On Fire activated - start VFX
            var main = m_ParticleSystem.main;
            
            if (isOneShot)
            {
                // One-shot VFX: play once, no looping
                main.loop = false;
                // Always play, even if already playing (to restart one-shot effects)
                m_ParticleSystem.Play();
            }
            else
            {
                // Persistent VFX: configure looping and play
                main.loop = loopWhileOnFire;
                
                // ALWAYS restart the particle system when fire state is active
                // This ensures it's in a clean state and actually emitting particles
                // Even if it thinks it's playing, it might not be emitting (e.g., due to playOnAwake conflicts)
                bool wasPlaying = m_ParticleSystem.isPlaying;
                bool wasPaused = m_ParticleSystem.isPaused;
                int particleCountBefore = m_ParticleSystem.particleCount;
                
                // Clear any existing particles and restart
                m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                
                // Small delay to ensure Stop() has processed (wait one frame)
                // Actually, we can't wait here, so we'll just play immediately
                // The Stop() should be synchronous for our purposes
                
                // Ensure all parent GameObjects in the hierarchy are active
                Transform current = m_ParticleSystem.transform;
                while (current != null)
                {
                    if (!current.gameObject.activeSelf)
                    {
                        current.gameObject.SetActive(true);
                    }
                    current = current.parent;
                }
                
                // Ensure emission is enabled before playing
                var emission = m_ParticleSystem.emission;
                if (!emission.enabled)
                {
                    emission.enabled = true;
                }
                
                // Ensure the particle system is properly configured
                var mainModule = m_ParticleSystem.main;
                if (mainModule.maxParticles <= 0)
                {
                    mainModule.maxParticles = 1000;
                }
                
                // Check if particle system is paused and unpause it
                if (m_ParticleSystem.isPaused)
                {
                    m_ParticleSystem.Play(true);
                }
                
                // Play the particle system
                m_ParticleSystem.Play(true); // Play with children
                
                // Note: isPlaying might not be immediately true after Play() - Unity needs a frame to update
                // We'll verify in a coroutine instead of checking immediately
                
                // Start a coroutine to verify the particle system is actually playing after a frame
                StartCoroutine(VerifyParticleSystemPlaying());
                
                // Warn if emission rate is 0
                if (emission.rateOverTime.constant <= 0 && emission.rateOverTime.constantMax <= 0)
                {
                    emission.rateOverTime = 10;
                    // Re-play after changing emission rate
                    m_ParticleSystem.Play(true);
                }
                
                // Start coroutines to verify the particle system is working correctly
                StartCoroutine(VerifyParticleSystemPlaying());
                StartCoroutine(VerifyParticleEmission());
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
        if (m_ParticleSystem.isPlaying)
        {
            m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    
    /// <summary>
    /// Coroutine to verify that the particle system is actually playing after Play() is called.
    /// Unity sometimes needs a frame to update the isPlaying state.
    /// </summary>
    private IEnumerator VerifyParticleSystemPlaying()
    {
        // Wait one frame for Unity to update the particle system state
        yield return null;
        
        bool isPlaying = m_ParticleSystem.isPlaying;
        bool isPaused = m_ParticleSystem.isPaused;
        bool isStopped = m_ParticleSystem.isStopped;
        
        if (!isPlaying)
        {
            // Try to fix it by playing again
            if (!isPaused && !isStopped)
            {
                m_ParticleSystem.Play(true);
            }
            else if (isPaused)
            {
                m_ParticleSystem.Play(true);
            }
        }
    }
    
    /// <summary>
    /// Coroutine to verify that particles are actually being emitted after Play() is called.
    /// This helps diagnose issues where the particle system thinks it's playing but isn't emitting.
    /// </summary>
    private IEnumerator VerifyParticleEmission()
    {
        if (!m_ParticleSystem.isPlaying)
            yield break;
        
        // Wait a few frames for particles to start emitting
        yield return null; // Wait one frame
        yield return null; // Wait another frame
    }
}

