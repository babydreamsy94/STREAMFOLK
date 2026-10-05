// Trigger: Command (!bonk)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";
    private static readonly Random rng = new Random();

    private static readonly string[] bonkPhrases = new[]
    {
        "@{invokerRaw} bonks @{targetRaw} on the head with a hammer!",
        "@{invokerRaw} hits @{targetRaw} with a rolled up newspaper!",
        "@{invokerRaw} cracks @{targetRaw} with a baseball bat!",
        "@{invokerRaw} taps @{targetRaw} with a wooden stick!",
    };

    private static readonly string[] selfBonkPhrases = new[]
    {
        "@{invokerRaw} bonked themselves...but why tho?"
    };

    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("!bonk: args is null, skipping.");
            return true;
        }

        string invokerRaw = GetArg("user");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invokerRaw))
        {
            CPH.LogWarn("!bonk: no invoker username found, skipping.");
            return true;
        }

        string invoker = Normalize(invokerRaw);
        string targetRaw = ExtractTarget(GetArg("rawInput"));
        string target = Normalize(targetRaw);

        var seen = GetDict(SeenUsersKey);
        if (seen == null)
        {
            CPH.LogWarn("!bonk: saved streamfolk.analytics.seenUsers data failed to load; skipping.");
            return true;
        }

        if (!seen.ContainsKey(invoker))
        {
            CPH.SendMessage($"🛑 @{invokerRaw} BONK FAILED! @{targetRaw} HAS NOT BEEN COUNTED AS ATTENDING THIS STREAM! 🛑");
            return true;
        }

        if (!string.IsNullOrEmpty(target) && !seen.ContainsKey(target))
        {
            CPH.SendMessage($"🛑 @{invokerRaw} BONK FAILED! @{targetRaw} HAS NOT DONE ATTENDANCE CHECK REDEEM! 🛑");
            return true;
        }

        string phrase;
        if (string.IsNullOrEmpty(target) || target == invoker)
            phrase = selfBonkPhrases[0].Replace("@{invokerRaw}", invokerRaw);
        else
            phrase = bonkPhrases[rng.Next(bonkPhrases.Length)]
                .Replace("@{invokerRaw}", invokerRaw)
                .Replace("@{targetRaw}", targetRaw);

        CPH.SendMessage(phrase);
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
            CPH.LogWarn($"!bonk: failed to deserialize '{key}'; skipping. Error: {ex.Message}");
            return null;
        }
    }
}
