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
    [Tooltip("Animator component for the LifeHeart animation controller. If not assigned, will try to find on this GameObject.")]
    [SerializeField] private Animator lifeHeartAnimator;
    
    [Header("Debug")]
    [Tooltip("Enable to see detailed logging.")]
    [SerializeField] private bool debugLogs = false;
    
    private ScoreManager m_ScoreManager;

    private void Awake()
    {
        // Auto-find Amount text if not assigned
        if (amountText == null)
        {
            amountText = GetComponentInChildren<TextMeshProUGUI>();
            if (amountText != null && debugLogs)
            {
                Debug.Log($"[LifeHeartUI] Auto-found Amount text: {amountText.gameObject.name}", this);
            }
        }
        
        if (amountText == null && debugLogs)
        {
            Debug.LogWarning("[LifeHeartUI] Amount text not found! Please assign it manually in the Inspector.", this);
        }
        
        // Auto-find Animator if not assigned
        if (lifeHeartAnimator == null)
        {
            lifeHeartAnimator = GetComponent<Animator>();
            if (lifeHeartAnimator != null && debugLogs)
            {
                Debug.Log($"[LifeHeartUI] Auto-found Animator: {lifeHeartAnimator.gameObject.name}", this);
            }
        }
        
        if (lifeHeartAnimator == null && debugLogs)
        {
            Debug.LogWarning("[LifeHeartUI] Animator not found! GainLife animation will not play.", this);
        }
    }

    private void Start()
    {
        // Find ScoreManager
        FindScoreManager();
    }

    private void FindScoreManager()
    {
        // Try to find ScoreManager in the scene
        // First, try to find through PlayArea hierarchy
        PlayAreaManager playAreaManager = GetComponentInParent<PlayAreaManager>();
        if (playAreaManager != null)
        {
            m_ScoreManager = playAreaManager.GetComponentInChildren<ScoreManager>();
        }
        
        // Fallback: search scene for ScoreManager
        if (m_ScoreManager == null)
        {
            ScoreManager[] allScoreManagers = FindObjectsByType<ScoreManager>(FindObjectsSortMode.None);
            if (allScoreManagers.Length > 0)
            {
                // Prefer one in Playing state, or just use the first one
                foreach (var sm in allScoreManagers)
                {
                    PlayAreaManager pam = sm.GetComponentInParent<PlayAreaManager>();
                    if (pam != null && pam.GetGameState() == PlayAreaManager.GameState.Playing)
                    {
                        m_ScoreManager = sm;
                        break;
                    }
                }
                
                if (m_ScoreManager == null)
                {
                    m_ScoreManager = allScoreManagers[0];
                }
            }
        }
        
        if (m_ScoreManager == null && debugLogs)
        {
            Debug.LogWarning("[LifeHeartUI] ScoreManager not found. Life count will not update.", this);
        }
    }

    /// <summary>
    /// Callback function to be called by animation event.
    /// Updates the "Amount" text to show the current life count.
    /// </summary>
    public void UpdateLifeAmount()
    {
        if (amountText == null)
        {
            if (debugLogs)
                Debug.LogWarning("[LifeHeartUI] Amount text is null! Cannot update life count.", this);
            return;
        }
        
        // Find ScoreManager if not already found
        if (m_ScoreManager == null)
        {
            FindScoreManager();
        }
        
        if (m_ScoreManager != null)
        {
            int currentLives = m_ScoreManager.GetLives();
            amountText.text = currentLives.ToString();
            
            if (debugLogs)
                Debug.Log($"[LifeHeartUI] Updated life amount to: {currentLives}", this);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning("[LifeHeartUI] ScoreManager not found! Cannot get current life count.", this);
        }
    }
    
    /// <summary>
    /// Triggers the GainLife animation on the LifeHeart Animator.
    /// Called when a life is gained.
    /// </summary>
    public void PlayGainLifeAnimation()
    {
        if (lifeHeartAnimator == null)
        {
            if (debugLogs)
                Debug.LogWarning("[LifeHeartUI] Animator is null! Cannot play GainLife animation.", this);
            return;
        }
        
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
                        if (debugLogs)
                            Debug.Log($"[LifeHeartUI] Triggered GainLife animation '{preferredName}' on LifeHeart Animator.", this);
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
                        if (debugLogs)
                            Debug.Log($"[LifeHeartUI] Triggered animation '{param.name}' on LifeHeart Animator (first available trigger).", this);
                        break;
                    }
                }
            }
        }
        
        if (!triggerFound && debugLogs)
        {
            Debug.LogWarning("[LifeHeartUI] No trigger parameters found in LifeHeart Animator! Please ensure the animator has a trigger parameter for the GainLife animation.", this);
        }
    }
}

