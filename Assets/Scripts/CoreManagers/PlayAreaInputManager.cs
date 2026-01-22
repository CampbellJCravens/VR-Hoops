using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles input for a specific PlayArea.
/// Attach this as a child component of PlayAreaManager.
/// Only responds to input when this play area is active (owned by local client and in Playing state).
/// </summary>
public class PlayAreaInputManager : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Input action for spawning a ball. Should be bound to a button (e.g., SecondaryButton).")]
    [SerializeField] private InputActionProperty spawnBallAction;
    
    [Tooltip("Input action for ending the game. Should be bound to a button.")]
    [SerializeField] private InputActionProperty endGameAction;

    [Header("References")]
    [Tooltip("The PlayAreaManager this input manager belongs to.")]
    [SerializeField] private PlayAreaManager playAreaManager;

    private void OnEnable()
    {
        spawnBallAction.action.performed += OnSpawnBallPerformed;
        spawnBallAction.action.Enable();
        
        endGameAction.action.performed += OnEndGamePerformed;
        endGameAction.action.Enable();
    }

    private void OnDisable()
    {
        spawnBallAction.action.performed -= OnSpawnBallPerformed;
        spawnBallAction.action.Disable();
        
        endGameAction.action.performed -= OnEndGamePerformed;
        endGameAction.action.Disable();
    }

    private void OnSpawnBallPerformed(InputAction.CallbackContext ctx)
    {
        // Only respond if this play area is active (owned by local client and in Playing state)
        if (playAreaManager.IsOwnedByLocalClient() && 
            playAreaManager.GetGameState() == PlayAreaManager.GameState.Playing)
        {
            playAreaManager.SpawnAndLaunchBall();
        }
    }

    private void OnEndGamePerformed(InputAction.CallbackContext ctx)
    {
        // Only respond if this play area is active (owned by local client and in Playing state)
        if (playAreaManager.IsOwnedByLocalClient() && 
            playAreaManager.GetGameState() == PlayAreaManager.GameState.Playing)
        {
            playAreaManager.EndGame();
        }
    }
}
