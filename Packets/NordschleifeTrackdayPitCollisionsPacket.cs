using AssettoServer.Network.ClientMessages;

namespace NordschleifeTrackdayPlugin.Packets;

[OnlineEvent(Key = "lightspeedPointsPitCollisionsReceive")]
public class NordschleifeTrackdayPitCollisionsPacket : OnlineEvent<NordschleifeTrackdayPitCollisionsPacket>
{
    [OnlineEventField(Name = "isPitCollisionsCounted")]
    public ushort IsPitCollisionsCounted = 1;
}