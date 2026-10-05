// Trigger: Command (!resetpats)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class CPHInline
{
    private const string PatStatsKey = "streamfolk.analytics.patStats";

    public bool Execute()
    {
        if (args == null || !args.ContainsKey("user") || args["user"] == null)
        {
            CPH.LogWarn("Reset Pats: no invoking user; skipping.");
            return true;
        }
        var invoker = args["user"].ToString();

        var broadcaster = CPH.TwitchGetBroadcaster();
        string streamerName = broadcaster == null ? "" : (broadcaster.UserLogin ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(streamerName))
        {
            CPH.LogWarn("StreamFolk: connect the Twitch broadcaster account before running this action.");
            return false;
        }

        if (!string.Equals(invoker, streamerName, StringComparison.OrdinalIgnoreCase))
        {
            CPH.SendMessage($"@{invoker}, only the broadcaster can use this command 🛑");
            return true;
        }

        // Clear streamfolk.analytics.patStats
        var empty = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var json = JsonConvert.SerializeObject(empty);
        CPH.SetGlobalVar(PatStatsKey, json, true);
        CPH.SetGlobalVar("streamfolk.analytics.patCounts", json, true);
        CPH.SetGlobalVar("streamfolk.analytics.patGiven", json, true);

        CPH.SendMessage($"@{invoker} cleared all pat stats!");
        return true;
    }
}
