using AssettoServer.Server.Configuration;

namespace NordschleifeTrackdayPlugin;

public class NordschleifeTrackdayConfiguration : IValidateConfiguration<NordschleifeTrackdayConfigurationValidator>
{
    public List<ulong> Admins { get; init; } = [];
    public Dictionary<string, int> Cars = [];
    public List<string> StarterCars = [];
    public Dictionary<int, int> CleanLapBonuses = [];

    public MetricsConfiguration Metrics { get; init; } = new();
    public ExtraCleanLapBonusConfiguration ExtraCleanLapBonus { get; init; } = new();
    public AnnouncementsConfiguration Announcements { get; init; } = new();
    public DiscordWebhookConfiguration DiscordWebhook { get; init; } = new();
    public IdleKickConfiguration IdleKick { get; init; } = new();
    public ExtraConfiguration Extra { get; init; } = new();
}

public class MetricsConfiguration
{
    public int StartingPoints { get; set; } = 500;
    public int PointsDeductLeavePits { get; set; } = 3;
    public int PointsDeductInvalidLap { get; set; } = 250;
    public int PointsDeductPitReEnter { get; set; } = 200;
    public double PointsDeductBySpeedFactor { get; set; } = 1.4;
    public int PointsDeductCollisionMax { get; set; } = 400;
    public int PointsRewardPerLap { get; set; } = 30;
    public int PointsRewardBeatPb { get; set; } = 50;
    public int PointsRewardBeatOtherPb { get; set; } = 75;
    public int PointsRewardConvoy { get; set; } = 150;
}

public class ExtraCleanLapBonusConfiguration
{
    public bool Enabled { get; set; } = true;
    public int NeededCleanLaps { get; set; } = 10;
    public int BonusPoints { get; set; } = 100;
}

public class AnnouncementsConfiguration
{
    public bool Enabled { get; set; } = true;
    public int Interval { get; set; } = 600;
    public List<string> Messages { get; init; } = [];
}

public class DiscordWebhookConfiguration
{
    public bool Enabled { get; set; } = true;
    public string WebhookURL { get; set; } = "";
}

public class IdleKickConfiguration
{
    public bool Enabled { get; set; } = true;
    public int DefaultMaxIdleTime { get; init; } = 600;
    public int LongerMaxIdleTime { get; init; } = 900;
    public int StarterMaxIdleTime { get; init; } = 3600;
    public bool AllowLongerMaxIdleForCleanLaps { get; init; } = true;
    public int LongerMaxIdleNeededCleanLaps { get; init; } = 5;
}

public class ExtraConfiguration
{
    public bool DoublePointWeekend { get; init; } = true;
    public bool ImmediateKickCarNotUnlocked { get; init; } = false;
    public bool AssignConvoyLeadersByPoints { get; init; } = true;
    public int ConvoyLeadersNeededPoints { get; init; } = 6500;
}