using NordschleifeTrackdayPlugin.Session;
using AssettoServer.Shared.Network.Packets.Shared;
using NordschleifeTrackdayPlugin.Convoy;

namespace NordschleifeTrackdayPlugin.Managers;

public class NordschleifeTrackdayConvoyManager
{
    private readonly NordschleifeTrackdayPlugin _plugin;
    private readonly Dictionary<ulong, NordschleifeTrackdayConvoy> _convoys = [];
    private readonly List<string> _onlineConvoyLeaders = [];

    public NordschleifeTrackdayConvoyManager(NordschleifeTrackdayPlugin plugin)
    {
        _plugin = plugin;
    }

    public Dictionary<ulong, NordschleifeTrackdayConvoy> Convoys()
    {
        return _convoys;
    }

    public void RemoveConvoy(ulong driver)
    {
        _convoys.Remove(driver);
    }

    public void AddConvoy(NordschleifeTrackdaySession driver, List<ulong> finishingDrivers, List<ulong> startingDrivers)
    {
        _convoys.Add(driver.Client().Guid, new NordschleifeTrackdayConvoy(driver, finishingDrivers, startingDrivers));
    }

    public async void CheckConvoy(NordschleifeTrackdayConvoy convoy, NordschleifeTrackdaySession session)
    {
        string? convoyLeaderDriver = session.Client().Name;
        ulong convoyLeaderGuid = session.Client().Guid;
        if (convoyLeaderDriver != null)
        {
            List<ulong> possibleDrivers = [69, 420];
            foreach (var recentLap in _plugin.RecentLapStarts())
            {
                if (convoyLeaderGuid != recentLap.Item1 && !possibleDrivers.Contains(recentLap.Item1) && convoy.IsStartTimeValid(convoy.StartedTimeMs()))
                {
                    possibleDrivers.Add(recentLap.Item1);
                }
            }

            int needed = NordschleifeTrackdayPlugin.Admins().Contains(session.Client().Guid) ? NordschleifeTrackdayPlugin.CONVOY_MIN_DRIVERS_NEEDED_ADMIN : NordschleifeTrackdayPlugin.CONVOY_MIN_DRIVERS_NEEDED;
            int possibleDriversCount = possibleDrivers.Count;
            int diff = needed - possibleDriversCount;
            if (possibleDriversCount < needed)
            {
                _ = EndConvoyAsync(session, false, $"Not enough drivers, you need {diff} more");
            }
            else
            {
                foreach (var (key, convoyObj) in _convoys)
                {
                    if (key == convoyLeaderGuid)
                    {
                        convoyObj.SetStartingDrivers(possibleDrivers);
                        break;
                    }
                }

                if (_plugin._config.DiscordWebhook.Enabled)
                {
                    await NordschleifeTrackdayPlugin.SendDiscordWebhook($"**Convoy started!**\nJoin: {NordschleifeTrackdayPlugin._serverLink}\nLeader: {session.Username()} ({session.Client().EntryCar.Model})\nDrivers: {possibleDriversCount}");
                }
            }
        }
    }

