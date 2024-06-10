using AssettoServer.Network.ClientMessages;

namespace NordschleifeTrackdayPlugin.Packets;

[OnlineEvent(Key = "lightspeedPointsCarLockedReceive")]
public class NordschleifeTrackdayCarLockedPacket : OnlineEvent<NordschleifeTrackdayCarLockedPacket>
{
    [OnlineEventField(Name = "isLocked")]
    public ushort IsLocked = 0;
}