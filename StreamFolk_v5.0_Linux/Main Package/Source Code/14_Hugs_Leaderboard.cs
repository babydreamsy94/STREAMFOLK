// Trigger: Command (!hboard)
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
            CPH.LogWarn("!hboard: args is null, skipping.");
            return true;
        }

        string invokerRaw = GetArg("user");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invokerRaw))
        {
            CPH.LogWarn("!hboard: no invoker username found, skipping.");
            return true;
        }

        string invoker = Normalize(invokerRaw);
        string mode = ExtractMode(GetArg("rawInput"));
        if (mode != "givers" && mode != "receivers")
        {
            CPH.SendMessage("Please specify either 'givers' or 'receivers'. Example: !hboard givers");
            return true;
        }

        var stats = GetDict(HugStatsKey);
        var seen = GetDict(SeenUsersKey);
        if (stats == null || seen == null)
        {
            CPH.LogWarn("!hboard: saved data failed to load; skipping.");
            return true;
        }

        var giverList = new List<KeyValuePair<string, int>>();
        var receiverList = new List<KeyValuePair<string, int>>();
        foreach (var kv in stats)
        {
            if (kv.Key.EndsWith("_given"))
            {
                string user = kv.Key.Substring(0, kv.Key.Length - "_given".Length);
                if (seen.ContainsKey(user))
                    giverList.Add(new KeyValuePair<string, int>(user, kv.Value));
            }
            else if (kv.Key.EndsWith("_received"))
            {
                string user = kv.Key.Substring(0, kv.Key.Length - "_received".Length);
                if (seen.ContainsKey(user))
                    receiverList.Add(new KeyValuePair<string, int>(user, kv.Value));
            }
        }

        giverList.Sort((a, b) => b.Value.CompareTo(a.Value));
        receiverList.Sort((a, b) => b.Value.CompareTo(a.Value));
        if (giverList.Count == 0 && receiverList.Count == 0)
        {
            CPH.SendMessage($"@{invoker}, no hugs have been recorded yet!");
            return true;
        }

        var giverParts = BuildTop3(giverList);
        var receiverParts = BuildTop3(receiverList);
        string giverBoard = giverParts.Count > 0 ? string.Join(" | ", giverParts) : "No hugs given yet";
        string receiverBoard = receiverParts.Count > 0 ? string.Join(" | ", receiverParts) : "No hugs received yet";
        if (mode == "givers")
            CPH.SendMessage($"💖 Hug Giver Leaderboard (Top 3): {giverBoard}");
        else
            CPH.SendMessage($"💖 Hug Receiver Leaderboard (Top 3): {receiverBoard}");
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
            CPH.LogWarn($"!hboard: failed to deserialize '{key}'; skipping. Error: {ex.Message}");
            return null;
        }
    }
}
