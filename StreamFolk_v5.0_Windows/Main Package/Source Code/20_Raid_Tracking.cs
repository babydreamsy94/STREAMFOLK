// Trigger: Twitch Raid
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string RaidsKey = "streamfolk.analytics.raids"; // [ raider1, raider2, ... ]

    private static readonly string[] ExcludedUsers =
    {
        "botname",
        "pokemoncommunitygame"
    };

    private bool IsExcluded(string user) =>
        ExcludedUsers.Contains(user, StringComparer.OrdinalIgnoreCase);

    public bool Execute()
    {
        // Guard against null args
        if (args == null)
        {
            CPH.LogWarn("Raid Tracking: args is null, skipping.");
            return true;
        }

        //
        // --- USERNAME EXTRACTION ---
        //
        string raiderRaw = null;
        string[] userKeys = { "userLogin", "userName", "user" };

        foreach (var key in userKeys)
        {
            if (args.ContainsKey(key))
            {
                var val = args[key]?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    raiderRaw = val;
                    break;
                }
            }
        }

        string raider = Normalize(raiderRaw);

        if (string.IsNullOrWhiteSpace(raider) || IsExcluded(raider))
        {
            CPH.LogWarn("Raid Tracking: Skipping raid due to empty or excluded raider username.");
            return true;
        }

        //
        // --- STORE RAID EVENT ---
        //
        var raids = GetList(RaidsKey);

        // FIX 1: Warn on duplicate but skip the add so the raider doesn't
        //         appear twice in the end-of-stream summary
        if (raids.Contains(raider, StringComparer.OrdinalIgnoreCase))
        {
            CPH.LogWarn($"Raid Tracking: '{raider}' has already raided this stream; skipping duplicate entry.");
            return true;
        }

        raids.Add(raider);
        SaveList(RaidsKey, raids);

        CPH.LogInfo($"Raid Tracking: Logged raid from {raider}. Total raids now: {raids.Count}.");

        return true;
    }

    private string Normalize(string s) =>
        (s ?? "").Trim().TrimStart('@').ToLowerInvariant();

    private List<string> GetList(string key)
    {
        var json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();
        try
        {
            return JsonConvert.DeserializeObject<List<string>>(json)
                   ?? new List<string>();
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"Raid Tracking: Failed to deserialize '{key}', starting fresh. Error: {ex.Message}");
            return new List<string>();
        }
    }

    private void SaveList(string key, List<string> list)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(list), true);
    }
}