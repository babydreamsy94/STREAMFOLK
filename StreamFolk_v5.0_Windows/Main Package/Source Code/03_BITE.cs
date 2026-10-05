// Trigger: Command (!bite)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";

    private static readonly Random rng = new Random();
    private static readonly object rngLock = new object();

    private static readonly string[] bitePhrases = new[]
    {
        "@{invokerRaw} sinks their teeth into @{targetRaw}!",
        "@{invokerRaw} playfully bites @{targetRaw}!",
        "@{invokerRaw} chomps down on @{targetRaw} like a snack!",
        "@{invokerRaw} gives @{targetRaw} a surprise nibble!",
        "@{invokerRaw} noms on @{targetRaw}'s finger!"
    };

    private static readonly string[] selfBitePhrases = new[]
    {
        "@{invokerRaw} nibbles on their flesh a bit....that's weird",
        "@{invokerRaw} nom nom nom... tasty fingers!",
        "@{invokerRaw} bites themselves and instantly regrets it!",
        "@{invokerRaw} chomps on their own arm like a snack!"
    };

    private int NextRandom(int maxValue)
    {
        lock (rngLock)
        {
            return rng.Next(maxValue);
        }
    }

    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("!bite: args is null, skipping.");
            return true;
        }

        string invokerRaw = GetArg("user");
        if (string.IsNullOrWhiteSpace(invokerRaw))
            invokerRaw = GetArg("userName");
        if (string.IsNullOrWhiteSpace(invokerRaw))
            invokerRaw = GetArg("userLogin");

        string invokerLogin = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(invokerLogin))
            invokerLogin = invokerRaw;

        if (string.IsNullOrWhiteSpace(invokerRaw))
        {
            CPH.LogWarn("!bite: no invoker username found, skipping.");
            return true;
        }

        string invoker = Normalize(invokerLogin);

        // Streamer.bot removes a Starts With command (such as !bite) from
        // rawInput. Therefore "!bite username" normally supplies only
        // "username" here. ExtractTarget also accepts the full command text
        // so the action remains safe when run from another trigger or test.
        string rawInput = GetArg("rawInput");
        string targetRaw = ExtractTarget(rawInput);
        string target = Normalize(targetRaw);

        var seen = GetDict(SeenUsersKey);

        if (!seen.ContainsKey(invoker))
        {
            CPH.SendMessage($"🛑 BITE FAILED! @{invokerRaw} HAS NOT BEEN COUNTED AS ATTENDING THIS STREAM! 🛑");
            return true;
        }

        if (!string.IsNullOrEmpty(target) &&
            !string.Equals(target, invoker, StringComparison.OrdinalIgnoreCase) &&
            !seen.ContainsKey(target))
        {
            CPH.SendMessage($"🛑 @{invokerRaw} BITE FAILED! @{targetRaw} HAS NOT BEEN COUNTED AS ATTENDING THIS STREAM! 🛑");
            return true;
        }

        string phrase;
        if (string.IsNullOrEmpty(target) ||
            string.Equals(target, invoker, StringComparison.OrdinalIgnoreCase))
        {
            phrase = selfBitePhrases[NextRandom(selfBitePhrases.Length)]
                .Replace("@{invokerRaw}", invokerRaw);
        }
        else
        {
            phrase = bitePhrases[NextRandom(bitePhrases.Length)]
                .Replace("@{invokerRaw}", invokerRaw)
                .Replace("@{targetRaw}", targetRaw);
        }

        CPH.SendMessage(phrase);
        return true;
    }

    private string ExtractTarget(string rawInput)
    {
        string value = (rawInput ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        // Compatibility for tests or triggers that provide the entire message.
        if (string.Equals(value, "!bite", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (value.StartsWith("!bite ", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(5).TrimStart();

        // Twitch login names cannot contain spaces. Ignore accidental extra text.
        int whitespace = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        if (whitespace >= 0)
            value = value.Substring(0, whitespace);

        return value.Trim().TrimStart('@');
    }

    private string GetArg(string key)
    {
        object value;
        if (!args.TryGetValue(key, out value) || value == null)
            return string.Empty;

        return value.ToString();
    }

    private string Normalize(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .TrimStart('@')
            .ToLowerInvariant();
    }

    private Dictionary<string, int> GetDict(string key)
    {
        string json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            Dictionary<string, int> raw =
                JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ??
                new Dictionary<string, int>();

            Dictionary<string, int> dict =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, int> pair in raw)
            {
                string normalizedKey = Normalize(pair.Key);
                if (!string.IsNullOrWhiteSpace(normalizedKey))
                    dict[normalizedKey] = pair.Value;
            }

            return dict;
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"!bite: Failed to deserialize '{key}'. Error: {ex.Message}");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}