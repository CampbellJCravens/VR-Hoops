using UnityEngine;

/// <summary>
/// Attached to each trigger collider (TopTrigger, MiddleTrigger, BottomTrigger).
/// Forwards trigger events to the HoopScorer component.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HoopTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Which part of the hoop this trigger represents.")]
    public HoopTriggerPart part;
    
    [Tooltip("Reference to the HoopScorer component on the hoop root.")]
    public HoopScorer scorer;

    [Header("Debug")]
    [Tooltip("Enable to log all trigger events for this trigger.")]
    public bool debugLogs = false;

    public enum HoopTriggerPart
    {
        Top,
        Middle,
        Bottom
    }

    private void OnTriggerEnter(Collider other)
    {
        if (debugLogs)
        {
            Debug.Log($"[HoopTrigger] {part} trigger ENTERED by: {other.gameObject.name} (Tag: {other.tag})", this);
        }

        if (scorer != null)
        {
            scorer.OnBallTriggerEnter(part, other);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[HoopTrigger] {part} trigger has no scorer reference assigned!", this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (debugLogs)
        {
            Debug.Log($"[HoopTrigger] {part} trigger EXITED by: {other.gameObject.name} (Tag: {other.tag})", this);
        }

        if (scorer != null)
        {
            scorer.OnBallTriggerExit(part, other);
        }
    }

    private void OnValidate()
    {
        // Ensure the collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[HoopTrigger] Collider on {gameObject.name} should be a trigger. Setting isTrigger = true.", this);
            col.isTrigger = true;
        }
    }
}

