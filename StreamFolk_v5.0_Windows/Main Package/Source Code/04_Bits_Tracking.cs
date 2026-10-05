// Trigger: Twitch Cheer
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string BitsKey = "streamfolk.analytics.bits";
    private const string TotalBitsKey = "streamfolk.analytics.totalBits";

    private static readonly string[] ExcludedUsers =
    {
        "botname",   // FIX 1: was "m0th3rbot" — corrected the typo
        "pokemoncommunitygame"
    };

    private bool IsExcluded(string user) =>
        ExcludedUsers.Contains(user, StringComparer.OrdinalIgnoreCase);

    public bool Execute()
    {
        // FIX 5: Guard against null args
        if (args == null)
        {
            CPH.LogWarn("Bit Tracking: args is null, skipping.");
            return true;
        }

        // FIX 2: Reordered priority — userLogin first as most reliable on cheer events
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
            CPH.LogWarn("Bit Tracking: No username found in args, skipping.");
            return true;
        }

        string user = Normalize(userRaw);

        if (string.IsNullOrWhiteSpace(user) || IsExcluded(user))
        {
            CPH.LogInfo($"Bit Tracking: Skipping excluded or empty user '{user}'.");
            return true;
        }

        // FIX 6: Log when bits key is missing
        if (!args.ContainsKey("bits"))
        {
            CPH.LogWarn($"Bit Tracking: 'bits' key not found in args for user '{user}', skipping.");
            return true;
        }

        int bits = 0;
        try
        {
            bits = Convert.ToInt32(args["bits"]);
        }
        catch
        {
            CPH.LogWarn($"Bit Tracking: Could not parse bits value '{args["bits"]}' for user '{user}', skipping.");
            return true;
        }

        // FIX 4: Bail out early if bits is zero or negative — nothing useful to record
        if (bits <= 0)
        {
            CPH.LogWarn($"Bit Tracking: Bits value is {bits} for user '{user}', skipping.");
            return true;
        }

        // Update per-user bits
        // FIX 3: Re-wrap deserialized dict to guarantee OrdinalIgnoreCase comparer
        var bitsDict = GetDict(BitsKey);
        if (bitsDict == null)
        {
            CPH.LogWarn("Bit Tracking: saved data failed to load; skipping this event to protect stored data.");
            return true;
        }
        if (!bitsDict.ContainsKey(user))
            bitsDict[user] = 0;
        bitsDict[user] += bits;
        SaveDict(BitsKey, bitsDict);

        // Update total bits
        int total = 0;
        try
        {
            var rawTotal = CPH.GetGlobalVar<object>(TotalBitsKey, true);
            if (rawTotal != null)
                total = Convert.ToInt32(rawTotal);
        }
        catch
        {
            CPH.LogWarn($"Bit Tracking: Could not parse existing total bits, resetting to 0 before adding.");
            total = 0;
        }

        total += bits;
        CPH.SetGlobalVar(TotalBitsKey, total, true);

        CPH.LogInfo($"Bit Tracking: Logged {bits} bits from {user}. Their total: {bitsDict[user]}. Stream total: {total}.");

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
            CPH.LogWarn($"Bit Tracking: Failed to deserialize '{key}'; NOT resetting and NOT saving. Error: {ex.Message}");
            return null;
        }
    }

    private void SaveDict(string key, Dictionary<string, int> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }
}