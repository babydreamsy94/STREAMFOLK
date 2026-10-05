using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";
    private const string AttendanceHistoryKey = "streamfolk.analytics.attendanceHistory";
    private static readonly string[] ExcludedUsers =
    {
        "botname",
        "pokemoncommunitygame",
        "kofistreambot",
        "nightbot",
        "streamelements",
        "streamlabs",
        "sery_bot",
    };
    public bool Execute()
    {
        if (args == null || IsTrueArg("isTest") || IsTrueArg("isSimulated"))
            return true;
        // Run Action Immediately inherits the Attendance trigger's arguments.
        // First Words supplies the repeat suppression; ordinary Message must
        // never turn this read-only script into a welcome on every chat line.
        if (CPH.GetEventType().ToString() != "TwitchFirstWord")
            return true;
        string user = LoginFrom(args, "userLogin", "userName", "user", "userDisplayName");
        if (user == null || CurrentExclusions().ContainsKey(user))
            return true;
        try
        {
            string seenJson = CPH.GetGlobalVar<string>(SeenUsersKey, true);
            if (string.IsNullOrWhiteSpace(seenJson))
            {
                CPH.LogInfo("Bot Welcomer: streamfolk.analytics.seenUsers is empty; run Attendance Check first.");
                return true;
            }

            var seenRaw = JsonConvert.DeserializeObject<Dictionary<string, int>>(seenJson);
            if (seenRaw == null)
                throw new InvalidOperationException("streamfolk.analytics.seenUsers contains JSON null.");
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in seenRaw)
                seen[entry.Key] = entry.Value;
            if (!seen.ContainsKey(user))
            {
                CPH.LogInfo("Bot Welcomer: " + user + " is not in streamfolk.analytics.seenUsers; welcome skipped.");
                return true;
            }

            string historyJson = CPH.GetGlobalVar<string>(AttendanceHistoryKey, true);
            var historyRaw = string.IsNullOrWhiteSpace(historyJson) ? new Dictionary<string, List<string>>() : JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(historyJson);
            if (historyRaw == null)
                throw new InvalidOperationException("streamfolk.analytics.attendanceHistory contains JSON null.");
            var history = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in historyRaw)
                history[entry.Key] = entry.Value;
            bool returning = false;
            List<string> dates;
            if (history.TryGetValue(user, out dates) && dates != null)
            {
                DateTime today = DateTime.Now.Date;
                foreach (string date in dates)
                {
                    DateTime parsed;
                    if (DateTime.TryParse(date, out parsed) && parsed.Date < today)
                    {
                        returning = true;
                        break;
                    }
                }
            }

            if (returning)
                CPH.SendMessage($"/me Welcome back, @{user}! It's good to see you here again! 🤗");
            else
                CPH.SendMessage($"/me Welcome @{user}! It looks like this is your first time here! 💖");
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogInfo("Bot Welcomer: welcome failed; attendance was not changed. " + ex.Message);
            return false;
        }
    }

    private Dictionary<string, bool> CurrentExclusions()
    {
        var excluded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in ExcludedUsers)
            AddName(excluded, name);
        AddName(excluded, LoginFrom(args, "broadcastUserName", "broadcastUserLogin"));
        try
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster != null)
                AddName(excluded, Normalize(broadcaster.UserLogin));
        }
        catch
        {
            CPH.LogInfo("Bot Welcomer: broadcaster lookup unavailable; using configured exclusions.");
        }

        try
        {
            var bot = CPH.TwitchGetBot();
            if (bot != null)
                AddName(excluded, Normalize(bot.UserLogin));
        }
        catch
        {
            CPH.LogInfo("Bot Welcomer: bot lookup unavailable; using configured exclusions.");
        }

        return excluded;
    }

    private static void AddName(Dictionary<string, bool> names, string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            names[name] = true;
    }

    private static string LoginFrom(Dictionary<string, object> values, params string[] keys)
    {
        foreach (string key in keys)
        {
            string name = Normalize(TextFrom(values, key));
            if (name.Length == 0 || name.Length > 25)
                continue;
            bool valid = true;
            foreach (char c in name)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    valid = false;
                    break;
                }

            if (valid)
                return name;
        }

        return null;
    }

    private static string TextFrom(Dictionary<string, object> values, params string[] keys)
    {
        if (values == null)
            return null;
        foreach (string key in keys)
            foreach (var entry in values)
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    // Reject numeric IDs/objects passed in a username field.
                    string text = entry.Value as string;
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }

        return null;
    }

    private bool IsTrueArg(string key)
    {
        object raw;
        bool value;
        return args.TryGetValue(key, out raw) && raw != null && bool.TryParse(raw.ToString(), out value) && value;
    }

    private static string Normalize(string value)
    {
        return (value ?? "").Trim().TrimStart('@').ToLowerInvariant();
    }
}