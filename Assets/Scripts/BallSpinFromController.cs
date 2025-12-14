using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// BallSpinFromController - Controller-driven ball spin system for VR basketball
/// 
/// This script samples the controller/hand's angular velocity while the ball is grabbed,
/// and applies that spin to the ball when released. This creates realistic ball rotation
/// based on wrist motion during the shooting motion.
/// 
/// TUNING GUIDE:
/// - spinMultiplier: How much controller rotation affects ball spin (1.0 = 1:1, 2.0 = double)
///   Start with 1.0 and increase if spin is too weak, decrease if too strong
/// 
/// - maxSpin: Maximum angular velocity magnitude (rad/s) to prevent unrealistic spins
///   Typical basketball: 10-30 rad/s. Start with 20 and adjust based on feel
/// 
/// - minReleaseSpeed: Only apply spin if ball is thrown with at least this speed (m/s)
///   Prevents wobbling when just dropping the ball. Start with 0.5 m/s
/// 
/// - sampleWindowSeconds: How many seconds of angular velocity history to average
///   Longer = smoother but less responsive. Start with 0.15 seconds (150ms)
/// 
/// XR INPUTS USED:
/// - For controllers: InputDevice.TryGetFeatureValue(CommonUsages.deviceAngularVelocity)
/// - For hand tracking: Calculated from rotation changes over time
/// 
/// INTEGRATION NOTES:
/// - This script adds to the angular velocity set by XR Grab Interactable on release
/// - If you want ONLY this script to control spin, set XR Grab Interactable's 
///   "Throw Angular Velocity Scale" to 0
/// - If you want both systems to contribute, leave "Throw Angular Velocity Scale" at 1
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class BallSpinFromController : MonoBehaviour
{
    // BEGIN BALL SPIN
    
    [Header("Spin Settings")]
    [Tooltip("Multiplier for how much controller rotation affects ball spin. Higher = more spin.")]
    [SerializeField] private float spinMultiplier = 1.0f;
    
    [Tooltip("Maximum angular velocity magnitude (rad/s) to prevent unrealistic spins.")]
    [SerializeField] private float maxSpin = 20f;
    
    [Tooltip("Minimum release speed (m/s) required to apply spin. Prevents wobbling when dropping.")]
    [SerializeField] private float minReleaseSpeed = 0.5f;
    
    [Tooltip("How many seconds of angular velocity history to average before release.")]
    [SerializeField] private float sampleWindowSeconds = 0.15f;
    
    [Header("Debug")]
    [Tooltip("Enable to log spin values and draw debug lines showing spin axis.")]
    [SerializeField] private bool debug = false;

    // Internal state
    private XRGrabInteractable m_GrabInteractable;
    private Rigidbody m_Rigidbody;
    private IXRSelectInteractor m_CurrentInteractor;
    private bool m_IsGrabbed;
    
    // Angular velocity sampling
    private struct AngularVelocitySample
    {
        public Vector3 angularVelocity;
        public float time;
    }
    
    private List<AngularVelocitySample> m_AngularVelocitySamples = new List<AngularVelocitySample>(20);
    private Quaternion m_LastRotation;
    private float m_LastRotationTime;
    private InputDevice? m_InputDevice;
    private XRNode m_XRNode;
    
    // Store desired angular velocity to apply after XR's detach
    private Vector3 m_PendingAngularVelocity;
    private bool m_HasPendingSpin;

    private void Awake()
    {
        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        m_Rigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectEntered.AddListener(OnGrab);
            m_GrabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDisable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectEntered.RemoveListener(OnGrab);
            m_GrabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void FixedUpdate()
    {
        if (m_IsGrabbed && m_CurrentInteractor != null)
        {
            SampleAngularVelocity();
        }
    }
    
    private IEnumerator ApplySpinAtEndOfFrame()
    {
        // Wait for end of frame to ensure XR's Detach() has run (which happens in LateUpdate)
        yield return new WaitForEndOfFrame();
        
        // Wait for the next FixedUpdate to apply the spin
        // This ensures physics will use the angular velocity we set
        yield return new WaitForFixedUpdate();
        
        if (m_HasPendingSpin)
        {
            // Set the angular velocity directly (XR's detach may have zeroed it)
            // We add to existing in case XR also set some angular velocity
            Vector3 currentAngularVel = m_Rigidbody.angularVelocity;
            m_Rigidbody.angularVelocity = currentAngularVel + m_PendingAngularVelocity;
            
            // Clear the flag
            m_HasPendingSpin = false;
            
            if (debug)
            {
                Debug.Log($"[BallSpinFromController] Applied spin after XR detach. Angular velocity: {m_Rigidbody.angularVelocity} (magnitude: {m_Rigidbody.angularVelocity.magnitude:F2} rad/s)");
            }
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        m_CurrentInteractor = args.interactorObject;
        m_IsGrabbed = true;
        
        // Clear previous samples
        m_AngularVelocitySamples.Clear();
        
        // Try to get InputDevice for direct angular velocity reading
        m_InputDevice = null;
        m_XRNode = XRNode.LeftHand; // Default
        
        // Determine which hand/controller
        if (m_CurrentInteractor.handedness == InteractorHandedness.Left)
        {
            m_XRNode = XRNode.LeftHand;
        }
        else if (m_CurrentInteractor.handedness == InteractorHandedness.Right)
        {
            m_XRNode = XRNode.RightHand;
        }
        
        // Try to get the InputDevice
        var inputDevices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(m_XRNode, inputDevices);
        if (inputDevices.Count > 0)
        {
            m_InputDevice = inputDevices[0];
        }
        
        // Initialize rotation tracking for hand tracking fallback
        if (m_CurrentInteractor.transform != null)
        {
            m_LastRotation = m_CurrentInteractor.transform.rotation;
            m_LastRotationTime = Time.time;
        }
        
        if (debug)
        {
            Debug.Log($"[BallSpinFromController] Grabbed with {(m_InputDevice.HasValue ? "controller" : "hand tracking")} on {m_XRNode}");
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (!m_IsGrabbed)
            return;
            
        m_IsGrabbed = false;
        
        // Calculate average angular velocity from samples
        Vector3 averageAngularVelocity = CalculateAverageAngularVelocity();
        
        // Get the ball's linear velocity at release
        Vector3 releaseVelocity = m_Rigidbody.linearVelocity;
        float releaseSpeed = releaseVelocity.magnitude;
        
        // Only apply spin if ball is thrown with sufficient speed
        if (releaseSpeed >= minReleaseSpeed)
        {
            // Apply spin multiplier
            Vector3 finalAngularVelocity = averageAngularVelocity * spinMultiplier;
            
            // Clamp to max spin
            if (finalAngularVelocity.magnitude > maxSpin)
            {
                finalAngularVelocity = finalAngularVelocity.normalized * maxSpin;
            }
            
            // Store the desired angular velocity to apply after XR's detach
            // XR Grab Interactable's Detach() runs in LateUpdate and will overwrite
            // our angular velocity, so we need to apply it after that
            m_PendingAngularVelocity = finalAngularVelocity;
            m_HasPendingSpin = true;
            
            // Start coroutine to apply spin at end of frame (after XR's LateUpdate)
            StartCoroutine(ApplySpinAtEndOfFrame());
            
            if (debug)
            {
                Debug.Log($"[BallSpinFromController] Released with spin: {finalAngularVelocity} (magnitude: {finalAngularVelocity.magnitude:F2} rad/s, release speed: {releaseSpeed:F2} m/s)");
            }
        }
        else
        {
            m_HasPendingSpin = false;
            if (debug)
            {
                Debug.Log($"[BallSpinFromController] Release speed too low ({releaseSpeed:F2} < {minReleaseSpeed}), not applying spin");
            }
        }
        
        // Clean up
        m_AngularVelocitySamples.Clear();
        m_CurrentInteractor = null;
        m_InputDevice = null;
    }

    private void SampleAngularVelocity()
    {
        Vector3 angularVelocity = Vector3.zero;
        bool hasAngularVelocity = false;
        
        // Try to get angular velocity from InputDevice (controllers)
        if (m_InputDevice.HasValue && m_InputDevice.Value.isValid)
        {
            if (m_InputDevice.Value.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out angularVelocity))
            {
                // Transform from controller local space to world space
                // The angular velocity from InputDevice is in the controller's local coordinate space
                if (m_CurrentInteractor != null && m_CurrentInteractor.transform != null)
                {
                    Transform controllerTransform = m_CurrentInteractor.transform;
                    // Transform the angular velocity vector from local to world space
                    angularVelocity = controllerTransform.TransformDirection(angularVelocity);
                }
                hasAngularVelocity = true;
            }
        }
        
        // Fallback: Calculate from rotation changes (hand tracking or if device doesn't support angular velocity)
        if (!hasAngularVelocity && m_CurrentInteractor != null && m_CurrentInteractor.transform != null)
        {
            Quaternion currentRotation = m_CurrentInteractor.transform.rotation;
            float currentTime = Time.time;
            float deltaTime = currentTime - m_LastRotationTime;
            
            if (deltaTime > 0.001f) // Avoid division by zero
            {
                // Calculate angular velocity from rotation difference
                Quaternion rotationDelta = currentRotation * Quaternion.Inverse(m_LastRotation);
                
                // Convert quaternion delta to angular velocity
                // This is an approximation: angular velocity ≈ 2 * (rotationDelta.axis * rotationDelta.angle) / deltaTime
                float angle;
                Vector3 axis;
                rotationDelta.ToAngleAxis(out angle, out axis);
                
                // Normalize angle to [-180, 180] range
                if (angle > 180f)
                    angle -= 360f;
                
                // Convert to radians per second
                // The axis is already in world space since we're using world rotations
                angularVelocity = (axis * (angle * Mathf.Deg2Rad)) / deltaTime;
                hasAngularVelocity = true;
                
                m_LastRotation = currentRotation;
                m_LastRotationTime = currentTime;
            }
        }
        
        // Store sample if we have valid data
        if (hasAngularVelocity)
        {
            AngularVelocitySample sample = new AngularVelocitySample
            {
                angularVelocity = angularVelocity,
                time = Time.time
            };
            
            m_AngularVelocitySamples.Add(sample);
            
            // Remove samples outside the window
            float cutoffTime = Time.time - sampleWindowSeconds;
            m_AngularVelocitySamples.RemoveAll(s => s.time < cutoffTime);
        }
    }

    private Vector3 CalculateAverageAngularVelocity()
    {
        if (m_AngularVelocitySamples.Count == 0)
            return Vector3.zero;
        
        // Simple average of all samples in the window
        // Could be improved with weighted average (more recent = higher weight)
        Vector3 sum = Vector3.zero;
        foreach (var sample in m_AngularVelocitySamples)
        {
            sum += sample.angularVelocity;
        }
        
        return sum / m_AngularVelocitySamples.Count;
    }
    

    private void OnDrawGizmos()
    {
        if (!debug || !m_IsGrabbed || m_CurrentInteractor == null)
            return;
        
        // Draw debug line showing current angular velocity
        Vector3 avgAngularVelocity = CalculateAverageAngularVelocity();
        if (avgAngularVelocity.magnitude > 0.01f)
        {
            Vector3 spinAxis = avgAngularVelocity.normalized;
            float spinMagnitude = avgAngularVelocity.magnitude;
            
            // Draw axis line
            Gizmos.color = Color.yellow;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + spinAxis * (spinMagnitude * 0.1f); // Scale for visibility
            Gizmos.DrawLine(startPos, endPos);
            
            // Draw arrow head
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(endPos, 0.02f);
        }
    }
    
    // END BALL SPIN
}

