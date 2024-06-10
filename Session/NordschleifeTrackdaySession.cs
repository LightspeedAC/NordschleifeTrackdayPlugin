using System.Data.SQLite;
using System.Globalization;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Shared;
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

    private bool _hostingConvoy = false;
    private long _lastAfkNotification = 0;

    public NordschleifeTrackdaySession(ACTcpClient client, NordschleifeTrackdayPlugin plugin)
    {
        _client = client;
        _plugin = plugin;
        _username = NordschleifeTrackdayPlugin.SanitizeUsername(client.Name ?? NordschleifeTrackdayPlugin.NO_NAME);
    }

    public void OnCreation()
    {
        Log.Information($"{NordschleifeTrackdayPlugin.PLUGIN_PREFIX}Session created: {_username}");
        var userData = GetUser(_client.Guid, _client.EntryCar.Model);
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
        UpdateUser(_client.Guid, _username, _client?.NationCode ?? "???", _points, _cleanLaps, MostRecentCleanLapStr(), _cuts, _collisions);
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
                if (NordschleifeTrackdayPlugin.RemoveOnlineConvoyLeader(this))
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
                if (NordschleifeTrackdayPlugin.AddOnlineConvoyLeader(this))
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
                Message = $"@{_client.Name} just earned a {_cleanLaps} clean laps bonus of {reward} points!"
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
        return _hostingConvoy;
    }

    public void SetHostingConvoy(bool b)
    {
        _hostingConvoy = b;
    }

    public long LastNotificationTime()
    {
        return _lastAfkNotification;
    }

    public void SetLastNotificationTime(long l)
    {
        _lastAfkNotification = l;
    }

    public (int Points, int CleanLapStreak, long LastCleanLap, int TotalLaps, uint PersonalBest, int Cuts, int Collisions) GetUser(ulong guid, string car)
    {
        int points = NordschleifeTrackdayPlugin._pointsStarting;
        int cleanLapStreak = 0;
        long lastCleanLap = 0;
        int totalLaps = 0;
        uint pb = 0;
        int cuts = 0;
        int collisions = 0;

        using (var command = new SQLiteCommand("SELECT points, clean_lap_streak, last_clean_lap, cuts, collisions FROM users WHERE id = @id LIMIT 0,1", _plugin._database))
        {
            command.Parameters.AddWithValue("@id", guid);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                points = Convert.ToInt32(reader["points"]);
                cleanLapStreak = Convert.ToInt32(reader["clean_lap_streak"]);
                string lastCleanLapString = Convert.ToDateTime(reader["last_clean_lap"]).ToString("yyyy-MM-dd HH:mm:ss") ?? "1970-01-02 00:00:00";
                DateTime lastCleanLapUtcDateTime = DateTime.ParseExact(lastCleanLapString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                lastCleanLap = ((DateTimeOffset)lastCleanLapUtcDateTime).ToUnixTimeSeconds();
                cuts = Convert.ToInt32(reader["cuts"]);
                collisions = Convert.ToInt32(reader["collisions"]);
            }
        }

        using (var command = new SQLiteCommand("SELECT COUNT(*) FROM laptimes WHERE user_id = @id", _plugin._database))
        {
            command.Parameters.AddWithValue("@id", guid);
            totalLaps = Convert.ToInt32(command.ExecuteScalar());
        }
        using (var command = new SQLiteCommand("SELECT time FROM laptimes WHERE user_id = @id AND car = @car ORDER BY time ASC LIMIT 0,1", _plugin._database))
        {
            command.Parameters.AddWithValue("@id", guid);
            command.Parameters.AddWithValue("@car", car);
            pb = Convert.ToUInt32(command.ExecuteScalar());
        }

        return (points, cleanLapStreak, lastCleanLap, totalLaps, pb, cuts, collisions);
    }

    public void UpdateUser(ulong guid, string name, string country, int points, int clean_lap_streak, string last_clean_lap, int cuts, int collisions)
    {
        using var updateCommand = new SQLiteCommand("INSERT INTO users (id, name, country, points, clean_lap_streak, last_clean_lap, cuts, collisions) VALUES (@id, @name, @country, @points, @clean_lap_streak, @last_clean_lap, @cuts, @collisions) ON CONFLICT(id) DO UPDATE SET name = excluded.name, country = excluded.country, points = excluded.points, clean_lap_streak = excluded.clean_lap_streak, last_clean_lap = excluded.last_clean_lap, cuts = excluded.cuts, collisions = excluded.collisions;", _plugin._database);
        updateCommand.Parameters.AddWithValue("@id", guid);
        updateCommand.Parameters.AddWithValue("@name", name);
        updateCommand.Parameters.AddWithValue("@country", country);
        updateCommand.Parameters.AddWithValue("@points", points);
        updateCommand.Parameters.AddWithValue("@clean_lap_streak", clean_lap_streak);
        updateCommand.Parameters.AddWithValue("@last_clean_lap", last_clean_lap);
        updateCommand.Parameters.AddWithValue("@cuts", cuts);
        updateCommand.Parameters.AddWithValue("@collisions", collisions);
        updateCommand.ExecuteNonQuery();
    }
}
