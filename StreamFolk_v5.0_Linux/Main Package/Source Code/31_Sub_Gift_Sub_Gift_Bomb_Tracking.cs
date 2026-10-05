using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

public class CPHInline
{
    private const string SubsKey = "streamfolk.analytics.subs";
    private const string SubsDetailedKey = "streamfolk.analytics.subsDetailed";
    private const string TotalSubsKey = "streamfolk.analytics.totalSubs";
    private static readonly string[] ExcludedUsers =
    {
        "botname",
        "pokemoncommunitygame"
    };
    public bool Execute()
    {
        try
        {
            if (args == null)
            {
                CPH.LogWarn("Sub Tracking: args is null.");
                return true;
            }

            CPH.LogInfo("Sub Tracking RAW: " + JsonConvert.SerializeObject(args));
            string trigger = GetTriggerName();
            CPH.LogInfo($"Detected Trigger: {trigger}");
            switch (trigger)
            {
                case "Subscription":
                    HandleSubscription();
                    break;
                case "Gift Subscription":
                    HandleGiftSubscription();
                    break;
                case "ReSubscription":
                case "Resubscription":
                case "Resub":
                    HandleResubscription();
                    break;
                case "Gift Bomb":
                    HandleGiftBomb();
                    break;
                default:
                    CPH.LogWarn($"Unknown trigger type: {trigger}");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"Sub Tracking Error: {ex}");
            return true;
        }
    }

    private void HandleSubscription()
    {
        string user = Normalize(GetFirst("userName", "displayName", "user", "userLogin"));
        if (string.IsNullOrWhiteSpace(user) || IsExcluded(user))
            return;
        string tier = NormalizeTier(GetFirst("tier", "subTier", "plan", "subPlan"));
        string subType = tier == "Prime" ? "Prime" : "Sub";
        UpdateSubData(user, BuildLabel(subType, tier));
        IncrementTotal(1);
        CPH.LogInfo($"Subscription tracked: {user} ({tier})");
    }

    private void HandleResubscription()
    {
        string user = Normalize(GetFirst("userName", "displayName", "user", "userLogin"));
        if (string.IsNullOrWhiteSpace(user) || IsExcluded(user))
            return;
        string tier = NormalizeTier(GetFirst("tier", "subTier", "plan", "subPlan"));
        int months = GetInt("cumulative_months", "cumulativeMonths", "monthsSubscribed", "streakMonths", "streak_months", "months", "monthCount");
        UpdateSubData(user, BuildLabel("Resub", tier, months));
        IncrementTotal(1);
        CPH.LogInfo($"Resub tracked: {user} ({tier}, {months} months)");
    }

    private void HandleGiftSubscription()
    {
        bool fromGiftBomb = GetBool("fromGiftBomb");

        // Streamer.bot emits individual Gift Subscription events for the
        // recipients of a Gift Bomb unless those events are ignored. The Gift
        // Bomb handler below records all bomb recipients in one pass, so skip
        // these child events to prevent double-counting the same subscriptions.
        if (fromGiftBomb)
        {
            CPH.LogInfo("Gift Subscription came from a Gift Bomb; recipient is tracked by the Gift Bomb event.");
            return;
        }

        string recipient = Normalize(GetFirst("recipientUserName", "recipientName", "recipientDisplayName"));
        string tier = NormalizeTier(GetFirst("tier", "subTier", "plan", "subPlan"));
        if (!string.IsNullOrWhiteSpace(recipient) && !IsExcluded(recipient))
            UpdateSubData(recipient, BuildGiftedLabel(tier));

        // streamfolk.analytics.subs counts actual subscriptions added to the channel.
        // The gifter is already recorded in the recipient's detailed label and
        // must not receive a second synthetic "subscription" entry.
        IncrementTotal(1);
    }

    private void HandleGiftBomb()
    {
        int gifts = GetInt("gifts", "giftCount", "massGiftCount");
        if (gifts <= 0)
            gifts = 1;
        string tier = NormalizeTier(GetFirst("tier", "subTier", "plan", "subPlan"));

        // Do not add a synthetic gifter entry to streamfolk.analytics.subs. Each actual
        // gifted subscription is represented by its recipient below, and the
        // gifter remains visible in that recipient's detailed label.
        for (int i = 0; i < gifts; i++)
        {
            string recipient = Normalize(GetString($"gift.recipientUserName{i}"));
            if (string.IsNullOrWhiteSpace(recipient) || IsExcluded(recipient))
                continue;
            UpdateSubData(recipient, BuildGiftedLabel(tier));
        }

        IncrementTotal(gifts);
        CPH.LogInfo($"Gift bomb processed ({gifts} gifts)");
    }

    private string GetIcon(string type, string tier)
    {
        switch (type)
        {
            case "Sub":
                return "🌟";
            case "Prime":
                return "📦";
            case "Resub":
                return "🔃";
            case "Gift":
            case "Gifted":
                return "🎁";
            case "GiftBomb":
                return "💣";
            default:
                return "✨";
        }
    }

