using AssettoServer.Server;
using AssettoServer.Network.Tcp;
using Serilog;
using System.Text.RegularExpressions;
using NordschleifeTrackdayPlugin.Managers;
using NordschleifeTrackdayPlugin.Session;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using System.Data.SQLite;
using AssettoServer.Server.Configuration;
using Microsoft.Extensions.Hosting;
using AssettoServer.Shared.Network.Packets;
using System.Reflection;
using NordschleifeTrackdayPlugin.Packets;
using AssettoServer.Server.Weather;
using System.Text.Json;
using System.Text;
using AssettoServer.Server.GeoParams;
using AssettoServer.Shared.Network.Packets.Incoming;

namespace NordschleifeTrackdayPlugin;

public class NordschleifeTrackdayPlugin : CriticalBackgroundService
{
    public const int CONVOY_MIN_DRIVERS_NEEDED_ADMIN = 1;
    public const int CONVOY_MIN_DRIVERS_NEEDED = 2;
    public const int LEADERBOARD_MAX_ENTRIES = 15;

    public const string PLUGIN_NAME = "NordschleifeTrackdayPlugin";
    public const string PLUGIN_PREFIX = $"[{PLUGIN_NAME}] ";
    public const string CONVOY_PREFIX = "CONVOYS > ";
    public const string TIP_PREFIX = "TIPS > ";
    public const string DOUBLE_POINTS_PREFIX = "X2 POINTS > ";
    public const string ANNOUNCEMENT_PREFIX = "SERVER > ";

    public const string NO_NAME = "Unknown";
    public const string DEFAULT_DATABASE_FILE = "nordschleife_trackday.sqlite";
    public SQLiteConnection _database = new();

    private static NordschleifeTrackdayPlugin? _instance;
    public readonly EntryCarManager _entryCarManager;
    public readonly SessionManager _asSessionManager;
    public readonly WeatherManager _weatherManager;
    public readonly NordschleifeTrackdaySessionManager _sessionManager;
    public readonly NordschleifeTrackdayConvoyManager _convoyManager;
    public readonly NordschleifeTrackdayConfiguration _config;

    private static readonly string[] _forbiddenUsernameSubstrings = ["discord", "@", "#", ":", "```"];
    private static readonly string[] _forbiddenUsernames = ["everyone", "here"];

    private bool _warnedSessionEnd = false;
    private readonly Queue<(ulong, long)> _recentLapStarts = [];//used to determine if a starting convoy is empty and should be ended 
    private readonly Dictionary<string, (string, uint)> _bestLapTimes = [];
    private Dictionary<ulong, (string, int)> _pointsLeaderboard = [];
    private int _currentAnnouncementIndex = 0;

    private static string _databasePath = "";
    private static readonly List<ulong> _admins = [];
    private static readonly List<ulong> _convoyLeaders = [];
    private static readonly List<(string, int)> _cars = [];
    private static readonly List<string> _starterCars = [];
    private static readonly List<(int, int)> _prominentCleanLapRewards = [];
    private static readonly List<string> _announcements = [];
    public static string _serverLink = "";
    public static int _pointsNeededForConvoyLeader = 6500;
    public static int _pointsStarting = 500;
    public static int _pointsDeductLeavePits = 3;
    public static int _pointsDeductInvalidLap = 250;
    public static int _pointsDeductPitReEnter = 200;
    public static double _pointsDeductBySpeedFactor = 1.4;
    public static int _pointsDeductCollisionMax = 500;
    public static int _pointsRewardPerLap = 30;
    public static int _pointsRewardBeatPb = 50;
    public static int _pointsRewardBeatTb = 75;
    public static int _pointsRewardConvoy = 150;

