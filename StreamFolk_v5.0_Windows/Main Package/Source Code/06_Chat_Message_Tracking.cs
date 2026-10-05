// Trigger: Twitch Chat Message
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string ChatByUserKey = "streamfolk.analytics.chatMessagesByUser";
    private const string ChatTotalKey = "streamfolk.analytics.chatTotalMessages";

    private static readonly string[] ExcludedUsers =
    {
        "botname",   // FIX 1: was "m0th3rbot" — corrected the typo
        "pokemoncommunitygame"
    };

    private bool IsExcluded(string user) =>
        ExcludedUsers.Contains(user, StringComparer.OrdinalIgnoreCase);

    public bool Execute()
    {
        // FIX 4: Guard against null args
        if (args == null)
        {
            CPH.LogWarn("Chat Tracking: args is null, skipping.");
            return true;
        }

        // FIX 2: Reordered priority — userLogin first as most reliable on chat events,
        //         using a loop that stops at the first valid value
        string userRaw = null;
        string[] userKeys = { "userLogin", "userName", "user" };
        foreach (var key in userKeys)
        {
            if (args.ContainsKey(key))
            {
                var val = args[key]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) { userRaw = val; break; }
            }
        }

        if (string.IsNullOrWhiteSpace(userRaw))
            return true;

        string user = Normalize(userRaw);

        if (string.IsNullOrWhiteSpace(user))
            return true;

        // FIX 6: Log excluded users so skips are visible in the log
        if (IsExcluded(user))
        {
            CPH.LogInfo($"Chat Tracking: Skipping excluded user '{user}'.");
            return true;
        }

        // Update per-user chat count
        // FIX 3: Re-wrap deserialized dict to guarantee OrdinalIgnoreCase comparer
        var byUser = GetDict(ChatByUserKey);
        if (byUser == null)
        {
            CPH.LogWarn("Chat Tracking: saved data failed to load; skipping this event to protect stored data.");
            return true;
        }
        if (!byUser.ContainsKey(user))
            byUser[user] = 0;
        byUser[user]++;
        SaveDict(ChatByUserKey, byUser);

        // Update total chat count
        int total = 0;
        try
        {
            var rawTotal = CPH.GetGlobalVar<object>(ChatTotalKey, true);
            if (rawTotal != null)
                total = Convert.ToInt32(rawTotal);
        }
        catch
        {
            // FIX 5: Log when total parse fails so it's visible in the log
            CPH.LogWarn($"Chat Tracking: Could not parse existing total, resetting to 0.");
            total = 0;
        }

        total++;
        CPH.SetGlobalVar(ChatTotalKey, total, true);

        return true;
    }

    private string Normalize(string s) => (s ?? "").Trim().TrimStart('@').ToLowerInvariant();

    // FIX 3: Re-wrap deserialized dict to guarantee OrdinalIgnoreCase comparer
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
            CPH.LogWarn($"Chat Tracking: Failed to deserialize '{key}'; NOT resetting and NOT saving. Error: {ex.Message}");
            return null;
        }
    }

    private void SaveDict(string key, Dictionary<string, int> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }
}