using UnityEngine;

/// <summary>
/// Handles launching basketballs from the ShootingMachine towards a target point.
/// Calculates trajectory with an arc to launch the ball.
/// </summary>
public class ShootingMachineLauncher : MonoBehaviour
{
    [Header("Launch Settings")]
    [SerializeField] private Transform launchPoint;
    
    [SerializeField] private Transform targetPoint;
    
    [Tooltip("Launch angle in degrees (45 = typical arc, higher = steeper arc).")]
    [SerializeField] private float launchAngle = 45f;
    
    [Tooltip("Multiplier to adjust launch velocity. Higher = faster launch.")]
    [SerializeField] private float velocityMultiplier = 1f;

    [Header("Canvas")]
    [Tooltip("Reference to the canvas GameObject. Canvas is always visible now.")]
    [SerializeField] private GameObject canvas;

    [Header("Debug")]
    [Tooltip("Enable to draw debug lines showing the launch trajectory.")]
    [SerializeField] private bool drawDebugTrajectory = false;

    private int m_BallsSpawned = 0;
    private PlayAreaManager m_PlayAreaManager;
    private AudioSource m_AudioSource;

    /// <summary>
    /// Launches a ball from the launch point towards the target point.
    /// </summary>
    /// <param name="ball">The basketball GameObject to launch.</param>
    public void LaunchBall(GameObject ball)
    {
        // Play ball machine sound effect from SoundManager (3D spatial audio)
        AudioClip ballMachineSound = SoundManager.GetBallMachine();
        float ballMachineVolume = SoundManager.GetBallMachineVolume();
        float effectiveVolume = SoundManager.GetEffectiveVolume(transform.position, ballMachineVolume);
        m_AudioSource.PlayOneShot(ballMachineSound, effectiveVolume);

        // Position ball at launch point
        ball.transform.position = launchPoint.position;
        ball.transform.rotation = launchPoint.rotation;

        // Calculate launch velocity
        Vector3 launchVelocity = CalculateLaunchVelocity(launchPoint.position, targetPoint.position, launchAngle);
        launchVelocity *= velocityMultiplier;

        // Apply velocity to ball's rigidbody
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        rb.linearVelocity = launchVelocity;
        rb.angularVelocity = Vector3.zero; // Start with no spin

        if (drawDebugTrajectory)
        {
            DrawTrajectory(launchPoint.position, launchVelocity);
        }

        // Track ball spawn count (canvas is now always visible)
        if (m_PlayAreaManager.IsOwnedByLocalClient())
        {
            m_BallsSpawned++;
        }
    }

    /// <summary>
    /// Calculates the initial velocity needed to launch from startPos to targetPos with the specified angle.
    /// Uses physics equations for projectile motion.
    /// </summary>
    private Vector3 CalculateLaunchVelocity(Vector3 startPos, Vector3 targetPos, float angleDegrees)
    {
        // Calculate horizontal and vertical distances
        Vector3 toTarget = targetPos - startPos;
        float horizontalDistance = new Vector3(toTarget.x, 0, toTarget.z).magnitude;
        float verticalDistance = toTarget.y;

        // Convert angle to radians
        float angleRad = angleDegrees * Mathf.Deg2Rad;

        // Calculate initial velocity magnitude using projectile motion equations
        // v = sqrt((g * d^2) / (2 * cos^2(θ) * (d * tan(θ) - h)))
        // Where: g = gravity, d = horizontal distance, h = vertical distance, θ = launch angle
        
        float gravity = Physics.gravity.magnitude;
        float cosAngle = Mathf.Cos(angleRad);
        float tanAngle = Mathf.Tan(angleRad);
        
        float denominator = 2f * cosAngle * cosAngle * (horizontalDistance * tanAngle - verticalDistance);
        
        // Avoid division by zero or negative values
        if (denominator <= 0f)
        {
            float fallbackVelocityMagnitude = Mathf.Sqrt(gravity * horizontalDistance / Mathf.Sin(2f * angleRad));
            Vector3 direction = toTarget.normalized;
            direction.y = Mathf.Tan(angleRad);
            direction.Normalize();
            return direction * fallbackVelocityMagnitude;
        }
        
        float velocityMagnitude = Mathf.Sqrt((gravity * horizontalDistance * horizontalDistance) / denominator);
        velocityMagnitude = Mathf.Max(velocityMagnitude, 1f); // Minimum velocity

        // Calculate direction
        Vector3 horizontalDirection = new Vector3(toTarget.x, 0, toTarget.z).normalized;
        Vector3 launchDirection = horizontalDirection + Vector3.up * tanAngle;
        launchDirection.Normalize();

        return launchDirection * velocityMagnitude;
    }

    /// <summary>
    /// Draws a debug line showing the predicted trajectory.
    /// </summary>
    private void DrawTrajectory(Vector3 startPos, Vector3 initialVelocity)
    {
        int segments = 50;
        float timeStep = 0.1f;
        Vector3 gravity = Physics.gravity;

        Vector3 previousPoint = startPos;
        for (int i = 1; i <= segments; i++)
        {
            float t = i * timeStep;
            Vector3 point = startPos + initialVelocity * t + 0.5f * gravity * t * t;
            
            Debug.DrawLine(previousPoint, point, Color.yellow, 2f);
            previousPoint = point;
        }
    }

    private void Awake()
    {
        
        // Canvas is always visible now
        canvas.SetActive(true);
        
        // Get or add AudioSource component for 3D spatial audio
        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null)
        {
            m_AudioSource = gameObject.AddComponent<AudioSource>();
        }
        m_AudioSource.playOnAwake = false;
        m_AudioSource.spatialBlend = 1.0f; // 3D sound (full spatial blend)
        m_AudioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Realistic distance falloff
        m_AudioSource.minDistance = 1f;
        m_AudioSource.maxDistance = 50f;
        
        FindPlayAreaManager();
        SubscribeToGameStateChanges();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameStateChanges();
    }

    
    private void FindPlayAreaManager()
    {
        m_PlayAreaManager = GetComponentInParent<PlayAreaManager>();
    }

    /// <summary>
    /// Subscribes to game state changes to reset ball count and unhide canvas when game starts.
    /// </summary>
    private void SubscribeToGameStateChanges()
    {
        m_PlayAreaManager.GameStateChanged += OnGameStateChanged;
    }

    /// <summary>
    /// Unsubscribes from game state changes.
    /// </summary>
    private void UnsubscribeFromGameStateChanges()
    {
        m_PlayAreaManager.GameStateChanged -= OnGameStateChanged;
    }

    /// <summary>
    /// Handles game state changes. Resets ball count when transitioning to Playing state.
    /// Canvas is now always visible.
    /// </summary>
    private void OnGameStateChanged(PlayAreaManager.GameState newState)
    {
        // Canvas is always visible now - no need to control visibility
        
        if (newState == PlayAreaManager.GameState.Playing)
        {
            // Reset ball spawn count when game starts (only for owner)
            if (m_PlayAreaManager.IsOwnedByLocalClient())
            {
                m_BallsSpawned = 0;
            }
        }
    }
}
