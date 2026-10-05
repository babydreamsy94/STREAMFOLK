// Trigger: Command (!pboard)
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
            CPH.LogWarn("!pboard: args is null, skipping.");
            return true;
        }

        string invoker = GetArg("user");
        if (string.IsNullOrWhiteSpace(invoker)) invoker = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invoker)) invoker = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invoker))
        {
            CPH.LogWarn("!pboard: no invoker username found, skipping.");
            return true;
        }

        string mode = ExtractMode(GetArg("rawInput"));
        if (mode != "givers" && mode != "receivers")
        {
            CPH.SendMessage("Please specify either 'givers' or 'receivers'. Example: !pboard givers");
            return true;
        }

        var patsReceived = GetDict(PatsKey);
        var patsGiven = GetDict(PatsGivenKey);
        var seen = GetDict(SeenUsersKey);
        if (patsReceived == null || patsGiven == null || seen == null)
        {
            CPH.LogWarn("!pboard: saved data failed to load; skipping.");
            return true;
        }

        var giverList = new List<KeyValuePair<string, int>>();
        foreach (var kv in patsGiven)
            if (seen.ContainsKey(kv.Key))
                giverList.Add(kv);
        giverList.Sort((a, b) => b.Value.CompareTo(a.Value));

        var receiverList = new List<KeyValuePair<string, int>>();
        foreach (var kv in patsReceived)
            if (seen.ContainsKey(kv.Key))
                receiverList.Add(kv);
        receiverList.Sort((a, b) => b.Value.CompareTo(a.Value));

        if (giverList.Count == 0 && receiverList.Count == 0)
        {
            CPH.SendMessage($"@{Normalize(invoker)}, I tried to show the board but everyone’s hiding under a mountain of plushies! 🧸");
            return true;
        }

        var giverParts = BuildTop3(giverList);
        var receiverParts = BuildTop3(receiverList);
        string giverBoard = giverParts.Count > 0 ? string.Join(" | ", giverParts) : "No pats given yet";
        string receiverBoard = receiverParts.Count > 0 ? string.Join(" | ", receiverParts) : "No pats received yet";
        if (mode == "givers")
            CPH.SendMessage($"Pat Giver Leaderboard (Top 3): {giverBoard}");
        else
            CPH.SendMessage($"Pat Receiver Leaderboard (Top 3): {receiverBoard}");
        return true;
    }

    private string GetArg(string key)
    {
        object value;
        if (!args.TryGetValue(key, out value) || value == null)
            return string.Empty;
        return value.ToString();
    }

    private string ExtractMode(string rawInput)
    {
        string[] parts = (rawInput ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int index = parts.Length > 0 && parts[0].StartsWith("!") ? 1 : 0;
        if (index >= parts.Length)
            return string.Empty;
        string mode = parts[index].ToLowerInvariant();
        return (mode == "givers" || mode == "receivers") ? mode : string.Empty;
    }

    private string Normalize(string s) => (s ?? "").Trim().TrimStart('@').ToLowerInvariant();

    private List<string> BuildTop3(List<KeyValuePair<string, int>> list)
    {
        var parts = new List<string>();
        for (int i = 0; i < list.Count && i < 3; i++)
            parts.Add($"{i + 1}) @{list[i].Key} — {list[i].Value}");
        return parts;
    }

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
            CPH.LogWarn($"!pboard: failed to deserialize '{key}'; skipping. Error: {ex.Message}");
            return null;
        }
    }
}
