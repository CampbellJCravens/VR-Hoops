using UnityEngine;

/// <summary>
/// Tracks the state of a basketball (whether it has scored or hit the rim).
/// Used by the lives system to determine if a life should be lost.
/// </summary>
public class BallStateTracker : MonoBehaviour
{
    private bool m_HasScored = false;
    private bool m_HasHitRim = false;

    /// <summary>
    /// Marks this ball as having scored a basket.
    /// </summary>
    public void MarkAsScored()
    {
        m_HasScored = true;
    }

    /// <summary>
    /// Marks this ball as having hit the rim.
    /// </summary>
    public void MarkAsHitRim()
    {
        m_HasHitRim = true;
    }

    /// <summary>
    /// Checks if this ball has scored a basket.
    /// </summary>
    public bool HasScored()
    {
        return m_HasScored;
    }

    /// <summary>
    /// Checks if this ball has hit the rim.
    /// </summary>
    public bool HasHitRim()
    {
        return m_HasHitRim;
    }

    /// <summary>
    /// Checks if this ball should cause a life loss.
    /// Returns true if the ball did NOT score and did NOT hit the rim.
    /// </summary>
    public bool ShouldLoseLife()
    {
        return !m_HasScored && !m_HasHitRim;
    }
}

