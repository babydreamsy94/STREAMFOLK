using System;
using System.Collections.Generic;

public class CPHInline
{
    private static readonly Random rng = new Random();
    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("StreamFolk: command args are null; skipping.");
            return true;
        }
        // ⭐ SAFE USERNAME PARSING + NORMALIZATION ⭐
        string invokerRaw = null;
        if (args.ContainsKey("user"))
            invokerRaw = args["user"]?.ToString();
        if (string.IsNullOrWhiteSpace(invokerRaw))
            return true;
        string invoker = invokerRaw.Trim().TrimStart('@').ToLowerInvariant();
        DateTime today = DateTime.Today;
        // Load dictionaries
        var stats = CPH.GetGlobalVar<Dictionary<string, int>>("streamfolk.analytics.firstStats", true) ?? new Dictionary<string, int>();
        var dates = CPH.GetGlobalVar<Dictionary<string, string>>("streamfolk.analytics.firstDates", true) ?? new Dictionary<string, string>();
        string streakKey = $"{invoker}_streak";
        string totalKey = $"{invoker}_total";
        string lastClaimKey = $"{invoker}_lastClaim";
        string todayWinnerKey = $"FirstWinner_{today:yyyy-MM-dd}";
        DateTime lastClaim = DateTime.MinValue;
        if (dates.ContainsKey(lastClaimKey))
            DateTime.TryParse(dates[lastClaimKey], out lastClaim);
        int currentStreak = stats.ContainsKey(streakKey) ? stats[streakKey] : 0;
        // --- Winner lock check ---
        if (dates.ContainsKey(todayWinnerKey))
        {
            string winner = dates[todayWinnerKey];
           if (winner.ToLowerInvariant() != invoker)
            {
                // Someone else already claimed FIRST! today → playful rejection
                string[] rejectionMessages =
                {
                    "Sorry @{user}, but you just missed it! @{winner} already grabbed FIRST! today.",
                    "Uh oh @{user}, but it looks like @{winner} already got to FIRST! before you! Better luck next time!",
                    "OH darn @{user}! @{winner} already got FIRST! Oh well....",
                    "You snooze you lose, @{user}! @{winner} got early and got FIRST! because of it!"
                };
                int index = rng.Next(rejectionMessages.Length);
                string msg = rejectionMessages[index].Replace("{user}", invokerRaw).Replace("{winner}", winner);
                CPH.SendMessage(msg);
                return true; // stop here, no streak update
            }
        }
        else
        {
            // No winner yet → set this user as today’s winner
            dates[todayWinnerKey] = invokerRaw;
            // Increment total claims for this user (no milestone phrases here)
            stats[totalKey] = stats.ContainsKey(totalKey) ? stats[totalKey] + 1 : 1;
        }

        // --- Streak logic ---
        if (lastClaim != DateTime.MinValue && lastClaim.Date == today.AddDays(-1).Date)
        {
            // Yesterday → continue streak
            stats[streakKey] = currentStreak + 1;
            // --- Unique milestone celebrations for streaks only ---
            if (stats[streakKey] == 5)
                CPH.SendMessage($"Congratulations @{invokerRaw}! You've managed to stay FIRST! for the past 5 times in a row! Keep it up, kiddo! 👍");
            else if (stats[streakKey] == 10)
                CPH.SendMessage($"INCREDIBLE, @{invokerRaw}! You've managed to stay first for the past TEN TIMES! You are amazing! 😲");
            else if (stats[streakKey] == 21)
                CPH.SendMessage($"BONG! BONG! @{invokerRaw} is now feeling like a deadman with their 21-0 streak! Let's hope no one breaks it!");
            else if (stats[streakKey] == 25)
                CPH.SendMessage($"How is it possible that @{invokerRaw} has managed to stay FIRST for the past 25 times?! Is anyone gonna end this streak?! 😲");
            else if (stats[streakKey] == 50)
                CPH.SendMessage($"50 FIRSTS IN A ROW for @{invokerRaw}?! Is there even anyone else trying at this point?!");
            else if (stats[streakKey] == 100)
            {
                // Milestone only — no reset
                CPH.SendMessage($"You've somehow managed to hit 100 FIRST!s....I have nothing else to say to you, @{invokerRaw} 👏");
            }
            else if (stats[streakKey] == 173)
            {
                // Goldberg milestone message first
                CPH.SendMessage($"OMG {invokerRaw}, YOU'VE DONE IT! YOU HIT 173 CONSECUTIVE FIRST!s! However, I have some bad news for you.... 😈");
                CPH.Wait(5000);
                stats[streakKey] = 1; // reset after 173
                CPH.SendMessage($"OH shoot! @{invokerRaw} got hit with a cattle prod & got pinned! Their streak is over! Wonder if they'll be able to recover from this... 🤔");
            }
            else
            {
                // Normal streak continuation (only if not a milestone)
                string[] continueMessages =
                {
                    "Watch out everyone! @{user} is currently on a hot streak of {streak} FIRST!s",
                    "Keep an eye on @{user}, folks! They're on a big streak of {streak} FIRST!s",
                    "Woah woah woah, @{user}! Keep up that streak of {streak} FIRST!s",
                    "@{user}'s streak of {streak} is making me feel like they're just inflating the number at this point!"
                };
                SendRandomMessage(continueMessages, invokerRaw, stats[streakKey]);
            }
        }
        else if (lastClaim != DateTime.MinValue && lastClaim.Date == today.Date)
        {
            // Same day → streak holds (but only for the winner)
            stats[streakKey] = currentStreak > 0 ? currentStreak : 1;
            string[] sameDayMessages =
            {
                "⏳ @{user}, you already claimed FIRST! today — streak holds at {streak}.",
                "✅ @{user}, FIRST! locked in for today. Streak stays at {streak}.",
                "🧷 @{user}, duplicate claim ignored. Streak remains {streak}."
            };
            SendRandomMessage(sameDayMessages, invokerRaw, stats[streakKey]);
        }
        else
        {
            // Missed days or reset → fresh start
            stats[streakKey] = 1;
            string[] freshStartMessages =
            {
                "🌱 @{user}, time for a new streak to begin! Current Streak: {streak}",
                "✨ @{user}, the streak has been reset! Current Streak: {streak}",
                "🔄 @{user}, fresh start! Current streak: {streak}."
            };
            SendRandomMessage(freshStartMessages, invokerRaw, stats[streakKey]);
        }

        // Save updated claim date
        dates[lastClaimKey] = today.ToString("yyyy-MM-dd");
        // Persist globals
        CPH.SetGlobalVar("streamfolk.analytics.firstStats", stats, true);
        CPH.SetGlobalVar("streamfolk.analytics.firstDates", dates, true);
        return true;
    }

    // --- Helper method ---
    private void SendRandomMessage(string[] messages, string invokerRaw, int streakValue = 0)
    {
        int index = rng.Next(messages.Length);
        string msg = messages[index];
        msg = msg.Replace("{user}", invokerRaw).Replace("{streak}", streakValue.ToString());
        CPH.SendMessage(msg);
    }
}
