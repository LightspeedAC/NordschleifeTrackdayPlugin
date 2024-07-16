# NordschleifeTrackdayPlugin

An [AssettoServer](https://github.com/compujuckel/AssettoServer "‌") plugin designed for Nurburgring Nordschleife servers! It brings progression for unlocking faster cars, convoys, variable idle kicking, and more. [Live example](https://acstuff.ru/s/q:race/online/join?ip=204.10.194.151&httpPort=8702)!

This plugin will only function properly if:
- Used on the **Tourist** version of the Nordschleife track
- Server has a **minimum CSP version of 1937 (v0.1.77)**

## Configuration

Enable CSP client messages in your `extra_cfg.yml`

```YAML
EnableClientMessages: true
```

Enable the plugin in your `extra_cfg.yml`

```YAML
EnablePlugins:
- NordschleifeTrackdayPlugin
```

Add the example plugin configuration to the bottom of your `extra_cfg.yml`

```yaml
---
!NordschleifeTrackdayConfiguration
# Path for the sqlite file used by this plugin (relative to server directory)
DatabasePath: "nordschleife_trackday.sqlite"
# List of steam id's for players with special permissions (giving/taking credits, hosting convoys, etc.)
Admins: []
# List of steam id's for players who are designated convoy leaders
ConvoyLeaders: []
# List of each car and how many points each requires
Cars:
  ks_mclaren_p1: 50000
  ferrari_f40_s3: 30000
  ks_lamborghini_aventador_sv: 26500
  ks_ferrari_488_gtb: 22000
  ks_porsche_911_gt3_rs: 19000
  ferrari_458: 18500
  ks_ford_gt40: 17000
  ks_mazda_rx7_tuned: 16000
  ks_corvette_c7_stingray: 14750
  ks_bmw_m4: 13800
  ks_porsche_cayman_gt4_std: 13000
  lotus_exige_v6_cup: 9000
  ks_porsche_718_cayman_s: 7500
  bmw_m3_e30_gra: 6000
  bmw_z4: 5150
  lotus_exige_240: 4750
  ks_audi_sport_quattro: 4200
  ks_nissan_370z: 3250
  ks_toyota_supra_mkiv: 2500
  ks_mazda_rx7_spirit_r: 1750
  ks_audi_a1s1: 850
  ks_mazda_mx5_nd: 350
  ks_toyota_gt86: 250
  ks_alfa_romeo_gta: 125
  abarth500: 75
  ks_toyota_ae86: 0
# List of cars that are for players with starting points or less (used for determining if the car a player is driving is a starter car for the idle kick feature)
StarterCars: ["ks_mazda_mx5_nd", "ks_toyota_gt86", "ks_alfa_romeo_gta", "abarth500", "ks_toyota_ae86"]
# List of major clean lap bonus points players can earn
CleanLapBonuses:
  3: 150
  4: 200
  5: 300
  10: 1500
  20: 2500
  30: 3500
  40: 4500
  50: 5500
  100: 10000
# Gives bonus points to players with more than x clean laps (if x is not in the CleanLapBonuses list, and is above NeededCleanLaps)
ExtraCleanLapBonus:
  Enabled: true
  NeededCleanLaps: 11 # How many clean laps are needed to start earning the point bonus specified below
  BonusPoints: 100 # How many points can be earned for every clean lap made at/above NeededCleanLaps
# Point earning and deductions
Metrics:
  StartingPoints: 500 # How many points new players start with
  PointsDeductLeavePits: 3 # How many points players "pay" for leaving pits
  PointsDeductInvalidLap: 25 # How many points players lose for completing an invalid lap
  PointsDeductPitReEnter: 50 # How many points players lose for re-entering pits (at tolls)
  PointsDeductBySpeedFactor: 1.4 # Calculating how many points to take in a collision. Lower = more points, higher = less points
  PointsDeductCollisionMax: 400 # The maximum amount of points a player can lose from a collision
  PointsRewardPerLap: 30 # How many points players earn for completing each clean lap
  PointsRewardBeatPb: 50 # How many points players earn for beating their personal best lap time
  PointsRewardBeatOtherPb: 75 # How many points players earn for beating the best lap time record (if its set by someone else)
  PointsRewardConvoy: 150 # How many points players earn for completing each clean lap with a convoy
# Automated messages in chat
Announcements:
  Enabled: true
  Interval: 600 # In seconds, how often to send an announcement
  Messages: ["If you need help, use /help and ask others for tips.", "Hope you're having fun on our server!"] # Your announcements
# Plugin sends a Discord webhook for convoys starting & finishing
DiscordWebhook:
  Enabled: true
  WebhookURL: "" # Your Discord webhook URL e.g. "https://discord.com/api/webhooks/x/x"
# Plugin kicks idle players
IdleKick:
  Enabled: true
  DefaultMaxIdleTime: 900 # In seconds, how long players can idle for by default
  LongerMaxIdleTime: 3600 # In seconds, how long players can idle for if they have more clean laps (specified in LongerMaxIdleNeededCleanLaps)
  StarterMaxIdleTime: 600 # In seconds, how long players can idle for if theyre in a starter car (these cars are probably the most used and should be available as often as they can)
  AllowLongerMaxIdleForCleanLaps: true # Whether to allow players with more clean laps to be able to idle longer
  LongerMaxIdleNeededCleanLaps: 5 # How many clean laps are needed to be able to idle longer
Extra:
  DoublePointWeekend: true # Whether to enable doubling points on weekends (every Saturday)
  ImmediateKickCarNotUnlocked: false # Whether to kick players on join for joining in a car they can't drive. If set to false, they'll be kicked after 30 seconds and during that time they can't drive the car, move, etc.
  Next2UnlockMaxEntries: 6 # How many cars are shown to the player when they run /next2unlock
  AssignConvoyLeadersByPoints: true # Whether to allow players to become convoy leaders by accumulating points
  ConvoyLeadersNeededPoints: 6500 # How many points players need to become a convoy leader (if AssignConvoyLeadersByPoints is set to true)
  CountCollisionsInPits: true # Whether to count collisions that occur within pits
```

## Features

### Progression with Points
Players earn points by completing a clean lap (without a cut or collision) and through bonuses. Each collision, pit re-entry, and invalid lap completion deducts points from the player. As players earn more points they unlock access to more cars, and as they reach a certain milestone in points they are assigned as convoy leaders (more on that below).

#### Earning points:
- Completing a clean lap
- Earning a point bonus (clean lap streak, convoys, etc.)

#### Point deductions:
- Each collision
- Reversing back into pits
- Completing an invalid lap

#### Available point bonuses:
- Completing a lap with a clean lap steak at 3 or higher
- Participating in and finishing a clean lap with a convoy
- Beating your personal best lap time
- Beating the best lap time record
- Earning points during double points weekend

### Convoys

Convoys allow all players on the server to follow 1 player (a convoy leader) and complete a lap with them to earn a big point bonus. Convoys can only be hosted by admins and/or players with a specific amount of points or more.

#### Note:

- To start a convoy, convoy leaders ideally pull up next to the ambulance near the pit exit. Once parked they run /cs. When the convoy leader starts driving, a message will be shown in the chat to let others know that the convoy is on the move.
- Convoy leaders should allow all other players in the convoy to finish the lap before them. The convoy leader should finish last, ideally staying right on the final straight and parking on the white concrete.
- Convoys can only conclude with a point bonus if there are 2 or more other players. For admins it can be 1 player.

### Variable Idle Kick

Players by default can idle for 15 minutes, after completing 5 clean laps they can idle for up to 60 minutes. There’s a fixed idle limit of 10 minutes in cars specified as “starter cars”.

### Announcements

Automated messages sent by the server at a specified interval.

### Commands
#### Default:
- `/help`: Quick guide and list of commands
- `/chelp`: Quick guide on convoys and list of convoy related commands
- `/convoys`: Shows a list of online convoy leaders and any ongoing convoys
- `/convoy`: Shows info on a specific convoy, like when it started and its players
- `/cars`: Shows the list of cars, how many points each of them require, and whether the player has unlocked them
- `/next2unlock`: Shows a list of cars the player is about to unlock
- `/unlocked`: Shows a list of cars the player has already unlocked and can drive
- `/bonuses`: Shows the list of available bonus points players can earn
- `/status`: Indicates the player's lap status (clean or not), their clean laps streak, points, cuts and collisions
- `/points`: Shows the player their points
- `/tpoints`: Shows a list of players with the most points
- `/pb`: Shows the player their best lap time with their current car
- `/best`: Shows the best lap time record for the player’s current car
- `/allbest`: Show the best lap time records for every car
- `/cl`: Shows the player’s clean laps streak
- `/tl`: Shows the player’s total laps
- `/ttl`: Shows a list of players with the most clean laps completed

#### Convoy Leader:
- `/cs`: Start a convoy
- `/ce`: End your convoy **(or someone elses if you're an admin)**

#### Admin:
- `/ct`: Allows players to transfer leadership of their convoy to another player, or leadership of a different convoy to another player
- `/afp`: Give points to yourself or others
- `/tfp`: Remove points from yourself or others
- `/rp`: Reset your points or someone elses
- `/rcc`: Reset your cuts/collisions or someone elses
- `/ca`: Add a temporary admin which is reset on server restart
- `/ra`: Temporarily remove an admin which is reset on server restart
- `/ccl`: Add a temporary convoy leader which is reset on server restart
- `/rcl`: Temporarily remove a convoy leader which is reset on server restart