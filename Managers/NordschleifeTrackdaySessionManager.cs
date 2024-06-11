using AssettoServer.Network.Tcp;
using NordschleifeTrackdayPlugin.Session;
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Shared;

namespace NordschleifeTrackdayPlugin.Managers;

public class NordschleifeTrackdaySessionManager
{
    private readonly NordschleifeTrackdayPlugin _plugin;
    private readonly Dictionary<ulong, NordschleifeTrackdaySession> _sessions = [];

    private readonly int _afkTimeAllowedDefault = 0;
    private readonly int _afkTimeAllowedLonger = 0;
    private readonly int _afkTimeAllowedStarterCar = 0;

    public NordschleifeTrackdaySessionManager(NordschleifeTrackdayPlugin plugin)
    {
        _plugin = plugin;

        _afkTimeAllowedDefault = _plugin._config.IdleKick.DefaultMaxIdleTime * 1000;
        _afkTimeAllowedLonger = _plugin._config.IdleKick.LongerMaxIdleTime * 1000;
        _afkTimeAllowedStarterCar = _plugin._config.IdleKick.StarterMaxIdleTime * 1000;

        System.Timers.Timer timer = new(5000);
        timer.Elapsed += new System.Timers.ElapsedEventHandler(UpdateSessions);
        timer.Start();
    }

    private void UpdateSessions(object? sender, EventArgs args)
    {
        foreach (var sessions in _sessions)
        {
            NordschleifeTrackdaySession session = sessions.Value;
            if (session.GetType() == typeof(NordschleifeTrackdaySession))
            {
                ACTcpClient client = session.Client();
                if (client.IsConnected && client.HasSentFirstUpdate)
                {
                    EntryCar car = client.EntryCar;
                    if (_plugin._config.IdleKick.Enabled)
                    {
                        int afkTimeAllowed = _afkTimeAllowedDefault;
                        int notificationInterval = 60000;
                        long currentTime = _plugin._asSessionManager.ServerTimeMilliseconds;
                        long timeAfk = currentTime - car.LastActiveTime;
                        if (NordschleifeTrackdayPlugin.StarterCars().Contains(car.Model))
                        {
                            afkTimeAllowed = _afkTimeAllowedStarterCar;
                        }
                        else if (_plugin._config.IdleKick.AllowLongerMaxIdleForCleanLaps && session.CleanLaps() >= _plugin._config.IdleKick.LongerMaxIdleNeededCleanLaps)
                        {
                            afkTimeAllowed = _afkTimeAllowedLonger;
                        }

                        if (timeAfk >= afkTimeAllowed)
                        {
                            _ = _plugin._entryCarManager.KickAsync(client, $"AFK for longer than allowed");
                            return;
                        }
                        else
                        {
                            int afkTimeAllowedMinutes = afkTimeAllowed / 60000;
                            int fullMinutesAfk = (int)(timeAfk / notificationInterval);
                            if (fullMinutesAfk > 0 && fullMinutesAfk <= afkTimeAllowedMinutes)
                            {
                                if (currentTime - session.LastNotificationTime() >= notificationInterval)
                                {
                                    client.SendPacket(new ChatMessage
                                    {
                                        SessionId = 255,
                                        Message = $"{fullMinutesAfk}/{afkTimeAllowedMinutes} minutes AFK. You'll be kicked at {afkTimeAllowedMinutes}!"
                                    });
                                    session.SetLastNotificationTime(currentTime);
                                }
                            }
                        }
                    }

                    if (session.DoingLap())
                    {
                        session.AddToAverageSpeed(session.Speed());
                    }
                }
            }
        }
    }

    public Dictionary<ulong, NordschleifeTrackdaySession> GetSessions()
    {
        return _sessions;
    }

    public NordschleifeTrackdaySession? GetSession(ulong guid)
    {
        if (_sessions.TryGetValue(guid, out NordschleifeTrackdaySession? value))
        {
            return value;
        }

        return null;
    }

    public NordschleifeTrackdaySession? AddSession(ACTcpClient client)
    {
        if (GetSession(client.Guid) != null)
        {
            return null;
        }

        NordschleifeTrackdaySession newSession = new(client, _plugin);
        _sessions[client.Guid] = newSession;
        newSession.OnCreation();

        return newSession;
    }

    public void RemoveSession(ACTcpClient client, NordschleifeTrackdaySession? providedSession = null)
    {
        NordschleifeTrackdaySession? session = providedSession ?? GetSession(client.Guid);
        if (session == null)
        {
            return;
        }

        session.OnRemove();
        _sessions.Remove(client.Guid);
    }
}