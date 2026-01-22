#if NORMCORE
using Normal.Realtime;
using Normal.Realtime.Serialization;

/// <summary>
/// RealtimeModel for syncing ScoreManager state across clients.
/// Syncs score, lives, on fire state
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
}
#endif
