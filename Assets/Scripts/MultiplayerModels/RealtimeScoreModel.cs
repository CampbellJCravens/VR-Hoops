#if NORMCORE
using Normal.Realtime;
using Normal.Realtime.Serialization;

/// <summary>
/// RealtimeModel for syncing ScoreManager state across clients.
/// Syncs score, lives, on fire state, and flash message data.
/// </summary>
[RealtimeModel]
public partial class RealtimeScoreModel {
    [RealtimeProperty(1, true, true)]
    private int _score;
    
    [RealtimeProperty(2, true, true)]
    private int _lives;
    
    [RealtimeProperty(3, true, true)]
    private bool _isOnFire;
    
    [RealtimeProperty(4, true, true)]
    private bool _isGameOver;
    
    [RealtimeProperty(5, true, true)]
    private int _flashPointsEarned; // Points earned for the flash message (-1 means no flash)
    
    [RealtimeProperty(6, true, true)]
    private bool _flashIsMoneyBall; // Whether the flash message should show "MONEY BALL!"
    
    [RealtimeProperty(7, true, true)]
    private bool _flashIsOnFire; // Whether the flash message should show "ON FIRE!"
}
#endif
