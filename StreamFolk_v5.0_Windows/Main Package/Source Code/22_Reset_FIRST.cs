// Trigger: Command (!resetfirst)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;

public class CPHInline
{
    public bool Execute()
    {
        var broadcaster = CPH.TwitchGetBroadcaster();
        string allowedUser = broadcaster == null ? "" : (broadcaster.UserLogin ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(allowedUser))
        {
            CPH.LogWarn("StreamFolk: connect the Twitch broadcaster account before running this action.");
            return false;
        }

        if (args == null || !args.ContainsKey("user") || args["user"] == null)
        {
            CPH.LogWarn("Reset FIRST!: no invoking user; skipping.");
            return true;
        }
        string invokerRaw = args["user"].ToString();

        // Only allow broadcaster
        if (!invokerRaw.Equals(allowedUser, StringComparison.OrdinalIgnoreCase))
        {
            CPH.SendMessage($"⛔ Sorry @{invokerRaw}, but only {allowedUser} can use this command.");
            return true;
        }

        // Clear both dictionaries
        CPH.SetGlobalVar("streamfolk.analytics.firstStats", new Dictionary<string, int>(), true);
        CPH.SetGlobalVar("streamfolk.analytics.firstDates", new Dictionary<string, string>(), true);

        CPH.SendMessage("💣 Reset complete — all FIRST! wins, streaks, and last claim dates wiped.");
        return true;
    }
}