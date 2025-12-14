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
        if (scorer == null)
        {
            Debug.LogError("❌ No HoopScorer component found on " + gameObject.name);
        }
        else
        {
            Debug.Log($"✅ HoopScorer found. Ball Tag: '{scorer.ballTag}'");
        }
        
        // Check triggers
        HoopTrigger[] triggers = GetComponentsInChildren<HoopTrigger>();
        Debug.Log($"Found {triggers.Length} HoopTrigger components");
        
        bool hasTop = false, hasMiddle = false, hasBottom = false;
        
        foreach (var trigger in triggers)
        {
            Collider col = trigger.GetComponent<Collider>();
            bool isTrigger = col != null && col.isTrigger;
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
        
        // Check for Ball tag
        GameObject[] balls = GameObject.FindGameObjectsWithTag("Ball");
        Debug.Log($"Found {balls.Length} GameObject(s) with 'Ball' tag");
        foreach (var ball in balls)
        {
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            Collider col = ball.GetComponent<Collider>();
            Collider[] childColliders = ball.GetComponentsInChildren<Collider>();
            Debug.Log($"  - {ball.name}: Rigidbody={rb != null}, Collider (self)={col != null}, Colliders (children)={childColliders.Length}");
            foreach (var childCol in childColliders)
            {
                Debug.Log($"    └─ {childCol.gameObject.name}: IsTrigger={childCol.isTrigger}, Enabled={childCol.enabled}");
            }
        }
        
        // Check ScoreManager (find through PlayArea hierarchy)
        ScoreManager scoreManager = ScoreManager.FindScoreManagerFor(gameObject);
        if (scoreManager == null)
        {
            Debug.LogWarning("⚠️ No ScoreManager found for this hoop (should be in PlayArea hierarchy)");
        }
        else
        {
            Debug.Log($"✅ ScoreManager found: {scoreManager.gameObject.name}");
        }
        
        Debug.Log("=== END SETUP CHECK ===");
    }
}

