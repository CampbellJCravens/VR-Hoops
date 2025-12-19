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

    [Header("Debug")]
    [Tooltip("Enable to see detailed logging for VFX state changes.")]
    [SerializeField] private bool debugLogs = false;

    private ParticleSystem m_ParticleSystem;
    private bool m_WasOnFire = false;
    private ScoreManager m_ScoreManager; // Cache the ScoreManager reference
    private bool m_Initialized = false;

    private void Awake()
    {
        // Ensure this GameObject is active (VFX won't show if GameObject is inactive)
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] GameObject {gameObject.name} is inactive! VFX will not be visible. Activating...", this);
            gameObject.SetActive(true);
        }
        
        // Try to find ParticleSystem on this GameObject first, then in children
        m_ParticleSystem = GetComponent<ParticleSystem>();
        if (m_ParticleSystem == null)
        {
            m_ParticleSystem = GetComponentInChildren<ParticleSystem>(true); // Include inactive
            if (m_ParticleSystem != null && debugLogs)
            {
                Debug.Log($"[OnFireVFXTrigger] ParticleSystem found in children of {gameObject.name}: {m_ParticleSystem.gameObject.name}", this);
            }
        }
        
        if (m_ParticleSystem == null)
        {
            Debug.LogError($"[OnFireVFXTrigger] ❌ CRITICAL: No ParticleSystem found on {gameObject.name} or its children! VFX will not work.", this);
        }
        else
        {
            // Ensure the ParticleSystem's GameObject is active
            if (!m_ParticleSystem.gameObject.activeSelf)
            {
                Debug.LogWarning($"[OnFireVFXTrigger] ParticleSystem GameObject {m_ParticleSystem.gameObject.name} is inactive! Activating...", this);
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
                Debug.LogWarning($"[OnFireVFXTrigger] ParticleSystem on {gameObject.name} is still playing after Stop() call! This may indicate a timing issue. Forcing stop...", this);
                // Force stop by setting time to 0 and clearing
                m_ParticleSystem.time = 0;
                m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                m_ParticleSystem.Clear(true);
            }
            
            // Ensure emission is disabled initially (we'll enable it when fire state activates)
            var emission = m_ParticleSystem.emission;
            if (emission.enabled && debugLogs)
            {
                Debug.Log($"[OnFireVFXTrigger] Emission was enabled on Awake for {gameObject.name}. It will be enabled when fire state activates.", this);
            }
            
            if (debugLogs)
            {
                var renderer = m_ParticleSystem.GetComponent<ParticleSystemRenderer>();
                // Reuse the emission variable declared above
                Debug.Log($"[OnFireVFXTrigger] Awake on {gameObject.name}. ParticleSystem found: {m_ParticleSystem.gameObject.name}, playOnAwake: {main.playOnAwake}, GameObject active: {gameObject.activeSelf}, PS GameObject active: {m_ParticleSystem.gameObject.activeSelf}, isPlaying: {m_ParticleSystem.isPlaying}, renderer.enabled: {(renderer != null ? renderer.enabled.ToString() : "N/A")}, emission.enabled: {emission.enabled}, emission.rateOverTime: {emission.rateOverTime.constant}", this);
            }
        }
    }

    private void OnEnable()
    {
        // Find ScoreManager through PlayArea hierarchy
        m_ScoreManager = ScoreManager.FindScoreManagerFor(gameObject);
        
        if (debugLogs)
            Debug.Log($"[OnFireVFXTrigger] OnEnable on {gameObject.name}. ScoreManager found: {m_ScoreManager != null}", this);
        
        // Subscribe to On Fire state changes for immediate updates
        if (m_ScoreManager != null)
        {
            m_ScoreManager.OnFireStateChanged += OnFireStateChangedHandler;
            if (debugLogs)
                Debug.Log($"[OnFireVFXTrigger] Subscribed to OnFireStateChanged event on {gameObject.name}", this);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[OnFireVFXTrigger] ScoreManager not found in OnEnable for {gameObject.name}. Will retry in Start().", this);
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
            if (m_ParticleSystem == null)
            {
                m_ParticleSystem = GetComponentInChildren<ParticleSystem>(true); // Include inactive
            }
            if (m_ParticleSystem == null)
            {
                Debug.LogError($"[OnFireVFXTrigger] InitializeVFX: No ParticleSystem found on {gameObject.name} or its children!", this);
                return;
            }
        }
        
        // Ensure the ParticleSystem's GameObject is active
        if (!m_ParticleSystem.gameObject.activeSelf)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] ParticleSystem GameObject {m_ParticleSystem.gameObject.name} is inactive in InitializeVFX! Activating...", this);
            m_ParticleSystem.gameObject.SetActive(true);
        }
        
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
            
            bool currentFireState = m_ScoreManager.IsOnFire();
            m_WasOnFire = currentFireState;
            
            if (debugLogs)
                Debug.Log($"[OnFireVFXTrigger] InitializeVFX on {gameObject.name}. Current fire state: {currentFireState}, ParticleSystem.isPlaying: {m_ParticleSystem.isPlaying}", this);
            
            // Always update VFX state, even if it seems correct (to ensure it's properly started)
            UpdateVFXState(currentFireState);
            m_Initialized = true;
            
            if (debugLogs)
                Debug.Log($"[OnFireVFXTrigger] InitializeVFX complete. VFX should now be: {(currentFireState ? "PLAYING" : "STOPPED")}, Actual state: {(m_ParticleSystem.isPlaying ? "PLAYING" : "STOPPED")}", this);
        }
        else
        {
            // If ScoreManager doesn't exist yet, ensure VFX is off
            if (m_ParticleSystem != null)
            {
                m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            
            if (debugLogs)
                Debug.LogWarning($"[OnFireVFXTrigger] ScoreManager not found in InitializeVFX for {gameObject.name}. VFX will remain off.", this);
        }
    }

    /// <summary>
    /// Event handler for On Fire state changes. Called immediately when state changes.
    /// </summary>
    private void OnFireStateChangedHandler(bool isOnFire)
    {
        if (debugLogs)
            Debug.Log($"[OnFireVFXTrigger] OnFireStateChangedHandler called on {gameObject.name}: isOnFire={isOnFire}, m_WasOnFire={m_WasOnFire}", this);
        
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
                
                // Initialize VFX state now that we found ScoreManager
                if (!m_Initialized)
                {
                    InitializeVFX();
                }
            }
        }

        // Fallback: Check if On Fire state has changed (in case event wasn't fired)
        // This ensures we catch any state changes even if event system fails
        if (m_ScoreManager != null && m_Initialized)
        {
            bool isOnFire = m_ScoreManager.IsOnFire();
            
            if (isOnFire != m_WasOnFire)
            {
                if (debugLogs)
                    Debug.Log($"[OnFireVFXTrigger] Fire state changed detected in Update on {gameObject.name}: {m_WasOnFire} → {isOnFire}", this);
                
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
        {
            if (debugLogs)
                Debug.LogWarning($"[OnFireVFXTrigger] Cannot update VFX state - ParticleSystem is null on {gameObject.name}", this);
            return;
        }
        
        // Ensure GameObject is active (VFX won't show if inactive)
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] GameObject {gameObject.name} is inactive! Activating to show VFX...", this);
            gameObject.SetActive(true);
        }
        
        // Ensure ParticleSystem GameObject is active
        if (!m_ParticleSystem.gameObject.activeSelf)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] ParticleSystem GameObject {m_ParticleSystem.gameObject.name} is inactive in UpdateVFXState! Activating...", this);
            m_ParticleSystem.gameObject.SetActive(true);
        }
        
        // Check if renderer is enabled
        var renderer = m_ParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            Debug.LogError($"[OnFireVFXTrigger] ❌ CRITICAL: No ParticleSystemRenderer found on {m_ParticleSystem.gameObject.name}! VFX will not be visible!", this);
            return;
        }
        if (!renderer.enabled)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] ParticleSystemRenderer is disabled on {m_ParticleSystem.gameObject.name}! Enabling...", this);
            renderer.enabled = true;
        }
        
        // Check if renderer has a material
        if (renderer.material == null && renderer.sharedMaterial == null)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] ⚠️ ParticleSystemRenderer on {m_ParticleSystem.gameObject.name} has no material! VFX may not be visible.", this);
        }

        if (debugLogs)
            Debug.Log($"[OnFireVFXTrigger] UpdateVFXState on {gameObject.name}: isOnFire={isOnFire}, isOneShot={isOneShot}, loopWhileOnFire={loopWhileOnFire}, stopWhenOffFire={stopWhenOffFire}, GameObject.active={gameObject.activeSelf}, Renderer.enabled={(renderer != null ? renderer.enabled.ToString() : "N/A")}", this);

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
                if (debugLogs)
                    Debug.Log($"[OnFireVFXTrigger] 🔥 Started one-shot VFX on {gameObject.name}", this);
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
                        Debug.LogWarning($"[OnFireVFXTrigger] Parent GameObject {current.name} is inactive! Activating...", this);
                        current.gameObject.SetActive(true);
                    }
                    current = current.parent;
                }
                
                // Ensure emission is enabled before playing
                var emission = m_ParticleSystem.emission;
                if (!emission.enabled)
                {
                    Debug.LogError($"[OnFireVFXTrigger] ❌ CRITICAL: Emission module is DISABLED on {gameObject.name}! Enabling it now...", this);
                    emission.enabled = true;
                }
                
                // Ensure the particle system is properly configured
                var mainModule = m_ParticleSystem.main;
                if (mainModule.maxParticles <= 0)
                {
                    Debug.LogWarning($"[OnFireVFXTrigger] ⚠️ maxParticles is {mainModule.maxParticles} on {gameObject.name}! Setting to 1000...", this);
                    mainModule.maxParticles = 1000;
                }
                
                // Check if particle system is paused and unpause it
                if (m_ParticleSystem.isPaused)
                {
                    Debug.LogWarning($"[OnFireVFXTrigger] ParticleSystem on {gameObject.name} is paused! Unpausing...", this);
                    m_ParticleSystem.Play(true);
                }
                
                // Play the particle system
                m_ParticleSystem.Play(true); // Play with children
                
                // Note: isPlaying might not be immediately true after Play() - Unity needs a frame to update
                // We'll verify in a coroutine instead of checking immediately
                int particleCountAfter = m_ParticleSystem.particleCount;
                
                if (debugLogs)
                {
                    var emissionModule = m_ParticleSystem.emission;
                    Debug.Log($"[OnFireVFXTrigger] 🔥 Started/Restarted persistent VFX on {gameObject.name} (looping: {loopWhileOnFire}, wasPlaying: {wasPlaying}, wasPaused: {wasPaused}, particleCount: {particleCountBefore}→{particleCountAfter}, maxParticles: {mainModule.maxParticles}, startLifetime: {mainModule.startLifetime.constant}, emission.enabled: {emissionModule.enabled}, emission.rateOverTime: {emissionModule.rateOverTime.constant})", this);
                }
                
                // Start a coroutine to verify the particle system is actually playing after a frame
                StartCoroutine(VerifyParticleSystemPlaying());
                
                // Warn if emission rate is 0
                if (emission.rateOverTime.constant <= 0 && emission.rateOverTime.constantMax <= 0)
                {
                    Debug.LogWarning($"[OnFireVFXTrigger] ⚠️ Emission rate is 0 or negative on {gameObject.name}! Particles may not be emitted. rateOverTime: {emission.rateOverTime.constant}. Setting to 10...", this);
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
                if (debugLogs)
                    Debug.Log($"[OnFireVFXTrigger] ❄️ Stopped VFX on {gameObject.name} (fire state deactivated)", this);
            }
            else
            {
                if (debugLogs)
                    Debug.Log($"[OnFireVFXTrigger] VFX not stopped on {gameObject.name} (isOneShot={isOneShot}, stopWhenOffFire={stopWhenOffFire})", this);
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
    
    /// <summary>
    /// Coroutine to verify that the particle system is actually playing after Play() is called.
    /// Unity sometimes needs a frame to update the isPlaying state.
    /// </summary>
    private IEnumerator VerifyParticleSystemPlaying()
    {
        // Wait one frame for Unity to update the particle system state
        yield return null;
        
        if (m_ParticleSystem == null)
            yield break;
        
        bool isPlaying = m_ParticleSystem.isPlaying;
        bool isPaused = m_ParticleSystem.isPaused;
        bool isStopped = m_ParticleSystem.isStopped;
        int particleCount = m_ParticleSystem.particleCount;
        var emissionModule = m_ParticleSystem.emission;
        
        if (!isPlaying)
        {
            Debug.LogError($"[OnFireVFXTrigger] ❌ CRITICAL: ParticleSystem on {gameObject.name} is NOT playing after Play() call (checked after 1 frame)! isPaused: {isPaused}, isStopped: {isStopped}, particleCount: {particleCount}, GameObject active: {gameObject.activeSelf}, GameObject activeInHierarchy: {gameObject.activeInHierarchy}, PS GameObject active: {m_ParticleSystem.gameObject.activeSelf}, PS GameObject activeInHierarchy: {m_ParticleSystem.gameObject.activeInHierarchy}, emission.enabled: {emissionModule.enabled}, emission.rateOverTime: {emissionModule.rateOverTime.constant}", this);
            
            // Try to fix it by playing again
            if (!isPaused && !isStopped)
            {
                Debug.LogWarning($"[OnFireVFXTrigger] Attempting to fix by calling Play() again...", this);
                m_ParticleSystem.Play(true);
            }
            else if (isPaused)
            {
                Debug.LogWarning($"[OnFireVFXTrigger] Particle system is paused. Unpausing...", this);
                m_ParticleSystem.Play(true);
            }
        }
        else if (debugLogs)
        {
            Debug.Log($"[OnFireVFXTrigger] ✅ ParticleSystem on {gameObject.name} is confirmed playing after 1 frame. particleCount: {particleCount}", this);
        }
    }
    
    /// <summary>
    /// Coroutine to verify that particles are actually being emitted after Play() is called.
    /// This helps diagnose issues where the particle system thinks it's playing but isn't emitting.
    /// </summary>
    private IEnumerator VerifyParticleEmission()
    {
        if (m_ParticleSystem == null || !m_ParticleSystem.isPlaying)
            yield break;
        
        // Wait a few frames for particles to start emitting
        yield return null; // Wait one frame
        yield return null; // Wait another frame
        
        if (m_ParticleSystem == null)
            yield break;
        
        int particleCount = m_ParticleSystem.particleCount;
        bool isPlaying = m_ParticleSystem.isPlaying;
        var emission = m_ParticleSystem.emission;
        
        if (debugLogs)
        {
            Debug.Log($"[OnFireVFXTrigger] VerifyParticleEmission on {gameObject.name}: isPlaying={isPlaying}, particleCount={particleCount}, emission.enabled={emission.enabled}, emission.rateOverTime={emission.rateOverTime.constant}", this);
        }
        
        // If we're supposed to be playing but have no particles after a few frames, something is wrong
        if (isPlaying && particleCount == 0 && emission.enabled && emission.rateOverTime.constant > 0)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] ⚠️ ParticleSystem on {gameObject.name} is playing but has 0 particles after 2 frames! This may indicate a configuration issue. Check: maxParticles ({m_ParticleSystem.main.maxParticles}), startLifetime ({m_ParticleSystem.main.startLifetime.constant}), and material assignment.", this);
        }
        else if (!isPlaying)
        {
            Debug.LogWarning($"[OnFireVFXTrigger] ⚠️ ParticleSystem on {gameObject.name} is NOT playing during verification! This may indicate it was stopped prematurely.", this);
        }
    }
}

