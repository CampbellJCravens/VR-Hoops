using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main scoring system for the basketball hoop.
/// Detects valid baskets by tracking ball movement through Top → Middle → Bottom triggers.
/// </summary>
public class HoopScorer : MonoBehaviour
{
    [Header("Ball Settings")]
    [Tooltip("Tag that identifies basketball objects.")]
    public string ballTag = "Ball";
    
    [Tooltip("Maximum time (seconds) allowed for a ball to pass from top to bottom trigger. Allows for balls that spin around the rim.")]
    public float maxSequenceTime = 5.0f;

    [Header("VFX")]
    [Tooltip("ParticleSystem for money ball burst effect. Should be located at BasketballHoop > Visuals > Net > VFX > MoneyBurst.")]
    [SerializeField] private ParticleSystem moneyBurstParticleSystem;

    [Header("Debug")]
    [Tooltip("Enable to see detailed logging of trigger events and scoring logic.")]
    public bool debugLogs = false;

    // Event that other systems can subscribe to
    public event Action OnScored;

    // Track active ball sequences
    private class BallSequence
    {
        public GameObject ball;
        public float topEnterTime;
        public bool enteredTop;
        public bool enteredMiddle;
        public bool enteredBottom;
        public bool scored; // Prevent double-scoring
    }

    private Dictionary<GameObject, BallSequence> m_ActiveSequences = new Dictionary<GameObject, BallSequence>();
    private AudioSource m_AudioSource;

    private void Awake()
    {
        // Auto-find MoneyBurst particle system if not assigned
        if (moneyBurstParticleSystem == null)
        {
            // Search in children: Visuals > Net > VFX > MoneyBurst
            Transform visuals = transform.Find("Visuals");
            if (visuals != null)
            {
                Transform net = visuals.Find("Net");
                if (net != null)
                {
                    Transform vfx = net.Find("VFX");
                    if (vfx != null)
                    {
                        Transform moneyBurst = vfx.Find("MoneyBurst");
                        if (moneyBurst != null)
                        {
                            moneyBurstParticleSystem = moneyBurst.GetComponent<ParticleSystem>();
                            if (moneyBurstParticleSystem != null && debugLogs)
                            {
                                Debug.Log("[HoopScorer] Auto-found MoneyBurst particle system.", this);
                            }
                        }
                    }
                }
            }

            // If still not found, try searching all children recursively
            if (moneyBurstParticleSystem == null)
            {
                ParticleSystem[] allParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem ps in allParticleSystems)
                {
                    if (ps.gameObject.name == "MoneyBurst")
                    {
                        moneyBurstParticleSystem = ps;
                        if (debugLogs)
                            Debug.Log($"[HoopScorer] Auto-found MoneyBurst particle system via recursive search: {ps.gameObject.name}", this);
                        break;
                    }
                }
            }

            if (moneyBurstParticleSystem == null && debugLogs)
            {
                Debug.LogWarning("[HoopScorer] MoneyBurst particle system not found. Money ball VFX will not play. Please assign it manually in the Inspector.", this);
            }
        }

