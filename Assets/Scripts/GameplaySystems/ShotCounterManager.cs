using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages shot counting by detecting when balls collide with any of the assigned trigger colliders.
/// Notifies the HoopPositionsManager when the shot threshold is reached.
/// </summary>
public class ShotCounterManager : MonoBehaviour
{
    [Header("Shot Settings")]
    [Tooltip("Number of shots required before moving to next hoop position.")]
    [SerializeField] private int shotsPerPosition = 3;
    
    [Tooltip("Tag that identifies basketball objects.")]
    [SerializeField] private string ballTag = "Ball";

    [Header("Trigger Colliders")]
    [Tooltip("Array of colliders that should trigger shot counting when a ball enters them. All colliders should be set as triggers.")]
    [SerializeField] private Collider[] triggerColliders;

    [Header("References")]
    [Tooltip("HoopPositionsManager to notify when shot threshold is reached.")]
    [SerializeField] private HoopPositionsManager hoopPositionsManager;
    
    [Tooltip("PlayAreaManager that owns this shot counter.")]
    [SerializeField] private PlayAreaManager playAreaManager;
    
    [Tooltip("ScoreManager for registering misses and life loss.")]
    [SerializeField] private ScoreManager scoreManager;
    
    // Track which balls have already been counted to prevent double counting
    private HashSet<GameObject> m_CountedBalls = new HashSet<GameObject>();
    
    // Event dispatched when a shot is registered (ball hits the ground)
    public event System.Action<GameObject> ShotRegistered;

    private void Awake()
    {
        // Ensure all assigned colliders are triggers
        foreach (Collider col in triggerColliders)
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }
    }

    private void Start()
    {
        // Automatically add trigger forwarder components to each collider if they don't have one
        foreach (Collider col in triggerColliders)
        {
            // Ensure the collider is a trigger
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
            
            // Add or get the forwarder component
            ShotCounterTriggerForwarder forwarder = col.GetComponent<ShotCounterTriggerForwarder>();
            forwarder = col.gameObject.AddComponent<ShotCounterTriggerForwarder>();
            forwarder.SetManager(this);
        }
    }

    /// <summary>
    /// Called when a collider enters any of the assigned trigger colliders.
    /// This method is called by ShotCounterTriggerForwarder components.
    /// </summary>
    public void OnTriggerEntered(Collider other)
    {
        // Only process shots on the owner client to avoid duplicate state updates
        if (!playAreaManager.IsOwnedByLocalClient())
        {
            return;
        }
        
        // Check if the colliding object itself has the tag
        if (other.CompareTag(ballTag))
        {
            RegisterShot(other.gameObject);
            return;
        }
        
        // If not, search up the hierarchy for the ball root
        GameObject ballRoot = FindBallRoot(other.gameObject);
        if (ballRoot != null)
        {
            RegisterShot(ballRoot);
            return;
        }
    }

    /// <summary>
    /// Finds the root GameObject of the ball (the one with the "Ball" tag).
    /// </summary>
    private GameObject FindBallRoot(GameObject colliderGameObject)
    {
        Transform current = colliderGameObject.transform;
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

    private void RegisterShot(GameObject ball)
    {
        // Check if this ball has already been counted
        if (m_CountedBalls.Contains(ball))
        {
            return; // Already counted, ignore
        }

        // Mark this ball as counted immediately to prevent double counting
        m_CountedBalls.Add(ball);
        
        // Check if this is a money ball before incrementing
        bool isMoneyBall = IsMoneyBall(ball);
        
        // Increment the shot counter in PlayAreaManager (never resets)
        int currentShotCount = playAreaManager.IncrementShotCount();
        
        // Dispatch ShotRegistered event
        ShotRegistered?.Invoke(ball);
        
        // Check if a life should be lost
        // Life is lost if: ball did NOT score AND ball did NOT hit the rim
        BallStateTracker tracker = ball.GetComponent<BallStateTracker>();
        bool ballScored = tracker.HasScored();
        
        // Register a miss if the ball didn't score (resets consecutive scores counter)
        // This should happen for ALL missed shots, regardless of whether a life is lost
        if (!ballScored)
        {
            scoreManager.RegisterMiss();
        }
        
        // VFX is now managed by OnFireVFXTrigger based on fire state - don't stop it here
        // The VFX will automatically stop when fire state is deactivated
        
        // Check if a life should be lost (complete miss: no score AND no rim hit)
        if (tracker.ShouldLoseLife())
        {
            scoreManager.LoseLife();
        }
        
        // Disable collider on the ball after a delay to prevent further collisions
        // Delay prevents the ball from falling through the ground
        StartCoroutine(DisableBallCollidersAfterDelay(ball, 0.5f));
        
        // Schedule ball destruction after 3 seconds
        StartCoroutine(DestroyBallAfterDelay(ball, 3f));
        
        // If this is a money ball, notify PlayAreaManager (it will move the hoop and unblock spawning)
        if (isMoneyBall)
        {
            playAreaManager.OnMoneyBallShotRegistered();
        }
        // Otherwise, check if we should move the hoop (every 3rd shot, but not for money balls since they handle it)
        else if (currentShotCount % 3 == 0)
        {
            // Notify HoopPositionsManager to move to next position
            hoopPositionsManager.MoveToNextPosition();
        }
    }
    
    /// <summary>
    /// Checks if a ball is a money ball by checking its material.
    /// </summary>
    private bool IsMoneyBall(GameObject ball)
    {
        BasketballVisualController visualController = ball.GetComponent<BasketballVisualController>();
        return visualController.IsMoneyBall();
    }

    /// <summary>
    /// Disables all colliders on the ball after a delay to prevent further collisions.
    /// </summary>
    private System.Collections.IEnumerator DisableBallCollidersAfterDelay(GameObject ball, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (ball != null) {
            Collider[] ballColliders = ball.GetComponentsInChildren<Collider>();
            foreach (Collider col in ballColliders)
            {
                if (col != null) { col.enabled = false; }
            }
        }
        
    }
    
    private System.Collections.IEnumerator DestroyBallAfterDelay(GameObject ball, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // VFX cleanup is handled automatically by OnFireVFXTrigger when fire state changes
        // No need to manually stop VFX here
        
        // Remove from counted set before destroying
        m_CountedBalls.Remove(ball);
        Destroy(ball);
    }

    /// <summary>
    /// Clears the counted balls set (counters are no longer reset, they increment indefinitely).
    /// </summary>
    public void ResetCounter()
    {
        // Only clear the counted balls set - counters are not reset
        m_CountedBalls.Clear();
    }

    /// <summary>
    /// Gets the current shot count from the unified counter in PlayAreaManager.
    /// </summary>
    public int GetCurrentShotCount()
    {
        return playAreaManager.GetShotCount();
    }

    // Removed StopAllVFXOnBall method - VFX is now managed entirely by OnFireVFXTrigger
    // based on the fire state. The VFX will automatically start/stop when fire state changes.
}