    public NordschleifeTrackdayPlugin(NordschleifeTrackdayConfiguration nordschleifeTrackdayConfiguration, GeoParamsManager geoParamsManager, ACServerConfiguration acServerConfiguration, EntryCarManager entryCarManager, SessionManager sessionManager, WeatherManager weatherManager, CSPServerScriptProvider cspServerScriptProvider, CSPClientMessageTypeManager cspClientMessageTypeManager, CSPFeatureManager cspFeatureManager, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        Log.Information("--------------------------------------");
        Log.Information($"{PLUGIN_NAME} - Jonfinity");
        Log.Information("--------------------------------------");

        _config = nordschleifeTrackdayConfiguration;
        LoadFromConfig(geoParamsManager, acServerConfiguration);
        StartDatabase();
        _instance = this;

        _entryCarManager = entryCarManager;
        _asSessionManager = sessionManager;
        _weatherManager = weatherManager;
        _sessionManager = new NordschleifeTrackdaySessionManager(this);
        _convoyManager = new NordschleifeTrackdayConvoyManager(this);
        applicationLifetime.ApplicationStopping.Register(ApplicationStopping);

        if (!acServerConfiguration.Extra.EnableClientMessages)
        {
            Log.Fatal($"{PLUGIN_PREFIX}'EnableClientMessages' is required to be set to 'true'!");
            applicationLifetime.StopApplication();
            return;
        }

        entryCarManager.ClientConnected += OnClientConnected;
        entryCarManager.ClientDisconnected += OnClientDisconnected;

        cspClientMessageTypeManager.RegisterClientMessageType(0x38BAECD0, new Action<ACTcpClient, PacketReader>(IncomingCollision));
        cspClientMessageTypeManager.RegisterClientMessageType(0xB7B908B4, new Action<ACTcpClient, PacketReader>(IncomingLapStart));
        cspClientMessageTypeManager.RegisterClientMessageType(0x236CD37F, new Action<ACTcpClient, PacketReader>(IncomingLapCut));
        cspClientMessageTypeManager.RegisterClientMessageType(0x4DA987D2, new Action<ACTcpClient, PacketReader>(IncomingPitLeave));
        cspClientMessageTypeManager.RegisterClientMessageType(0x2BD9A705, new Action<ACTcpClient, PacketReader>(IncomingPitReEntry));
        cspClientMessageTypeManager.RegisterClientMessageType(0x34213D1E, new Action<ACTcpClient, PacketReader>(IncomingPitConvoyLeave));
        cspClientMessageTypeManager.RegisterClientMessageType(0xE5E9C8E, new Action<ACTcpClient, PacketReader>(IncomingConvoyNearFinish));
        cspClientMessageTypeManager.RegisterClientMessageType(0xA5968DCA, new Action<ACTcpClient, PacketReader>(IncomingPitTeleport));
        cspClientMessageTypeManager.RegisterClientMessageType(0x1B27688C, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtNorthTurn));
        cspClientMessageTypeManager.RegisterClientMessageType(0x8C75B5C8, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtAirfield));
        cspClientMessageTypeManager.RegisterClientMessageType(0xEC91598C, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtFoxhole));
        cspClientMessageTypeManager.RegisterClientMessageType(0xCCF18B8F, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtKallenForest));
        cspClientMessageTypeManager.RegisterClientMessageType(0xDE66F651, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtWaterMill));
        cspClientMessageTypeManager.RegisterClientMessageType(0x2B81FBFE, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtLittleValley));
        cspClientMessageTypeManager.RegisterClientMessageType(0xCA16E6C9, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtFirstCarousel));
        cspClientMessageTypeManager.RegisterClientMessageType(0x6EA934BA, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtBrunnchen));
        cspClientMessageTypeManager.RegisterClientMessageType(0xDB83048A, new Action<ACTcpClient, PacketReader>(IncomingConvoyAtSecondCarousel));

        using var stream = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("NordschleifeTrackdayPlugin.Lua.NordschleifeTrackdayScript.lua")!);
        cspServerScriptProvider.AddScript(stream.ReadToEnd(), "NordschleifeTrackdayScript.lua");

        System.Timers.Timer timer = new(_config.Announcements.Interval * 1000);
        timer.Elapsed += new System.Timers.ElapsedEventHandler(DoAnnouncements);
        timer.Start();

        System.Timers.Timer leaderboardTimer = new(1800 * 1000);
        timer.Elapsed += new System.Timers.ElapsedEventHandler(UpdateLeaderboard);
        timer.Start();
    }

    private void ApplicationStopping()
    {
        foreach (var session in _sessionManager.GetSessions())
        {
            session.Value.OnRemove();
        }
    }

    public static NordschleifeTrackdayPlugin? Instance()
    {
        return _instance;
    }

    private void LoadFromConfig(GeoParamsManager geoParamsManager, ACServerConfiguration acServerConfiguration)
    {
        Task.Delay(10 * 1000).ContinueWith((_) => _serverLink = $"https://acstuff.ru/s/q:race/online/join?ip={geoParamsManager.GeoParams.Ip}&httpPort={acServerConfiguration.Server.HttpPort}");//geoParamsManager.GeoParams isnt populated immediately

        _databasePath = _config.DatabasePath;
        if (_databasePath == "")
        {
            _databasePath = DEFAULT_DATABASE_FILE;
        }

        foreach (var guid in _config.Admins)
        {
            _admins.Add(guid);
        }
        Log.Information($"{PLUGIN_PREFIX}Found ({_config.Admins.Count}) admins..");

        foreach (var guid in _config.ConvoyLeaders)
        {
            _admins.Add(guid);
        }
        Log.Information($"{PLUGIN_PREFIX}Found ({_config.ConvoyLeaders.Count}) convoy leaders..");

        foreach (var item in _config.CleanLapBonuses)
        {
            _prominentCleanLapRewards.Add(new(item.Key, item.Value));
        }
        Log.Information($"{PLUGIN_PREFIX}Found ({_config.CleanLapBonuses.Count}) clean lap bonuses..");

        foreach (var message in _config.Announcements.Messages)
        {
            _announcements.Add(message);
        }
        Log.Information($"{PLUGIN_PREFIX}Found ({_config.Announcements.Messages.Count}) announcements..");

        int starterCars = 0;
        foreach (var item in _config.Cars)
        {
            if (_config.StarterCars.Contains(item.Key))
            {
                starterCars++;
            }
            _cars.Add(new(item.Key, item.Value));
        }
        Log.Information($"{PLUGIN_PREFIX}Found ({_config.Cars.Count}) cars ({starterCars} starter)..");

        _pointsNeededForConvoyLeader = _config.Extra.ConvoyLeadersNeededPoints;
        _pointsStarting = _config.Metrics.StartingPoints;
        _pointsDeductLeavePits = _config.Metrics.PointsDeductLeavePits;
        _pointsDeductInvalidLap = _config.Metrics.PointsDeductInvalidLap;
        _pointsDeductPitReEnter = _config.Metrics.PointsDeductPitReEnter;
        _pointsDeductBySpeedFactor = _config.Metrics.PointsDeductBySpeedFactor;
        _pointsDeductCollisionMax = _config.Metrics.PointsDeductCollisionMax;
        _pointsRewardPerLap = _config.Metrics.PointsRewardPerLap;
        _pointsRewardBeatPb = _config.Metrics.PointsRewardBeatPb;
        _pointsRewardBeatTb = _config.Metrics.PointsRewardBeatOtherPb;
        _pointsRewardConvoy = _config.Metrics.PointsRewardConvoy;
    }

    private void StartDatabase()
    {
        string? path = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        if (!File.Exists(_databasePath))
        {
            SQLiteConnection.CreateFile(_databasePath);
        }
        Log.Information($"{PLUGIN_PREFIX}Found database file at \"{_databasePath}\"..");
        _database = new($"Data Source={_databasePath};Version=3;");
        _database.Open();

        using (var command = new SQLiteCommand($"CREATE TABLE IF NOT EXISTS users (id INT PRIMARY KEY, name VARCHAR(255) NOT NULL, country VARCHAR(255) NOT NULL, points INT NOT NULL DEFAULT {_pointsStarting}, clean_lap_streak INT NOT NULL, last_clean_lap datetime DEFAULT '1970-01-02 00:00:00', cuts INT NOT NULL DEFAULT 0, collisions INT NOT NULL DEFAULT 0)", _database))
        {
            command.ExecuteNonQuery();
        }
        using (var command = new SQLiteCommand("CREATE TABLE IF NOT EXISTS laptimes (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id INT NOT NULL, car VARCHAR(255) NOT NULL, time INT NOT NULL, average_speedkmh SMALLINT NOT NULL, completed_on datetime DEFAULT '1970-01-02 00:00:00', FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE)", _database))
        {
            command.ExecuteNonQuery();
        }

        foreach (var car in _cars)
        {
            uint lapTime = 0;
            string username = "";
            using var command = new SQLiteCommand("SELECT laptimes.time, users.name FROM laptimes JOIN users ON laptimes.user_id = users.id WHERE laptimes.car = @car ORDER BY laptimes.time ASC LIMIT 0,1", _database);
            command.Parameters.AddWithValue("@car", car.Item1);
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    lapTime = Convert.ToUInt32(reader["time"]);
                    username = reader.GetString(reader.GetOrdinal("name"));
                }
            }
            _bestLapTimes.Add(car.Item1, (username, lapTime));
        }

        _pointsLeaderboard = GetPointsLeaderboard();
    }

    private void DoAnnouncements(object? sender, EventArgs args)
    {
        if (_asSessionManager.ServerTimeMilliseconds < 60000)
        {
            return;
        }

        long currentSessionTime = _asSessionManager.CurrentSession.SessionTimeMilliseconds;
        if (!_warnedSessionEnd && (currentSessionTime + 360000) >= _asSessionManager.CurrentSession.TimeLeftMilliseconds)
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"{ANNOUNCEMENT_PREFIX}The server session ends in 6 minutes, you'll be teleported to pits shortly."
            });
            Log.Information($"{PLUGIN_PREFIX}Just warned of nearing server session end in 6 mins!");
            _warnedSessionEnd = true;
        }

        _entryCarManager.BroadcastPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"{ANNOUNCEMENT_PREFIX}{_announcements[_currentAnnouncementIndex]}"
        });
        _currentAnnouncementIndex = (_currentAnnouncementIndex + 1) % _announcements.Count;
    }

    private void UpdateLeaderboard(object? sender, EventArgs args)
    {
        _pointsLeaderboard = GetPointsLeaderboard();
    }

    public static List<ulong> Admins()
    {
        return _admins;
    }

    public static void AddAdmin(ulong guid)
    {
        _admins.Add(guid);
    }

    public static void RemoveAdmin(ulong guid)
    {
        _admins.Remove(guid);
    }

    public static List<ulong> ConvoyLeaders()
    {
        return _convoyLeaders;
    }

    public static void AddConvoyLeader(ulong guid)
    {
        _convoyLeaders.Add(guid);
    }

    public static void RemoveConvoyLeader(ulong guid)
    {
        _convoyLeaders.Remove(guid);
    }

    public static List<(string, int)> Cars()
    {
        return _cars;
    }

    public static List<string> StarterCars()
    {
        return _starterCars;
    }

    public static List<(int, int)> ProminentCleanLapRewards()
    {
        return _prominentCleanLapRewards;
    }

    public static int GetCleanLapPointsReward(int cleanLaps)
    {
        if (_instance == null)
        {
            return 0;
        }

        foreach (var reward in _prominentCleanLapRewards)
        {
            if (reward.Item1 == cleanLaps)
            {
                return reward.Item2;
            }
        }

        if (_instance._config.ExtraCleanLapBonus.Enabled && cleanLaps >= _instance._config.ExtraCleanLapBonus.NeededCleanLaps)
        {
            return _instance._config.ExtraCleanLapBonus.BonusPoints;
        }

        return 0;
    }

    public void AddRecentLapStart(ulong driver, long time)
    {
        int count = _recentLapStarts.Count;
        if (count > 5)
        {
            _recentLapStarts.Dequeue();
        }

        _recentLapStarts.Enqueue((driver, time));
    }

    public Queue<(ulong, long)> RecentLapStarts()
    {
        return _recentLapStarts;
    }

    public Dictionary<string, (string, uint)> BestLapTimes()
    {
        return _bestLapTimes;
    }

    private void IncomingCollision(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        int speed = reader.Read<int>();
        if (speed < 0)
        {
            return;
        }

        int takenPoints = (int)(speed / _pointsDeductBySpeedFactor);
        if (takenPoints < 1)
        {
            return;
        }
        if (session.HostingConvoy())
        {
            takenPoints *= 2;
        }
        int max = session.HostingConvoy() ? _pointsDeductCollisionMax * 2 : _pointsDeductCollisionMax;
        if (takenPoints > max)
        {
            takenPoints = max;
        }

        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You had a collision at a speed of {speed}km/h and lost {takenPoints} points!"
        });
        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"{TIP_PREFIX}Since you had a collision, teleport to pits to preserve your clean laps and avoid a {_pointsDeductInvalidLap} point deduction. You can always use /status to check on this."
        });
        session.TakePoints(takenPoints);
        session.SetClean(false);
        session.AddCollision();
        Log.Information($"{PLUGIN_PREFIX}{session.Username()} had a collision!");
    }

    private void IncomingLapStart(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate || client.Name == null)
        {
            return;
        }

        long serverTimeMs = _asSessionManager.ServerTimeMilliseconds;
        session.SetDoingLap(true);
        session.SetLapStart(serverTimeMs);
        session.ResetAverageSpeed();
        foreach (var (key, convoy) in _convoyManager.Convoys())
        {
            if (key == client.Guid)
            {
                convoy.SetStartedTimeMs(serverTimeMs);
                _entryCarManager.BroadcastPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy has crossed the starting line! You still have 20 seconds to catch up if you'd like to earn a convoy bonus."
                });
                Task.Delay(20 * 1000).ContinueWith((_) => _convoyManager.CheckConvoy(_convoyManager.Convoys()[key], session));
                break;
            }
        }

        AddRecentLapStart(client.Guid, serverTimeMs);
        Log.Information($"{PLUGIN_PREFIX}{session.Username()} started a lap!");
    }

    private void IncomingLapCut(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        session.SetClean(false);
        session.AddCut();
        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"{TIP_PREFIX}You had a cut! Teleport to pits to preserve your clean laps and avoid a {_pointsDeductInvalidLap} point deduction. You can always use /status to check on this."
        });
        Log.Information($"{PLUGIN_PREFIX}{session.Username()} had a lap cut!");
    }

    private void IncomingPitLeave(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You paid {_pointsDeductLeavePits} points for leaving pits."
        });
        if (IsDoublePoints())
        {
            client.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"{DOUBLE_POINTS_PREFIX}It's that time, double points weekend! Make sure to get in as many laps as you can to get the most out of bonuses, etc. Enjoy!"
            });
        }
        session.TakePoints(_pointsDeductLeavePits);
    }

    private void IncomingPitReEntry(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        _entryCarManager.BroadcastPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"@{session.Username()} was deducted {_pointsDeductPitReEnter} points for driving back into pits!"
        });
        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"{TIP_PREFIX}You drove back into pits which is prohibited! Teleport back to pits to preserve your clean lap status."
        });
        session.SetClean(false);
        session.TakePoints(_pointsDeductPitReEnter);
        if (session.DoingLap())
        {
            session.SetDoingLap(false);
        }

        Log.Information($"{PLUGIN_PREFIX}{session.Username()} re-entered pits and was deducted points!");
    }

    private async void IncomingPitConvoyLeave(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        foreach (var (key, convoy) in _convoyManager.Convoys())
        {
            if (key == client.Guid)
            {
                await BroadcastWithDelayAsync($"{CONVOY_PREFIX}@{session.Username()}'s convoy is on the move!");
                break;
            }
        }
    }

    private void IncomingConvoyNearFinish(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        foreach (var (key, convoy) in _convoyManager.Convoys())
        {
            if (key == client.Guid)
            {
                _entryCarManager.BroadcastPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is approaching the finish line! Make sure to pass them and cross the finish line to claim your convoy bonus."
                });
                client.SendPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"{TIP_PREFIX}Hey convoy leader! Turn on your hazards, slowly let off the gas and pull off to the right where the concrete is!"
                });
                break;
            }
        }
    }

    private void IncomingPitTeleport(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        session.SetDoingLap(false);
        if (!session.IsClean())
        {
            client.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = "Your lap status is now clean after teleporting to pits!"
            });
        }
        session.SetClean(true);
        Log.Information($"{PLUGIN_PREFIX}{session.Username()} teleported to pits!");
    }

    private void IncomingConvoyAtNorthTurn(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at KM 2 (Nordkehre) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtAirfield(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at KM 4 (Flugplatz) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtFoxhole(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at KM 6 (Fuchsröhre) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtKallenForest(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at KM 8 (Kallenhard) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtWaterMill(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at KM 10 (Ex-Mühle) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtLittleValley(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at KM 12 (Kesselchen) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtFirstCarousel(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at the first carousel (Karussell) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtBrunnchen(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at YouTube Corner (Brünnchen) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void IncomingConvoyAtSecondCarousel(ACTcpClient client, PacketReader reader)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || !client.HasSentFirstUpdate)
        {
            return;
        }

        if (session.HostingConvoy())
        {
            foreach (var (key, convoy) in _convoyManager.Convoys())
            {
                if (key == client.Guid)
                {
                    _entryCarManager.BroadcastPacket(new ChatMessage
                    {
                        SessionId = 255,
                        Message = $"{CONVOY_PREFIX}@{session.Username()}'s convoy is at the second carousel (Kleines Karussell) going {session.Speed()}km/h."
                    });
                    break;
                }
            }
        }
    }

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        NordschleifeTrackdaySession? session = _sessionManager.AddSession(client);
        if (session == null)
        {
            return;
        }
        client.FirstUpdateSent += OnClientSpawn;
        client.LapCompleted += OnClientLapCompleted;

        string carModel = client.EntryCar.Model;
        foreach (var car in _cars)
        {
            if (car.Item1 == carModel)
            {
                int currentPoints = session.Points();
                if (car.Item2 > currentPoints)
                {
                    int more = car.Item2 - currentPoints;
                    string str = $"not allowed to drive the {carModel}! You need {more} points.";
                    session.PrepareKick(str);
                }
                break;
            }
        }
    }

    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        client.FirstUpdateSent -= OnClientSpawn;
        client.LapCompleted -= OnClientLapCompleted;
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null)
        {
            return;
        }

        if (client.HasSentFirstUpdate && !session.HasPreparedKick())
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"@{session.Username()}[{session.Points()}] disconnected and left the {client.EntryCar.Model} for grabs!"
            });
        }

        if (client.Name == null)
        {
            return;
        }
        foreach (var (key, convoy) in _convoyManager.Convoys())
        {
            if (key == client.Guid)
            {
                _ = _convoyManager.EndConvoyAsync(session, false);
            }

            convoy.RemoveFinishingDriver(client.Guid);
        }

        if (_convoyManager.RemoveOnlineConvoyLeader(session))
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"{CONVOY_PREFIX}Convoy leader {session.Username()} is no longer online."
            });
        }
        _sessionManager.RemoveSession(client, session);
    }

    private void OnClientSpawn(ACTcpClient client, EventArgs args)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null)
        {
            return;
        }

        string carModel = client.EntryCar.Model;
        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"_____"
        });
        _entryCarManager.BroadcastPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"@{session.Username()}[{session.Points()}] connected with the {carModel}!"
        });
        if (_convoyManager.AddOnlineConvoyLeader(session))
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"{CONVOY_PREFIX}Convoy leader {session.Username()} is now online."
            });
        }

        uint bestLapTimeToBeat = 0;
        string bestLapTimeToBeatBy = "";
        foreach (var (key, value) in _bestLapTimes)
        {
            if (key == carModel)
            {
                bestLapTimeToBeat = value.Item2;
                bestLapTimeToBeatBy = value.Item1;
                break;
            }
        }
        string lapTimeStr = TimeSpan.FromMilliseconds(bestLapTimeToBeat).ToString(@"mm\:ss\:fff");
        string yourselfStr = bestLapTimeToBeatBy != "" && bestLapTimeToBeatBy == client.Name ? " (You)" : "";
        client.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = bestLapTimeToBeat < 1 ? $"There's no lap time set for the {carModel} yet! You can be the first to set it." : $"The best lap time with the {carModel} is {lapTimeStr} by @{bestLapTimeToBeatBy}{yourselfStr}!"
        });

        string? kickReason = session.PreparedKickReason();
        if (kickReason != null)
        {
            if (_config.Extra.ImmediateKickCarNotUnlocked)
            {
                _entryCarManager.KickAsync(client, kickReason);
            }
            else
            {
                Task.Delay(30 * 1000).ContinueWith((_) => _ = _entryCarManager.KickAsync(client, kickReason));
                client.SendPacket(new ChatMessage
                {
                    SessionId = 255,
                    Message = $"You'll be kicked in 30 seconds! You are {kickReason}."
                });
                Task.Delay(5 * 1000).ContinueWith((_) => client.SendPacket(new NordschleifeTrackdayCarLockedPacket { IsLocked = 1 }));//sending immediately doesnt work
            }
        }
    }

    private void OnClientLapCompleted(ACTcpClient client, LapCompletedEventArgs args)
    {
        NordschleifeTrackdaySession? session = _sessionManager.GetSession(client.Guid);
        if (session == null || client.Name == null || !session.DoingLap() || session.LapStart() == 0)
        {
            return;
        }

        bool isLapInvalid = args.Packet.Cuts > 0 || !session.IsClean();
        bool rewardedForConvoy = false;
        foreach (var (key, convoy) in _convoyManager.Convoys())
        {
            if (!session.HostingConvoy() && !isLapInvalid && !rewardedForConvoy && (convoy.IsStartTimeValid(session.LapStart()) || convoy.StartingDrivers().Contains(client.Guid)))//checking convoy.StartingDrivers() ALSO allows drivers to be valid for a convoy bonus if they restart a lap but catch up to the same convoy
            {
                rewardedForConvoy = true;
                convoy.AddFinishingDriver(client.Guid);
            }
            else if (session.HostingConvoy() && key == client.Guid)
            {
                _ = _convoyManager.EndConvoyAsync(session);
            }
        }

        session.SetLapStart(0);
        session.SetDoingLap(false);
        if (isLapInvalid)
        {
            client.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"You completed an invalid lap and lost {_pointsDeductInvalidLap} points! Next time, make sure to teleport to the pits if your lap becomes invalid by a collision or cut. Also remember to check your lap status with /status."
            });
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"@{session.Username()} completed an invalid lap and lost {_pointsDeductInvalidLap} points!"
            });
            Log.Information($"{PLUGIN_PREFIX}{session.Username()} completed an invalid lap!");
            session.ResetCleanLaps();
            session.ResetMostRecentCleanLap();
            session.TakePoints(_pointsDeductInvalidLap);
            return;
        }

        uint lapTimeMs = args.Packet.LapTime;
        string carModel = client.EntryCar.Model;
        uint bestLapTimeToBeat = 0;
        string bestLapTimeToBeatBy = "";
        bool justBeat = false;
        string messageA = "";
        foreach (var (key, value) in _bestLapTimes)
        {
            if (key == carModel)
            {
                bestLapTimeToBeat = value.Item2;
                bestLapTimeToBeatBy = value.Item1;
                if (lapTimeMs < value.Item2 || value.Item2 < 1)
                {
                    justBeat = true;
                    _bestLapTimes[key] = (session.Username(), lapTimeMs);
                }
                break;
            }
        }
        string bestLapTimeToBeatStr = TimeSpan.FromMilliseconds(bestLapTimeToBeat).ToString(@"mm\:ss\:fff");
        string formattedLapTime = TimeSpan.FromMilliseconds(lapTimeMs).ToString(@"mm\:ss\:fff");
        if (justBeat)
        {
            if (bestLapTimeToBeat > 0 && bestLapTimeToBeatBy != "" && bestLapTimeToBeatBy != client.Name)
            {
                long lapTimeDifferenceMs = Math.Abs(bestLapTimeToBeat - lapTimeMs);
                string timeEnglish;
                if (lapTimeDifferenceMs >= 60000)
                {
                    long timeInMinutes = lapTimeDifferenceMs / 60000;
                    timeEnglish = $"{timeInMinutes} {(timeInMinutes == 1 ? "minute" : "minutes")}";
                }
                else
                {
                    double timeInSeconds = lapTimeDifferenceMs / 1000.0;
                    timeEnglish = $"{timeInSeconds:F3} {(timeInSeconds == 1.000 ? "second" : "seconds")}";
                }

                messageA = $"@{session.Username()} just beat @{bestLapTimeToBeatBy}'s lap time record of {bestLapTimeToBeatStr} by {timeEnglish} in the {carModel} and set a new record! {_pointsRewardBeatTb} points earned!";
                session.AddPoints(_pointsRewardBeatTb);
            }
            else if (bestLapTimeToBeat < 1 && bestLapTimeToBeatBy == "")
            {
                messageA = $"@{session.Username()} just set a record lap time of {formattedLapTime} in the {carModel}!";
            }
        }
        else if (!justBeat && bestLapTimeToBeat > 0)
        {
            messageA = $"@{bestLapTimeToBeatBy} still holds the best lap time record of {bestLapTimeToBeatStr} in the {carModel}!";
        }
        _entryCarManager.BroadcastPacket(new ChatMessage
        {
            SessionId = 255,
            Message = messageA
        });

        string messageB = $"@{session.Username()} completed a lap with a time of {formattedLapTime} averaging {session.AverageSpeed()}km/h. {_pointsRewardPerLap} points earned!";
        if (session.BestLapTime() != 0 && lapTimeMs < session.BestLapTime())
        {
            int totalPointsEarned = _pointsRewardPerLap + _pointsRewardBeatPb;
            messageB = $"@{session.Username()} just did a new personal best lap time of {formattedLapTime} averaging {session.AverageSpeed()}km/h! {totalPointsEarned} ({_pointsRewardPerLap} + {_pointsRewardBeatPb}) points earned!";
            session.AddPoints(_pointsRewardBeatPb);
            session.SetBestLapTime(lapTimeMs);
        }
        _entryCarManager.BroadcastPacket(new ChatMessage
        {
            SessionId = 255,
            Message = messageB
        });

        Log.Information($"{PLUGIN_PREFIX}{session.Username()} completed a clean lap!");
        session.AddPoints(_pointsRewardPerLap);
        session.AddCleanLap();
        CreateLaptime(client, lapTimeMs, session.AverageSpeed());
    }

    public static string SanitizeUsername(string name)
    {
        foreach (string str in _forbiddenUsernames)
        {
            if (name == str)
            {
                return $"_{str}";
            }
        }

        foreach (string str in _forbiddenUsernameSubstrings)
        {
            name = Regex.Replace(name, str, new string('*', str.Length), RegexOptions.IgnoreCase);
        }

        name = name[..Math.Min(name.Length, 80)];

        return name;
    }

    public static bool IsDoublePoints()
    {
        if (_instance == null || !_instance._config.Extra.DoublePointWeekend)
        {
            return false;
        }

        DayOfWeek day = DateTime.Today.DayOfWeek;
        return day == DayOfWeek.Saturday;
    }

    public static async Task SendDiscordWebhook(string message)
    {
        if (_instance == null || !_instance._config.DiscordWebhook.Enabled)
        {
            return;
        }

        string webhookUrl = _instance?._config.DiscordWebhook.WebhookURL ?? "";
        if (webhookUrl == "")
        {
            Log.Information($"{PLUGIN_PREFIX}Discord webhook not sent, URL in config is empty.");
            return;
        }

        using HttpClient client = new();
        string jsonPayload = JsonSerializer.Serialize(new { content = message });
        StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync(webhookUrl, content);
        if (response.IsSuccessStatusCode)
        {
            Log.Information($"{PLUGIN_PREFIX}Discord webhook sent successfully.");
        }
        else
        {
            Log.Information($"{PLUGIN_PREFIX}Failed to send Discord webhook. Status code: {response.StatusCode}");
        }
    }

    public async Task BroadcastWithDelayAsync(string msg, int delay = 1000, int repeat = 3)
    {
        for (int i = 0; i < repeat; i++)
        {
            _entryCarManager.BroadcastPacket(new ChatMessage
            {
                SessionId = 255,
                Message = msg
            });

            if (i < 2)
            {
                await Task.Delay(delay);
            }
        }
    }

    public Dictionary<ulong, (string, int)> Leaderboard()
    {
        return _pointsLeaderboard;
    }

    private Dictionary<ulong, (string, int)> GetPointsLeaderboard()
    {
        Dictionary<ulong, (string, int)> leaderboard = [];
        using (var command = new SQLiteCommand($"SELECT id, name, points FROM users ORDER BY points DESC LIMIT 0,{LEADERBOARD_MAX_ENTRIES}", _database))
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ulong id = Convert.ToUInt64(reader["id"]);
                string name = reader["name"].ToString() ?? NO_NAME;
                int points = Convert.ToInt32(reader["points"]);
                leaderboard.Add(id, (name, points));
            }
        }

        return leaderboard;
    }

    public void CreateLaptime(ACTcpClient client, uint time, int speed)
    {
        using var command = new SQLiteCommand("INSERT INTO laptimes (user_id, car, time, average_speedkmh, completed_on) VALUES (@user_id, @car, @time, @average_speedkmh, @completed_on)", _database);
        command.Parameters.AddWithValue("@user_id", client.Guid);
        command.Parameters.AddWithValue("@car", client.EntryCar.Model);
        command.Parameters.AddWithValue("@time", time);
        command.Parameters.AddWithValue("@average_speedkmh", speed);
        command.Parameters.AddWithValue("@completed_on", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.ExecuteNonQuery();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
