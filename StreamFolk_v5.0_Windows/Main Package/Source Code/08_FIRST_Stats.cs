using System;
using System.Collections.Generic;

public class CPHInline
{
    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("StreamFolk: command args are null; skipping.");
            return true;
        }
        // ⭐ SAFE USERNAME PARSING ⭐
        string invokerRaw = null;
        if (args.ContainsKey("user"))
            invokerRaw = args["user"]?.ToString();
        if (string.IsNullOrWhiteSpace(invokerRaw))
            return true;
        // Normalize username
        string invoker = invokerRaw.Trim().TrimStart('@').ToLowerInvariant();
        // Load stats dictionary safely (case-insensitive)
        var stats = CPH.GetGlobalVar<Dictionary<string, int>>("streamfolk.analytics.firstStats", true) ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Ensure dictionary is case-insensitive
        if (!(stats.Comparer.Equals(StringComparer.OrdinalIgnoreCase)))
        {
            stats = new Dictionary<string, int>(stats, StringComparer.OrdinalIgnoreCase);
        }

        // Build keys using normalized username
        string streakKey = $"{invoker}_streak";
        string totalKey = $"{invoker}_total";
        int streak = stats.ContainsKey(streakKey) ? stats[streakKey] : 0;
        int total = stats.ContainsKey(totalKey) ? stats[totalKey] : 0;
        if (total > 0)
        {
            if (streak > 1)
            {
                CPH.SendMessage($"📊 @{invokerRaw}, you’ve claimed FIRST! {total} times overall, and your current streak is {streak} FIRST!s.");
            }
            else if (streak == 1)
            {
                CPH.SendMessage($"📊 @{invokerRaw}, you’ve claimed FIRST! {total} times overall. You’re at the start of a new streak!");
            }
            else
            {
                CPH.SendMessage($"📊 @{invokerRaw}, you’ve claimed FIRST! {total} times overall, but you’re not on a streak right now.");
            }
        }
        else
        {
            CPH.SendMessage($"Hold up there, @{invokerRaw}! You haven’t claimed FIRST! yet.");
        }

        return true;
    }
}