        // Ensure MoneyBurst particle system doesn't play on awake
        if (moneyBurstParticleSystem != null)
        {
            var main = moneyBurstParticleSystem.main;
            main.playOnAwake = false;
            // Stop it if it's already playing
            if (moneyBurstParticleSystem.isPlaying)
            {
                moneyBurstParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        
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
        
        if (debugLogs)
        {
            Debug.Log($"[HoopScorer] AudioSource initialized. Spatial Blend: {m_AudioSource.spatialBlend}, Rolloff: {m_AudioSource.rolloffMode}, Min Distance: {m_AudioSource.minDistance}, Max Distance: {m_AudioSource.maxDistance}", this);
        }
    }

    public void OnBallTriggerEnter(HoopTrigger.HoopTriggerPart part, Collider other)
    {
        // Find the root GameObject with the Ball tag (check collider's GameObject and all parents)
        GameObject ballRoot = FindBallRoot(other.gameObject);
        if (ballRoot == null)
        {
            if (debugLogs)
                Debug.Log($"[HoopScorer] Ignoring {other.gameObject.name} - no parent with '{ballTag}' tag found", this);
            return;
        }

        GameObject ball = ballRoot;

        // Get or create sequence for this ball
        if (!m_ActiveSequences.TryGetValue(ball, out BallSequence sequence))
        {
            sequence = new BallSequence { ball = ball };
            m_ActiveSequences[ball] = sequence;
        }

        // Log current sequence state
        if (debugLogs)
        {
            string sequenceState = $"Top:{sequence.enteredTop} Middle:{sequence.enteredMiddle} Bottom:{sequence.enteredBottom} Scored:{sequence.scored}";
            Debug.Log($"[HoopScorer] {part} TRIGGER ENTER | Ball: {ball.name} | Sequence: [{sequenceState}]", this);
        }

        switch (part)
        {
            case HoopTrigger.HoopTriggerPart.Top:
                if (!sequence.enteredTop)
                {
                    // Start the sequence - no velocity check needed
                    sequence.enteredTop = true;
                    sequence.topEnterTime = Time.time;
                    
                    if (debugLogs)
                        Debug.Log($"[HoopScorer] ✓ TOP trigger PASSED. Starting sequence at time {Time.time:F3}", this);
                }
                else
                {
                    if (debugLogs)
                        Debug.Log($"[HoopScorer] TOP trigger - Already entered, ignoring duplicate entry", this);
                }
                break;

            case HoopTrigger.HoopTriggerPart.Middle:
                if (!sequence.enteredMiddle)
                {
                    if (sequence.enteredTop)
                    {
                        // Normal case: Top → Middle
                        float timeSinceTop = Time.time - sequence.topEnterTime;
                        // Check if still within time window
                        if (timeSinceTop <= maxSequenceTime)
                        {
                            sequence.enteredMiddle = true;
                            
                            if (debugLogs)
                                Debug.Log($"[HoopScorer] ✓ MIDDLE trigger PASSED. Time since top: {timeSinceTop:F3}s (max: {maxSequenceTime:F3}s)", this);
                        }
                        else
                        {
                            if (debugLogs)
                                Debug.LogWarning($"[HoopScorer] ✗ MIDDLE trigger FAILED - Too late! Time since top: {timeSinceTop:F3}s > Max: {maxSequenceTime:F3}s. Resetting sequence.", this);
                            // Reset sequence
                            m_ActiveSequences.Remove(ball);
                        }
                    }
                    else
                    {
                        // Ball entered Middle before Top - this can happen if triggers overlap or ball enters from side
                        // Don't start a sequence, just ignore it
                        if (debugLogs)
                            Debug.LogWarning($"[HoopScorer] ✗ MIDDLE trigger FAILED - Entered before TOP! Sequence state: Top={sequence.enteredTop}, Middle={sequence.enteredMiddle}. Ignoring (waiting for Top first).", this);
                    }
                }
                else
                {
                    if (debugLogs)
                        Debug.Log($"[HoopScorer] MIDDLE trigger - Already entered, ignoring duplicate entry", this);
                }
                break;

            case HoopTrigger.HoopTriggerPart.Bottom:
                if (!sequence.enteredBottom && !sequence.scored)
                {
                    if (sequence.enteredTop && sequence.enteredMiddle)
                    {
                        float timeSinceTop = Time.time - sequence.topEnterTime;
                        // Check if still within time window
                        if (timeSinceTop <= maxSequenceTime)
                        {
                            sequence.enteredBottom = true;
                            sequence.scored = true;
                            
                            if (debugLogs)
                                Debug.Log($"[HoopScorer] ✓ BOTTOM trigger PASSED. Time since top: {timeSinceTop:F3}s (max: {maxSequenceTime:F3}s). SCORE!", this);
                            
                            // Valid basket!
                            HandleScore(ball);
                            
                            // Clean up sequence
                            m_ActiveSequences.Remove(ball);
                        }
                        else
                        {
                            if (debugLogs)
                                Debug.LogWarning($"[HoopScorer] ✗ BOTTOM trigger FAILED - Too late! Time since top: {timeSinceTop:F3}s > Max: {maxSequenceTime:F3}s.", this);
                            m_ActiveSequences.Remove(ball);
                        }
                    }
                    else
                    {
                        // Ball entered Bottom before completing sequence
                        if (debugLogs)
                            Debug.LogWarning($"[HoopScorer] ✗ BOTTOM trigger FAILED - Incomplete sequence! Top: {sequence.enteredTop}, Middle: {sequence.enteredMiddle}. Expected both to be true.", this);
                    }
                }
                else
                {
                    if (debugLogs)
                        Debug.Log($"[HoopScorer] BOTTOM trigger - Already scored or entered, ignoring duplicate entry (Scored: {sequence.scored}, Entered: {sequence.enteredBottom})", this);
                }
                break;
        }
    }

    public void OnBallTriggerExit(HoopTrigger.HoopTriggerPart part, Collider other)
    {
        // Find the root GameObject with the Ball tag
        GameObject ball = FindBallRoot(other.gameObject);
        if (ball == null)
            return;

        // Log exit with sequence state
        if (debugLogs)
        {
            if (m_ActiveSequences.TryGetValue(ball, out BallSequence sequence))
            {
                string sequenceState = $"Top:{sequence.enteredTop} Middle:{sequence.enteredMiddle} Bottom:{sequence.enteredBottom} Scored:{sequence.scored}";
                Debug.Log($"[HoopScorer] {part} TRIGGER EXIT | Ball: {ball.name} | Sequence: [{sequenceState}]", this);
            }
            else
            {
                Debug.Log($"[HoopScorer] {part} TRIGGER EXIT | Ball: {ball.name} | No active sequence", this);
            }
        }

        // If ball exits top trigger without entering middle, reset sequence
        if (part == HoopTrigger.HoopTriggerPart.Top)
        {
            if (m_ActiveSequences.TryGetValue(ball, out BallSequence sequence))
            {
                if (sequence.enteredTop && !sequence.enteredMiddle)
                {
                    if (debugLogs)
                        Debug.LogWarning($"[HoopScorer] ✗ Ball exited TOP without entering MIDDLE. Resetting sequence. Time since top: {Time.time - sequence.topEnterTime:F3}s", this);
                    m_ActiveSequences.Remove(ball);
                }
            }
        }
    }

    private void HandleScore(GameObject ball)
    {
        if (debugLogs)
            Debug.Log($"[HoopScorer] 🏀 SCORE! Ball: {ball.name}", this);

        // Play swish sound effect from SoundManager (3D spatial audio from the hoop)
        AudioClip swishSound = SoundManager.GetSwish();
        float swishVolume = SoundManager.GetSwishVolume();
        
        if (debugLogs)
        {
            Debug.Log($"[HoopScorer] Attempting to play swish sound. Sound: {(swishSound != null ? swishSound.name : "NULL")}, Volume: {swishVolume:F2}, AudioSource: {(m_AudioSource != null ? "EXISTS" : "NULL")}", this);
        }
        
        if (swishSound != null && m_AudioSource != null)
        {
            // Ensure AudioSource is enabled and GameObject is active
            if (!m_AudioSource.enabled)
            {
                Debug.LogWarning("[HoopScorer] AudioSource is disabled! Enabling it now.", this);
                m_AudioSource.enabled = true;
            }
            
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[HoopScorer] HoopScorer GameObject is not active in hierarchy! Sound may not play.", this);
            }
            
            m_AudioSource.PlayOneShot(swishSound, swishVolume);
            
            if (debugLogs)
            {
                Debug.Log($"[HoopScorer] ✅ PlayOneShot called! Sound: {swishSound.name}, Volume: {swishVolume:F2}, AudioSource enabled: {m_AudioSource.enabled}, GameObject active: {gameObject.activeInHierarchy}", this);
            }
        }
        else if (swishSound == null)
        {
            Debug.LogWarning("[HoopScorer] ❌ Swish sound not found in SoundManager! Make sure SoundManager exists in scene and has Swish audio clip assigned.", this);
        }
        else if (swishSound != null && m_AudioSource == null)
        {
            Debug.LogError("[HoopScorer] ❌ Swish sound found but AudioSource is missing! This should not happen - AudioSource should be created in Awake().", this);
        }

        // Mark ball as scored
        BallStateTracker tracker = ball.GetComponent<BallStateTracker>();
        if (tracker != null)
        {
            tracker.MarkAsScored();
        }

        // Invoke event
        OnScored?.Invoke();

        // Get hoop row for scoring
        int hoopRow = GetHoopRow();
        
        // Check if ball is a money ball
        bool isMoneyBall = false;
        BasketballVisualController visualController = ball.GetComponent<BasketballVisualController>();
        if (visualController != null)
        {
            isMoneyBall = visualController.IsMoneyBall();
        }

        // Play money burst VFX if this is a money ball
        if (isMoneyBall && moneyBurstParticleSystem != null)
        {
            moneyBurstParticleSystem.Play();
            if (debugLogs)
                Debug.Log("[HoopScorer] 💰 Money ball scored! Playing MoneyBurst particle effect.", this);
        }
        else if (isMoneyBall && moneyBurstParticleSystem == null)
        {
            if (debugLogs)
                Debug.LogWarning("[HoopScorer] Money ball scored but MoneyBurst particle system is not assigned!", this);
        }

        // Update score manager (find through PlayArea hierarchy)
        ScoreManager scoreManager = ScoreManager.FindScoreManagerFor(gameObject);
        if (scoreManager != null)
        {
#if NORMCORE
            // Only register score on the client that owns the PlayArea
            // This prevents double-counting when all clients see the ball pass through the hoop
            PlayAreaManager playAreaManager = scoreManager.GetPlayAreaManager();
            if (playAreaManager != null && playAreaManager.IsOwnedByLocalClient())
            {
                scoreManager.RegisterScore(hoopRow, isMoneyBall);
                if (debugLogs)
                    Debug.Log($"[HoopScorer] RegisterScore called (owner of PlayArea)", this);
            }
            else
            {
                if (debugLogs)
                    Debug.Log($"[HoopScorer] RegisterScore skipped (not owner of PlayArea. Owner: {playAreaManager?.GetOwner()}, Local client will receive score via model sync)", this);
            }
#else
            scoreManager.RegisterScore(hoopRow, isMoneyBall);
#endif
        }
        else
        {
            Debug.LogWarning($"[HoopScorer] Could not find ScoreManager for {gameObject.name}. Score will not be registered.", this);
        }
    }

