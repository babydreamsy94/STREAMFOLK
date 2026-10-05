// Trigger: Command (!pstats)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private const string PatsKey = "streamfolk.analytics.patCounts";
    private const string PatsGivenKey = "streamfolk.analytics.patGiven";
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";

    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("Pat Stats: args is null, skipping.");
            return true;
        }

        string invokerRaw = GetArg("user");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invokerRaw))
        {
            CPH.LogWarn("Pat Stats: no invoker username found, skipping.");
            return true;
        }

        string targetRaw = ExtractTarget(GetArg("rawInput"));
        if (string.IsNullOrWhiteSpace(targetRaw))
            targetRaw = invokerRaw;
        string targetNorm = Normalize(targetRaw);

        var patsReceived = GetDict(PatsKey);
        var patsGiven = GetDict(PatsGivenKey);
        var seen = GetDict(SeenUsersKey);
        if (patsReceived == null || patsGiven == null || seen == null)
        {
            CPH.LogWarn("Pat Stats: saved data failed to load; skipping.");
            return true;
        }

        if (!seen.ContainsKey(targetNorm))
        {
            CPH.SendMessage($"@{invokerRaw}, I tried to check @{targetRaw}'s stats but they’re hiding under a mountain of plushies! 🧸");
            return true;
        }

        int receivedCount = patsReceived.ContainsKey(targetNorm) ? patsReceived[targetNorm] : 0;
        int givenCount = patsGiven.ContainsKey(targetNorm) ? patsGiven[targetNorm] : 0;
        CPH.SendMessage($"@{targetRaw} has given {givenCount} pats and received {receivedCount} pats!");
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
            CPH.LogWarn($"Pat Stats: failed to deserialize '{key}'; skipping. Error: {ex.Message}");
            return null;
        }
    }
}
