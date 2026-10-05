using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";
    private const string AttendanceHistoryKey = "streamfolk.analytics.attendanceHistory";
    private const string SessionStartKey = "streamfolk.analytics.sessionStartLocal";

    private static readonly string[] ExcludedUsers =
    {
        "botname",
        "pokemoncommunitygame",
        "kofistreambot"
    };

    private bool IsExcluded(string user)
    {
        if (string.IsNullOrWhiteSpace(user))
            return false;
        var n = Normalize(user);
        foreach (var u in ExcludedUsers)
            if (u.Equals(n, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private const bool ManageChatModes = false;

    public bool Execute()
    {
        var broadcaster = CPH.TwitchGetBroadcaster();
        string streamerUser = broadcaster == null ? "" : (broadcaster.UserLogin ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(streamerUser))
        {
            CPH.LogWarn("StreamFolk: connect the Twitch broadcaster account before running this action.");
            return false;
        }
        var userNorm = Normalize(streamerUser);

        // --- Ensure streamfolk.analytics.seenUsers exists and is case-insensitive ---
        var seen = GetDict(SeenUsersKey);
        if (!seen.ContainsKey(userNorm))
        {
            seen[userNorm] = 1;
            SaveDict(SeenUsersKey, seen);
            CPH.LogInfo($"Stream Online: added streamer '{userNorm}' to streamfolk.analytics.seenUsers.");
        }
        else
        {
            CPH.LogInfo($"Stream Online: streamer '{userNorm}' already present in streamfolk.analytics.seenUsers.");
        }

        // --- Ensure streamfolk.analytics.attendanceHistory exists and is updated consistently ---
        var history = GetHistory();
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        DateTime todayDate = DateTime.Now.Date;

        if (!history.ContainsKey(userNorm))
            history[userNorm] = new List<string>();

        // FIX 4: Use parsed date comparison instead of raw string to avoid format variation
        var parsedDates = history[userNorm]
            .Select(d => DateTime.TryParse(d, out var dt) ? dt.Date : (DateTime?)null)
            .Where(d => d.HasValue)
            .Select(d => d.Value)
            .ToList();

        if (!parsedDates.Any(d => d == todayDate))
        {
            history[userNorm].Add(today);
            SaveHistory(history);
            CPH.LogInfo($"Stream Online: added today's date ({today}) to streamfolk.analytics.attendanceHistory for '{userNorm}'.");
        }
        else
        {
            CPH.LogInfo($"Stream Online: streamfolk.analytics.attendanceHistory already contains today's date for '{userNorm}'.");
        }

        // --- Reset per-stream analytics ---
        // FIX 1: Added streamfolk.analytics.subsDetailed to reset list so previous stream's
        //         sub type details don't bleed into the new stream's summary
        CPH.SetGlobalVar("streamfolk.analytics.chatMessagesByUser", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.chatTotalMessages", 0, true);
        CPH.SetGlobalVar("streamfolk.analytics.subs", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.subsDetailed", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.totalSubs", 0, true);
        CPH.SetGlobalVar("streamfolk.analytics.follows", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.totalFollows", 0, true);
        CPH.SetGlobalVar("streamfolk.analytics.raids", "[]", true);
        CPH.SetGlobalVar("streamfolk.analytics.bits", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.totalBits", 0, true);
        CPH.SetGlobalVar("streamfolk.analytics.attendanceSummaryJson", "{}", true);

        // Mark session start (LOCAL TIME, ISO 8601)
        CPH.SetGlobalVar(SessionStartKey, DateTime.Now.ToString("o"), true);

        // Unlock chat + welcome
        CPH.SendMessage($"/me Welcome to @{userNorm}'s stream! StreamFolk is ready for this session.");
        if (ManageChatModes)
        {
            CPH.Wait(3000);
            CPH.TwitchEmoteOnly(false);
            CPH.TwitchSubscriberOnly(false);
            CPH.TwitchSlowMode(false);
        }
        // Stable Streamer.bot requires a duration argument.
        // null keeps the pinned message until the end of the stream.
        CPH.TwitchSendAndPinMessage($"👋 Welcome to @{userNorm}'s stream! This stream is powered by StreamFolk, a community-focused stream analytics system built in Streamer.bot. Most attendance is recorded automatically, but if StreamFolk somehow misses you, we have the “Attendance Check!” redeem as a backup. Thanks for stopping on by! 💙");
        CPH.TwitchUpdatePinnedMessageDuration(null);
        return true;
    
    
    }

    // --- Helpers ---
    private string Normalize(string s) => (s ?? "").Trim().TrimStart('@').ToLowerInvariant();

    // FIX 2: Re-wrap deserialized dict to guarantee OrdinalIgnoreCase comparer
    private Dictionary<string, int> GetDict(string key)
    {
        var json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raw = JsonConvert.DeserializeObject<Dictionary<string, int>>(json)
                      ?? new Dictionary<string, int>();
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
                dict[kv.Key] = kv.Value;
            return dict;
        }
        catch (Exception ex)
        {
            CPH.LogInfo($"GetDict: failed to deserialize key '{key}', resetting. Error: {ex.Message}");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveDict(string key, Dictionary<string, int> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }

    // FIX 3: Re-wrap deserialized dict to guarantee OrdinalIgnoreCase comparer
    private Dictionary<string, List<string>> GetHistory()
    {
        var json = CPH.GetGlobalVar<string>(AttendanceHistoryKey, true);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raw = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                      ?? new Dictionary<string, List<string>>();
            var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
                dict[kv.Key] = kv.Value ?? new List<string>();
            return dict;
        }
        catch (Exception ex)
        {
            CPH.LogInfo($"GetHistory: failed to deserialize streamfolk.analytics.attendanceHistory. NOT resetting. Error: {ex.Message}");
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveHistory(Dictionary<string, List<string>> dict)
    {
        CPH.SetGlobalVar(AttendanceHistoryKey, JsonConvert.SerializeObject(dict, Formatting.Indented), true);
    }
}