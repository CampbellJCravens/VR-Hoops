using UnityEngine;

/// <summary>
/// Handles launching basketballs from the ShootingMachine towards a target point.
/// Calculates trajectory with an arc to launch the ball.
/// </summary>
public class ShootingMachineLauncher : MonoBehaviour
{
    [Header("Launch Settings")]
    [Tooltip("Transform where the ball should spawn and launch from (usually a child of Cannon).")]
    [SerializeField] private Transform launchPoint;
    
    [Tooltip("Target transform to launch towards (usually PlayerShootingPoint). If not assigned, will search parent PlayArea.")]
    [SerializeField] private Transform targetPoint;
    
    [Tooltip("Launch angle in degrees (45 = typical arc, higher = steeper arc).")]
    [SerializeField] private float launchAngle = 45f;
    
    [Tooltip("Multiplier to adjust launch velocity. Higher = faster launch.")]
    [SerializeField] private float velocityMultiplier = 1f;

    [Header("Canvas")]
    [Tooltip("Reference to the canvas GameObject. Will be hidden after spawning specified number of balls.")]
    [SerializeField] private GameObject canvas;

    [Header("Canvas Visibility Settings")]
    [Tooltip("Number of balls to spawn before hiding the canvas.")]
    [SerializeField] private int hideCanvasAfterBalls = 2;

    [Header("Debug")]
    [Tooltip("Enable to draw debug lines showing the launch trajectory.")]
    [SerializeField] private bool drawDebugTrajectory = false;

    private int m_BallsSpawned = 0;
    private PlayAreaManager m_PlayAreaManager;

    /// <summary>
    /// Launches a ball from the launch point towards the target point.
    /// </summary>
    /// <param name="ball">The basketball GameObject to launch.</param>
    public void LaunchBall(GameObject ball)
    {
        if (ball == null)
        {
            Debug.LogWarning("[ShootingMachineLauncher] Cannot launch null ball.", this);
            return;
        }

        if (launchPoint == null)
        {
            Debug.LogError("[ShootingMachineLauncher] Launch point is not assigned!", this);
            return;
        }

        if (targetPoint == null)
        {
            Debug.LogError("[ShootingMachineLauncher] Target point is not assigned!", this);
            return;
        }

        // Position ball at launch point
        ball.transform.position = launchPoint.position;
        ball.transform.rotation = launchPoint.rotation;

        // Calculate launch velocity
        Vector3 launchVelocity = CalculateLaunchVelocity(launchPoint.position, targetPoint.position, launchAngle);
        launchVelocity *= velocityMultiplier;

        // Apply velocity to ball's rigidbody
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
#if UNITY_2023_3_OR_NEWER
            rb.linearVelocity = launchVelocity;
#else
            rb.velocity = launchVelocity;
#endif
            rb.angularVelocity = Vector3.zero; // Start with no spin
        }
        else
        {
            Debug.LogWarning("[ShootingMachineLauncher] Ball does not have a Rigidbody component!", this);
        }

        if (drawDebugTrajectory)
        {
            DrawTrajectory(launchPoint.position, launchVelocity);
        }

        // Track ball spawn count and hide canvas if threshold reached (only for owner)
        if (m_PlayAreaManager != null && m_PlayAreaManager.IsOwnedByLocalClient())
        {
            m_BallsSpawned++;
            if (canvas != null && m_BallsSpawned >= hideCanvasAfterBalls)
            {
                canvas.SetActive(false);
            }
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
            // Fallback: use a simple calculation
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
        AutoFindReferences();
        
        // Hide canvas by default
        if (canvas != null)
        {
            canvas.SetActive(false);
        }
        
        FindPlayAreaManager();
        SubscribeToGameStateChanges();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameStateChanges();
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        // Auto-find launch point if not assigned
        if (launchPoint == null)
        {
            Transform cannon = transform.Find("Cannon");
            if (cannon != null)
            {
                launchPoint = cannon.Find("LaunchPosition");
                if (launchPoint == null)
                {
                    // Try alternative names
                    launchPoint = cannon.Find("Launch Point");
                    if (launchPoint == null)
                    {
                        launchPoint = cannon.Find("LaunchPoint");
                    }
                }
            }
        }

        // Auto-find target point if not assigned (search parent PlayArea)
        if (targetPoint == null)
        {
            // Search up the hierarchy for PlayAreaManager
            PlayAreaManager playArea = GetComponentInParent<PlayAreaManager>();
            if (playArea != null)
            {
                targetPoint = playArea.GetPlayerShootingPoint();
            }
            else
            {
                // Try finding by name in parent
                Transform parent = transform.parent;
                while (parent != null)
                {
                    Transform found = parent.Find("PlayerShootingPoint");
                    if (found == null)
                    {
                        found = parent.Find("Player Shooting Point");
                    }
                    if (found != null)
                    {
                        targetPoint = found;
                        break;
                    }
                    parent = parent.parent;
                }
            }
        }
    }

    /// <summary>
    /// Finds the PlayAreaManager in the parent hierarchy.
    /// </summary>
    private void FindPlayAreaManager()
    {
        m_PlayAreaManager = GetComponentInParent<PlayAreaManager>();
        if (m_PlayAreaManager == null)
        {
            // Try searching up the hierarchy
            Transform parent = transform.parent;
            while (parent != null && m_PlayAreaManager == null)
            {
                m_PlayAreaManager = parent.GetComponent<PlayAreaManager>();
                parent = parent.parent;
            }
        }
    }

    /// <summary>
    /// Subscribes to game state changes to reset ball count and unhide canvas when game starts.
    /// </summary>
    private void SubscribeToGameStateChanges()
    {
        if (m_PlayAreaManager != null)
        {
            m_PlayAreaManager.GameStateChanged += OnGameStateChanged;
        }
    }

    /// <summary>
    /// Unsubscribes from game state changes.
    /// </summary>
    private void UnsubscribeFromGameStateChanges()
    {
        if (m_PlayAreaManager != null)
        {
            m_PlayAreaManager.GameStateChanged -= OnGameStateChanged;
        }
    }

    /// <summary>
    /// Handles game state changes. Unhides canvas and resets ball count when transitioning to Playing state.
    /// Only shows canvas for the client that owns this play area.
    /// </summary>
    private void OnGameStateChanged(PlayAreaManager.GameState newState)
    {
        // Only show/hide canvas for the client that owns this play area
        if (m_PlayAreaManager == null || !m_PlayAreaManager.IsOwnedByLocalClient())
        {
            // This client doesn't own the play area, ensure canvas is hidden
            if (canvas != null)
            {
                canvas.SetActive(false);
            }
            return;
        }

        if (newState == PlayAreaManager.GameState.Playing)
        {
            // Reset ball spawn count and unhide canvas when game starts (only for owner)
            m_BallsSpawned = 0;
            if (canvas != null)
            {
                canvas.SetActive(true);
            }
        }
    }
}

