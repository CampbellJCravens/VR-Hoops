#if NORMCORE
using Normal.Realtime;
using Normal.Realtime.Serialization;

/// <summary>
/// RealtimeModel for syncing PlayAreaManager state across clients.
/// Syncs game state and which client is using this play area.
/// </summary>
[RealtimeModel]
public partial class RealtimePlayAreaModel {
    [RealtimeProperty(1, true, true)]
    private int _gameState; // 0 = Pregame, 1 = Playing, 2 = GameOver
    
    [RealtimeProperty(2, true, true)]
    private int _ownerClientID; // Client ID of the player using this play area (-1 if unoccupied)
    
    [RealtimeProperty(3, true, true)]
    private int _shotCount; // Unified shot counter
    
    [RealtimeProperty(4, true, true)]
    private int _ballSpawnCount; // Ball spawn counter (determines money ball)
    
    [RealtimeProperty(5, true, true)]
    private int _hoopCoordinateX; // Hoop position column (x coordinate)
    
    [RealtimeProperty(6, true, true)]
    private int _hoopCoordinateY; // Hoop position row (y coordinate)
}
#endif
