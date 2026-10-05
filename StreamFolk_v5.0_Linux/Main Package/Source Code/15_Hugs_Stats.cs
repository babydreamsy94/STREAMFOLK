// Trigger: Command (!hugstats / !hstats)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private const string HugStatsKey = "streamfolk.analytics.hugStats";
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";

    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("Hug Stats: args is null, skipping.");
            return true;
        }

        string invokerRaw = GetArg("user");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invokerRaw))
        {
            CPH.LogWarn("Hug Stats: no invoker username found, skipping.");
            return true;
        }

        string targetRaw = ExtractTarget(GetArg("rawInput"));
        if (string.IsNullOrWhiteSpace(targetRaw))
            targetRaw = invokerRaw;
        string target = Normalize(targetRaw);

        var seen = GetDict(SeenUsersKey);
        var stats = GetDict(HugStatsKey);
        if (seen == null || stats == null)
        {
            CPH.LogWarn("Hug Stats: saved data failed to load; skipping.");
            return true;
        }

        if (!seen.ContainsKey(target))
        {
            CPH.SendMessage($"@{invokerRaw}, @{targetRaw} hasn’t chatted yet, so they don’t have hug stats 🧸✨");
            return true;
        }

        string givenKey = target + "_given";
        string receivedKey = target + "_received";
        int hugsGiven = stats.ContainsKey(givenKey) ? stats[givenKey] : 0;
        int hugsReceived = stats.ContainsKey(receivedKey) ? stats[receivedKey] : 0;
        CPH.SendMessage($"@{targetRaw} has given {hugsGiven} hugs 💖 and received {hugsReceived} hugs 🧸");
        return true;
    }

    private string GetArg(string key)
    {
        object value;
        if (!args.TryGetValue(key, out value) || value == null)
            return string.Empty;
        return value.ToString();
    }

    private string ExtractTarget(string rawInput)
    {
        string[] parts = (rawInput ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int index = parts.Length > 0 && parts[0].StartsWith("!") ? 1 : 0;
        if (index >= parts.Length)
            return string.Empty;
        return parts[index].Trim().TrimStart('@');
    }

    private string Normalize(string s) => (s ?? "").Trim().TrimStart('@').ToLowerInvariant();

    private Dictionary<string, int> GetDict(string key)
    {
        string json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raw = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
            if (raw == null)
                throw new InvalidOperationException(key + " contains JSON null.");
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw) dict[kv.Key] = kv.Value;
            return dict;
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"Hug Stats: failed to deserialize '{key}'; skipping. Error: {ex.Message}");
            return null;
        }
    }
}
