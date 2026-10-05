// Trigger: Command (!resetattendance)
// References: Newtonsoft.Json
using System;
using System.Collections.Generic;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";


    public bool Execute()
    {
        if (args == null || !args.ContainsKey("user") || args["user"] == null)
        {
            CPH.LogWarn("Reset Attendance: no invoking user; skipping.");
            return true;
        }
        string invoker = args["user"].ToString().Trim().TrimStart('@').ToLowerInvariant();

        var broadcaster = CPH.TwitchGetBroadcaster();
        string streamerName = broadcaster == null ? "" : (broadcaster.UserLogin ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(streamerName))
        {
            CPH.LogWarn("StreamFolk: connect the Twitch broadcaster account before running this action.");
            return false;
        }

        // Only the connected broadcaster can run this command.
        if (invoker != streamerName)
        {
            CPH.SendMessage($"/me @{invoker} you do not have permission to reset attendance.");
            return true;
        }

        // ⭐ Reset streamfolk.analytics.seenUsers by overwriting with an empty dictionary
        var emptyDict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(emptyDict);
        CPH.SetGlobalVar(SeenUsersKey, json, true);

        // Confirmation
        CPH.SendMessage("/me Attendance Check has been reset!");
        CPH.LogInfo("streamfolk.analytics.seenUsers cleared via !resetattendance command.");

        return true;
    }
}