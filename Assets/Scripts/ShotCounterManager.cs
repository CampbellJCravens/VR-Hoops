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
    [Tooltip("HoopPositionsManager to notify when shot threshold is reached. If not assigned, will search parent.")]
    [SerializeField] private HoopPositionsManager hoopPositionsManager;
    
    [Tooltip("PlayAreaManager that owns this shot counter. If not assigned, will search parent.")]
    [SerializeField] private PlayAreaManager playAreaManager;
    
    // Track which balls have already been counted to prevent double counting
    private HashSet<GameObject> m_CountedBalls = new HashSet<GameObject>();
    
    // Event dispatched when a shot is registered (ball hits the ground)
    public event System.Action<GameObject> ShotRegistered;

    private void Awake()
    {
        // Validate trigger colliders
        if (triggerColliders == null || triggerColliders.Length == 0)
        {
            Debug.LogWarning($"[ShotCounterManager] No trigger colliders assigned on {gameObject.name}. Shot counting will not work!", this);
        }
        else
        {
            // Ensure all assigned colliders are triggers
            foreach (Collider col in triggerColliders)
            {
                if (col != null && !col.isTrigger)
                {
                    Debug.LogWarning($"[ShotCounterManager] Collider {col.gameObject.name} is not set as a trigger. Setting it now.", this);
                    col.isTrigger = true;
                }
            }
        }

        // Auto-find PlayAreaManager if not assigned
        if (playAreaManager == null)
        {
            playAreaManager = GetComponentInParent<PlayAreaManager>();
        }
        
        // Auto-find HoopPositionsManager if not assigned
        if (hoopPositionsManager == null)
        {
            hoopPositionsManager = GetComponentInParent<HoopPositionsManager>();
            if (hoopPositionsManager == null && playAreaManager != null)
            {
                hoopPositionsManager = playAreaManager.GetComponentInChildren<HoopPositionsManager>();
            }
        }
    }

    private void Start()
    {
        // Automatically add trigger forwarder components to each collider if they don't have one
        if (triggerColliders != null)
        {
            foreach (Collider col in triggerColliders)
            {
                if (col != null)
                {
                    // Ensure the collider is a trigger
                    if (!col.isTrigger)
                    {
                        col.isTrigger = true;
                    }
                    
                    // Add or get the forwarder component
                    ShotCounterTriggerForwarder forwarder = col.GetComponent<ShotCounterTriggerForwarder>();
                    if (forwarder == null)
                    {
                        forwarder = col.gameObject.AddComponent<ShotCounterTriggerForwarder>();
                    }
                    forwarder.SetManager(this);
                }
            }
        }
    }

    /// <summary>
    /// Called when a collider enters any of the assigned trigger colliders.
    /// This method is called by ShotCounterTriggerForwarder components.
    /// </summary>
    public void OnTriggerEntered(Collider other)
    {
        Debug.Log($"[ShotCounterManager] OnTriggerEntered called with: {other.gameObject.name} (Tag: {other.tag})", this);
        
        // Check if the colliding object itself has the tag
        if (other.CompareTag(ballTag))
        {
            Debug.Log($"[ShotCounterManager] Found ball directly: {other.gameObject.name}", this);
            RegisterShot(other.gameObject);
            return;
        }

        // Check if parent has the tag
        GameObject ballRoot = FindBallRoot(other.gameObject);
        
        if (ballRoot == null)
        {
            Debug.Log($"[ShotCounterManager] Could not find ball root for {other.gameObject.name}", this);
            return;
        }

        Debug.Log($"[ShotCounterManager] Found ball root: {ballRoot.name}", this);
        RegisterShot(ballRoot);
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
            Debug.Log($"[ShotCounterManager] Ball {ball.name} already counted, ignoring.", this);
            return; // Already counted, ignore
        }

        // Mark this ball as counted immediately to prevent double counting
        m_CountedBalls.Add(ball);
        
        // Check if this is a money ball before incrementing
        bool isMoneyBall = IsMoneyBall(ball);
        
        // Increment the shot counter in PlayAreaManager (never resets)
        int currentShotCount = 0;
        if (playAreaManager != null)
        {
            currentShotCount = playAreaManager.IncrementShotCount();
        }
        else
        {
            Debug.LogWarning($"[ShotCounterManager] PlayAreaManager not found! Shot counter will not work correctly.", this);
        }
        
        Debug.Log($"[ShotCounterManager] Shot registered! Ball: {ball.name}, Shot count: {currentShotCount}, Is money ball: {isMoneyBall}", this);
        
        // Dispatch ShotRegistered event
        ShotRegistered?.Invoke(ball);
        
        // Check if a life should be lost
        // Life is lost if: ball did NOT score AND ball did NOT hit the rim
        BallStateTracker tracker = ball.GetComponent<BallStateTracker>();
        bool ballScored = tracker != null && tracker.HasScored();
        
        // VFX is now managed by OnFireVFXTrigger based on fire state - don't stop it here
        // The VFX will automatically stop when fire state is deactivated
        
        if (tracker != null && tracker.ShouldLoseLife())
        {
            // Lose a life (find ScoreManager through PlayArea hierarchy)
            ScoreManager scoreManager = ScoreManager.FindScoreManagerFor(gameObject);
            if (scoreManager != null)
            {
                scoreManager.LoseLife();
            }
            else
            {
                Debug.LogWarning($"[ShotCounterManager] Could not find ScoreManager for {gameObject.name}. Life will not be lost.", this);
            }
        }
        else if (tracker == null)
        {
            Debug.LogWarning($"[ShotCounterManager] Ball {ball.name} does not have BallStateTracker component. Cannot determine if life should be lost.", this);
        }
        
        // Disable collider on the ball after a delay to prevent further collisions
        // Delay prevents the ball from falling through the ground
        StartCoroutine(DisableBallCollidersAfterDelay(ball, 0.5f));
        
        // Schedule ball destruction after 3 seconds
        StartCoroutine(DestroyBallAfterDelay(ball, 3f));
        
        // If this is a money ball, notify PlayAreaManager (it will move the hoop and unblock spawning)
        if (isMoneyBall && playAreaManager != null)
        {
            playAreaManager.OnMoneyBallShotRegistered();
        }
        // Otherwise, check if we should move the hoop (every 3rd shot, but not for money balls since they handle it)
        else if (currentShotCount % 3 == 0)
        {
            Debug.Log($"[ShotCounterManager] Shot count reached multiple of 3! Moving hoop to next position.", this);
            
            // Notify HoopPositionsManager to move to next position
            if (hoopPositionsManager != null)
            {
                hoopPositionsManager.MoveToNextPosition();
            }
            else
            {
                Debug.LogWarning("[ShotCounterManager] Cannot move hoop - HoopPositionsManager not found!", this);
            }
        }
    }
    
    /// <summary>
    /// Checks if a ball is a money ball by checking its material.
    /// </summary>
    private bool IsMoneyBall(GameObject ball)
    {
        BasketballVisualController visualController = ball.GetComponent<BasketballVisualController>();
        if (visualController != null)
        {
            return visualController.IsMoneyBall();
        }
        return false;
    }

    /// <summary>
    /// Disables all colliders on the ball after a delay to prevent further collisions.
    /// </summary>
    private System.Collections.IEnumerator DisableBallCollidersAfterDelay(GameObject ball, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (ball != null)
        {
            Collider[] ballColliders = ball.GetComponentsInChildren<Collider>();
            foreach (Collider col in ballColliders)
            {
                col.enabled = false;
            }
            Debug.Log($"[ShotCounterManager] Disabled all colliders on ball {ball.name} after {delay} second delay.", this);
        }
    }
    
    private System.Collections.IEnumerator DestroyBallAfterDelay(GameObject ball, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (ball != null)
        {
            // VFX cleanup is handled automatically by OnFireVFXTrigger when fire state changes
            // No need to manually stop VFX here
            
            // Remove from counted set before destroying
            m_CountedBalls.Remove(ball);
            Destroy(ball);
            Debug.Log($"[ShotCounterManager] Destroyed ball {ball.name} after {delay} seconds.", this);
        }
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
        if (playAreaManager != null)
        {
            return playAreaManager.GetShotCount();
        }
        return 0;
    }

    // Removed StopAllVFXOnBall method - VFX is now managed entirely by OnFireVFXTrigger
    // based on the fire state. The VFX will automatically start/stop when fire state changes.
}

