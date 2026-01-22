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
        // Ensure MoneyBurst particle system doesn't play on awake
        var main = moneyBurstParticleSystem.main;
        main.playOnAwake = false;
        // Stop it if it's already playing
        if (moneyBurstParticleSystem.isPlaying)
        {
            moneyBurstParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        // Get or add AudioSource component for 3D spatial audio
        m_AudioSource = GetComponent<AudioSource>();
        m_AudioSource = gameObject.AddComponent<AudioSource>();
        
        // Always ensure 3D spatial audio settings are configured (even if AudioSource already existed)
        m_AudioSource.playOnAwake = false;
        m_AudioSource.spatialBlend = 1.0f; // 3D sound (full spatial blend)
        m_AudioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Realistic distance falloff
        m_AudioSource.minDistance = 1f;
        m_AudioSource.maxDistance = 50f;
    }

    public void OnBallTriggerEnter(HoopTrigger.HoopTriggerPart part, Collider other)
    {
        // Find the root GameObject with the Ball tag (check collider's GameObject and all parents)
        GameObject ballRoot = FindBallRoot(other.gameObject);
        if (ballRoot == null)
        {
            return;
        }

        GameObject ball = ballRoot;

        // Get or create sequence for this ball
        if (!m_ActiveSequences.TryGetValue(ball, out BallSequence sequence))
        {
            sequence = new BallSequence { ball = ball };
            m_ActiveSequences[ball] = sequence;
        }

        switch (part)
        {
            case HoopTrigger.HoopTriggerPart.Top:
                if (!sequence.enteredTop)
                {
                    // Start the sequence - no velocity check needed
                    sequence.enteredTop = true;
                    sequence.topEnterTime = Time.time;
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
                        }
                        else
                        {
                            // Reset sequence
                            m_ActiveSequences.Remove(ball);
                        }
                    }
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
                            
                            // Valid basket!
                            HandleScore(ball);
                            
                            // Clean up sequence
                            m_ActiveSequences.Remove(ball);
                        }
                        else
                        {
                            m_ActiveSequences.Remove(ball);
                        }
                    }
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

        // If ball exits top trigger without entering middle, reset sequence
        if (part == HoopTrigger.HoopTriggerPart.Top)
        {
            if (m_ActiveSequences.TryGetValue(ball, out BallSequence sequence))
            {
                if (sequence.enteredTop && !sequence.enteredMiddle)
                {
                    m_ActiveSequences.Remove(ball);
                }
            }
        }
    }

    private void HandleScore(GameObject ball)
    {
        // Play swish sound effect from SoundManager (3D spatial audio from the hoop)
        AudioClip swishSound = SoundManager.GetSwish();
        float swishVolume = SoundManager.GetSwishVolume();
        
        // Ensure AudioSource is enabled and GameObject is active
        if (!m_AudioSource.enabled)
        {
            m_AudioSource.enabled = true;
        }
        
        
        float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, swishVolume);
        m_AudioSource.PlayOneShot(swishSound, effectiveVolume);

        // Check if ball is a money ball
        bool isMoneyBall = false;
        BasketballVisualController visualController = ball.GetComponent<BasketballVisualController>();
        isMoneyBall = visualController.IsMoneyBall();

        // Play money burst VFX if this is a money ball
        if (isMoneyBall)
        {
            moneyBurstParticleSystem.Play();
        }

        // Mark ball as scored
        BallStateTracker tracker = ball.GetComponent<BallStateTracker>();
        if (tracker != null)
        {
            tracker.MarkAsScored();

            // Invoke event
            OnScored?.Invoke();

            // Get hoop row for scoring
            int hoopRow = GetHoopRow();
            
            // Update score manager (find through PlayArea hierarchy)
            PlayAreaManager playAreaManager = GetComponentInParent<PlayAreaManager>();
            ScoreManager scoreManager = playAreaManager.GetScoreManager();
            // Only register score on the client that owns the PlayArea
            if (playAreaManager.IsOwnedByLocalClient())
            {
                scoreManager.RegisterScore(hoopRow, isMoneyBall);
            }
        }
    }

    /// <summary>
    /// Gets the current row index of the hoop (0 = first row, 1 = second row, 2 = third row).
    /// </summary>
    private int GetHoopRow()
    {
        HoopPositionsManager positionsManager = GetComponentInParent<HoopPositionsManager>();
        Vector2Int currentCoord = positionsManager.GetCurrentCoordinate();
        // coordinate.y is the row (0 = first row, 1 = second row, 2 = third row)
        return currentCoord.y;
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
