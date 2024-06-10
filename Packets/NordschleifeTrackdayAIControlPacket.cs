using AssettoServer.Network.ClientMessages;

namespace NordschleifeTrackdayPlugin.Packets;

[OnlineEvent(Key = "lightspeedPointsAIControlReceive")]
public class NordschleifeTrackdayAIControlPacket : OnlineEvent<NordschleifeTrackdayAIControlPacket>
{
    [OnlineEventField(Name = "isAIControlled")]
    public ushort IsAIControlled = 0;
}