    public bool StartConvoy(NordschleifeTrackdaySession session)
    {
        if (_convoys.ContainsKey(session.Client().Guid))
        {
            return false;
        }

        AddConvoy(session, [], []);
        session.SetHostingConvoy(true);

        _plugin._entryCarManager.BroadcastPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}@{session.Username()} ({session.Client().EntryCar.Model}) is starting a convoy! Make sure to meet at pits to earn a convoy bonus of {NordschleifeTrackdayPlugin._pointsRewardConvoy} points for completing a lap with them."
        });
        return true;
    }

    public async Task<bool> EndConvoyAsync(NordschleifeTrackdaySession session, bool lapCompleted = true, string reason = "")
    {
        ulong guid = session.Client().Guid;
        if (!_convoys.ContainsKey(guid))
        {
            return false;
        }

        var convoy = _convoys[guid];
        int driversNeeded = NordschleifeTrackdayPlugin.Admins().Contains(guid) ? NordschleifeTrackdayPlugin.CONVOY_MIN_DRIVERS_NEEDED_ADMIN : NordschleifeTrackdayPlugin.CONVOY_MIN_DRIVERS_NEEDED;
        if (lapCompleted && convoy.HasStarted())
        {
            List<string> finishingDriversStr = [];
            int driversCount = convoy.FinishingDrivers().Count;
            if (driversCount >= driversNeeded)
            {
                for (int i = 0; i < driversCount; i++)
                {
                    ulong driverGuid = convoy.FinishingDrivers()[i];
                    NordschleifeTrackdaySession? otherDriverSession = _plugin._sessionManager.GetSession(driverGuid);
                    if (otherDriverSession != null)
                    {
                        otherDriverSession.AddPoints(NordschleifeTrackdayPlugin._pointsRewardConvoy);
                        _plugin._entryCarManager.BroadcastPacket(new ChatMessage
                        {
                            SessionId = 255,
                            Message = $"@{otherDriverSession.Username()} earned a convoy bonus of {NordschleifeTrackdayPlugin._pointsRewardConvoy} points!"
                        });

                        finishingDriversStr.Add(otherDriverSession.Username());
                    }
                    else
                    {
                        finishingDriversStr.Add(NordschleifeTrackdayPlugin.NO_NAME);
                    }
                }

                string driversEnglish = driversCount == 1 ? "driver" : "drivers";
                _plugin._entryCarManager.BroadcastPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}@{session.Username()}'s convoy concluded with {driversCount} {driversEnglish}! It started with {convoy.StartingDrivers().Count}."
                });
                session.AddPoints(NordschleifeTrackdayPlugin._pointsRewardConvoy);
                _plugin._entryCarManager.BroadcastPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"@{session.Username()} earned a convoy bonus of {NordschleifeTrackdayPlugin._pointsRewardConvoy} points!"
                });

                if (_plugin._config.DiscordWebhook.Enabled)
                {
                    await NordschleifeTrackdayPlugin.SendDiscordWebhook($"**{session.Username()}'s convoy has finished!**\nJoin for the next: {NordschleifeTrackdayPlugin._serverLink}\nFinishing Drivers ({driversCount}): {string.Join(", ", finishingDriversStr)}");
                }
            }
            else
            {
                _plugin._entryCarManager.BroadcastPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}@{session.Username()}'s convoy finished but didn't have enough drivers to give out rewards. At least {driversNeeded} drivers were required!"
                });
            }
        }
        else
        {
            _plugin._entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = reason == "" ? $"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}@{session.Username()}'s convoy was ended." : $"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}@{session.Username()}'s convoy was automatically ended for: {reason}."
            });
        }

        RemoveConvoy(guid);
        session.SetHostingConvoy(false);
        return true;
    }

    public bool AddOnlineConvoyLeader(NordschleifeTrackdaySession session)
    {
        string? name = session.Client().Name;
        if (name == null || !IsAConvoyLeader(session))
        {
            return false;

        }

        if (!_onlineConvoyLeaders.Contains(name))
        {
            _onlineConvoyLeaders.Add(name);
        }

        return true;
    }

    public bool RemoveOnlineConvoyLeader(NordschleifeTrackdaySession session)
    {
        string? name = session.Client().Name;
        if (name == null)
        {
            return false;

        }

        if (!_onlineConvoyLeaders.Contains(name))
        {
            return false;
        }

        _onlineConvoyLeaders.Remove(name);
        return true;
    }

    public List<string> OnlineConvoyLeaders()
    {
        return _onlineConvoyLeaders;
    }

    public bool IsAConvoyLeader(NordschleifeTrackdaySession session)
    {
        return NordschleifeTrackdayPlugin.Admins().Contains(session.Client().Guid) || (_plugin._config.Extra.AssignConvoyLeadersByPoints && session.Points() >= NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader);
    }
}