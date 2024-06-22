using System.Data.SQLite;
using System.Globalization;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Shared;
using NordschleifeTrackdayPlugin.Convoy;
using Serilog;

namespace NordschleifeTrackdayPlugin.Session;

public sealed class NordschleifeTrackdaySession
{
    private readonly NordschleifeTrackdayPlugin _plugin;

    private readonly ACTcpClient _client;
    private readonly string _username = NordschleifeTrackdayPlugin.NO_NAME;
    private string? _kickReason = null;

    private int _points = NordschleifeTrackdayPlugin._pointsStarting;
    private long _lapStart = 0;
    private bool _doingLap = false;
    private bool _clean = true;
    private uint _bestLapTime = 0;
    private readonly Queue<int> _speedList = new();

    private int _totalLaps = 0;
    private int _cleanLaps = 0;
    private long _mostRecentCleanLap = 0;

    private int _cuts = 0;
    private int _collisions = 0;

    private NordschleifeTrackdayConvoy? _hostingConvoy = null;
    private long _lastAfkNotification = 0;

    public NordschleifeTrackdaySession(ACTcpClient client, NordschleifeTrackdayPlugin plugin)
    {
        _client = client;
        _plugin = plugin;
        _username = NordschleifeTrackdayUtils.SanitizeUsername(client.Name ?? NordschleifeTrackdayPlugin.NO_NAME);
    }

    public void OnCreation()
    {
        Log.Information($"{NordschleifeTrackdayPlugin.PLUGIN_PREFIX}Session created: {_username}");
        var userData = NordschleifeTrackdayUtils.GetUser(_plugin._database, NordschleifeTrackdayPlugin._pointsStarting, _client.Guid, _client.EntryCar.Model);
        _points = userData.Points;
        _cleanLaps = userData.CleanLapStreak;
        _mostRecentCleanLap = userData.LastCleanLap;
        _bestLapTime = userData.PersonalBest;
        _totalLaps = userData.TotalLaps;
        _cuts = userData.Cuts;
        _collisions = userData.Collisions;

        CheckCleanLapStreak();
    }

    public void OnRemove()
    {
        CheckCleanLapStreak();

        Log.Information($"{NordschleifeTrackdayPlugin.PLUGIN_PREFIX}Session removed: {_username}");
        NordschleifeTrackdayUtils.UpdateUser(_plugin._database, _client.Guid, _username, _client?.NationCode ?? "???", _points, _cleanLaps, MostRecentCleanLapStr(), _cuts, _collisions);
    }

    public ACTcpClient Client()
    {
        return _client;
    }

    public string Username()
    {
        return _username;
    }

    public void PrepareKick(string? str)
    {
        _kickReason = str;
    }

    public string? PreparedKickReason()
    {
        return _kickReason;
    }

    public bool HasPreparedKick()
    {
        return _kickReason != null;
    }

    public bool DoingLap()
    {
        return _doingLap == true;
    }

    public void SetDoingLap(bool b)
    {
        _doingLap = b;
    }

    public bool IsClean()
    {
        return _clean == true;
    }

    public void SetClean(bool b)
    {
        _clean = b;
    }

    public uint BestLapTime()
    {
        return _bestLapTime;
    }

    public void SetBestLapTime(uint u)
    {
        _bestLapTime = u;
    }

    public int Points()
    {
        return _points;
    }

