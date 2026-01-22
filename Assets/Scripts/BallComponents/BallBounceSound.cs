using UnityEngine;

/// <summary>
/// Plays a bounce sound effect when the basketball collides with the ground or other surfaces.
/// Attach this to the basketball GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallBounceSound : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Minimum collision velocity magnitude to trigger bounce sound. Prevents quiet touches from playing sound.")]
    [SerializeField] private float minCollisionVelocity = 0.5f;
    

    private AudioSource m_AudioSource;
    private Rigidbody m_Rigidbody;
    private float m_LastBounceTime = 0f;
    private float m_BounceCooldown = 0.1f; // Prevent multiple sounds from rapid bounces

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        
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
        // Check if collision is strong enough to warrant a sound
        float collisionVelocity = collision.relativeVelocity.magnitude;
        
        if (collisionVelocity < minCollisionVelocity)
        {
            return;
        }
        
        // Cooldown to prevent multiple sounds from rapid bounces
        if (Time.time - m_LastBounceTime < m_BounceCooldown)
        {
            return;
        }
        
        // Play bounce sound effect from SoundManager
        AudioClip bounceSound = SoundManager.GetBallBounce();
        
        // Adjust volume based on collision velocity (louder bounces = louder sound)
        float velocityBasedVolume = Mathf.Clamp01(collisionVelocity / 10f); // Normalize to 0-1 range, assuming max velocity around 10 m/s
        velocityBasedVolume = Mathf.Clamp(velocityBasedVolume, 0.3f, 1.0f); // Keep volume between 30% and 100%
        
        // Multiply by the configured bounce volume from SoundManager
        float configuredVolume = SoundManager.GetBallBounceVolume();
        float finalVolume = velocityBasedVolume * configuredVolume;
        
        float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, finalVolume);
        m_AudioSource.PlayOneShot(bounceSound, effectiveVolume);
        m_LastBounceTime = Time.time;
    }
}

