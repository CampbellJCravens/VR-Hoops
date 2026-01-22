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

    public enum HoopTriggerPart
    {
        Top,
        Middle,
        Bottom
    }

    private void OnTriggerEnter(Collider other)
    {
        scorer.OnBallTriggerEnter(part, other);
    }

    private void OnTriggerExit(Collider other)
    {
        scorer.OnBallTriggerExit(part, other);
    }

    private void OnValidate()
    {
        // Ensure the collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }
}