    public void TakePoints(int i)
    {
        int before = _points;

        _points -= i;
        if (_points < 0)
        {
            _points = 0;
        }

        if (before > 0)
        {
            foreach (var item in NordschleifeTrackdayPlugin.Cars())
            {
                string carModel = _client.EntryCar.Model;
                if (item.Item1 == carModel)
                {
                    if (before >= item.Item2 && _points < item.Item2)
                    {
                        int more = item.Item2 - _points;
                        _ = _plugin._entryCarManager.KickAsync(_client, $"no longer allowed to drive the {carModel}! You need {more} points.");
                    }
                    break;
                }
            }

            if (_points < NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader && before >= NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader)
            {
                if (_plugin._convoyManager.RemoveOnlineConvoyLeader(this))
                {
                    _client.SendPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = "You no longer have enough points to be a convoy leader!"
                    });
                }
            }
        }
    }

    public void AddPoints(int i, bool doDouble = true)
    {
        int before = _points;

        if (doDouble && NordschleifeTrackdayPlugin.IsDoublePoints())
        {
            i *= 2;
            _client.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"{NordschleifeTrackdayPlugin.DOUBLE_POINTS_PREFIX}Double points weekend! Your points were doubled to {i}."
            });
        }
        _points += i;
        if (_points >= NordschleifeTrackdayPlugin._pointsStarting)
        {
            foreach (var item in NordschleifeTrackdayPlugin.Cars())
            {
                if (before < item.Item2 && _points >= item.Item2)
                {
                    _plugin._entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"@{_client.Name} just unlocked access to the {item.Item1} at {item.Item2} points!"
                    });
                }
            }

            if (_points >= NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader && before < NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader)
            {
                if (_plugin._convoyManager.AddOnlineConvoyLeader(this))
                {
                    _plugin._entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"@{_username} is now a convoy leader after reaching {NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader} points!"
                    });
                }
            }
        }
    }

    public long LapStart()
    {
        return _lapStart;
    }

    public void SetLapStart(long l)
    {
        _lapStart = l;
    }

    public int Speed()
    {
        return (int)(_client.EntryCar.Status.Velocity.Length() * 3.6f);
    }

    public int AverageSpeed()
    {
        if (_speedList.Count < 1)
        {
            return 0;
        }

        return _speedList.Sum() / _speedList.Count;
    }

    public void ResetAverageSpeed()
    {
        _speedList.Clear();
    }

    public void AddToAverageSpeed(int speed)
    {
        if (_speedList.Count > 100)
        {
            _speedList.Dequeue();
        }

        _speedList.Enqueue(speed);
    }

    public int TotalLaps()
    {
        return _totalLaps;
    }

    private void AddToTotalLaps()
    {
        _totalLaps++;
    }

    public int Cuts()
    {
        return _cuts;
    }

    public void AddCut()
    {
        _cuts++;
    }

    public int Collisions()
    {
        return _collisions;
    }

    public void AddCollision()
    {
        _collisions++;
    }

    public int CleanLaps()
    {
        return _cleanLaps;
    }

    public void AddCleanLap()
    {
        CheckCleanLapStreak();
        _cleanLaps++;
        AddToTotalLaps();
        UpdateMostRecentCleanLap();

        int reward = NordschleifeTrackdayPlugin.GetCleanLapPointsReward(_cleanLaps);
        if (reward > 0)
        {
            AddPoints(reward);
            _plugin._entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"@{_client.Name} just did {_cleanLaps} clean laps and earned a bonus of {reward} points!"
            });
        }
    }

    private void CheckCleanLapStreak()
    {
        if (HasCleanLapStreak() && IsCleanLapStreakExpired())
        {
            ResetCleanLaps();
            ResetMostRecentCleanLap();
        }
    }

    public void ResetCleanLaps()
    {
        _cleanLaps = 0;
    }

    public string MostRecentCleanLapStr()
    {
        return _mostRecentCleanLap < 1 ? "1970-01-02 00:00:00" : DateTimeOffset.FromUnixTimeSeconds(_mostRecentCleanLap).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public long MostRecentCleanLap()
    {
        return _mostRecentCleanLap;
    }

    private bool HasCleanLapStreak()
    {
        return _cleanLaps > 0;
    }

    private bool IsCleanLapStreakExpired()
    {
        return _mostRecentCleanLap > 0 && (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _mostRecentCleanLap) >= 3600;
    }

    private void UpdateMostRecentCleanLap()
    {
        _mostRecentCleanLap = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public void ResetMostRecentCleanLap()
    {
        _mostRecentCleanLap = 0;
    }

    public bool HostingConvoy()
    {
        return _hostingConvoy != null;
    }

    public NordschleifeTrackdayConvoy? Convoy()
    {
        return _hostingConvoy;
    }

    public void SetHostingConvoy(NordschleifeTrackdayConvoy? convoy)
    {
        _hostingConvoy = convoy;
    }

    public long LastNotificationTime()
    {
        return _lastAfkNotification;
    }

    public void SetLastNotificationTime(long l)
    {
        _lastAfkNotification = l;
    }
}
