// StreamFolk: eight live/final Stream Deck statistics.
// Configure your own Status Indicator Button IDs, then enable the action and timer.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string SeenUsersKey = "streamfolk.analytics.seenUsers";
    private const string AttendanceHistoryKey = "streamfolk.analytics.attendanceHistory";
    private const string ChatMessagesKey = "streamfolk.analytics.chatMessagesByUser";
    private const string SessionStartKey = "streamfolk.analytics.sessionStartLocal";
    private const string TotalBitsKey = "streamfolk.analytics.totalBits";
    private const string BitsByUserKey = "streamfolk.analytics.bits";
    private const string FinalSummaryKey = "streamfolk.analytics.finalSummaryJson";
    private const string StreamDeckStatsKey = "streamfolk.analytics.streamDeckStatsJson";
    private const string TestModeKey = "streamfolk.analytics.streamDeckTestMode";
    private const string TestStartedKey = "streamfolk.analytics.streamDeckTestStartedLocal";

    // Official Streamer.bot Status Indicator Button IDs.
    private const string AttendeesButtonId = "";
    private const string ReturningButtonId = "";
    private const string NewAttendeesButtonId = "";
    private const string RetentionButtonId = "";
    private const string ChatMessagesButtonId = "";
    private const string MessagesPerMinuteButtonId = "";
    private const string StreamDurationButtonId = "";
    private const string BitsThisStreamButtonId = "";

    private static readonly string[] ExcludedUsers =
    {
        "botname",
        "pokemoncommunitygame",
        "kofistreambot",
    };

    public bool Execute()
    {
        try
        {
            DateTime sessionStart = ParseDateTime(
                CPH.GetGlobalVar<string>(SessionStartKey, true));

            FinalSummary finalSummary = GetFinalSummary();
            DateTime finalGenerated = finalSummary == null
                ? DateTime.MinValue
                : ParseDateTime(finalSummary.GeneratedAtLocal);

            bool hasCurrentSession = sessionStart != DateTime.MinValue &&
                                     sessionStart > finalGenerated;

            if (IsTestModeEnabled())
            {
                if (hasCurrentSession)
                {
                    DisableTestMode(
                        "Stream Deck Stats: Safe test mode ended automatically " +
                        "because a real live session was detected.");
                }
                else
                {
                    PublishStats(BuildTestStats());
                    return true;
                }
            }

            StreamDeckStats stats = hasCurrentSession
                ? BuildLiveStats(sessionStart)
                : BuildLastStreamStats(finalSummary);

            PublishStats(stats);

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("Stream Deck Stats: " + ex.Message);
            return true;
        }
    }

    private void PublishStats(StreamDeckStats stats)
    {
        // Push directly through the official Streamer.bot Stream Deck plugin.
        SetConfiguredDeckValue(
            AttendeesButtonId,
            Math.Max(0, stats.TotalAttendees).ToString(CultureInfo.InvariantCulture));

        SetConfiguredDeckValue(
            ReturningButtonId,
            Math.Max(0, stats.ReturningAttendees).ToString(CultureInfo.InvariantCulture));

        SetConfiguredDeckValue(
            NewAttendeesButtonId,
            Math.Max(0, stats.NewAttendees).ToString(CultureInfo.InvariantCulture));

        SetConfiguredDeckValue(
            RetentionButtonId,
            Math.Max(0.0, stats.RetentionPercent).ToString("0.0", CultureInfo.InvariantCulture));

        SetConfiguredDeckValue(
            ChatMessagesButtonId,
            Math.Max(0, stats.ChatMessages).ToString(CultureInfo.InvariantCulture));

        SetConfiguredDeckValue(
            MessagesPerMinuteButtonId,
            Math.Max(0.0, stats.MessagesPerMinute).ToString("0.0", CultureInfo.InvariantCulture));

        SetConfiguredDeckValue(
            StreamDurationButtonId,
            string.IsNullOrWhiteSpace(stats.StreamDuration)
                ? "0m"
                : stats.StreamDuration);

        SetConfiguredDeckValue(
            BitsThisStreamButtonId,
            Math.Max(0, stats.BitsThisStream).ToString(CultureInfo.InvariantCulture));

        // Keep the compact snapshot for troubleshooting and compatibility.
        CPH.SetGlobalVar(
            StreamDeckStatsKey,
            JsonConvert.SerializeObject(stats, Formatting.None),
            true);
    }

    private void SetConfiguredDeckValue(string buttonId, string value)
    {
        if (!string.IsNullOrWhiteSpace(buttonId))
            CPH.StreamDeckSetValue(buttonId, value);
    }

    private StreamDeckStats BuildTestStats()
    {
        DateTime started = ParseDateTime(
            CPH.GetGlobalVar<string>(TestStartedKey, true));

        if (started == DateTime.MinValue)
        {
            started = DateTime.Now;
            CPH.SetGlobalVar(TestStartedKey, started.ToString("o"), true);
        }

        int tick = (int)Math.Floor(
            Math.Max(0.0, (DateTime.Now - started).TotalSeconds) / 3.0);

        int totalAttendees = 12 + (tick % 13);
        int newAttendees = 2 + (tick % 4);
        int returningAttendees = Math.Max(0, totalAttendees - newAttendees);

        return new StreamDeckStats
        {
            GeneratedAtLocal = DateTime.Now.ToString("o"),
            DisplayMode = "LIVE",
            TotalAttendees = totalAttendees,
            ReturningAttendees = returningAttendees,
            NewAttendees = newAttendees,
            RetentionPercent = Math.Round(62.5 + ((tick % 12) * 2.5), 1),
            ChatMessages = 120 + ((tick % 500) * 7),
            MessagesPerMinute = Math.Round(1.5 + ((tick % 15) * 0.2), 1),
            StreamDuration = FormatDuration(75.0 + tick),
            BitsThisStream = 100 + ((tick % 40) * 25)
        };
    }

    private bool IsTestModeEnabled()
    {
        string value = CPH.GetGlobalVar<string>(TestModeKey, true);
        bool enabled;
        if (!bool.TryParse(value, out enabled) || !enabled)
            return false;

        DateTime started = ParseDateTime(
            CPH.GetGlobalVar<string>(TestStartedKey, true));

        if (started != DateTime.MinValue &&
            (DateTime.Now - started).TotalMinutes >= 10.0)
        {
            DisableTestMode(
                "Stream Deck Stats: Safe test mode ended automatically " +
                "after ten minutes.");
            return false;
        }

        return true;
    }

    private void DisableTestMode(string logMessage)
    {
        CPH.SetGlobalVar(TestModeKey, "False", true);
        CPH.SetGlobalVar(TestStartedKey, "", true);
        CPH.LogInfo(logMessage);
    }

    private StreamDeckStats BuildLiveStats(DateTime sessionStart)
    {
        Dictionary<string, int> seen = GetIntDictionary(SeenUsersKey);
        Dictionary<string, List<string>> history = GetHistory();
        DateTime today = DateTime.Now.Date;

        List<string> attendees = seen.Keys
            .Select(Normalize)
            .Where(user => !string.IsNullOrWhiteSpace(user) && !IsExcluded(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int returning = 0;
        int newly = 0;

        foreach (string user in attendees)
        {
            List<string> dates;
            if (!TryGetHistory(history, user, out dates))
            {
                newly++;
                continue;
            }

            bool hasPastAttendance = dates.Any(dateText =>
            {
                DateTime date;
                return DateTime.TryParse(dateText, out date) && date.Date < today;
            });

            if (hasPastAttendance)
                returning++;
            else
                newly++;
        }

        List<string> previousAttendees = GetPreviousStreamAttendees(
            history,
            today);

        int retainedAttendees = previousAttendees
            .Intersect(attendees, StringComparer.OrdinalIgnoreCase)
            .Count();

        double retentionPercent = previousAttendees.Count > 0
            ? Math.Round(
                ((double)retainedAttendees / previousAttendees.Count) * 100.0,
                1)
            : 0.0;

        int chatMessages = GetIntDictionary(ChatMessagesKey)
            .Where(pair => !IsExcluded(pair.Key))
            .Sum(pair => Math.Max(0, pair.Value));

        double elapsedMinutes = Math.Max(0.0, (DateTime.Now - sessionStart).TotalMinutes);
        double messagesPerMinute = elapsedMinutes > 0.0
            ? Math.Round(chatMessages / elapsedMinutes, 1)
            : 0.0;

        return new StreamDeckStats
        {
            GeneratedAtLocal = DateTime.Now.ToString("o"),
            DisplayMode = "LIVE",
            TotalAttendees = attendees.Count,
            ReturningAttendees = returning,
            NewAttendees = newly,
            RetentionPercent = retentionPercent,
            ChatMessages = chatMessages,
            MessagesPerMinute = messagesPerMinute,
            StreamDuration = FormatDuration(elapsedMinutes),
            BitsThisStream = GetCurrentBits()
        };
    }

    private StreamDeckStats BuildLastStreamStats(FinalSummary finalSummary)
    {
        if (finalSummary == null)
        {
            return new StreamDeckStats
            {
                GeneratedAtLocal = DateTime.Now.ToString("o"),
                DisplayMode = "READY",
                StreamDuration = "0m"
            };
        }

        AttendanceSummary attendance = finalSummary.Attendance ?? new AttendanceSummary();

        return new StreamDeckStats
        {
            GeneratedAtLocal = DateTime.Now.ToString("o"),
            DisplayMode = "LAST STREAM",
            TotalAttendees = Math.Max(0, attendance.TotalAttendees),
            ReturningAttendees = attendance.Returning == null ? 0 : attendance.Returning.Count,
            NewAttendees = attendance.Newly == null ? 0 : attendance.Newly.Count,
            RetentionPercent = Math.Round(Math.Max(0.0, finalSummary.RetentionRate) * 100.0, 1),
            ChatMessages = Math.Max(0, finalSummary.TotalMessages),
            MessagesPerMinute = finalSummary.DurationMinutes > 0.0
                ? Math.Round(
                    Math.Max(0, finalSummary.TotalMessages) /
                    Math.Max(0.01, finalSummary.DurationMinutes),
                    1)
                : 0.0,
            StreamDuration = FormatDuration(finalSummary.DurationMinutes),
            BitsThisStream = Math.Max(0, finalSummary.TotalBits)
        };
    }

    private int GetCurrentBits()
    {
        int totalBits = 0;

        try
        {
            totalBits = Math.Max(0, CPH.GetGlobalVar<int>(TotalBitsKey, true));
        }
        catch
        {
            string rawTotal = CPH.GetGlobalVar<string>(TotalBitsKey, true);
            int parsed;

            if (int.TryParse(
                rawTotal,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                totalBits = Math.Max(0, parsed);
            }
        }

        if (totalBits > 0)
            return totalBits;

        return GetIntDictionary(BitsByUserKey)
            .Sum(pair => Math.Max(0, pair.Value));
    }

    private string FormatDuration(double totalMinutes)
    {
        if (double.IsNaN(totalMinutes) ||
            double.IsInfinity(totalMinutes) ||
            totalMinutes <= 0.0)
        {
            return "0m";
        }

        int wholeMinutes = Math.Max(0, (int)Math.Floor(totalMinutes));
        int hours = wholeMinutes / 60;
        int minutes = wholeMinutes % 60;

        return hours > 0
            ? hours.ToString(CultureInfo.InvariantCulture) + "h " +
              minutes.ToString(CultureInfo.InvariantCulture) + "m"
            : minutes.ToString(CultureInfo.InvariantCulture) + "m";
    }

    private List<string> GetPreviousStreamAttendees(
        Dictionary<string, List<string>> history,
        DateTime today)
    {
        List<DateTime> pastDates = history
            .SelectMany(pair => pair.Value ?? new List<string>())
            .Select(dateText =>
            {
                DateTime parsed;
                return DateTime.TryParse(dateText, out parsed)
                    ? (DateTime?)parsed.Date
                    : null;
            })
            .Where(date => date.HasValue && date.Value < today)
            .Select(date => date.Value)
            .Distinct()
            .OrderByDescending(date => date)
            .ToList();

        if (pastDates.Count == 0)
            return new List<string>();

        DateTime previousDate = pastDates[0];

        return history
            .Where(pair => !IsExcluded(pair.Key))
            .Where(pair => (pair.Value ?? new List<string>()).Any(dateText =>
            {
                DateTime parsed;
                return DateTime.TryParse(dateText, out parsed) &&
                       parsed.Date == previousDate;
            }))
            .Select(pair => Normalize(pair.Key))
            .Where(user => !string.IsNullOrWhiteSpace(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryGetHistory(
        Dictionary<string, List<string>> history,
        string user,
        out List<string> dates)
    {
        foreach (KeyValuePair<string, List<string>> pair in history)
        {
            if (string.Equals(
                Normalize(pair.Key),
                Normalize(user),
                StringComparison.OrdinalIgnoreCase))
            {
                dates = pair.Value ?? new List<string>();
                return true;
            }
        }

        dates = new List<string>();
        return false;
    }

    private FinalSummary GetFinalSummary()
    {
        string json = CPH.GetGlobalVar<string>(FinalSummaryKey, true);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<FinalSummary>(json);
        }
        catch (Exception ex)
        {
            CPH.LogWarn("Stream Deck Stats: final summary could not be read: " + ex.Message);
            return null;
        }
    }

    private Dictionary<string, int> GetIntDictionary(string key)
    {
        string json = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            Dictionary<string, int> raw =
                JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ??
                new Dictionary<string, int>();

            Dictionary<string, int> result =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, int> pair in raw)
                result[Normalize(pair.Key)] = pair.Value;

            return result;
        }
        catch (Exception ex)
        {
            CPH.LogWarn("Stream Deck Stats: " + key + " could not be read: " + ex.Message);
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, List<string>> GetHistory()
    {
        string json = CPH.GetGlobalVar<string>(AttendanceHistoryKey, true);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            Dictionary<string, List<string>> raw =
                JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json) ??
                new Dictionary<string, List<string>>();

            Dictionary<string, List<string>> result =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<string>> pair in raw)
                result[Normalize(pair.Key)] = pair.Value ?? new List<string>();

            return result;
        }
        catch (Exception ex)
        {
            CPH.LogWarn("Stream Deck Stats: streamfolk.analytics.attendanceHistory could not be read: " + ex.Message);
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private DateTime ParseDateTime(string value)
    {
        DateTime parsed;
        return DateTime.TryParse(value, out parsed) ? parsed : DateTime.MinValue;
    }

    private bool IsExcluded(string user)
    {
        string normalized = Normalize(user);

        foreach (string excludedUser in ExcludedUsers)
        {
            if (string.Equals(
                normalized,
                Normalize(excludedUser),
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string Normalize(string user)
    {
        return (user ?? string.Empty)
            .Trim()
            .TrimStart('@')
            .ToLowerInvariant();
    }

    private class StreamDeckStats
    {
        [JsonProperty("generatedAtLocal")]
        public string GeneratedAtLocal { get; set; }

        [JsonProperty("displayMode")]
        public string DisplayMode { get; set; }

        [JsonProperty("totalAttendees")]
        public int TotalAttendees { get; set; }

        [JsonProperty("returningAttendees")]
        public int ReturningAttendees { get; set; }

        [JsonProperty("newAttendees")]
        public int NewAttendees { get; set; }

        [JsonProperty("retentionPercent")]
        public double RetentionPercent { get; set; }

        [JsonProperty("chatMessages")]
        public int ChatMessages { get; set; }

        [JsonProperty("messagesPerMinute")]
        public double MessagesPerMinute { get; set; }

        [JsonProperty("streamDuration")]
        public string StreamDuration { get; set; }

        [JsonProperty("bitsThisStream")]
        public int BitsThisStream { get; set; }
    }

    private class AttendanceSummary
    {
        public int TotalAttendees { get; set; }
        public List<string> Returning { get; set; }
        public List<string> Newly { get; set; }
    }

    private class FinalSummary
    {
        public string GeneratedAtLocal { get; set; }
        public double DurationMinutes { get; set; }
        public int TotalMessages { get; set; }
        public int TotalBits { get; set; }
        public AttendanceSummary Attendance { get; set; }
        public double RetentionRate { get; set; }
    }
}
