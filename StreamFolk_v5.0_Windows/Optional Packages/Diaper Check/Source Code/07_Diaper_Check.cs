using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private static readonly Random rng = new Random();

    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogInfo("Diaper Check: args is null, exiting.");
            return true;
        }

        // userName is the Twitch login on the normal command trigger; keep
        // userLogin/user as defensive fallbacks for tests and alternate triggers.
        string triggeringUser = GetArg("userName");
        if (string.IsNullOrWhiteSpace(triggeringUser)) triggeringUser = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(triggeringUser)) triggeringUser = GetArg("user");
        triggeringUser = Normalize(triggeringUser);
        if (string.IsNullOrWhiteSpace(triggeringUser))
        {
            CPH.LogInfo("Diaper Check: no username provided, exiting.");
            return true;
        }

        string rawInput = GetArg("rawInput");
        string targetArg = ExtractTarget(rawInput);
        bool noArgument = string.IsNullOrWhiteSpace(targetArg);
        string targetUser = noArgument ? triggeringUser : Normalize(targetArg);

        char[] trimChars =
        {
            ',',
            '.',
            '!',
            '?',
            ':',
            ';',
            ')',
            ']',
            '}',
            '\"',
            '\''
        };
        targetUser = targetUser.TrimEnd(trimChars).ToLowerInvariant();

        var seenDict = GetSeenUsers();
        if (seenDict == null)
        {
            CPH.LogWarn("Diaper Check: saved streamfolk.analytics.seenUsers data failed to load; skipping.");
            return true;
        }

        if (!seenDict.ContainsKey(targetUser))
        {
            CPH.SendMessage($"🛑 @{triggeringUser} DIAPER CHECK FAILED! @{targetUser} HAS NOT BEEN COUNTED AS ATTENDING THIS STREAM! 🛑");
            return true;
        }

        int soggyRoll = rng.Next(0, 101);
        int stinkyRoll = rng.Next(0, 101);
        string playfulMsg = GetPlayfulMessage(soggyRoll, stinkyRoll);

        if (noArgument)
            CPH.SendMessage($"@{triggeringUser} is {soggyRoll}% soggy & {stinkyRoll}% stinky! {playfulMsg}");
        else
            CPH.SendMessage($"@{targetUser} is {soggyRoll}% soggy & {stinkyRoll}% stinky! {playfulMsg}");

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

    private string Normalize(string value)
    {
        return (value ?? "").Trim().TrimStart('@').ToLowerInvariant();
    }

    private Dictionary<string, int> GetSeenUsers()
    {
        string seenJson = CPH.GetGlobalVar<string>("streamfolk.analytics.seenUsers", true);
        if (string.IsNullOrWhiteSpace(seenJson))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var raw = JsonConvert.DeserializeObject<Dictionary<string, int>>(seenJson);
            if (raw == null)
                throw new InvalidOperationException("streamfolk.analytics.seenUsers contains JSON null.");

            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
                seen[kv.Key] = kv.Value;
            return seen;
        }
        catch (Exception ex)
        {
            CPH.LogWarn("Diaper Check: failed to deserialize 'streamfolk.analytics.seenUsers'; cannot verify attendance. Error: " + ex.Message);
            return null;
        }
    }

    private static string GetPlayfulMessage(int soggy, int stinky)
    {
        if (soggy == 100 && stinky == 100)
        {
            var options = new[]
            {
                "Time for a much-needed change!",
                "Good thing I caught this before it was too late!",
                "Wow this one got really filled up!"
            };
            return options[rng.Next(options.Length)];
        }
        else if (soggy >= 51 && soggy <= 100 && stinky >= 51 && stinky <= 100)
        {
            var options = new[]
            {
                "These pamps are getting pretty heavy!",
                "Someone's been putting their pamps to good use!",
                "Looks like these pamps are doing their job well!",
            };
            return options[rng.Next(options.Length)];
        }
        else
        {
            var options = new[]
            {
                "Not quite full enough, I'd say.",
                "Don't worry, these can handle much more!",
                "These have PLENTY of room left!"
            };
            return options[rng.Next(options.Length)];
        }
    }
}
