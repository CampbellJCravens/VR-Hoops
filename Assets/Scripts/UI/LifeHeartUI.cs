using UnityEngine;
using TMPro;

/// <summary>
/// Manages the LifeHeart UI element on the ScreenCanvas.
/// Updates the "Amount" text when called by an animation event.
/// Attach this to the LifeHeart GameObject (ScreenCanvas > Rotator > Lost A Life).
/// </summary>
public class LifeHeartUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("TextMeshProUGUI component that displays the life count. Should be the 'Amount' text child of this GameObject.")]
    [SerializeField] private TextMeshProUGUI amountText;
    
    [Header("Animation")]
    [Tooltip("Animator component for the LifeHeart animation controller.")]
    [SerializeField] private Animator lifeHeartAnimator;
    
    private PlayAreaManager m_ActivePlayArea;

    // Gets the active PlayAreaManager (the one in Playing state that is owned by the local client).
    private PlayAreaManager GetActivePlayAreaManager()
    {
        // If we have a cached active PlayArea, check if it's still in Playing state and owned by us
        if (m_ActivePlayArea != null && 
            m_ActivePlayArea.GetGameState() == PlayAreaManager.GameState.Playing &&
            m_ActivePlayArea.IsOwnedByLocalClient())
        {
            return m_ActivePlayArea;
        }
        
        // Otherwise, search for the PlayArea in Playing state that we own
        m_ActivePlayArea = null;
        
        PlayAreaManager[] allPlayAreas = FindObjectsByType<PlayAreaManager>(FindObjectsSortMode.None);
        foreach (PlayAreaManager playArea in allPlayAreas)
        {
            if (playArea.GetGameState() == PlayAreaManager.GameState.Playing && 
                playArea.IsOwnedByLocalClient())
            {
                m_ActivePlayArea = playArea;
                return m_ActivePlayArea;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Callback function to be called by animation event.
    /// Updates the "Amount" text to show the current life count.
    /// </summary>
    public void UpdateLifeAmount()
    {
        PlayAreaManager activePlayArea = GetActivePlayAreaManager();
        if (activePlayArea == null) { return; }
        ScoreManager scoreManager = activePlayArea.GetScoreManager();
        int currentLives = scoreManager.GetLives();
        amountText.text = currentLives.ToString();
    }
    
    /// <summary>
    /// Triggers the GainLife animation on the LifeHeart Animator.
    /// Called when a life is gained.
    /// </summary>
    public void PlayGainLifeAnimation()
    {
        // Look for trigger parameters in the animator
        bool triggerFound = false;
        if (lifeHeartAnimator.parameters.Length > 0)
        {
            // Look for a trigger parameter (prioritize "GainLife" and other common names)
            string[] preferredTriggers = { "GainLife", "Gain", "AddLife", "LifeGained", "Play", "Show" };
            
            // First, try preferred trigger names
            foreach (string preferredName in preferredTriggers)
            {
                foreach (AnimatorControllerParameter param in lifeHeartAnimator.parameters)
                {
                    if (param.type == AnimatorControllerParameterType.Trigger && param.name == preferredName)
                    {
                        lifeHeartAnimator.SetTrigger(preferredName);
                        triggerFound = true;
                        break;
                    }
                }
                if (triggerFound) break;
            }
            
            // If no preferred trigger found, use the first trigger parameter
            if (!triggerFound)
            {
                foreach (AnimatorControllerParameter param in lifeHeartAnimator.parameters)
                {
                    if (param.type == AnimatorControllerParameterType.Trigger)
                    {
                        lifeHeartAnimator.SetTrigger(param.name);
                        triggerFound = true;
                        break;
                    }
                }
            }
        }
    }
}
