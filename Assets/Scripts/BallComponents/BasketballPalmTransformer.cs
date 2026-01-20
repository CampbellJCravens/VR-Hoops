using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Custom grab transformer that positions the basketball in the palm rather than at the fingertips.
/// For right hand: positions ball at 9 o'clock (left side).
/// For left hand: positions ball at 3 o'clock (right side).
/// </summary>
public class BasketballPalmTransformer : XRBaseGrabTransformer
{
    /// <summary>
    /// Register this transformer to run with single grab interactions.
    /// This transformer should run AFTER the default transformer to modify the target pose.
    /// </summary>
    protected override RegistrationMode registrationMode => RegistrationMode.Single;
    [Header("Palm Positioning")]
    [Tooltip("Distance to offset the ball into the palm (perpendicular to hand forward). Positive values move the ball toward the palm.")]
    [SerializeField] private float palmOffsetDistance = 0.08f;
    
    [Tooltip("Additional depth offset to push the ball deeper into the hand (along hand forward). Negative values move deeper.")]
    [SerializeField] private float depthOffset = -0.03f;

    public void SetPalmOffsetDistance(float value) => palmOffsetDistance = value;
    public void SetDepthOffset(float value) => depthOffset = value;

    private IXRSelectInteractor m_Interactor;
    private bool m_IsLeftHand;

    /// <inheritdoc />
    public override void OnGrab(XRGrabInteractable grabInteractable)
    {
        base.OnGrab(grabInteractable);

        if (grabInteractable.interactorsSelecting.Count > 0)
        {
            m_Interactor = grabInteractable.interactorsSelecting[0];
            m_IsLeftHand = m_Interactor.handedness == InteractorHandedness.Left;
        }
    }

    /// <inheritdoc />
    public override void OnGrabCountChanged(XRGrabInteractable grabInteractable, Pose targetPose, Vector3 localScale)
    {
        base.OnGrabCountChanged(grabInteractable, targetPose, localScale);

        if (grabInteractable.interactorsSelecting.Count > 0)
        {
            m_Interactor = grabInteractable.interactorsSelecting[0];
            m_IsLeftHand = m_Interactor.handedness == InteractorHandedness.Left;
        }
    }

    /// <inheritdoc />
    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        // Only process during Dynamic phase to avoid interfering with other transformers
        // Skip if offsets are zero (let default transformer handle it)
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        if (m_Interactor == null || grabInteractable.interactorsSelecting.Count == 0)
            return;

        // Skip if both offsets are zero - let the default transformer handle it
        if (Mathf.Approximately(palmOffsetDistance, 0f) && Mathf.Approximately(depthOffset, 0f))
            return;

        // Get the hand's attach transform orientation
        var interactorAttachTransform = m_Interactor.GetAttachTransform(grabInteractable);
        if (interactorAttachTransform == null)
            return;

        // Get the hand's local coordinate system
        // Note: The hand's forward/right/up vectors are relative to the hand's orientation
        // For Quest controllers/hands, forward typically points where you're pointing
        Vector3 handForward = interactorAttachTransform.forward;
        Vector3 handRight = interactorAttachTransform.right;
        Vector3 handUp = interactorAttachTransform.up;
        
        // Debug: Log the hand orientation (remove after debugging)
        // Debug.Log($"Hand: {(m_IsLeftHand ? "Left" : "Right")}, Forward: {handForward}, Right: {handRight}, Up: {handUp}");
        
        // For right hand: offset to the left (9 o'clock) = negative right direction
        // For left hand: offset to the right (3 o'clock) = positive right direction
        Vector3 palmDirection = m_IsLeftHand ? handRight : -handRight;
        
        // Calculate the offset in world space relative to the hand's attach transform
        // Palm offset: perpendicular to hand forward (left/right) - this moves the ball to the side
        // Depth offset: along hand forward (back into palm) - negative moves it back
        // IMPORTANT: We're NOT using handUp to avoid upward movement
        Vector3 worldOffset = (palmDirection * palmOffsetDistance) + (handForward * depthOffset);
        
        // Apply the offset to the target pose
        // This modifies the pose that was already calculated by the default transformer
        targetPose.position += worldOffset;
    }
}

