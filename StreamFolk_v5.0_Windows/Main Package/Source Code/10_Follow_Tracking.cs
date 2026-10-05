// Trigger: Twitch Follow
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string FollowsKey = "streamfolk.analytics.follows";
    private const string TotalFollowsKey = "streamfolk.analytics.totalFollows";

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
            CPH.LogWarn("Follow Tracking: args is null, skipping.");
            return true;
        }

        // FIX 2: Reordered priority — userLogin first as most reliable on follow events,
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
        {
            CPH.LogWarn("Follow Tracking: No username found in args, skipping.");
            return true;
        }

        string user = Normalize(userRaw);

        if (string.IsNullOrWhiteSpace(user))
        {
            CPH.LogWarn("Follow Tracking: Username normalized to empty, skipping.");
            return true;
        }

        // FIX 7: Log excluded user skips so they're visible in the log
        if (IsExcluded(user))
        {
            CPH.LogInfo($"Follow Tracking: Skipping excluded user '{user}'.");
            return true;
        }

        // FIX 3: Re-wrap deserialized dict to guarantee OrdinalIgnoreCase comparer
        var follows = GetDict(FollowsKey);
        if (follows == null)
        {
            CPH.LogWarn("Follow Tracking: saved data failed to load; skipping this event to protect stored data.");
            return true;
        }

        // FIX 5: A follow is a one-time event per user — record as 1, not a running count.
        //         If the same user triggers this twice (re-follow, event replay), log a warning
        //         but don't increment so the data stays accurate.
        if (follows.ContainsKey(user))
        {
            CPH.LogWarn($"Follow Tracking: '{user}' already has a follow entry this stream. Possible duplicate event — skipping increment.");
            return true;
        }

        follows[user] = 1;
        SaveDict(FollowsKey, follows);

        // Update total follows
        int total = 0;
        try
        {
            var rawTotal = CPH.GetGlobalVar<object>(TotalFollowsKey, true);
            if (rawTotal != null)
                total = Convert.ToInt32(rawTotal);
        }
        catch
        {
            CPH.LogWarn("Follow Tracking: Could not parse existing total, resetting to 0.");
            total = 0;
        }

        total++;
        CPH.SetGlobalVar(TotalFollowsKey, total, true);

        CPH.LogInfo($"Follow Tracking: Logged follow from '{user}'. Stream total follows: {total}.");

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
            CPH.LogWarn($"Follow Tracking: Failed to deserialize '{key}'; NOT resetting and NOT saving. Error: {ex.Message}");
            return null;
        }
    }

    private void SaveDict(string key, Dictionary<string, int> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }
}