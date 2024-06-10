using FluentValidation;

namespace NordschleifeTrackdayPlugin;

public class NordschleifeTrackdayConfigurationValidator : AbstractValidator<NordschleifeTrackdayConfiguration>
{
    public NordschleifeTrackdayConfigurationValidator()
    {
        RuleFor(cfg => cfg.Admins).NotNull();
        RuleForEach(cfg => cfg.Cars).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.Value).GreaterThanOrEqualTo(0);
        });
        RuleForEach(cfg => cfg.StarterCars).NotNull();
        RuleForEach(cfg => cfg.CleanLapBonuses).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.Value).GreaterThanOrEqualTo(1);
        });

        RuleFor(cfg => cfg.Metrics).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.StartingPoints).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsDeductLeavePits).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsDeductInvalidLap).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsDeductPitReEnter).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsDeductBySpeedFactor).GreaterThanOrEqualTo(0.001);
            ch.RuleFor(cfg => cfg.PointsDeductCollisionMax).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsRewardPerLap).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsRewardBeatPb).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsRewardBeatOtherPb).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.PointsRewardConvoy).GreaterThanOrEqualTo(1);
        });
        RuleFor(cfg => cfg.ExtraCleanLapBonus).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.Enabled).NotNull();
            ch.RuleFor(cfg => cfg.NeededCleanLaps).GreaterThanOrEqualTo(1);
            ch.RuleFor(cfg => cfg.BonusPoints).GreaterThanOrEqualTo(1);
        });
        RuleFor(cfg => cfg.Announcements).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.Enabled).NotNull();
            ch.RuleFor(cfg => cfg.Interval).NotNull().GreaterThanOrEqualTo(60);
            ch.RuleFor(cfg => cfg.UseDefaultMessages).NotNull();
            ch.RuleFor(cfg => cfg.CustomMessages).NotNull();
        });
        RuleFor(cfg => cfg.DiscordWebhook).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.Enabled).NotNull();
            ch.RuleFor(cfg => cfg.WebhookURL).NotNull();
        });
        RuleFor(cfg => cfg.IdleKick).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.Enabled).NotNull();
            ch.RuleFor(cfg => cfg.DefaultMaxIdleTime).NotNull().GreaterThanOrEqualTo(60);
            ch.RuleFor(cfg => cfg.LongerMaxIdleTime).NotNull().GreaterThanOrEqualTo(60);
            ch.RuleFor(cfg => cfg.StarterMaxIdleTime).NotNull().GreaterThanOrEqualTo(60);
            ch.RuleFor(cfg => cfg.AllowLongerMaxIdleForCleanLaps).NotNull();
            ch.RuleFor(cfg => cfg.LongerMaxIdleNeededCleanLaps).GreaterThanOrEqualTo(1);
        });
        RuleFor(cfg => cfg.Extra).NotNull().ChildRules(ch =>
        {
            ch.RuleFor(cfg => cfg.DoublePointWeekend).NotNull();
            ch.RuleFor(cfg => cfg.ImmediateKickCarNotUnlocked).NotNull();
            ch.RuleFor(cfg => cfg.AssignConvoyLeadersByPoints).NotNull();
            ch.RuleFor(cfg => cfg.ConvoyLeadersNeededPoints).GreaterThanOrEqualTo(1);
        });
    }
}
