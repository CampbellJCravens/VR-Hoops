using UnityEngine;

/// <summary>
/// Tracks when a basketball collides with the rim.
/// Attach this to the rim collider GameObject (tagged as "Rim").
/// </summary>
[RequireComponent(typeof(Collider))]
public class RimCollisionTracker : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag that identifies basketball objects.")]
    [SerializeField] private string ballTag = "Ball";

    [Header("Debug")]
    [Tooltip("Enable to log rim collision events.")]
    [SerializeField] private bool debugLogs = false;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the colliding object is a ball
        GameObject ball = FindBallRoot(collision.gameObject);
        if (ball != null)
        {
            // Mark the ball as having hit the rim
            BallStateTracker tracker = ball.GetComponent<BallStateTracker>();
            if (tracker != null)
            {
                // Only mark as hit rim if ball hasn't scored yet (once scored, don't override)
                if (!tracker.HasScored())
                {
                    tracker.MarkAsHitRim();
                    
                    if (debugLogs)
                        Debug.Log($"[RimCollisionTracker] Ball {ball.name} hit the rim.", this);
                }
            }
            else if (debugLogs)
            {
                Debug.LogWarning($"[RimCollisionTracker] Ball {ball.name} does not have BallStateTracker component.", this);
            }
        }
    }

    /// <summary>
    /// Finds the root GameObject of the ball (the one with the "Ball" tag).
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

    private void OnValidate()
    {
        // Ensure collider is NOT a trigger (we want collision, not trigger)
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger)
        {
            Debug.LogWarning("[RimCollisionTracker] Rim collider should not be a trigger. Setting isTrigger = false.", this);
            col.isTrigger = false;
        }
    }
}

