using UnityEngine;

/// <summary>
/// Tracks when a basketball collides with a wall piece in the PlayArea.
/// Attach this to wall piece collider GameObjects.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallCollisionTracker : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag that identifies basketball objects.")]
    [SerializeField] private string ballTag = "Ball";
    
    [Tooltip("Minimum collision velocity magnitude to trigger wall hit sound. Prevents quiet touches from playing sound.")]
    [SerializeField] private float minCollisionVelocity = 0.5f;
    
    [Tooltip("Cooldown time (seconds) between wall hit sounds to prevent rapid, overlapping playback.")]
    [SerializeField] private float wallHitCooldown = 0.1f;

    private AudioSource m_AudioSource;
    private float m_LastWallHitTime = 0f;

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
        
        // Get collision velocity for sound volume scaling
        float collisionVelocity = collision.relativeVelocity.magnitude;
        
        // Check if collision is strong enough and cooldown has passed
        if (collisionVelocity >= minCollisionVelocity && Time.time - m_LastWallHitTime >= wallHitCooldown)
        {
            // Play wall hit sound effect from SoundManager
            AudioClip wallHitSound = SoundManager.GetWallHit();
            float wallHitVolume = SoundManager.GetWallHitVolume();
            
            // Adjust volume based on collision velocity (louder hits = louder sound)
            float velocityBasedVolume = Mathf.Clamp01(collisionVelocity / 10f); // Normalize to 0-1 range
            velocityBasedVolume = Mathf.Clamp(velocityBasedVolume, 0.3f, 1.0f); // Keep between 30% and 100%
            
            // Multiply by the configured wall hit volume from SoundManager
            float finalVolume = velocityBasedVolume * wallHitVolume;
            
            float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, finalVolume);
            m_AudioSource.PlayOneShot(wallHitSound, effectiveVolume);
            m_LastWallHitTime = Time.time;
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
        if (col.isTrigger)
        {
            Debug.LogWarning("[WallCollisionTracker] Wall collider should not be a trigger. Setting isTrigger = false.", this);
            col.isTrigger = false;
        }
    }
}
