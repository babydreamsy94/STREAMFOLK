using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";
    private const string AttendanceHistoryKey = "streamfolk.analytics.attendanceHistory";
    // Keep this small list identical in the companion Bot Welcomer.
    // The connected broadcaster and bot are also excluded automatically.
    private static readonly string[] ExcludedUsers =
    {
        "botname",
        "pokemoncommunitygame",
        "kofistreambot",
        "nightbot",
        "streamelements",
        "streamlabs",
        "sery_bot"
    };
    private static readonly object AttendanceLock = new object ();
    private static readonly HttpClient TwitchStatusClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    public bool Execute()
    {
        if (args == null || IsTrueArg("isTest") || IsTrueArg("isSimulated"))
            return true;
        lock (AttendanceLock)
        {
            try
            {
                string source = CPH.GetEventType().ToString();
                bool requireLive = false;
                bool redeem = false;
                var names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                switch (source)
                {
                    case "TwitchPresentViewers":
                        if (!IsTrueArg("isLive"))
                            return true;
                        object rawUsers;
                        if (!args.TryGetValue("users", out rawUsers))
                            throw new InvalidOperationException("Present Viewers is missing users.");
                        var users = rawUsers as IEnumerable<Dictionary<string, object>>;
                        if (users == null)
                            throw new InvalidOperationException("Unexpected Present Viewers users type.");
                        foreach (var entry in users)
                            AddName(names, LoginFrom(entry, "userLogin", "login", "userName", "username", "user_name", "user", "name"));
                        break;
                    case "TwitchGiftSub":
                    case "TwitchGiftBomb":
                        if (AnonymousSupport())
                            return true;
                        // Native gift triggers use the ordinary user fields for
                        // the gifter. Also accept the existing tracker's explicit
                        // gifter aliases. NEVER fall back to recipient fields.
                        string gifter = GiftActor();
                        string recipient = LoginFrom(args, "recipientUserName", "recipientUserLogin");
                        if (gifter != null && string.Equals(gifter, recipient, StringComparison.OrdinalIgnoreCase))
                            return true;
                        AddName(names, gifter);
                        requireLive = true;
                        break;
                    case "TwitchSub":
                    case "TwitchReSub":
                        // Defensive guard if a gifted-recipient notification is
                        // ever routed as a generic subscription event.
                        if (IsTrueArg("isGift") || IsTrueArg("is_gift") || IsTrueArg("fromGiftBomb"))
                            return true;
                        goto case "TwitchFollow";
                    case "TwitchFollow":
                    case "TwitchCheer":
                    case "TwitchRaid":
                        if (AnonymousSupport())
                            return true;
                        AddName(names, LoginFrom(args, "userLogin", "userName", "user"));
                        requireLive = true;
                        break;
                    case "TwitchRewardRedemption":
                        // Attach only the Attendance Check reward to this action.
                        redeem = true;
                        goto case "TwitchFirstWord";
                    case "TwitchFirstWord":
                    case "TwitchChatMessage":
                        AddName(names, LoginFrom(args, "userLogin", "userName", "user", "userDisplayName"));
                        break;
                    case "CommandTriggered":
                        // Preserve optional Twitch attendance commands.
                        string platform = TextFrom(args, "commandSource");
                        if (!string.IsNullOrWhiteSpace(platform) && !string.Equals(platform, "Twitch", StringComparison.OrdinalIgnoreCase))
                            return true;
                        AddName(names, LoginFrom(args, "userLogin", "userName", "user", "userDisplayName"));
                        break;
                    default:
                        // Do not treat arbitrary events containing a username
                        // (for example moderation or gift-recipient events) as attendance.
                        return true;
                }

                if (names.Count == 0)
                    return true;
                var excluded = CurrentExclusions();
                foreach (string name in new List<string>(names.Keys))
                    if (excluded.ContainsKey(name))
                        names.Remove(name);
                if (names.Count == 0)
                    return true;
                var seen = ReadDictionary<int>(SeenUsersKey);
                foreach (string name in new List<string>(names.Keys))
                    if (seen.ContainsKey(name))
                        names.Remove(name);
                if (names.Count == 0)
                {
                    if (redeem)
                        RefundDuplicateRedeem();
                    return true;
                }

                // Only new support attendees need a live-status request.
                // In a gift bomb, subsequent events for the same gifter are no-ops.
                if (requireLive && !BroadcasterIsLive())
                    return true;
                return RegisterNewNames(names, seen, source);
            }
            catch (Exception ex)
            {
                CPH.LogInfo("Attendance Check: update failed; saved data was not reset. " + ex.Message);
                return false;
            }
        }
    }

    private bool RegisterNewNames(Dictionary<string, bool> names, Dictionary<string, int> seen, string source)
    {
        // Read both existing JSON strings before any save. Bad JSON stops this
        // event instead of silently replacing the existing record with {}.
        var history = ReadDictionary<List<string>>(AttendanceHistoryKey);
        DateTime today = DateTime.Now.Date;
        bool historyChanged = false;
        foreach (string name in names.Keys)
        {
            List<string> dates;
            if (!history.TryGetValue(name, out dates) || dates == null)
            {
                dates = new List<string>();
                history[name] = dates;
            }

            bool recordedToday = false;
            foreach (string date in dates)
            {
                DateTime parsed;
                if (DateTime.TryParse(date, out parsed) && parsed.Date == today)
                {
                    recordedToday = true;
                    break;
                }
            }

            if (!recordedToday)
            {
                dates.Add(today.ToString("yyyy-MM-dd"));
                historyChanged = true;
            }

            seen[name] = 1;
        }

        // Preserve existing values, dates and names. History saves first so a
        // failed history save cannot mark new users as already checked in.
        if (historyChanged)
            CPH.SetGlobalVar(AttendanceHistoryKey, JsonConvert.SerializeObject(history, Formatting.Indented), true);
        CPH.SetGlobalVar(SeenUsersKey, JsonConvert.SerializeObject(seen), true);
        CPH.LogInfo("Attendance Check: " + source + " silently added " + names.Count + " account(s).");
        return true;
    }

    private bool BroadcasterIsLive()
    {
        try
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster == null || string.IsNullOrWhiteSpace(broadcaster.UserId))
            {
                CPH.LogInfo("Attendance Check: cannot identify the broadcaster; support attendance skipped.");
                return false;
            }

            string token = CPH.TwitchOAuthToken;
            string clientId = CPH.TwitchClientId;
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(clientId))
            {
                CPH.LogInfo("Attendance Check: Twitch authentication unavailable; support attendance skipped.");
                return false;
            }

            string url = "https://api.twitch.tv/helix/streams?user_id=" + Uri.EscapeDataString(broadcaster.UserId);
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Client-Id", clientId);
                using (var response = TwitchStatusClient.SendAsync(request).GetAwaiter().GetResult())
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        CPH.LogInfo("Attendance Check: Twitch live check returned HTTP " + (int)response.StatusCode + "; support attendance skipped.");
                        return false;
                    }

                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var data = JObject.Parse(body)["data"] as JArray;
                    if (data == null)
                        throw new InvalidOperationException("Missing stream data.");
                    foreach (var stream in data)
                    {
                        if ((string)stream["user_id"] == broadcaster.UserId && (string)stream["type"] == "live")
                            return true;
                    }

                    CPH.LogInfo("Attendance Check: broadcaster is offline; support attendance skipped.");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception TYPE only. Never log credentials, request
            // headers, response bodies or the complete event arguments.
            CPH.LogInfo("Attendance Check: live status unavailable (" + ex.GetType().Name + "); support attendance skipped. Chat/redeem/presence remain available.");
            return false;
        }
    }

    private string GiftActor()
    {
        string[] keys =
        {
            "gifterUserLogin",
            "gifterLogin",
            "gifterUserName",
            "gifterName",
            "gifterDisplayName"
        };
        string explicitGifter = TextFrom(args, keys);
        if (explicitGifter != null)
            return LoginFrom(args, keys);
        return LoginFrom(args, "userLogin", "userName", "user");
    }

    private bool AnonymousSupport()
    {
        foreach (string key in new[]
        {
            "anonymous",
            "isAnonymous",
            "is_anonymous"
        }

        )
        {
            object raw;
            if (!args.TryGetValue(key, out raw) || raw == null)
                continue;
            bool value;
            if (!bool.TryParse(raw.ToString(), out value) || value)
                return true;
        }

        foreach (string key in new[]
        {
            "gifterUserLogin",
            "gifterLogin",
            "gifterUserName",
            "gifterName",
            "gifterDisplayName",
            "userLogin",
            "userName",
            "user"
        }

        )
        {
            string name = Normalize(TextFrom(args, key));
            if (name == "anonymous" || name == "ananonymousgifter" || name == "anonymousgifter" || name == "anonymouscheerer")
                return true;
        }

        return false;
    }

    private void RefundDuplicateRedeem()
    {
        string rewardId = TextFrom(args, "rewardId");
        string redemptionId = TextFrom(args, "redemptionId");
        if (!string.IsNullOrWhiteSpace(rewardId) && !string.IsNullOrWhiteSpace(redemptionId))
        {
            bool refunded = CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            CPH.LogInfo(refunded ? "Attendance Check: duplicate attendance redeem refunded." : "Attendance Check: duplicate redeem found, but Twitch did not confirm the refund.");
        }
        else
            CPH.LogInfo("Attendance Check: duplicate redeem; missing IDs, no refund attempted.");
    }

    private Dictionary<string, T> ReadDictionary<T>(string key)
    {
        string json = CPH.GetGlobalVar<string>(key, true);
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;
        var raw = JsonConvert.DeserializeObject<Dictionary<string, T>>(json);
        if (raw == null)
            throw new InvalidOperationException(key + " contains JSON null.");
        foreach (var item in raw)
            result[item.Key] = item.Value;
        return result;
    }

    private Dictionary<string, bool> CurrentExclusions()
    {
        var excluded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in ExcludedUsers)
            AddName(excluded, name);
        AddName(excluded, LoginFrom(args, "broadcastUserName", "broadcastUserLogin"));
        try
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster != null)
                AddName(excluded, Normalize(broadcaster.UserLogin));
        }
        catch
        {
            CPH.LogInfo("Attendance Check: broadcaster lookup unavailable; using configured exclusions.");
        }

        try
        {
            var bot = CPH.TwitchGetBot();
            if (bot != null)
                AddName(excluded, Normalize(bot.UserLogin));
        }
        catch
        {
            CPH.LogInfo("Attendance Check: bot lookup unavailable; using configured exclusions.");
        }

        return excluded;
    }

    private static void AddName(Dictionary<string, bool> names, string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            names[name] = true;
    }

    private static string LoginFrom(Dictionary<string, object> values, params string[] keys)
    {
        foreach (string key in keys)
        {
            string name = Normalize(TextFrom(values, key));
            if (name.Length == 0 || name.Length > 25)
                continue;
            bool valid = true;
            foreach (char c in name)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    valid = false;
                    break;
                }

            if (valid)
                return name;
        }

        return null;
    }

    private static string TextFrom(Dictionary<string, object> values, params string[] keys)
    {
        if (values == null)
            return null;
        foreach (string key in keys)
            foreach (var entry in values)
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    // Reject numeric IDs/objects passed in a username field.
                    string text = entry.Value as string;
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }

        return null;
    }

    private bool IsTrueArg(string key)
    {
        object raw;
        bool value;
        return args.TryGetValue(key, out raw) && raw != null && bool.TryParse(raw.ToString(), out value) && value;
    }

    private static string Normalize(string value)
    {
        return (value ?? "").Trim().TrimStart('@').ToLowerInvariant();
    }
}