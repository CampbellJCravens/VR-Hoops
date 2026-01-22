using UnityEngine;

/// <summary>
/// Debug helper to verify hoop scoring system setup.
/// Attach this to the hoop root to check if everything is configured correctly.
/// </summary>
public class HoopScorerDebugger : MonoBehaviour
{
    [ContextMenu("Check Setup")]
    public void CheckSetup()
    {
        Debug.Log("=== HOOP SCORER SETUP CHECK ===");
        
        // Check HoopScorer
        HoopScorer scorer = GetComponent<HoopScorer>();
        Debug.Log($"✅ HoopScorer found. Ball Tag: '{scorer.ballTag}'");
        
        // Check triggers
        HoopTrigger[] triggers = GetComponentsInChildren<HoopTrigger>();
        Debug.Log($"Found {triggers.Length} HoopTrigger components");
        
        bool hasTop = false, hasMiddle = false, hasBottom = false;
        
        foreach (var trigger in triggers)
        {
            Collider col = trigger.GetComponent<Collider>();
            bool isTrigger = col.isTrigger;
            bool hasScorer = trigger.scorer != null;
            
            string status = (isTrigger && hasScorer) ? "✅" : "❌";
            Debug.Log($"{status} {trigger.part} Trigger: isTrigger={isTrigger}, hasScorer={hasScorer}");
            
            if (trigger.part == HoopTrigger.HoopTriggerPart.Top) hasTop = true;
            if (trigger.part == HoopTrigger.HoopTriggerPart.Middle) hasMiddle = true;
            if (trigger.part == HoopTrigger.HoopTriggerPart.Bottom) hasBottom = true;
        }
        
        if (!hasTop) Debug.LogError("❌ Missing Top trigger!");
        if (!hasMiddle) Debug.LogError("❌ Missing Middle trigger!");
        if (!hasBottom) Debug.LogError("❌ Missing Bottom trigger!");
        
        Debug.Log("=== END SETUP CHECK ===");
    }
}
