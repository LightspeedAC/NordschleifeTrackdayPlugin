using AssettoServer.Commands;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Shared;
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

    [Command("afp", "addpoints")]
    public void AddFakePoints(int i = 0, ACTcpClient? driver = null)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
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

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(providedOther ? otherGuid : Client.Guid);
        if (session == null)
        {
            return;
        }

        session.AddPoints(i, false);
        Reply(providedOther ? $"Gave {i} points to @{driver?.Name}." : $"Added {i} points.");
        driver?.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You were given {i} points by @{Client.Name}!"
        });
    }

    [Command("tfp", "takepoints")]
    public void TakeFakePoints(int i = 0, ACTcpClient? driver = null)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
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

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(providedOther ? otherGuid : Client.Guid);
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

    [Command("rp", "resetpoints")]
    public void ResetPoints(ACTcpClient? driver = null)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
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

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(providedOther ? otherGuid : Client.Guid);
        if (session == null)
        {
            return;
        }

        session.ResetPoints();
        Reply(providedOther ? $"Reset @{driver?.Name}'s points." : $"Points reset.");
        driver?.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"Your points were reset by @{Client.Name}!"
        });
    }

    [Command("rcc", "resetcc")]
    public void ResetCutsCollisions(ACTcpClient? driver = null)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
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

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(providedOther ? otherGuid : Client.Guid);
        if (session == null)
        {
            return;
        }

        session.ResetCuts();
        session.ResetCollisions();
        Reply(providedOther ? $"Reset @{driver?.Name}'s cuts and collisions." : $"Cuts and collisions reset.");
        driver?.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"Your cuts and collisions were reset by @{Client.Name}!"
        });
    }

    [Command("ca", "createadmin")]
    public void CreateAdmin(ACTcpClient driver)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (driver.Guid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdayPlugin.AddAdmin(driver.Guid);
        Reply($"You set {driver.Name} as an admin (temp).");
        driver.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You are now an admin!"
        });
    }

    [Command("ra", "removeadmin")]
    public void RemoveAdmin(ACTcpClient driver)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (driver.Guid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdayPlugin.RemoveAdmin(driver.Guid);
        Reply($"You removed {driver.Name} as an admin (temp).");
        driver.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You are no longer an admin!"
        });
    }

    [Command("ccl", "createconvoyleader")]
    public void CreateConvoyLeader(ACTcpClient driver)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (driver.Guid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdayPlugin.AddConvoyLeader(driver.Guid);
        Reply($"You set {driver.Name} as a convoy leader (temp).");
        driver.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You are now a convoy leader!"
        });
    }

    [Command("rcl", "removeconvoyleader")]
    public void RemoveConvoyLeader(ACTcpClient driver)
    {
        if (Client == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (driver.Guid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdayPlugin.RemoveConvoyLeader(driver.Guid);
        Reply($"You removed {driver.Name} as a convoy leader (temp).");
        driver.SendPacket(new ChatMessage
        {
            SessionId = 255,
            Message = $"You are no longer a convoy leader!"
        });
    }

    [Command("cs", "convoystart", "startconvoy")]
    public void ConvoyStart()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        if (!_plugin._convoyManager.IsAConvoyLeader(session))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (session.DoingLap())
        {
            Reply("Convoys can only be started from pits.");
            return;
        }

        if (!_plugin._convoyManager.StartConvoy(session))
        {
            Reply("You're already hosting a convoy.");
            return;
        }
    }

    [Command("ce", "convoyend", "endconvoy")]
    public void ConvoyEnd(ACTcpClient? driverConvoyLeader = null)
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(driverConvoyLeader?.Guid ?? Client.Guid);
        if (session == null)
        {
            return;
        }

        string? name = driverConvoyLeader?.Name;
        bool otherProvied = name != null;
        if (otherProvied)
        {
            if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
            {
                Reply("You can't end another driver's convoy!");
                return;
            }

            Reply($"You ended {name}'s convoy.");
            driverConvoyLeader?.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"Your convoy was ended by {Client.Name}."
            });
        }
        else
        {
            if (!_plugin._convoyManager.IsAConvoyLeader(session))
            {
                Reply("You have no access to this command!");
                return;
            }

            Reply("Your convoy was ended.");
        }

        _ = _plugin._convoyManager.EndConvoyAsync(session, false);
    }

    [Command("ct", "convoytransfer", "transferconvoy")]
    public async void ConvoyTransfer(ACTcpClient driver, ACTcpClient? driverConvoyLeader = null)
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        if (!NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
        {
            Reply("You have no access to this command!");
            return;
        }

        if (driver.Guid == Client.Guid)
        {
            Reply("You can't use this command on yourself!");
            return;
        }

        NordschleifeTrackdaySession? otherSession = _plugin._sessionManager.GetSession(driver.Guid);
        if (otherSession == null)
        {
            Reply("Driver not found with a valid session!");
            return;
        }

        bool success = false;
        NordschleifeTrackdaySession? otherConvoyLeaderSession = null;
        if (driverConvoyLeader != null)
        {
            otherConvoyLeaderSession = _plugin._sessionManager.GetSession(driverConvoyLeader.Guid);
            if (otherConvoyLeaderSession == null)
            {
                Reply("Convoy leader not found with a valid session!");
                return;
            }
        }

        ACTcpClient search = driverConvoyLeader ?? Client;
        NordschleifeTrackdaySession ssn = otherConvoyLeaderSession ?? session;
        foreach (var convoy in _plugin._convoyManager.Convoys())
        {
            if (convoy.Key == search.Guid)
            {
                ssn.SetHostingConvoy(null);
                otherSession.SetHostingConvoy(convoy.Value);
                _plugin._convoyManager.RemoveConvoy(search.Guid);
                _plugin._convoyManager.AddConvoy(otherSession, convoy.Value.FinishingDrivers(), convoy.Value.StartingDrivers());
                success = true;
                break;
            }
        }

        string name = driverConvoyLeader != null ? driverConvoyLeader?.Name ?? NordschleifeTrackdayPlugin.NO_NAME : Client?.Name ?? NordschleifeTrackdayPlugin.NO_NAME;
        if (!success)
        {
            Reply(driverConvoyLeader != null ? $"{driverConvoyLeader.Name} is not currently hosting a convoy." : "You're not currently hosting a convoy.");
        }
        else
        {
            Reply($"Successfully transferred leadership of {(driverConvoyLeader != null ? "{name}'s" : "your")} convoy to {driver.Name}.");
            driver.SendPacket(new ChatMessage
            {
                SessionId = 255,
                Message = $"You're now taking leadership of {name}'s convoy, goodluck!"
            });
        }

        await _plugin.BroadcastWithDelayAsync($"{NordschleifeTrackdayPlugin.CONVOY_PREFIX}Leadership of {name}'s convoy was transferred to {driver.Name} in the {driver.EntryCar.Model}!");
    }

    [Command("chelp")]
    public void ConvoyHelp()
    {
        if (Client == null)
        {
            return;
        }

        bool isAdmin = NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid);
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
        Reply($"- /ce: End your convoy{(isAdmin ? ", or another convoy" : "")}");
        if (isAdmin)
        {
            Reply("- /ct[args: driver, driverConvoyLeader(opt)]: Transfer leadership of your convoy to another driver, or the leadership of another convoy to another driver");
        }
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
        Reply("- /cars: See the list of cars, how many points each of them require, and whether you've unlocked them");
        Reply("- /next2unlock: See a list of cars you're about to unlock");
        Reply("- /unlocked: See a list of cars you've already unlocked");
        Reply("- /bonuses: See a list of bonus points you can earn");
        Reply("- /status: See your lap status, your clean laps, points, and cuts/collisions");
        Reply("- /points: See your points");
        Reply("- /tpoints: See a list of drivers with the most points");
        Reply($"- /pb: See your best lap time with the {Client.EntryCar.Model}");
        Reply($"- /best: See the best lap time record for the {Client.EntryCar.Model}");
        Reply("- /allbest: See the best lap time records for every car on the server");
        Reply("- /cl: See your clean laps streak");
        Reply("- /tl: See your total laps");
        Reply("- /ttl: See a list of drivers with the most clean laps completed");
        if (NordschleifeTrackdayPlugin.Admins().Contains(Client.Guid))
        {
            Reply("- /afp[args: int, driver(opt)]: Add points to yourself or others");
            Reply("- /tfp[args: int, driver(opt)]: Remove points from yourself or others");
            Reply("- /rp[driver(opt)]: Reset your points or someone elses");
            Reply("- /rcc[driver(opt)]: Reset your cuts/collisions or someone elses");
            Reply("- /ca[args: driver]: Add a temporary admin (doesn't save during server restart)");
            Reply("- /ra[args: driver]: Temporarily remove an admin (doesn't save during server restart)");
            Reply("- /ccl[args: driver]: Add a temporary convoy leader (doesn't save during server restart)");
            Reply("- /rcl[args: driver]: Temporarily remove a convoy leader (doesn't save during server restart)");
        }
    }

    [Command("convoys", "cys")]
    public void Convoys()
    {
        if (Client == null)
        {
            return;
        }

        if (_plugin._convoyManager.OnlineConvoyLeaders().Count > 0)
        {
            Reply("Online Convoy Leaders:");
            foreach (var name in _plugin._convoyManager.OnlineConvoyLeaders())
            {
                Reply($"\n- {name}");
            }
        }
        else
        {
            Reply("There are no convoy leaders currently online.");
        }

        var convoys = _plugin._convoyManager.Convoys();
        if (convoys.Count > 0)
        {
            Reply("Ongoing Convoys:");
            foreach (var convoy in convoys)
            {
                string startedStr = "waiting";
                if (convoy.Value.HasStarted())
                {
                    startedStr = "started";
                }
                else if (convoy.Value.IsOnMove())
                {
                    startedStr = "on the move";
                }
                Reply($"\n- {convoy.Value.Leader().Username()} ({startedStr}): {convoy.Value.StartingDrivers().Count} drivers");
            }
        }
    }

    [Command("convoy", "cy")]
    public void ConvoyInfo(ACTcpClient driver)
    {
        if (Client == null)
        {
            return;
        }
        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(driver.Guid);
        if (session == null)
        {
            Reply("Driver not found with a valid session!");
            return;
        }

        var convoys = _plugin._convoyManager.Convoys();
        if (convoys.Count > 0)
        {
            foreach (var convoy in convoys)
            {
                if (convoy.Key == driver.Guid)
                {
                    int driversCount = convoy.Value.StartingDrivers().Count;
                    List<string> startingDriversStr = [];
                    for (int i = 0; i < driversCount; i++)
                    {
                        NordschleifeTrackdaySession? otherDriverSession = _plugin._sessionManager.GetSession(convoy.Value.StartingDrivers()[i]);
                        if (otherDriverSession != null)
                        {
                            startingDriversStr.Add(otherDriverSession.Username());
                        }
                        else
                        {
                            startingDriversStr.Add(NordschleifeTrackdayPlugin.NO_NAME);
                        }
                    }
                    string driversStr = "populated 20 seconds after start";
                    if (driversCount > 0)
                    {
                        driversStr = string.Join(", ", startingDriversStr);
                    }
                    string timeEnglish = "not yet";
                    string averageSpeedEnglish = "";
                    if (convoy.Value.HasStarted())
                    {
                        long timeInSeconds = (_plugin._asSessionManager.ServerTimeMilliseconds - convoy.Value.StartedTimeMs()) / 1000;
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

                    Reply($"{convoy.Value.Leader().Username()}'s Convoy ({driver.EntryCar.Model}):\nStarted: {timeEnglish}{averageSpeedEnglish}");
                    Reply($"\nDrivers ({driversCount}): {driversStr}");
                    break;
                }
                else
                {
                    Reply($"No convoy by the leader '{driver.Name}' was found.");
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
        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        foreach (var car in NordschleifeTrackdayPlugin.Cars())
        {
            int diff = Math.Abs(session.Points() - car.Item2);
            string unlockedStr = session.Points() >= car.Item2 ? "Unlocked" : $"Locked - {diff} more {(diff == 1 ? "point" : "points")}";
            Reply($"\n- {car.Item1}: {car.Item2} points ({unlockedStr})");
        }
    }

    [Command("next2unlock", "cars2unlock")]
    public void NextCarsToUnlock()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        int max = _plugin._config.Extra.Next2UnlockMaxEntries;
        Reply($"Next ({max}) Cars to Unlock:");
        int c = 0;
        var cars = NordschleifeTrackdayPlugin.Cars();
        cars.Reverse();
        foreach (var car in cars)
        {
            if (session.Points() < car.Item2)
            {
                int diff = car.Item2 - session.Points();
                Reply($"\n- {car.Item1}: {diff} more points");
                c++;
                if (c >= max)
                {
                    break;
                }
            }
        }
        if (c < 1)
        {
            Reply($"\n- You've already unlocked every car");
        }
    }

    [Command("unlocked", "unlock", "carsunlocked")]
    public void UnlockedCars()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        Reply("Unlocked Cars:");
        foreach (var car in NordschleifeTrackdayPlugin.Cars())
        {
            if (session.Points() >= car.Item2)
            {
                Reply($"\n- {car.Item1}");
            }
        }
    }

    [Command("bonuses", "bns")]
    public void Bonuses()
    {
        var inst = NordschleifeTrackdayPlugin.Instance();
        if (Client == null || inst == null)
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
        if (_plugin._config.ExtraCleanLapBonus.Enabled)
        {
            Reply($"\n(every other clean lap at/above {inst._config.ExtraCleanLapBonus.NeededCleanLaps} earns you {inst._config.ExtraCleanLapBonus.BonusPoints} bonus points)");
        }
    }

    [Command("status", "sts")]
    public void Status()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
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

    [Command("points", "pts")]
    public void Points()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        Reply($"You have {session.Points()} points.");
    }

    [Command("tpoints", "tpts")]
    public void TopPoints()
    {
        Reply("Richest Drivers:");
        int i = 1;
        foreach (var entry in _plugin.PointsLeaderboard())
        {
            Reply($"{i}. @{entry.Value.Item1} - {entry.Value.Item2} points");
            i++;
            if (i > NordschleifeTrackdayPlugin.LEADERBOARD_MAX_ENTRIES)
            {
                break;
            }
        }
    }

    [Command("pb", "personalbest", "mybest")]
    public void PersonalBest()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        uint bestLapTime = session.BestLapTime();
        string bestLapTimeStr = TimeSpan.FromMilliseconds(bestLapTime).ToString(@"mm\:ss\:fff");
        Reply(bestLapTime > 0 ? $"Your best lap time with the {Client.EntryCar.Model} is {bestLapTimeStr}." : $"You haven't yet completed a clean lap with the {Client.EntryCar.Model}, now is the time!");
    }

    [Command("best", "fastest")]
    public void Best()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        string carModel = Client.EntryCar.Model;
        uint bestLapTimeToBeat = 0;
        string bestLapTimeToBeatBy = "";
        foreach (var (key, value) in _plugin.BestLapTimes())
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

    [Command("allbest", "ab")]
    public void AllBest()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        var bestLapTimesSorted = _plugin.BestLapTimes()
            .OrderBy(pair => pair.Value.Item2 < 1 ? uint.MaxValue : pair.Value.Item2)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        Reply("List of Best Lap Times:");
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

    [Command("cl", "laps", "cleanlaps")]
    public void CleanLaps()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
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

    [Command("tl", "totallaps")]
    public void TotalLaps()
    {
        if (Client == null)
        {
            return;
        }

        NordschleifeTrackdaySession? session = _plugin._sessionManager.GetSession(Client.Guid);
        if (session == null)
        {
            return;
        }

        Reply($"You have {session.TotalLaps()} total laps.");
    }

    [Command("ttl", "toptotallaps")]
    public void TopTotalLaps()
    {
        Reply("Cleanest Drivers:");
        int i = 1;
        foreach (var entry in _plugin.TotalLapsLeaderboard())
        {
            Reply($"{i}. @{entry.Value.Item1} - {entry.Value.Item2} clean laps");
            i++;
            if (i > NordschleifeTrackdayPlugin.LEADERBOARD_MAX_ENTRIES)
            {
                break;
            }
        }
    }
}
