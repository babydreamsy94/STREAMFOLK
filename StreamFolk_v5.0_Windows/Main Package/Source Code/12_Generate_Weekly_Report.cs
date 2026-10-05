/*
==============================================================================
STREAMFOLK - GENERATE WEEKLY REPORT
==============================================================================

PURPOSE
Builds a Monday-through-Sunday StreamFolk analytics report from persistent
stream history, compares it with the previous week, saves it as a text file,
and stores the latest report and file path in persistent globals.

DEFAULT WEEK
The Monday-through-Sunday week containing the newest archived stream. This
prevents a report run on Monday from selecting the brand-new empty week.

OPTIONAL DATE INPUT
Supply any date in yyyy-MM-dd format. The report automatically uses the Monday
through Sunday containing that date.

Accepted sources:
- an argument named weekStart
- an argument named week
- an argument named date
- rawInput, such as: !weeklyreport 2026-07-06

GLOBALS READ
streamfolk.analytics.streamHistory
streamfolk.analytics.monthlyHistory (legacy migration only)

GLOBALS WRITTEN
streamfolk.analytics.streamHistory (only when legacy history is migrated)
streamfolk.analytics.latestWeeklyReport
streamfolk.analytics.latestWeeklyReportStart
streamfolk.analytics.latestWeeklyReportEnd
streamfolk.analytics.latestWeeklyReportPath

OUTPUT FOLDER
Documents\StreamSummaries\Weekly Reports
==============================================================================
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

public class CPHInline
{
    private const string StreamHistoryKey = "streamfolk.analytics.streamHistory";
    private const string LegacyMonthlyHistoryKey = "streamfolk.analytics.monthlyHistory";
    private const string LatestReportKey = "streamfolk.analytics.latestWeeklyReport";
    private const string LatestReportStartKey = "streamfolk.analytics.latestWeeklyReportStart";
    private const string LatestReportEndKey = "streamfolk.analytics.latestWeeklyReportEnd";
    private const string LatestReportPathKey = "streamfolk.analytics.latestWeeklyReportPath";

    public bool Execute()
    {
        List<StreamRecord> history;
        if (!TryLoadHistory(out history))
            return true;

        DateTime targetWeekStart = ResolveTargetWeekStart(history);
        DateTime targetWeekEnd = targetWeekStart.AddDays(6);
        DateTime nextWeekStart = targetWeekStart.AddDays(7);
        DateTime previousWeekStart = targetWeekStart.AddDays(-7);
        DateTime previousWeekEndExclusive = targetWeekStart;
        DateTime previousBaselineWeekStart = targetWeekStart.AddDays(-14);

        List<StreamRecord> currentRecords = history
            .Where(x => IsWithinRange(x, targetWeekStart, nextWeekStart))
            .OrderBy(x => GetRecordDate(x))
            .ToList();

        if (currentRecords.Count == 0)
        {
            CPH.LogWarn(
                "StreamFolk Weekly Report: No archived streams were found for " +
                targetWeekStart.ToString("yyyy-MM-dd") + " through " +
                targetWeekEnd.ToString("yyyy-MM-dd") + ".");
            return true;
        }

        List<StreamRecord> previousRecords = history
            .Where(x => IsWithinRange(x, previousWeekStart, previousWeekEndExclusive))
            .OrderBy(x => GetRecordDate(x))
            .ToList();

        List<StreamRecord> previousBaselineRecords = history
            .Where(x => IsWithinRange(
                x,
                previousBaselineWeekStart,
                previousWeekStart))
            .OrderBy(x => GetRecordDate(x))
            .ToList();

        WeeklyTotals current = CalculateTotals(
            currentRecords,
            previousRecords);

        WeeklyTotals previous = previousRecords.Count > 0
            ? CalculateTotals(previousRecords, previousBaselineRecords)
            : null;

        string report = BuildReport(
            targetWeekStart,
            targetWeekEnd,
            previousWeekStart,
            previousWeekStart.AddDays(6),
            current,
            previous);

        string path;
        try
        {
            path = SaveReport(targetWeekStart, targetWeekEnd, report);
        }
        catch (Exception ex)
        {
            CPH.LogWarn("StreamFolk Weekly Report: The report could not be saved. No report globals were changed. Error: " + ex.Message);
            return true;
        }

        CPH.SetGlobalVar(LatestReportKey, report, true);
        CPH.SetGlobalVar(LatestReportStartKey, targetWeekStart.ToString("yyyy-MM-dd"), true);
        CPH.SetGlobalVar(LatestReportEndKey, targetWeekEnd.ToString("yyyy-MM-dd"), true);
        CPH.SetGlobalVar(LatestReportPathKey, path, true);

        CPH.LogInfo(
            "StreamFolk Weekly Report: Generated report for " +
            targetWeekStart.ToString("yyyy-MM-dd") + " through " +
            targetWeekEnd.ToString("yyyy-MM-dd") + " at " + path);

        return true;
    }

    private DateTime ResolveTargetWeekStart(List<StreamRecord> history)
    {
        DateTime parsed;
        string requested = GetArgValue("weekStart");

        if (string.IsNullOrWhiteSpace(requested))
            requested = GetArgValue("week");

        if (string.IsNullOrWhiteSpace(requested))
            requested = GetArgValue("date");

        if (TryParseDate(requested, out parsed))
            return GetMonday(parsed);

        string rawInput = GetArgValue("rawInput");
        if (!string.IsNullOrWhiteSpace(rawInput))
        {
            string[] tokens = rawInput.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawToken in tokens)
            {
                string token = rawToken.Trim().Trim(',', '.', ';', ':', '!', '?');
                if (TryParseDate(token, out parsed))
                    return GetMonday(parsed);
            }
        }

        // With no explicit date, report the week containing the newest valid
        // archived stream. This is especially important on Mondays, when the
        // current calendar week has just started and often contains no data.
        DateTime latestRecordDate = (history ?? new List<StreamRecord>())
            .Where(x => x != null)
            .Select(x => GetRecordDate(x))
            .Where(x => x != DateTime.MinValue && x.Date <= DateTime.Now.Date)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        if (latestRecordDate != DateTime.MinValue)
        {
            DateTime latestWeekStart = GetMonday(latestRecordDate);
            CPH.LogInfo(
                "StreamFolk Weekly Report: No date was supplied. Using the " +
                "week containing the newest archived stream (" +
                latestRecordDate.ToString("yyyy-MM-dd") + "), beginning " +
                latestWeekStart.ToString("yyyy-MM-dd") + ".");
            return latestWeekStart;
        }

        // Preserve the original behavior only when history contains no usable
        // dates. TryLoadHistory normally prevents this fallback from occurring.
        return GetMonday(DateTime.Now);
    }

    private bool TryParseDate(string value, out DateTime date)
    {
        DateTime parsed;
        bool success = DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed);

        date = success ? parsed.Date : DateTime.MinValue;
        return success;
    }

    private string GetArgValue(string key)
    {
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return null;

        string value = args[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool TryLoadHistory(out List<StreamRecord> history)
    {
        history = new List<StreamRecord>();
        string currentJson = CPH.GetGlobalVar<string>(StreamHistoryKey, true);

        if (!string.IsNullOrWhiteSpace(currentJson))
            return TryDeserializeHistory(currentJson, StreamHistoryKey, out history);

        string legacyJson = CPH.GetGlobalVar<string>(LegacyMonthlyHistoryKey, true);
        if (string.IsNullOrWhiteSpace(legacyJson))
        {
            CPH.LogWarn("StreamFolk Weekly Report: streamfolk.analytics.streamHistory is empty.");
            return false;
        }

        if (!TryDeserializeHistory(legacyJson, LegacyMonthlyHistoryKey, out history))
            return false;

        string migratedJson = JsonConvert.SerializeObject(history, Formatting.Indented);
        CPH.SetGlobalVar(StreamHistoryKey, migratedJson, true);
        CPH.LogInfo("StreamFolk Weekly Report: Migrated legacy streamfolk.analytics.monthlyHistory into streamfolk.analytics.streamHistory. The original variable was preserved.");
        return true;
    }

    private bool TryDeserializeHistory(
        string json,
        string sourceKey,
        out List<StreamRecord> history)
    {
        history = new List<StreamRecord>();

        try
        {
            List<StreamRecord> loaded = JsonConvert.DeserializeObject<List<StreamRecord>>(json);
            history = loaded ?? new List<StreamRecord>();
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogWarn("StreamFolk Weekly Report: Could not read " + sourceKey + ". Error: " + ex.Message);
            return false;
        }
    }

    private bool IsWithinRange(StreamRecord record, DateTime startInclusive, DateTime endExclusive)
    {
        DateTime recordDate = GetRecordDate(record);
        return recordDate >= startInclusive && recordDate < endExclusive;
    }

    private WeeklyTotals CalculateTotals(
        List<StreamRecord> records,
        List<StreamRecord> baselineRecords)
    {
        StreamRecord mostAttended = records
            .OrderByDescending(x => Math.Max(0, x.TotalAttendees))
            .ThenBy(x => GetRecordDate(x))
            .First();

        StreamRecord leastAttended = records
            .OrderBy(x => Math.Max(0, x.TotalAttendees))
            .ThenBy(x => GetRecordDate(x))
            .First();

        int totalReturning = records.Sum(x => Math.Max(0, x.ReturningAttendees));
        int totalNew = records.Sum(x => Math.Max(0, x.NewAttendees));
        List<string> allUsers = GetUniqueAttendees(records);

        List<string> uniqueNewUsers = records
            .SelectMany(x => x.NewUsers ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        RetentionResult retention = CalculateRetention(
            baselineRecords,
            records);

        return new WeeklyTotals
        {
            StreamCount = records.Count,
            TotalAttendanceOccurrences = records.Sum(x => Math.Max(0, x.TotalAttendees)),
            UniqueAttendees = allUsers.Count,
            UniqueNewAttendees = uniqueNewUsers.Count,
            AverageAttendance = records.Average(x => Math.Max(0, x.TotalAttendees)),
            MostAttended = mostAttended,
            LeastAttended = leastAttended,
            TotalReturningAttendees = totalReturning,
            TotalNewAttendees = totalNew,
            TotalMessages = records.Sum(x => Math.Max(0, x.TotalMessages)),
            AverageMessagesPerStream = records.Average(x => Math.Max(0, x.TotalMessages)),
            OverallRetentionPercent = retention.Percent,
            HasRetentionBaseline = retention.HasBaseline,
            TotalFollows = records.Sum(x => Math.Max(0, x.TotalFollows)),
            TotalSubs = records.Sum(x => Math.Max(0, x.TotalSubs)),
            TotalBits = records.Sum(x => Math.Max(0, x.TotalBits)),
            TotalRaids = records.Sum(x => Math.Max(0, x.TotalRaids)),
            TotalMinutes = records.Sum(x => Math.Max(0, x.DurationMinutes))
        };
    }

    private RetentionResult CalculateRetention(
        List<StreamRecord> baselineRecords,
        List<StreamRecord> currentRecords)
    {
        bool baselineRosterComplete;
        bool currentRosterComplete;

        List<string> baselineAttendees = GetUniqueAttendees(
            baselineRecords,
            out baselineRosterComplete);

        List<string> currentAttendees = GetUniqueAttendees(
            currentRecords,
            out currentRosterComplete);

        if (!baselineRosterComplete ||
            !currentRosterComplete ||
            baselineAttendees.Count == 0)
        {
            return new RetentionResult
            {
                HasBaseline = false,
                Percent = 0
            };
        }

        int retainedAttendees = baselineAttendees
            .Intersect(
                currentAttendees,
                StringComparer.OrdinalIgnoreCase)
            .Count();

        return new RetentionResult
        {
            HasBaseline = true,
            Percent = Math.Round(
                ((double)retainedAttendees / baselineAttendees.Count) * 100.0,
                2)
        };
    }

    private List<string> GetUniqueAttendees(
        List<StreamRecord> records,
        out bool rosterComplete)
    {
        rosterComplete = true;
        List<string> users = new List<string>();

        foreach (StreamRecord record in records ?? new List<StreamRecord>())
        {
            if (record == null)
                continue;

            List<string> recordUsers = (record.ReturningUsers ?? new List<string>())
                .Concat(record.NewUsers ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeUser)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (Math.Max(0, record.TotalAttendees) > 0 &&
                recordUsers.Count != Math.Max(0, record.TotalAttendees))
            {
                rosterComplete = false;
            }

            users.AddRange(recordUsers);
        }

        return users
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetUniqueAttendees(List<StreamRecord> records)
    {
        bool rosterComplete;
        return GetUniqueAttendees(records, out rosterComplete);
    }

    private string NormalizeUser(string user)
    {
        return (user ?? string.Empty)
            .Trim()
            .TrimStart('@')
            .ToLowerInvariant();
    }

    private string BuildReport(
        DateTime targetWeekStart,
        DateTime targetWeekEnd,
        DateTime previousWeekStart,
        DateTime previousWeekEnd,
        WeeklyTotals current,
        WeeklyTotals previous)
    {
        StringBuilder sb = new StringBuilder();
        string currentRange = FormatDateRange(targetWeekStart, targetWeekEnd);
        string previousRange = FormatDateRange(previousWeekStart, previousWeekEnd);

        sb.AppendLine("════════════════════════════════════════════════════════════");
        sb.AppendLine("💙 STREAMFOLK — WEEKLY COMMUNITY REPORT 💙");
        sb.AppendLine("📅 " + currentRange);
        sb.AppendLine("════════════════════════════════════════════════════════════");
        sb.AppendLine("🕒 Report Generated: " + DateTime.Now.ToString("MMMM d, yyyy • h:mm tt"));
        sb.AppendLine();

        AppendSectionHeader(sb, "📊 WEEK AT A GLANCE");
        AppendMetric(sb, "🎥", "Streams Broadcast", current.StreamCount.ToString("N0"));
        AppendMetric(sb, "⏱️", "Total Stream Time", FormatDuration(current.TotalMinutes));
        AppendMetric(sb, "👥", "Unique Attendees", current.UniqueAttendees.ToString("N0"));
        AppendMetric(sb, "🌱", "Unique New Attendees", current.UniqueNewAttendees.ToString("N0"));
        AppendMetric(sb, "🔁", "Total Attendance Occurrences", current.TotalAttendanceOccurrences.ToString("N0"));
        AppendMetric(sb, "📊", "Average Attendance per Stream", current.AverageAttendance.ToString("0.00"));
        sb.AppendLine();

        AppendSectionHeader(sb, "🏆 STREAM HIGHLIGHTS");

        if (current.StreamCount == 1)
        {
            sb.AppendLine("🌟 FEATURED STREAM");
            AppendStreamDetails(sb, current.MostAttended);
        }
        else
        {
            sb.AppendLine("🥇 MOST ATTENDED STREAM");
            AppendStreamDetails(sb, current.MostAttended);
            sb.AppendLine();
            sb.AppendLine("📉 LEAST ATTENDED STREAM");
            AppendStreamDetails(sb, current.LeastAttended);
        }

        sb.AppendLine();
        AppendSectionHeader(sb, "👥 COMMUNITY & RETENTION");
        AppendMetric(sb, "🔁", "Returning Attendance Occurrences", current.TotalReturningAttendees.ToString("N0"));
        AppendMetric(sb, "🌱", "New Attendance Occurrences", current.TotalNewAttendees.ToString("N0"));

        if (current.HasRetentionBaseline)
        {
            AppendMetric(
                sb,
                "💖",
                "Overall Weekly Retention Rate",
                current.OverallRetentionPercent.ToString("0.00") + "%");
            sb.AppendLine("   ↳ Previous-week attendees who returned ÷ previous week's unique attendees");
        }
        else
        {
            AppendMetric(sb, "💖", "Overall Weekly Retention Rate", "N/A");
            sb.AppendLine("   ↳ A complete previous-week attendee roster is required");
        }

        sb.AppendLine();

        AppendSectionHeader(sb, "💬 CHAT ENGAGEMENT");
        AppendMetric(sb, "💬", "Total Chat Messages", current.TotalMessages.ToString("N0"));
        AppendMetric(sb, "📈", "Average Messages per Stream", current.AverageMessagesPerStream.ToString("0.00"));
        sb.AppendLine();

        AppendSectionHeader(sb, "💜 COMMUNITY SUPPORT");
        AppendMetric(sb, "💜", "New Follows", current.TotalFollows.ToString("N0"));
        AppendMetric(sb, "🌟", "Subscription Units", current.TotalSubs.ToString("N0"));
        AppendMetric(sb, "💎", "Bits Cheered", current.TotalBits.ToString("N0"));
        AppendMetric(sb, "🚀", "Raids Received", current.TotalRaids.ToString("N0"));
        sb.AppendLine();

        AppendSectionHeader(sb, "📈 WEEK-TO-WEEK COMPARISON");
        sb.AppendLine("🗓️ Previous Week: " + previousRange);
        sb.AppendLine();

        if (previous == null)
        {
            sb.AppendLine("   ℹ️ No archived stream data exists for the previous week.");
            sb.AppendLine("   This section will populate once both weeks contain archived streams.");
        }
        else
        {
            AppendComparison(sb, "🎥 Streams Broadcast", current.StreamCount, previous.StreamCount, "0");
            AppendComparison(sb, "👥 Unique Attendees", current.UniqueAttendees, previous.UniqueAttendees, "0");
            AppendComparison(sb, "🔁 Attendance Occurrences", current.TotalAttendanceOccurrences, previous.TotalAttendanceOccurrences, "0");
            AppendComparison(sb, "📊 Average Attendance", current.AverageAttendance, previous.AverageAttendance, "0.00");
            AppendComparison(sb, "🔁 Returning Attendance", current.TotalReturningAttendees, previous.TotalReturningAttendees, "0");
            AppendComparison(sb, "🌱 New Attendance", current.TotalNewAttendees, previous.TotalNewAttendees, "0");
            AppendComparison(sb, "💬 Total Messages", current.TotalMessages, previous.TotalMessages, "0");
            AppendComparison(sb, "📈 Average Messages", current.AverageMessagesPerStream, previous.AverageMessagesPerStream, "0.00");
            AppendRetentionComparison(sb, current, previous);
            AppendComparison(sb, "💜 Follows", current.TotalFollows, previous.TotalFollows, "0");
            AppendComparison(sb, "🌟 Subscription Units", current.TotalSubs, previous.TotalSubs, "0");
            AppendComparison(sb, "💎 Bits", current.TotalBits, previous.TotalBits, "0");
            AppendComparison(sb, "🚀 Raids", current.TotalRaids, previous.TotalRaids, "0");
        }

        sb.AppendLine();
        AppendSectionHeader(sb, "📝 REPORT NOTES");
        sb.AppendLine("• Weeks run Monday through Sunday using each stream's local start date.");
        sb.AppendLine("• Returning and new attendance totals are occurrences summed across streams.");
        sb.AppendLine("• A returning viewer may count once for every stream they attended.");
        sb.AppendLine("• Unique attendees are deduplicated across the entire week.");
        sb.AppendLine("• Retention is the share of the previous week's unique audience that returned this week.");
        sb.AppendLine("• Each stream uses its most recently tracked category and title.");
        sb.AppendLine();
        sb.AppendLine("════════════════════════════════════════════════════════════");
        sb.AppendLine("💙 StreamFolk: Built by Streamers. Powered by Community. 💙");
        sb.AppendLine("════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    private void AppendSectionHeader(StringBuilder sb, string title)
    {
        sb.AppendLine(title);
        sb.AppendLine("────────────────────────────────────────────────────────────");
    }

    private void AppendMetric(StringBuilder sb, string icon, string label, string value)
    {
        sb.AppendLine(icon + " " + label + ": " + value);
    }

    private void AppendStreamDetails(StringBuilder sb, StreamRecord record)
    {
        if (record == null)
        {
            sb.AppendLine("   No stream data available.");
            return;
        }

        string category = string.IsNullOrWhiteSpace(record.Category)
            ? "Unknown Category"
            : record.Category.Trim();

        string title = string.IsNullOrWhiteSpace(record.StreamTitle)
            ? "No title recorded"
            : record.StreamTitle.Trim();

        sb.AppendLine("   📅 Date: " + GetRecordDate(record).ToString("dddd, MMMM d, yyyy"));
        sb.AppendLine("   🎮 Category: " + category);
        sb.AppendLine("   👥 Attendance: " + Math.Max(0, record.TotalAttendees).ToString("N0"));
        sb.AppendLine("   💬 Chat Messages: " + Math.Max(0, record.TotalMessages).ToString("N0"));
        sb.AppendLine("   📝 Title: " + title);
    }

    private string FormatDateRange(DateTime start, DateTime end)
    {
        if (start.Year == end.Year && start.Month == end.Month)
            return start.ToString("MMMM d") + "–" + end.ToString("d, yyyy");

        if (start.Year == end.Year)
            return start.ToString("MMMM d") + " – " + end.ToString("MMMM d, yyyy");

        return start.ToString("MMMM d, yyyy") + " – " + end.ToString("MMMM d, yyyy");
    }

    private string FormatDuration(double totalMinutes)
    {
        int roundedMinutes = Math.Max(0, (int)Math.Round(totalMinutes));
        int hours = roundedMinutes / 60;
        int minutes = roundedMinutes % 60;
        return hours + "h " + minutes + "m";
    }

    private void AppendComparison(
        StringBuilder sb,
        string label,
        double current,
        double previous,
        string numberFormat)
    {
        double difference = current - previous;
        string trendIcon = GetTrendIcon(difference);
        string changeText;

        if (Math.Abs(previous) < 0.000001)
        {
            changeText = Math.Abs(current) < 0.000001
                ? "No change"
                : difference.ToString("+" + numberFormat + ";-" + numberFormat + ";" + numberFormat) +
                  " (previous week: 0)";
        }
        else
        {
            double percentChange = (difference / previous) * 100.0;
            changeText = difference.ToString("+" + numberFormat + ";-" + numberFormat + ";" + numberFormat) +
                " (" + percentChange.ToString("+0.00;-0.00;0.00") + "%)";
        }

        sb.AppendLine(
            trendIcon + " " + label + ": " +
            current.ToString(numberFormat) +
            " vs " + previous.ToString(numberFormat) +
            " | " + changeText);
    }

    private void AppendPercentagePointComparison(
        StringBuilder sb,
        string label,
        double current,
        double previous)
    {
        double difference = current - previous;
        string trendIcon = GetTrendIcon(difference);

        sb.AppendLine(
            trendIcon + " " + label + ": " +
            current.ToString("0.00") + "% vs " +
            previous.ToString("0.00") + "%" +
            " | " + difference.ToString("+0.00;-0.00;0.00") +
            " percentage points");
    }

    private void AppendRetentionComparison(
        StringBuilder sb,
        WeeklyTotals current,
        WeeklyTotals previous)
    {
        if (current != null &&
            previous != null &&
            current.HasRetentionBaseline &&
            previous.HasRetentionBaseline)
        {
            AppendPercentagePointComparison(
                sb,
                "💖 Retention Rate",
                current.OverallRetentionPercent,
                previous.OverallRetentionPercent);
            return;
        }

        string currentText = current != null && current.HasRetentionBaseline
            ? current.OverallRetentionPercent.ToString("0.00") + "%"
            : "N/A";

        string previousText = previous != null && previous.HasRetentionBaseline
            ? previous.OverallRetentionPercent.ToString("0.00") + "%"
            : "N/A";

        sb.AppendLine(
            "ℹ️ 💖 Retention Rate: " +
            currentText +
            " vs " +
            previousText +
            " | A complete earlier-week baseline is required");
    }

    private string GetTrendIcon(double difference)
    {
        if (difference > 0.000001)
            return "⬆️";

        if (difference < -0.000001)
            return "⬇️";

        return "➖";
    }

    private string SaveReport(DateTime weekStart, DateTime weekEnd, string report)
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string folder = Path.Combine(documents, "StreamSummaries", "Weekly Reports");
        Directory.CreateDirectory(folder);

        string filename =
            "Stream_Suite_Weekly_Report_" +
            weekStart.ToString("yyyy-MM-dd") +
            "_to_" +
            weekEnd.ToString("yyyy-MM-dd") +
            ".txt";

        string path = Path.Combine(folder, filename);
        File.WriteAllText(path, report, Encoding.UTF8);
        return path;
    }

    private DateTime GetMonday(DateTime date)
    {
        int difference = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-difference);
    }

    private DateTime GetRecordDate(StreamRecord record)
    {
        DateTime parsed;

        if (record != null && DateTime.TryParse(record.StreamStartLocal, out parsed))
            return parsed;

        if (record != null && DateTime.TryParse(record.StreamDate, out parsed))
            return parsed;

        if (record != null && DateTime.TryParse(record.GeneratedAtLocal, out parsed))
            return parsed;

        return DateTime.MinValue;
    }

    private class WeeklyTotals
    {
        public int StreamCount { get; set; }
        public int TotalAttendanceOccurrences { get; set; }
        public int UniqueAttendees { get; set; }
        public int UniqueNewAttendees { get; set; }
        public double AverageAttendance { get; set; }
        public StreamRecord MostAttended { get; set; }
        public StreamRecord LeastAttended { get; set; }
        public int TotalReturningAttendees { get; set; }
        public int TotalNewAttendees { get; set; }
        public int TotalMessages { get; set; }
        public double AverageMessagesPerStream { get; set; }
        public double OverallRetentionPercent { get; set; }
        public bool HasRetentionBaseline { get; set; }
        public int TotalFollows { get; set; }
        public int TotalSubs { get; set; }
        public int TotalBits { get; set; }
        public int TotalRaids { get; set; }
        public double TotalMinutes { get; set; }
    }

    private class StreamRecord
    {
        public string RecordId { get; set; }
        public string MonthKey { get; set; }
        public string WeekStart { get; set; }
        public string StreamDate { get; set; }
        public string StreamStartLocal { get; set; }
        public string GeneratedAtLocal { get; set; }
        public string Category { get; set; }
        public string StreamTitle { get; set; }
        public double DurationMinutes { get; set; }
        public int TotalAttendees { get; set; }
        public int ReturningAttendees { get; set; }
        public int NewAttendees { get; set; }
        public List<string> ReturningUsers { get; set; }
        public List<string> NewUsers { get; set; }
        public int TotalMessages { get; set; }
        public int TotalFollows { get; set; }
        public int TotalSubs { get; set; }
        public int TotalBits { get; set; }
        public int TotalRaids { get; set; }
        public double StreamRetentionRate { get; set; }
    }

    private class RetentionResult
    {
        public bool HasBaseline { get; set; }
        public double Percent { get; set; }
    }
}