    private string DetermineTier()
    {
        string tier = GetFirst("tier", "subTier")?.Trim()?.ToLowerInvariant();
        switch (tier)
        {
            case "prime":
                return "Prime";
            case "tier 1":
            case "1000":
                return "Tier 1";
            case "tier 2":
            case "2000":
                return "Tier 2";
            case "tier 3":
            case "3000":
                return "Tier 3";
            default:
                CPH.LogWarn($"Unknown tier: '{tier}'");
                return "Tier 1";
        }
    }

    private void UpdateSubData(string user, string label)
    {
        // Load both persisted structures before either one is mutated/saved.
        // If either JSON value is corrupt, the exception reaches Execute() and
        // this event is skipped without overwriting stored data.
        var totals = GetDictInt(SubsKey);
        var details = GetDictList(SubsDetailedKey);

        if (!totals.ContainsKey(user))
            totals[user] = 0;
        totals[user]++;

        if (!details.ContainsKey(user))
            details[user] = new List<string>();
        details[user].Add(label);

        SaveDictInt(SubsKey, totals);
        SaveDictList(SubsDetailedKey, details);
    }

    private string BuildGiftedLabel(string tier)
    {
        string label = BuildLabel("Gifted", tier);
        // Native gift events identify the giver through the Twitch user fields.
        // Keep older explicit gifter arguments supported; never use recipient fields.
        if (GetBool("anonymous") || GetBool("isAnonymous"))
            return label + " • Gifted by Anonymous";
        string gifter = Normalize(GetFirst("gifterUserLogin", "gifterUserName", "gifterName", "gifterDisplayName", "userLogin", "userName", "user"));
        if (gifter == "ananonymousgifter")
            return label + " • Gifted by Anonymous";
        if (string.IsNullOrWhiteSpace(gifter))
            return label + " • Gifter unknown";
        return label + " • Gifted by @" + gifter;
    }

    private string BuildLabel(string type, string tier, int months = 0, int count = 1)
    {
        string icon = GetIcon(type, tier);
        string label;
        if (type == "Resub")
        {
            label = $"{icon} Resub: {tier}";
        }
        else if (type == "Prime")
        {
            label = $"{icon} Prime";
        }
        else if (type == "Gifted")
        {
            label = $"{icon} Gifted: {tier}";
        }
        else if (type == "GiftBomb")
        {
            label = $"{icon} Gift Bomb: {tier}";
        }
        else
        {
            label = $"{icon} {tier}";
        }

        if (months > 0)
            label += $" • {months}m";
        if (count > 1)
            label += $" • x{count}";
        return label;
    }

    private void IncrementTotal(int amount)
    {
        int total = CPH.GetGlobalVar<int>(TotalSubsKey, true);
        total += amount;
        if (total < 0)
            total = amount;
        CPH.SetGlobalVar(TotalSubsKey, total, true);
        CPH.LogInfo($"Total subs updated: {total}");
    }

    private bool IsExcluded(string user)
    {
        return ExcludedUsers.Contains(user, StringComparer.OrdinalIgnoreCase);
    }

    private string Normalize(string value)
    {
        return (value ?? "").Trim().TrimStart('@').ToLowerInvariant();
    }

    private string NormalizeTier(string tier)
    {
        tier = (tier ?? "").Trim().ToLowerInvariant();
        switch (tier)
        {
            case "prime":
            case "primegaming":
                return "Prime";
            case "1000":
            case "tier 1":
            case "tier1":
            case "1":
                return "Tier 1";
            case "2000":
            case "tier 2":
            case "tier2":
            case "2":
                return "Tier 2";
            case "3000":
            case "tier 3":
            case "tier3":
            case "3":
                return "Tier 3";
            default:
                return "Tier 1";
        }
    }

    private string GetTriggerName()
    {
        return GetFirst("triggerName", "eventSource", "eventType") ?? "";
    }

    private string GetFirst(params string[] keys)
    {
        foreach (string key in keys)
        {
            if (args.ContainsKey(key))
            {
                string value = args[key]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private string GetString(string key)
    {
        if (!args.ContainsKey(key))
            return null;
        return args[key]?.ToString();
    }

    private bool GetBool(string key)
    {
        if (!args.ContainsKey(key))
            return false;
        bool.TryParse(args[key]?.ToString(), out bool result);
        return result;
    }

    private int GetInt(params string[] keys)
    {
        foreach (string key in keys)
        {
            if (!args.ContainsKey(key))
                continue;
            if (int.TryParse(args[key]?.ToString(), out int result))
            {
                return result;
            }
        }

        return 0;
    }

    private Dictionary<string, int> GetDictInt(string key)
    {
        string json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
        if (raw == null)
            throw new InvalidOperationException(key + " contains JSON null.");
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
            dict[kv.Key] = kv.Value;
        return dict;
    }

    private void SaveDictInt(string key, Dictionary<string, int> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }

    private Dictionary<string, List<string>> GetDictList(string key)
    {
        string json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
        if (raw == null)
            throw new InvalidOperationException(key + " contains JSON null.");
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
            dict[kv.Key] = kv.Value ?? new List<string>();
        return dict;
    }

    private void SaveDictList(string key, Dictionary<string, List<string>> dict)
    {
        CPH.SetGlobalVar(key, JsonConvert.SerializeObject(dict), true);
    }
}