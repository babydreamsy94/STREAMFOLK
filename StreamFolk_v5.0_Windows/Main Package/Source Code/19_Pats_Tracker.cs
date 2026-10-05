// Trigger: Command (!pat)
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
            CPH.LogWarn("!pat: args is null, skipping.");
            return true;
        }

        string invokerRaw = GetArg("user");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invokerRaw)) invokerRaw = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invokerRaw))
        {
            CPH.LogWarn("!pat: no invoker username found, skipping.");
            return true;
        }

        string targetRaw = ExtractTarget(GetArg("rawInput"));
        if (string.IsNullOrWhiteSpace(targetRaw))
            targetRaw = invokerRaw;

        string invokerNorm = Normalize(invokerRaw);
        string targetNorm = Normalize(targetRaw);
        bool isSelfPat = invokerNorm == targetNorm;

        // Load every required dictionary before mutating or saving any of them.
        var seen = GetDict(SeenUsersKey);
        var patsReceived = GetDict(PatsKey);
        var patsGiven = GetDict(PatsGivenKey);
        if (seen == null || patsReceived == null || patsGiven == null)
        {
            CPH.LogWarn("!pat: saved data failed to load; skipping to protect stored data.");
            return true;
        }

        if (!isSelfPat && !seen.ContainsKey(targetNorm))
        {
            CPH.SendMessage($"🛑 @{invokerRaw} PAT FAILED! @{targetRaw} HAS NOT BEEN COUNTED AS ATTENDING THIS STREAM! 🛑");
            return true;
        }

        int receivedCount = patsReceived.ContainsKey(targetNorm) ? patsReceived[targetNorm] + 1 : 1;
        int givenCount = patsGiven.ContainsKey(invokerNorm) ? patsGiven[invokerNorm] + 1 : 1;
        patsReceived[targetNorm] = receivedCount;
        patsGiven[invokerNorm] = givenCount;

        SaveDict(PatsKey, patsReceived);
        SaveDict(PatsGivenKey, patsGiven);

        var rng = new Random();
        string[] selfPhrases =
        {
            $"@{invokerRaw} patted themselves on the back. Good job, kiddo! ✨ (You have received {receivedCount} pats now!)",
            $"@{invokerRaw} used their plushie to give themselves a pat! How cute! 🥺 (You have received {receivedCount} pats now!)",
            $"@{invokerRaw} patted their padded bum like the cute baby they are 🤭 (You have received {receivedCount} pats now!)",
            $"@{invokerRaw} touched their fluffy ears and made their tail wag! (You have received {receivedCount} pats now!)",
            $"@{invokerRaw} curls up for naptime with a cozy pat. 🌙 (You have received {receivedCount} pats now!)",
        };

        string[] otherPhrases =
        {
            $"@{invokerRaw} patted @{targetRaw} on the back! Proud of them! 😊 (They have received {receivedCount} pats now!)",
            $"@{invokerRaw} ruffled @{targetRaw}'s hair! How adorable! 🥺 (They have received {receivedCount} pats now!)",
            $"@{invokerRaw} patted @{targetRaw}'s padded bum! How cute! 🤭 (They have received {receivedCount} pats now!)",
            $"@{invokerRaw} played with @{targetRaw}'s fluffy ears and made their tail wag! (They have received {receivedCount} pats now!)",
            $"@{invokerRaw} used their plushie to pat @{targetRaw} on the head! Someone seems shy! 🤭 (They have received {receivedCount} pats now!)",
        };

        CPH.SendMessage(isSelfPat ? selfPhrases[rng.Next(selfPhrases.Length)] : otherPhrases[rng.Next(otherPhrases.Length)]);
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
            CPH.LogWarn($"!pat: failed to deserialize '{key}'; NOT resetting and NOT saving. Error: {ex.Message}");
            return null;
        }
    }

    private void SaveDict(string key, Dictionary<string, int> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }
}
