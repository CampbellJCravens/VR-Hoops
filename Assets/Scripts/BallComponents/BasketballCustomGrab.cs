using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Custom grab handler for basketball that positions it in the palm.
/// This replaces the need for the transformer system and gives direct control.
/// 
/// Setup:
/// 1. Add this component to your basketball (alongside XR Grab Interactable)
/// 2. On XR Grab Interactable: Disable "Track Position" and "Track Rotation"
/// 3. This script will handle all positioning manually
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class BasketballCustomGrab : MonoBehaviour
{
    [Header("Grab Settings")]
    [Tooltip("Distance to offset the ball into the palm (perpendicular to hand forward).")]
    [SerializeField] private float palmOffsetDistance = 0.08f;
    
    [Tooltip("Additional depth offset to push the ball deeper into the hand (along hand forward).")]
    [SerializeField] private float depthOffset = -0.03f;
    
    [Tooltip("How fast the ball moves to the target position when grabbed.")]
    [SerializeField] private float grabLerpSpeed = 20f;
    
    [Tooltip("How fast the ball rotates to match hand rotation.")]
    [SerializeField] private float rotationLerpSpeed = 20f;

    [Header("Basketball Rotation")]
    [Tooltip("Enable to lock the basketball rotation to a fixed orientation relative to the hand.")]
    [SerializeField] private bool lockRotation = true;
    
    [Tooltip("Rotation offset in Euler angles (X, Y, Z) to align basketball grooves with fingertips. Adjust these values to fine-tune the alignment.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private Rigidbody m_Rigidbody;
    private XRGrabInteractable m_GrabInteractable;
    private IXRSelectInteractor m_CurrentInteractor;
    private bool m_IsGrabbed;
    private Quaternion m_GrabRotationOffset;

    private void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_GrabInteractable = GetComponent<XRGrabInteractable>();
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

    private void Update()
    {
        if (!m_IsGrabbed || m_CurrentInteractor == null)
            return;

        UpdateGrabPosition();
    }

    public void OnGrab(SelectEnterEventArgs args)
    {
        m_CurrentInteractor = args.interactorObject;
        m_IsGrabbed = true;
        
        // Calculate the grab offset based on hand orientation
        CalculateGrabOffset();
        
        // Store the rotation offset
        var handTransform = m_CurrentInteractor.transform;
        m_GrabRotationOffset = Quaternion.Inverse(handTransform.rotation) * transform.rotation;
    }

    public void OnRelease(SelectExitEventArgs args)
    {
        m_IsGrabbed = false;
        m_CurrentInteractor = null;
    }

    private void CalculateGrabOffset()
    {
        // This method is called when grabbing to store the rotation offset
        // The position offset is calculated dynamically in UpdateGrabPosition
        // No action needed here, but kept for potential future use
    }

    private void UpdateGrabPosition()
    {
        if (m_CurrentInteractor == null)
            return;

        var handTransform = m_CurrentInteractor.transform;
        bool isLeftHand = m_CurrentInteractor.handedness == InteractorHandedness.Left;
        
        // Get hand orientation
        Vector3 handForward = handTransform.forward;
        Vector3 handRight = handTransform.right;
        
        // Calculate palm direction
        Vector3 palmDirection = isLeftHand ? handRight : -handRight;
        
        // Calculate target position
        Vector3 worldOffset = (palmDirection * palmOffsetDistance) + (handForward * depthOffset);
        Vector3 targetPosition = handTransform.position + worldOffset;
        
        // Calculate target rotation
        Quaternion targetRotation;
        if (lockRotation)
        {
            // Use fixed rotation offset relative to hand orientation
            Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
            targetRotation = handTransform.rotation * offsetRotation;
        }
        else
        {
            // Match hand rotation with stored offset (preserves ball's original rotation relative to hand)
            targetRotation = handTransform.rotation * m_GrabRotationOffset;
        }
        
        // Smoothly move and rotate the ball
        Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * grabLerpSpeed);
        Quaternion newRotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
        
        // Apply to rigidbody
        m_Rigidbody.MovePosition(newPosition);
        m_Rigidbody.MoveRotation(newRotation);
        
        // Zero out velocity to keep it stable while grabbed
        // Only set velocity if rigidbody is not kinematic
        if (!m_Rigidbody.isKinematic)
        {
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
        }
    }
}

