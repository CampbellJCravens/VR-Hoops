using UnityEngine;

/// <summary>
/// Tracks when a basketball collides with the rim.
/// Attach this to the rim collider GameObject (tagged as "Rim").
/// </summary>
    [RequireComponent(typeof(Collider))]
public class RimCollisionTracker : MonoBehaviour
{
    private void Awake()
    {
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
    }
    [Header("Settings")]
    [Tooltip("Tag that identifies basketball objects.")]
    [SerializeField] private string ballTag = "Ball";
    
    [Tooltip("Minimum collision velocity magnitude to trigger rim hit sound. Prevents quiet touches from playing sound.")]
    [SerializeField] private float minCollisionVelocity = 0.5f;
    
    [Tooltip("Cooldown time (seconds) between rim hit sounds to prevent rapid, overlapping playback.")]
    [SerializeField] private float rimHitCooldown = 0.1f;

    [Header("Debug")]
    [Tooltip("Enable to log rim collision events.")]
    [SerializeField] private bool debugLogs = false;

    private AudioSource m_AudioSource;
    private float m_LastRimHitTime = 0f;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the colliding object is a ball
        GameObject ball = FindBallRoot(collision.gameObject);
        if (ball != null)
        {
            // Get collision velocity for sound volume scaling
            float collisionVelocity = collision.relativeVelocity.magnitude;
            
            // Check if collision is strong enough and cooldown has passed
            if (collisionVelocity >= minCollisionVelocity && Time.time - m_LastRimHitTime >= rimHitCooldown)
            {
                // Play rim hit sound effect from SoundManager
                AudioClip rimHitSound = SoundManager.GetRimHit();
                float rimHitVolume = SoundManager.GetRimHitVolume();
                
                if (rimHitSound != null && m_AudioSource != null)
                {
                    // Adjust volume based on collision velocity (louder hits = louder sound)
                    float velocityBasedVolume = Mathf.Clamp01(collisionVelocity / 10f); // Normalize to 0-1 range
                    velocityBasedVolume = Mathf.Clamp(velocityBasedVolume, 0.3f, 1.0f); // Keep between 30% and 100%
                    
                    // Multiply by the configured rim hit volume from SoundManager
                    float finalVolume = velocityBasedVolume * rimHitVolume;
                    
                    float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, finalVolume);
                    m_AudioSource.PlayOneShot(rimHitSound, effectiveVolume);
                    m_LastRimHitTime = Time.time;
                    
                    if (debugLogs)
                    {
                        Debug.Log($"[RimCollisionTracker] Playing rim hit sound. Collision velocity: {collisionVelocity:F2} m/s, Final volume: {finalVolume:F2}", this);
                    }
                }
                else if (rimHitSound == null && debugLogs)
                {
                    Debug.LogWarning("[RimCollisionTracker] Rim hit sound not found in SoundManager! Make sure SoundManager exists in scene and has Rim Hit audio clip assigned.", this);
                }
            }
            else if (collisionVelocity < minCollisionVelocity && debugLogs)
            {
                Debug.Log($"[RimCollisionTracker] Collision velocity too low ({collisionVelocity:F2} < {minCollisionVelocity}), not playing sound.", this);
            }
            
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

