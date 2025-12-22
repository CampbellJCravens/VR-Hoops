using UnityEngine;

/// <summary>
/// Tracks when a basketball collides with the backboard.
/// Attach this to the backboard collider GameObject.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BackboardCollisionTracker : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag that identifies basketball objects.")]
    [SerializeField] private string ballTag = "Ball";
    
    [Tooltip("Minimum collision velocity magnitude to trigger backboard hit sound. Prevents quiet touches from playing sound.")]
    [SerializeField] private float minCollisionVelocity = 0.5f;
    
    [Tooltip("Cooldown time (seconds) between backboard hit sounds to prevent rapid, overlapping playback.")]
    [SerializeField] private float backboardHitCooldown = 0.1f;

    [Header("Debug")]
    [Tooltip("Enable to log backboard collision events.")]
    [SerializeField] private bool debugLogs = false;

    private AudioSource m_AudioSource;
    private float m_LastBackboardHitTime = 0f;

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

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the colliding object is a ball
        GameObject ball = FindBallRoot(collision.gameObject);
        if (ball != null)
        {
            // Get collision velocity for sound volume scaling
            float collisionVelocity = collision.relativeVelocity.magnitude;
            
            // Check if collision is strong enough and cooldown has passed
            if (collisionVelocity >= minCollisionVelocity && Time.time - m_LastBackboardHitTime >= backboardHitCooldown)
            {
                // Play backboard hit sound effect from SoundManager
                AudioClip backboardHitSound = SoundManager.GetBackboardHit();
                float backboardHitVolume = SoundManager.GetBackboardHitVolume();
                
                if (backboardHitSound != null && m_AudioSource != null)
                {
                    // Adjust volume based on collision velocity (louder hits = louder sound)
                    float velocityBasedVolume = Mathf.Clamp01(collisionVelocity / 10f); // Normalize to 0-1 range
                    velocityBasedVolume = Mathf.Clamp(velocityBasedVolume, 0.3f, 1.0f); // Keep between 30% and 100%
                    
                    // Multiply by the configured backboard hit volume from SoundManager
                    float finalVolume = velocityBasedVolume * backboardHitVolume;
                    
                    float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, finalVolume);
                    m_AudioSource.PlayOneShot(backboardHitSound, effectiveVolume);
                    m_LastBackboardHitTime = Time.time;
                    
                    if (debugLogs)
                    {
                        Debug.Log($"[BackboardCollisionTracker] Playing backboard hit sound. Collision velocity: {collisionVelocity:F2} m/s, Final volume: {finalVolume:F2}", this);
                    }
                }
                else if (backboardHitSound == null && debugLogs)
                {
                    Debug.LogWarning("[BackboardCollisionTracker] Backboard hit sound not found in SoundManager! Make sure SoundManager exists in scene and has Backboard Hit audio clip assigned.", this);
                }
            }
            else if (collisionVelocity < minCollisionVelocity && debugLogs)
            {
                Debug.Log($"[BackboardCollisionTracker] Collision velocity too low ({collisionVelocity:F2} < {minCollisionVelocity}), not playing sound.", this);
            }
            
            if (debugLogs)
                Debug.Log($"[BackboardCollisionTracker] Ball {ball.name} hit the backboard.", this);
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
            Debug.LogWarning("[BackboardCollisionTracker] Backboard collider should not be a trigger. Setting isTrigger = false.", this);
            col.isTrigger = false;
        }
    }
}

