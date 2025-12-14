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

        // Update score manager (find through PlayArea hierarchy)
        ScoreManager scoreManager = ScoreManager.FindScoreManagerFor(gameObject);
        if (scoreManager != null)
        {
            scoreManager.RegisterScore(hoopRow, isMoneyBall);
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

