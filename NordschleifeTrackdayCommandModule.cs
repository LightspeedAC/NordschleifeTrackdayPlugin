using AssettoServer.Commands;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Shared;
using NordschleifeTrackdayPlugin.Packets;
using NordschleifeTrackdayPlugin.Session;
using Qmmands;

namespace NordschleifeTrackdayPlugin;

public class NordschleifeTrackdayCommandModule : ACModuleBase
{
    private readonly NordschleifeTrackdayPlugin _plugin;

    public NordschleifeTrackdayCommandModule(NordschleifeTrackdayPlugin plugin)
    {
        _plugin = plugin;
    }

    [Command("afp")]
    public void AddFakePoints(int i = 0, ACTcpClient? driver = null)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin._admins.Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        bool providedOther = driver != null;
        ulong otherGuid = driver?.Guid ?? 0;
        if (providedOther && otherGuid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(providedOther ? otherGuid : Client.Guid);
        if (session == null)
        {
            return;
        }

        session.AddPoints(i);
        Reply(providedOther ? $"Gave {i} points to @{driver?.Name}." : $"Added {i} points.");
        driver?.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You were given {i} points by @{Client.Name}!"
        });
    }

    [Command("tfp")]
    public void TakeFakePoints(int i = 0, ACTcpClient? driver = null)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin._admins.Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        bool providedOther = driver != null;
        ulong otherGuid = driver?.Guid ?? 0;
        if (providedOther && otherGuid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(providedOther ? otherGuid : Client.Guid);
        if (session == null)
        {
            return;
        }

        session.TakePoints(i);
        Reply(providedOther ? $"Took {i} points from @{driver?.Name}." : $"Took {i} points.");
        driver?.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"{i} points were taken from you by @{Client.Name}!"
        });
    }

    [Command("cs")]
    public void ConvoyStart()
    {
        if (Client == null || Client.Name == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.IsAConvoyLeader(session))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (session.DoingLap())
        {
            Reply("Convoys can only be started from pits.");
            return;
        }

        if (!NordschleifeTrackdayPlugin.StartConvoy(Client.Name, session))
        {
            Reply("You're already hosting a convoy.");
            return;
        }
    }

    [Command("ce")]
    public void ConvoyEnd()
    {
        if (Client == null || Client.Name == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.IsAConvoyLeader(session))
        {
            Reply("You have no access to this command!");
            return;
        }

        _ = NordschleifeTrackdayPlugin.EndConvoyAsync(Client.Name, session, false);
    }

    [Command("ct")]
    public void ConvoyTransfer(ACTcpClient driver)
    {
        if (Client == null || Client.Name == null || driver.Name == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin._admins.Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (driver.Guid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdaySession? otherSession = _plugin._nordschleifeTrackdaySessionManager.GetSession(driver.Guid);
        if (otherSession == null)
        {
            Reply("Driver not found with a valid session!");
            return;
        }

        if (otherSession.DoingLap())
        {
            Reply($"{driver.Name} isn't at pits! You can only transfer the leadership of a convoy to drivers currently in pits.");
            return;
        }

        bool success = false;
        foreach (var convoy in NordschleifeTrackdayPlugin.Convoys())
        {
            if (convoy.Key == Client.Name)
            {
                session.SetHostingConvoy(false);
                otherSession.SetHostingConvoy(true);
                NordschleifeTrackdayPlugin.RemoveConvoy(Client.Name);
                NordschleifeTrackdayPlugin.AddConvoy(driver.Name, convoy.Value.Item2, convoy.Value.Item3);
                success = true;
                break;
            }
        }

        if (!success)
        {
            Reply("You're not currently hosting a convoy.");
        }
        else
        {
            Reply($"Successfully transferred leadership of the convoy to {driver.Name}.");
            driver.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"You're now taking leadership of {Client.Name}'s convoy, goodluck!"
            });
        }
    }

    [Command("chelp")]
    public void ConvoyHelp()
    {
        if (Client == null)
        {
            return;
        }

        Reply("Basics:");
        Reply($"Convoys can only be started from pits, and by a few drivers. Each convoy must have at least {NordschleifeTrackdayPlugin.CONVOY_MIN_DRIVERS_NEEDED} participating drivers to give out rewards on lap completion. That reward is {NordschleifeTrackdayPlugin._pointsRewardConvoy} points.");
        Reply($"Covnoy leaders must keep a steady pace (8-9 minute lap) and ensure they can complete the entire lap without disconnecting, or having a collision. This is because many people may be relying on them for points, bonus, etc.!");
        Reply($"In a convoy, the leader should enforce slower cars being first and faster cars towards the back of the convoy. Make sure to respect this rule as to not leave anyone behind!");
        Reply($"While any convoy leader is hosting a convoy, point deductions from collisions are doubled for them only. This is just to enforce clean convoys as best as possible.");
        Reply($"As a convoy approaches the finish line, the leader will pull off to the right, allowing all other drivers to overtake them and finish their lap. So be sure to stay left as you make your final turn onto the straight.");
        Reply($"After the convoy leader is done waiting for all other drivers to finish their lap, they themselves will finish their lap to conclude the convoy and reward the participating drivers their convoy bonus!");
        Reply($"It's important to note that only drivers who start their lap within 20 seconds of any convoy's lap start, will be able to claim a convoy bonus on lap completion- again, before the convoy leader finishes their lap.");
        Reply("Convoy Leader Requirements:");
        Reply($"To host convoys, you require {NordschleifeTrackdayPlugin._pointsNeededForConvoyLeader}+ points! As you hit the required points, you'll be automatically able to start and end your own convoys. Abusing this system can result in the reset and ban of your account!");
        Reply("Commands:");
        Reply("- /cs: Start a convoy");
        Reply("- /ce: End your convoy");
        Reply("- /ct[args: driver]: Transfer leadership of your convoy");
    }

    [Command("help")]
    public void Help()
    {
        if (Client == null)
        {
            return;
        }

        Reply("Basics:");
        Reply($"Every driver starts with {NordschleifeTrackdayPlugin._pointsStarting} points. Each clean lap you complete earns you {NordschleifeTrackdayPlugin._pointsRewardPerLap} points, each time you beat your best lap time, you earn an additional {NordschleifeTrackdayPlugin._pointsRewardBeatPb} points.");
        Reply($"Beating a lap time record set by someone else earns you {NordschleifeTrackdayPlugin._pointsRewardBeatTb} points!");
        Reply("As you complete more clean laps and create a streak, you earn even more bonus points (refer to /bonuses for more info).");
        Reply("This streak of clean laps will be reset after an hour of no new laps being completed.");
        Reply("Each collision will cost you points, the final amount is dependant on your speed at the time of the collision.");
        Reply("You'll also be deducted points for completing an invalid lap, whether it be by a cut or a collision with a driver, or the enviornment.");
        Reply("Commands:");
        Reply("- /chelp: See the convoy help command");
        Reply("- /convoys: See a list of online convoy leaders and any ongoing convoys");
        Reply("- /convoy[args: driver]: See info on a specific convoy, like when it started and its drivers");
        Reply("- /cars: See the list of cars, how many points each of them require, and whether you have unlocked them");
        Reply("- /bonuses: See a list of bonus points you can earn");
        Reply("- /status: See if you're clean, your clean laps streak, points, cuts and collisions");
        Reply("- /points: See your points");
        Reply($"- /pb: See your best lap time with the {Client.EntryCar.Model}");
        Reply($"- /best: See the best lap time record for the {Client.EntryCar.Model}");
        Reply("- /allbest: See the best lap time records for every car on the server");
        Reply("- /cl: See your clean laps streak");
        Reply("- /tl: See your total laps");
        if (NordschleifeTrackdayPlugin._admins.Contains(Client.Guid))
        {
            Reply("- /afp[args: int, driver(opt)]: Add points to self or others");
            Reply("- /tfp[args: int, driver(opt)]: Remove points from self or others");
        }
    }

    [Command("convoys")]
    public void Convoys()
    {
        if (Client == null)
        {
            return;
        }

        if (NordschleifeTrackdayPlugin.OnlineConvoyLeaders().Count > 0)
        {
            Reply("Online Convoy Leaders:");
            foreach (var name in NordschleifeTrackdayPlugin.OnlineConvoyLeaders())
            {
                Reply($"\n- {name}");
            }
        }
        else
        {
            Reply("There are no convoy leaders currently online.");
        }

        if (NordschleifeTrackdayPlugin.Convoys().Count > 0)
        {
            Reply("Ongoing Convoys:");
            foreach (var convoy in NordschleifeTrackdayPlugin.Convoys())
            {
                string startedStr = convoy.Value.Item1 > 0 ? "started" : "waiting";
                Reply($"\n- {convoy.Key} ({startedStr}): {convoy.Value.Item3.Count} drivers");
            }
        }
    }

    [Command("convoy")]
    public void ConvoyInfo(ACTcpClient driver)
    {
        if (Client == null || driver.Name == null)
        {
            return;
        }
        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(driver.Guid);
        if (session == null)
        {
            Reply("Driver not found with a valid session!");
            return;
        }

        string convoyLeaderName = driver.Name;
        if (NordschleifeTrackdayPlugin.Convoys().Count > 0)
        {
            foreach (var convoy in NordschleifeTrackdayPlugin.Convoys())
            {
                if (convoy.Key.StartsWith(convoyLeaderName))
                {
                    int driversCount = convoy.Value.Item3.Count;
                    string driversStr = driversCount > 0 ? string.Join(", ", convoy.Value.Item3) : "populated 20 seconds after start";
                    string timeEnglish = "not yet";
                    string averageSpeedEnglish = "";
                    if (convoy.Value.Item1 > 0)
                    {
                        long timeInSeconds = (_plugin._sessionManager.ServerTimeMilliseconds - convoy.Value.Item1) / 1000;
                        if (timeInSeconds >= 60)
                        {
                            long timeInMinutes = timeInSeconds / 60;
                            timeEnglish = $"{timeInMinutes} {(timeInMinutes == 1 ? "minute" : "minutes")} ago";
                        }
                        else
                        {
                            timeEnglish = $"{timeInSeconds} {(timeInSeconds == 1 ? "second" : "seconds")} ago";
                        }

                        averageSpeedEnglish = $"\nAverage speed: {session.AverageSpeed()}km/h";
                    }

                    Reply($"{convoy.Key}'s Convoy ({driver.EntryCar.Model}):\nStarted: {timeEnglish}{averageSpeedEnglish}");
                    Reply($"\nDrivers ({driversCount}): {driversStr}");
                    break;
                }
                else
                {
                    Reply($"No convoy by the leader '{convoyLeaderName}' was found.");
                }
            }
        }
        else
        {
            Reply("There are no ongoing convoys.");
        }
    }

    [Command("cars")]
    public void Cars()
    {
        if (Client == null)
        {
            return;
        }

        Reply("List of Cars:");
        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            foreach (var car in NordschleifeTrackdayPlugin.Cars())
            {
                Reply($"\n- {car.Item1}: {car.Item2} points");
            }
        }
        else
        {
            foreach (var car in NordschleifeTrackdayPlugin.Cars())
            {
                int diff = Math.Abs(session.Points() - car.Item2);
                string unlockedStr = session.Points() >= car.Item2 ? "Unlocked" : $"Locked - {diff} more {(diff == 1 ? "point" : "points")}";
                Reply($"\n- {car.Item1}: {car.Item2} points ({unlockedStr})");
            }
        }
    }

    [Command("bonuses")]
    public void Bonuses()
    {
        if (Client == null || NordschleifeTrackdayPlugin._instance == null)
        {
            return;
        }

        Reply("List of Point Bonuses:");
        Reply($"\n- Beating a personal best lap time: {NordschleifeTrackdayPlugin._pointsRewardBeatPb} points");
        Reply($"\n- Beating the best lap time record (set by someone else): {NordschleifeTrackdayPlugin._pointsRewardBeatTb} points");
        foreach (var reward in NordschleifeTrackdayPlugin.ProminentCleanLapRewards())
        {
            Reply($"\n- {reward.Item1} clean laps: {reward.Item2} points");
        }
        Reply($"\n(every other clean lap at/above {NordschleifeTrackdayPlugin._instance._config.ExtraCleanLapBonus.NeededCleanLaps} earns you {NordschleifeTrackdayPlugin._instance._config.ExtraCleanLapBonus.BonusPoints} bonus points)");
    }

    [Command("status")]
    public void Status()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        int cleanLaps = session.CleanLaps();
        string lapsEnglish = cleanLaps > 1 || cleanLaps == 0 ? "laps" : "lap";
        int cuts = session.Cuts();
        int collisions = session.Collisions();
        Reply(session.IsClean() ? $"You're clean! You have {cleanLaps} clean {lapsEnglish}, and {session.Points()} points. You have {cuts} cuts and {collisions} collisions." : $"You're NOT clean, you must teleport to pits! You have {cleanLaps} clean {lapsEnglish}, and {session.Points()} points. You have {cuts} cuts and {collisions} collisions.");
    }

    [Command("points")]
    public void Points()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        Reply($"You have {session.Points()} points.");
    }

    [Command("pb")]
    public void PersonalBest()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        uint bestLapTime = session.BestLapTime();
        string bestLapTimeStr = TimeSpan.FromMilliseconds(bestLapTime).ToString(@"mm\:ss\:fff");
        Reply(bestLapTime > 0 ? $"Your best lap time with the {Client.EntryCar.Model} is {bestLapTimeStr}." : $"You haven't yet completed a clean lap with the {Client.EntryCar.Model}, now is the time!");
    }

    [Command("best")]
    public void Best()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        string carModel = Client.EntryCar.Model;
        uint bestLapTimeToBeat = 0;
        string bestLapTimeToBeatBy = "";
        foreach (var (key, value) in NordschleifeTrackdayPlugin.BestLapTimes())
        {
            if (key == carModel)
            {
                bestLapTimeToBeat = value.Item2;
                bestLapTimeToBeatBy = value.Item1;
                break;
            }
        }
        string lapTimeStr = TimeSpan.FromMilliseconds(bestLapTimeToBeat).ToString(@"mm\:ss\:fff");
        string yourselfStr = bestLapTimeToBeatBy != "" && bestLapTimeToBeatBy == Client.Name ? " (You)" : "";
        Reply(bestLapTimeToBeat < 1 ? $"There's no lap time set for the {carModel} yet! You can be the first to set it." : $"The best lap time with the {carModel} is {lapTimeStr} by @{bestLapTimeToBeatBy}{yourselfStr}!");
    }

    [Command("allbest")]
    public void AllBest()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        var bestLapTimesSorted = NordschleifeTrackdayPlugin.BestLapTimes()
            .OrderBy(pair => pair.Value.Item2 < 1 ? uint.MaxValue : pair.Value.Item2)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        Reply("List of Best Laptimes:");
        foreach (var (key, value) in bestLapTimesSorted)
        {
            string carModel = key;
            uint bestLapTimeToBeat = value.Item2;
            string bestLapTimeToBeatBy = value.Item1;
            string lapTimeStr = TimeSpan.FromMilliseconds(bestLapTimeToBeat).ToString(@"mm\:ss\:fff");
            string yourselfStr = bestLapTimeToBeatBy != "" && bestLapTimeToBeatBy == Client.Name ? " (You)" : "";
            Reply(bestLapTimeToBeat > 0 ? $"\n- {carModel}: {lapTimeStr} by @{bestLapTimeToBeatBy}{yourselfStr}" : $"\n- {carModel}: no time set yet");
        }
    }

    [Command("cl")]
    public void CleanLaps()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        int cleanLaps = session.CleanLaps();
        string lapsEnglish = cleanLaps > 1 ? "laps" : "lap";
        long timeDiff = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - session.MostRecentCleanLap());
        string timeEnglish;
        if (timeDiff >= 60)
        {
            long timeInMinutes = timeDiff / 60;
            timeEnglish = $"{timeInMinutes} {(timeInMinutes == 1 ? "minute" : "minutes")} ago";
        }
        else
        {
            timeEnglish = $"{timeDiff} {(timeDiff == 1 ? "second" : "seconds")} ago";
        }
        Reply(session.CleanLaps() > 0 ? $"You currently have {cleanLaps} clean {lapsEnglish}. Your most recent clean lap was completed {timeEnglish}." : $"You currently have no clean lap streak.");
    }

    [Command("tl")]
    public void TotalLaps()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._nordschleifeTrackdaySessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        Reply($"You have {session.TotalLaps()} total laps.");
    }
}
