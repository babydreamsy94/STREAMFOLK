/*
==============================================================================
STREAMFOLK - GENERATE MONTHLY REPORT (TRUE RETENTION)
==============================================================================

PURPOSE
Builds the existing StreamFolk monthly performance report from
streamfolk.analytics.streamHistory while calculating retention from exact attendee-roster
overlap.

TRUE MONTHLY RETENTION
Unique attendees from the previous calendar month who attended again during
the selected month / all unique attendees from the previous calendar month.

DEFAULT MONTH
The current local calendar month.

OPTIONAL INPUT
- current
- previous
- yyyy-MM
- any yyyy-MM-dd date inside the desired month

Supply the value through month, date, reportMonth, or rawInput.

GLOBALS READ
streamfolk.analytics.streamHistory
streamfolk.analytics.monthlyHistory (legacy migration only)

GLOBALS WRITTEN
streamfolk.analytics.streamHistory (legacy migration only)
streamfolk.analytics.latestMonthlyReport
streamfolk.analytics.latestMonthlyReportMonth
streamfolk.analytics.latestMonthlyReportStart
streamfolk.analytics.latestMonthlyReportEnd
streamfolk.analytics.latestMonthlyReportPath

OUTPUT FOLDER
Documents\StreamSummaries\Monthly Reports
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
    private const string LegacyHistoryKey = "streamfolk.analytics.monthlyHistory";
    private const string LatestReportKey = "streamfolk.analytics.latestMonthlyReport";
    private const string LatestReportMonthKey = "streamfolk.analytics.latestMonthlyReportMonth";
    private const string LatestReportStartKey = "streamfolk.analytics.latestMonthlyReportStart";
    private const string LatestReportEndKey = "streamfolk.analytics.latestMonthlyReportEnd";
    private const string LatestReportPathKey = "streamfolk.analytics.latestMonthlyReportPath";

    public bool Execute()
    {
        List<StreamRecord> history;
        if (!TryLoadHistory(out history))
            return true;

        DateTime targetMonth = ResolveTargetMonth();
        DateTime targetStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
        DateTime targetEnd = targetStart.AddMonths(1).AddDays(-1);
        DateTime previousStart = targetStart.AddMonths(-1);
        DateTime previousEnd = targetStart.AddDays(-1);
        DateTime earlierStart = previousStart.AddMonths(-1);
        DateTime earlierEnd = previousStart.AddDays(-1);

        List<StreamRecord> currentRecords = GetRecordsInPeriod(
            history,
            targetStart,
            targetEnd);

        if (currentRecords.Count == 0)
        {
            CPH.LogWarn(
                "StreamFolk Monthly Report: No archived streams were found for " +
                targetStart.ToString("MMMM yyyy") + ".");
            return true;
        }

        List<StreamRecord> previousRecords = GetRecordsInPeriod(
            history,
            previousStart,
            previousEnd);

        List<StreamRecord> earlierRecords = GetRecordsInPeriod(
            history,
            earlierStart,
            earlierEnd);

        MonthlyTotals current = CalculateTotals(
            currentRecords,
            previousRecords);

        MonthlyTotals previous = previousRecords.Count > 0
            ? CalculateTotals(previousRecords, earlierRecords)
            : null;

        string report = BuildReport(
            targetStart,
            targetEnd,
            previousStart,
            previousEnd,
            current,
            previous,
            currentRecords);

        string path;
        try
        {
            path = SaveReport(targetStart, report);
        }
        catch (Exception ex)
        {
            CPH.LogWarn(
                "StreamFolk Monthly Report: The report could not be saved. " +
                "No report globals were changed. Error: " + ex.Message);
            return true;
        }

        CPH.SetGlobalVar(LatestReportKey, report, true);
        CPH.SetGlobalVar(
            LatestReportMonthKey,
            targetStart.ToString("yyyy-MM"),
            true);
        CPH.SetGlobalVar(
            LatestReportStartKey,
            targetStart.ToString("yyyy-MM-dd"),
            true);
        CPH.SetGlobalVar(
            LatestReportEndKey,
            targetEnd.ToString("yyyy-MM-dd"),
            true);
        CPH.SetGlobalVar(LatestReportPathKey, path, true);

        CPH.LogInfo(
            "StreamFolk Monthly Report: Generated " +
            targetStart.ToString("MMMM yyyy") +
            " report at " + path);

        return true;
    }

    private DateTime ResolveTargetMonth()
    {
        string[] argumentNames =
        {
            "month",
            "date",
            "reportMonth"
        };

        foreach (string argumentName in argumentNames)
        {
            DateTime parsed;
            if (TryParseMonthToken(GetArgValue(argumentName), out parsed))
                return parsed;
        }

        string rawInput = GetArgValue("rawInput");
        if (!string.IsNullOrWhiteSpace(rawInput))
        {
            string[] tokens = rawInput.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawToken in tokens)
            {
                string token = rawToken
                    .Trim()
                    .Trim(',', '.', ';', ':', '!', '?');

                DateTime parsed;
                if (TryParseMonthToken(token, out parsed))
                    return parsed;
            }
        }

        DateTime now = DateTime.Now;
        return new DateTime(now.Year, now.Month, 1);
    }

    private bool TryParseMonthToken(string value, out DateTime month)
    {
        month = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string token = value.Trim();
        DateTime now = DateTime.Now;

        if (string.Equals(token, "current", StringComparison.OrdinalIgnoreCase))
        {
            month = new DateTime(now.Year, now.Month, 1);
            return true;
        }

        if (string.Equals(token, "previous", StringComparison.OrdinalIgnoreCase))
        {
            month = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            return true;
        }

        DateTime parsed;
        string[] formats =
        {
            "yyyy-MM",
            "yyyy-MM-dd"
        };

        if (!DateTime.TryParseExact(
            token,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed))
        {
            return false;
        }

        month = new DateTime(parsed.Year, parsed.Month, 1);
        return true;
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
            return TryDeserializeHistory(
                currentJson,
                StreamHistoryKey,
                out history);

        string legacyJson = CPH.GetGlobalVar<string>(LegacyHistoryKey, true);
        if (string.IsNullOrWhiteSpace(legacyJson))
        {
            CPH.LogWarn(
                "StreamFolk Monthly Report: streamfolk.analytics.streamHistory is empty.");
            return false;
        }

        if (!TryDeserializeHistory(
            legacyJson,
            LegacyHistoryKey,
            out history))
        {
            return false;
        }

        CPH.SetGlobalVar(
            StreamHistoryKey,
            JsonConvert.SerializeObject(history, Formatting.Indented),
            true);

        CPH.LogInfo(
            "StreamFolk Monthly Report: Migrated legacy " +
            "streamfolk.analytics.monthlyHistory into streamfolk.analytics.streamHistory. " +
            "The original variable was preserved.");

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
            List<StreamRecord> loaded =
                JsonConvert.DeserializeObject<List<StreamRecord>>(json);

            history = (loaded ?? new List<StreamRecord>())
                .Where(x => x != null)
                .OrderBy(GetRecordDate)
                .ToList();

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogWarn(
                "StreamFolk Monthly Report: " + sourceKey +
                " contains invalid JSON. No history or report globals were " +
                "changed. Error: " + ex.Message);
            return false;
        }
    }

    private List<StreamRecord> GetRecordsInPeriod(
        List<StreamRecord> history,
        DateTime start,
        DateTime end)
    {
        DateTime inclusiveStart = start.Date;
        DateTime exclusiveEnd = end.Date.AddDays(1);

        return (history ?? new List<StreamRecord>())
            .Where(x =>
            {
                DateTime date = GetRecordDate(x);
                return date >= inclusiveStart && date < exclusiveEnd;
            })
            .OrderBy(GetRecordDate)
            .ToList();
    }

    private MonthlyTotals CalculateTotals(
        List<StreamRecord> records,
        List<StreamRecord> baselineRecords)
    {
        StreamRecord mostAttended = records
            .OrderByDescending(x => Math.Max(0, x.TotalAttendees))
            .ThenBy(GetRecordDate)
            .First();

        StreamRecord leastAttended = records
            .OrderBy(x => Math.Max(0, x.TotalAttendees))
            .ThenBy(GetRecordDate)
            .First();

        List<string> uniqueAttendees = GetUniqueAttendees(records);
        List<string> uniqueNewAttendees = records
            .SelectMany(x => x.NewUsers ?? new List<string>())
            .Select(NormalizeUser)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        RetentionResult retention = CalculateRetention(
            baselineRecords,
            records);

        return new MonthlyTotals
        {
            StreamCount = records.Count,
            TotalMinutes = records.Sum(x => Math.Max(0, x.DurationMinutes)),
            AverageMinutes = records.Average(
                x => Math.Max(0, x.DurationMinutes)),
            TotalAttendanceOccurrences = records.Sum(
                x => Math.Max(0, x.TotalAttendees)),
            AverageAttendance = records.Average(
                x => Math.Max(0, x.TotalAttendees)),
            UniqueAttendees = uniqueAttendees.Count,
            UniqueNewAttendees = uniqueNewAttendees.Count,
            TotalReturningAttendees = records.Sum(
                x => Math.Max(0, x.ReturningAttendees)),
            TotalNewAttendees = records.Sum(
                x => Math.Max(0, x.NewAttendees)),
            OverallRetentionPercent = retention.Percent,
            HasRetentionBaseline = retention.HasBaseline,
            TotalMessages = records.Sum(x => Math.Max(0, x.TotalMessages)),
            AverageMessagesPerStream = records.Average(
                x => Math.Max(0, x.TotalMessages)),
            MostAttended = mostAttended,
            LeastAttended = leastAttended,
            TotalSubs = records.Sum(x => Math.Max(0, x.TotalSubs)),
            TotalFollows = records.Sum(x => Math.Max(0, x.TotalFollows)),
            TotalRaids = records.Sum(x => Math.Max(0, x.TotalRaids)),
            TotalBits = records.Sum(x => Math.Max(0, x.TotalBits))
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

        foreach (StreamRecord record in
            records ?? new List<StreamRecord>())
        {
            if (record == null)
                continue;

            List<string> recordUsers =
                (record.ReturningUsers ?? new List<string>())
                .Concat(record.NewUsers ?? new List<string>())
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
        DateTime targetStart,
        DateTime targetEnd,
        DateTime previousStart,
        DateTime previousEnd,
        MonthlyTotals current,
        MonthlyTotals previous,
        List<StreamRecord> currentRecords)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("────────────────────────────────");
        sb.AppendLine("      📊 MONTHLY PERFORMANCE REPORT");
        sb.AppendLine("────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine(
            "📅 Report Month: " + targetStart.ToString("MMMM yyyy"));
        sb.AppendLine(
            "🕒 Report Generated: " + DateTime.Now.ToString("h:mm tt"));
        sb.AppendLine(
            "🎥 Streams Broadcast: " + current.StreamCount.ToString("N0"));
        sb.AppendLine(
            "⏱️ Total Stream Time: " +
            FormatDuration(current.TotalMinutes));
        sb.AppendLine(
            "⏱️ Average Stream Length: " +
            FormatDuration(current.AverageMinutes));
        sb.AppendLine();

        sb.AppendLine("👥 Audience Overview");
        sb.AppendLine(
            "• Total Attendance Occurrences: " +
            current.TotalAttendanceOccurrences.ToString("N0"));
        sb.AppendLine(
            "• Average Attendance per Stream: " +
            current.AverageAttendance.ToString("0.00"));
        sb.AppendLine(
            "• Unique Attendees: " +
            current.UniqueAttendees.ToString("N0"));
        sb.AppendLine(
            "• Unique New Attendees: " +
            current.UniqueNewAttendees.ToString("N0"));
        sb.AppendLine(
            "• Returning Attendance Occurrences: " +
            current.TotalReturningAttendees.ToString("N0"));
        sb.AppendLine(
            "• New Attendance Occurrences: " +
            current.TotalNewAttendees.ToString("N0"));
        sb.AppendLine(
            "• Overall Monthly Retention Rate: " +
            FormatRetention(current));
        sb.AppendLine();

        sb.AppendLine("💬 Engagement Overview");
        sb.AppendLine(
            "• Total Messages: " + current.TotalMessages.ToString("N0"));
        sb.AppendLine(
            "• Average Messages per Stream: " +
            current.AverageMessagesPerStream.ToString("0.00"));
        sb.AppendLine();

        sb.AppendLine("🏆 Stream Highlights");
        AppendStreamHighlight(sb, "Most Attended Stream", current.MostAttended);
        sb.AppendLine();
        AppendStreamHighlight(
            sb,
            "Least Attended Stream",
            current.LeastAttended);
        sb.AppendLine();

        sb.AppendLine("💙 Support Overview");
        sb.AppendLine(
            "• Subscriptions: " + current.TotalSubs.ToString("N0"));
        sb.AppendLine(
            "• Follows: " + current.TotalFollows.ToString("N0"));
        sb.AppendLine(
            "• Raids: " + current.TotalRaids.ToString("N0"));
        sb.AppendLine(
            "• Bits: " + current.TotalBits.ToString("N0"));
        sb.AppendLine();

        sb.AppendLine("📅 Weekly Breakdown");
        AppendWeeklyBreakdown(
            sb,
            currentRecords,
            targetStart,
            targetEnd);
        sb.AppendLine();

        sb.AppendLine("📈 Month-to-Month Comparison");
        sb.AppendLine(
            "• Previous Month: " + previousStart.ToString("MMMM yyyy"));

        if (previous == null)
        {
            sb.AppendLine(
                "• No archived stream data exists for the previous month.");
            sb.AppendLine(
                "• Comparisons will appear once both months contain " +
                "archived streams.");
        }
        else
        {
            AppendComparison(
                sb,
                "Streams Broadcast",
                current.StreamCount,
                previous.StreamCount,
                "0");
            AppendComparison(
                sb,
                "Total Attendance Occurrences",
                current.TotalAttendanceOccurrences,
                previous.TotalAttendanceOccurrences,
                "0");
            AppendComparison(
                sb,
                "Average Attendance",
                current.AverageAttendance,
                previous.AverageAttendance,
                "0.00");
            AppendComparison(
                sb,
                "Unique Attendees",
                current.UniqueAttendees,
                previous.UniqueAttendees,
                "0");
            AppendComparison(
                sb,
                "Unique New Attendees",
                current.UniqueNewAttendees,
                previous.UniqueNewAttendees,
                "0");
            AppendComparison(
                sb,
                "Total Messages",
                current.TotalMessages,
                previous.TotalMessages,
                "0");
            AppendComparison(
                sb,
                "Average Messages per Stream",
                current.AverageMessagesPerStream,
                previous.AverageMessagesPerStream,
                "0.00");
            AppendRetentionComparison(sb, current, previous);
            AppendComparison(
                sb,
                "Subscriptions",
                current.TotalSubs,
                previous.TotalSubs,
                "0");
            AppendComparison(
                sb,
                "Follows",
                current.TotalFollows,
                previous.TotalFollows,
                "0");
            AppendComparison(
                sb,
                "Raids",
                current.TotalRaids,
                previous.TotalRaids,
                "0");
            AppendComparison(
                sb,
                "Bits",
                current.TotalBits,
                previous.TotalBits,
                "0");
        }

        sb.AppendLine();
        sb.AppendLine("📝 Report Notes");
        sb.AppendLine(
            "• Months use each stream's local start date.");
        sb.AppendLine(
            "• Returning and new attendance totals are occurrences summed " +
            "across streams.");
        sb.AppendLine(
            "• The same attendee may count once for every stream they attended.");
        sb.AppendLine(
            "• Unique attendees are counted only once across the entire month.");
        sb.AppendLine(
            "• Retention is the share of the previous month's unique audience " +
            "that returned this month.");
        sb.AppendLine(
            "• Weekly breakdowns are clipped to the selected calendar month.");
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────");
        sb.AppendLine("💙 Love Life One Day At a Time 💙");
        sb.AppendLine("────────────────────────────────");

        return sb.ToString();
    }

    private string FormatRetention(MonthlyTotals totals)
    {
        return totals != null && totals.HasRetentionBaseline
            ? totals.OverallRetentionPercent.ToString("0.0") + "%"
            : "N/A";
    }

    private void AppendStreamHighlight(
        StringBuilder sb,
        string label,
        StreamRecord record)
    {
        sb.AppendLine("• " + label + ":");

        if (record == null)
        {
            sb.AppendLine("    - No stream data available.");
            return;
        }

        string category = string.IsNullOrWhiteSpace(record.Category)
            ? "Unknown Category"
            : record.Category.Trim();

        string title = string.IsNullOrWhiteSpace(record.StreamTitle)
            ? "No title recorded"
            : record.StreamTitle.Trim();

        sb.AppendLine(
            "    - Date: " + GetRecordDate(record).ToString("MMM dd, yyyy"));
        sb.AppendLine("    - Category: " + category);
        sb.AppendLine(
            "    - Attendance: " +
            Math.Max(0, record.TotalAttendees).ToString("N0"));
        sb.AppendLine(
            "    - Chat Messages: " +
            Math.Max(0, record.TotalMessages).ToString("N0"));
        sb.AppendLine("    - Title: " + title);
    }

    private void AppendWeeklyBreakdown(
        StringBuilder sb,
        List<StreamRecord> records,
        DateTime monthStart,
        DateTime monthEnd)
    {
        List<IGrouping<DateTime, StreamRecord>> weeks = records
            .GroupBy(x => GetMonday(GetRecordDate(x)))
            .OrderBy(x => x.Key)
            .ToList();

        foreach (IGrouping<DateTime, StreamRecord> week in weeks)
        {
            List<StreamRecord> weekRecords = week
                .OrderBy(GetRecordDate)
                .ToList();

            DateTime displayStart = week.Key < monthStart
                ? monthStart
                : week.Key;

            DateTime weekEnd = week.Key.AddDays(6);
            DateTime displayEnd = weekEnd > monthEnd
                ? monthEnd
                : weekEnd;

            sb.AppendLine(
                "• " + displayStart.ToString("MMM d") +
                "–" + displayEnd.ToString("M/d/yyyy"));
            sb.AppendLine(
                "    - Streams: " + weekRecords.Count.ToString("N0"));
            sb.AppendLine(
                "    - Average Attendance: " +
                weekRecords.Average(
                    x => Math.Max(0, x.TotalAttendees)).ToString("0.00"));
            sb.AppendLine(
                "    - Chat Messages: " +
                weekRecords.Sum(
                    x => Math.Max(0, x.TotalMessages)).ToString("N0"));
            sb.AppendLine(
                "    - Follows: " +
                weekRecords.Sum(
                    x => Math.Max(0, x.TotalFollows)).ToString("N0"));
            sb.AppendLine(
                "    - Subscriptions: " +
                weekRecords.Sum(
                    x => Math.Max(0, x.TotalSubs)).ToString("N0"));
            sb.AppendLine(
                "    - Bits: " +
                weekRecords.Sum(
                    x => Math.Max(0, x.TotalBits)).ToString("N0"));
            sb.AppendLine(
                "    - Raids: " +
                weekRecords.Sum(
                    x => Math.Max(0, x.TotalRaids)).ToString("N0"));
        }
    }

    private void AppendComparison(
        StringBuilder sb,
        string label,
        double current,
        double previous,
        string numberFormat)
    {
        double difference = current - previous;
        string percentChange;

        if (Math.Abs(previous) < 0.000001)
        {
            percentChange = Math.Abs(current) < 0.000001
                ? "0.00%"
                : "N/A; previous month was 0";
        }
        else
        {
            percentChange =
                ((difference / previous) * 100.0)
                .ToString("+0.00;-0.00;0.00") + "%";
        }

        sb.AppendLine(
            "• " + label + ": " +
            current.ToString(numberFormat) +
            " vs " + previous.ToString(numberFormat) +
            " | Change: " +
            difference.ToString(
                "+" + numberFormat +
                ";-" + numberFormat +
                ";" + numberFormat) +
            " (" + percentChange + ")");
    }

    private void AppendRetentionComparison(
        StringBuilder sb,
        MonthlyTotals current,
        MonthlyTotals previous)
    {
        if (current != null &&
            previous != null &&
            current.HasRetentionBaseline &&
            previous.HasRetentionBaseline)
        {
            double difference =
                current.OverallRetentionPercent -
                previous.OverallRetentionPercent;

            sb.AppendLine(
                "• Retention Rate: " +
                current.OverallRetentionPercent.ToString("0.0") + "% vs " +
                previous.OverallRetentionPercent.ToString("0.0") + "%" +
                " | Change: " +
                difference.ToString("+0.0;-0.0;0.0") +
                " percentage points");
            return;
        }

        sb.AppendLine(
            "• Retention Rate: " +
            FormatRetention(current) +
            " vs " +
            FormatRetention(previous) +
            " | A complete earlier-month baseline is required");
    }

    private string SaveReport(DateTime monthStart, string report)
    {
        string documents =
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string folder = Path.Combine(
            documents,
            "StreamSummaries",
            "Monthly Reports");

        Directory.CreateDirectory(folder);

        string filename =
            "Stream_Suite_Monthly_Report_" +
            monthStart.ToString("yyyy-MM") +
            ".txt";

        string path = Path.Combine(folder, filename);
        File.WriteAllText(path, report, Encoding.UTF8);
        return path;
    }

    private string FormatDuration(double totalMinutes)
    {
        int roundedMinutes = Math.Max(0, (int)Math.Round(totalMinutes));
        int hours = roundedMinutes / 60;
        int minutes = roundedMinutes % 60;
        return hours + "h " + minutes + "m";
    }

    private DateTime GetMonday(DateTime date)
    {
        int difference =
            (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-difference);
    }

    private DateTime GetRecordDate(StreamRecord record)
    {
        DateTime parsed;

        if (record != null &&
            DateTime.TryParse(record.StreamStartLocal, out parsed))
        {
            return parsed;
        }

        if (record != null &&
            DateTime.TryParse(record.StreamDate, out parsed))
        {
            return parsed;
        }

        if (record != null &&
            DateTime.TryParse(record.GeneratedAtLocal, out parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }

    private class MonthlyTotals
    {
        public int StreamCount { get; set; }
        public double TotalMinutes { get; set; }
        public double AverageMinutes { get; set; }
        public int TotalAttendanceOccurrences { get; set; }
        public double AverageAttendance { get; set; }
        public int UniqueAttendees { get; set; }
        public int UniqueNewAttendees { get; set; }
        public int TotalReturningAttendees { get; set; }
        public int TotalNewAttendees { get; set; }
        public double OverallRetentionPercent { get; set; }
        public bool HasRetentionBaseline { get; set; }
        public int TotalMessages { get; set; }
        public double AverageMessagesPerStream { get; set; }
        public StreamRecord MostAttended { get; set; }
        public StreamRecord LeastAttended { get; set; }
        public int TotalSubs { get; set; }
        public int TotalFollows { get; set; }
        public int TotalRaids { get; set; }
        public int TotalBits { get; set; }
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