    /// <summary>
    /// Gets the current row index of the hoop (0 = first row, 1 = second row, 2 = third row).
    /// </summary>
    private int GetHoopRow()
    {
        // Try to find HoopPositionsManager in parent hierarchy
        HoopPositionsManager positionsManager = GetComponentInParent<HoopPositionsManager>();
        if (positionsManager == null)
        {
            // Try searching in PlayArea
            Transform playArea = transform.parent;
            while (playArea != null && !playArea.name.Contains("PlayArea"))
            {
                playArea = playArea.parent;
            }
            if (playArea != null)
            {
                positionsManager = playArea.GetComponentInChildren<HoopPositionsManager>();
            }
        }

        if (positionsManager != null)
        {
            Vector2Int currentCoord = positionsManager.GetCurrentCoordinate();
            // coordinate.y is the row (0 = first row, 1 = second row, 2 = third row)
            return currentCoord.y;
        }

        // Default to row 0 if we can't find the positions manager
        if (debugLogs)
            Debug.LogWarning("[HoopScorer] Could not find HoopPositionsManager. Defaulting to row 0 for scoring.", this);
        return 0;
    }

    private void Update()
    {
        // Clean up stale sequences (balls that took too long or left the area)
        List<GameObject> toRemove = new List<GameObject>();
        
        foreach (var kvp in m_ActiveSequences)
        {
            BallSequence seq = kvp.Value;
            
            // Remove if sequence is too old
            if (Time.time - seq.topEnterTime > maxSequenceTime * 2f)
            {
                toRemove.Add(kvp.Key);
            }
            // Remove if ball was destroyed
            else if (seq.ball == null)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            m_ActiveSequences.Remove(key);
        }
    }

    /// <summary>
    /// Finds the root GameObject with the Ball tag by checking the collider's GameObject and all its parents.
    /// </summary>
    private GameObject FindBallRoot(GameObject colliderObject)
    {
        Transform current = colliderObject.transform;
        
        // Check the collider's GameObject and all parents
        while (current != null)
        {
            if (current.CompareTag(ballTag))
            {
                return current.gameObject;
            }
            current = current.parent;
        }
        
        return null;
    }
}

