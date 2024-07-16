using System.Data.SQLite;
using System.Globalization;
using System.Text.RegularExpressions;
using AssettoServer.Network.Tcp;

public static class NordschleifeTrackdayUtils
{
    private static readonly string[] _forbiddenUsernameSubstrings = ["discord", "@", "#", ":", "```"];
    private static readonly string[] _forbiddenUsernames = ["everyone", "here"];

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

    public static Dictionary<ulong, (string, int)> GetLeaderboard(int type, SQLiteConnection db, int maxEntries)
    {
        Dictionary<ulong, (string, int)> leaderboard = [];
        if (type == 0)
        {
            using var command = new SQLiteCommand($"SELECT id, name, points FROM users ORDER BY points DESC LIMIT 0,{maxEntries}", db);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ulong id = Convert.ToUInt64(reader["id"]);
                string name = reader["name"].ToString() ?? "Unknown";
                int points = Convert.ToInt32(reader["points"]);
                leaderboard.Add(id, (name, points));
            }
        }
        else if (type == 1)
        {
            using var command = new SQLiteCommand(@$"
            WITH LapCounts AS (
                SELECT user_id, COUNT(*) AS lap_count
                FROM laptimes
                GROUP BY user_id
            )
            SELECT u.id AS user_id, u.name, lc.lap_count
            FROM LapCounts lc
            JOIN users u ON lc.user_id = u.id
            ORDER BY lc.lap_count DESC
            LIMIT 0,{maxEntries}", db);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ulong id = Convert.ToUInt64(reader["user_id"]);
                string name = reader["name"].ToString() ?? "Unknown";
                int totalLaps = Convert.ToInt32(reader["lap_count"]);
                if (!leaderboard.ContainsKey(id))
                {
                    leaderboard.Add(id, (name, totalLaps));
                }
            }
        }

        return leaderboard;
    }

    public static void CreateLaptime(SQLiteConnection db, ACTcpClient client, uint time, int speed)
    {
        using var command = new SQLiteCommand("INSERT INTO laptimes (user_id, car, time, average_speedkmh, completed_on) VALUES (@user_id, @car, @time, @average_speedkmh, @completed_on)", db);
        command.Parameters.AddWithValue("@user_id", client.Guid);
        command.Parameters.AddWithValue("@car", client.EntryCar.Model);
        command.Parameters.AddWithValue("@time", time);
        command.Parameters.AddWithValue("@average_speedkmh", speed);
        command.Parameters.AddWithValue("@completed_on", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.ExecuteNonQuery();
    }

    public static (int Points, int CleanLapStreak, long LastCleanLap, int TotalLaps, uint PersonalBest, int Cuts, int Collisions) GetUser(SQLiteConnection db, int startingPoints, ulong guid, string car)
    {
        int points = startingPoints;
        int cleanLapStreak = 0;
        long lastCleanLap = 0;
        int totalLaps = 0;
        uint pb = 0;
        int cuts = 0;
        int collisions = 0;

        using (var command = new SQLiteCommand("SELECT points, clean_lap_streak, last_clean_lap, cuts, collisions FROM users WHERE id = @id LIMIT 0,1", db))
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

        using (var command = new SQLiteCommand("SELECT COUNT(*) FROM laptimes WHERE user_id = @id", db))
        {
            command.Parameters.AddWithValue("@id", guid);
            totalLaps = Convert.ToInt32(command.ExecuteScalar());
        }
        using (var command = new SQLiteCommand("SELECT time FROM laptimes WHERE user_id = @id AND car = @car ORDER BY time ASC LIMIT 0,1", db))
        {
            command.Parameters.AddWithValue("@id", guid);
            command.Parameters.AddWithValue("@car", car);
            pb = Convert.ToUInt32(command.ExecuteScalar());
        }

        return (points, cleanLapStreak, lastCleanLap, totalLaps, pb, cuts, collisions);
    }

    public static void UpdateUser(SQLiteConnection db, ulong guid, string name, string country, int points, int clean_lap_streak, string last_clean_lap, int cuts, int collisions)
    {
        using var updateCommand = new SQLiteCommand("INSERT INTO users (id, name, country, points, clean_lap_streak, last_clean_lap, cuts, collisions) VALUES (@id, @name, @country, @points, @clean_lap_streak, @last_clean_lap, @cuts, @collisions) ON CONFLICT(id) DO UPDATE SET name = excluded.name, country = excluded.country, points = excluded.points, clean_lap_streak = excluded.clean_lap_streak, last_clean_lap = excluded.last_clean_lap, cuts = excluded.cuts, collisions = excluded.collisions;", db);
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
