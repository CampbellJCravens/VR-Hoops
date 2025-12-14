using UnityEngine;

/// <summary>
/// Helper component that forwards OnTriggerEnter events to a ShotCounterManager.
/// This component is automatically added to trigger colliders by ShotCounterManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShotCounterTriggerForwarder : MonoBehaviour
{
    private ShotCounterManager m_Manager;

    /// <summary>
    /// Sets the ShotCounterManager that should receive trigger events.
    /// </summary>
    public void SetManager(ShotCounterManager manager)
    {
        m_Manager = manager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_Manager != null)
        {
            // Forward the trigger event to the manager
            // Pass 'other' (the collider that entered) so the manager can process the ball
            m_Manager.OnTriggerEntered(other);
        }
    }
}

