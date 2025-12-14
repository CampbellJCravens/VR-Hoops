#if NORMCORE
using Normal.Realtime;
using Normal.Realtime.Serialization;

/// <summary>
/// RealtimeModel for syncing basketball material type across clients.
/// Syncs which material the ball should use (Orange, Black, or RedWhiteBlue).
/// </summary>
[RealtimeModel]
public partial class RealtimeBasketballModel
{
    /// <summary>
    /// Material type: 0 = Orange, 1 = Black, 2 = RedWhiteBlue
    /// </summary>
    [RealtimeProperty(1, true, true)]
    private int _materialType;
}
#endif

