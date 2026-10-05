/*
==============================================================================
STREAMFOLK - GENERATE YEARLY REPORT (TRUE RETENTION)
==============================================================================

PURPOSE
Builds the existing StreamFolk yearly performance report from
streamfolk.analytics.streamHistory while calculating retention from exact attendee-roster
overlap.

TRUE YEARLY RETENTION
Unique attendees from the previous calendar year who attended again during the
selected year / all unique attendees from the previous calendar year.

DEFAULT YEAR
The current local calendar year.

OPTIONAL INPUT
- current
- previous
- yyyy
- yyyy-MM
- any yyyy-MM-dd date inside the desired year

Supply the value through year, date, reportYear, or rawInput.

GLOBALS READ
streamfolk.analytics.streamHistory
streamfolk.analytics.monthlyHistory (legacy migration only)

GLOBALS WRITTEN
streamfolk.analytics.streamHistory (legacy migration only)
streamfolk.analytics.latestYearlyReport
streamfolk.analytics.latestYearlyReportYear
streamfolk.analytics.latestYearlyReportStart
streamfolk.analytics.latestYearlyReportEnd
streamfolk.analytics.latestYearlyReportPath

OUTPUT FOLDER
Documents\StreamSummaries\Yearly Reports
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
    private const string LatestReportKey = "streamfolk.analytics.latestYearlyReport";
    private const string LatestReportYearKey = "streamfolk.analytics.latestYearlyReportYear";
    private const string LatestReportStartKey = "streamfolk.analytics.latestYearlyReportStart";
    private const string LatestReportEndKey = "streamfolk.analytics.latestYearlyReportEnd";
    private const string LatestReportPathKey = "streamfolk.analytics.latestYearlyReportPath";

    public bool Execute()
    {
        List<StreamRecord> history;
        if (!TryLoadHistory(out history))
            return true;

        int targetYear = ResolveTargetYear();
        DateTime targetStart = new DateTime(targetYear, 1, 1);
        DateTime targetEnd = new DateTime(targetYear, 12, 31);
        DateTime previousStart = targetStart.AddYears(-1);
        DateTime previousEnd = targetStart.AddDays(-1);
        DateTime earlierStart = previousStart.AddYears(-1);
        DateTime earlierEnd = previousStart.AddDays(-1);

        List<StreamRecord> currentRecords = GetRecordsInPeriod(
            history,
            targetStart,
            targetEnd);

        if (currentRecords.Count == 0)
        {
            CPH.LogWarn(
                "StreamFolk Yearly Report: No archived streams were found " +
                "for " + targetYear + ".");
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

        YearlyTotals current = CalculateTotals(
            currentRecords,
            previousRecords);

        YearlyTotals previous = previousRecords.Count > 0
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
            path = SaveReport(targetYear, report);
        }
        catch (Exception ex)
        {
            CPH.LogWarn(
                "StreamFolk Yearly Report: The report could not be saved. " +
                "No report globals were changed. Error: " + ex.Message);
            return true;
        }

        CPH.SetGlobalVar(LatestReportKey, report, true);
        CPH.SetGlobalVar(
            LatestReportYearKey,
            targetYear.ToString(CultureInfo.InvariantCulture),
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
            "StreamFolk Yearly Report: Generated " +
            targetYear + " report at " + path);

        return true;
    }

    private int ResolveTargetYear()
    {
        string[] argumentNames =
        {
            "year",
            "date",
            "reportYear"
        };

        foreach (string argumentName in argumentNames)
        {
            int parsedYear;
            if (TryParseYearToken(GetArgValue(argumentName), out parsedYear))
                return parsedYear;
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

                int parsedYear;
                if (TryParseYearToken(token, out parsedYear))
                    return parsedYear;
            }
        }

        return DateTime.Now.Year;
    }

    private bool TryParseYearToken(string value, out int year)
    {
        year = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string token = value.Trim();
        int nowYear = DateTime.Now.Year;

        if (string.Equals(token, "current", StringComparison.OrdinalIgnoreCase))
        {
            year = nowYear;
            return true;
        }

        if (string.Equals(token, "previous", StringComparison.OrdinalIgnoreCase))
        {
            year = nowYear - 1;
            return true;
        }

        int parsedYear;
        if (token.Length == 4 &&
            int.TryParse(
                token,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out parsedYear) &&
            parsedYear >= 3 &&
            parsedYear <= 9999)
        {
            year = parsedYear;
            return true;
        }

        DateTime parsedDate;
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
            out parsedDate))
        {
            return false;
        }

        year = parsedDate.Year;
        return year >= 3;
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
                "StreamFolk Yearly Report: streamfolk.analytics.streamHistory is empty.");
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
            "StreamFolk Yearly Report: Migrated legacy " +
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
                "StreamFolk Yearly Report: " + sourceKey +
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

    private YearlyTotals CalculateTotals(
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

        return new YearlyTotals
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
        YearlyTotals current,
        YearlyTotals previous,
        List<StreamRecord> currentRecords)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("────────────────────────────────");
        sb.AppendLine("       📊 YEARLY PERFORMANCE REPORT");
        sb.AppendLine("────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("📅 Report Year: " + targetStart.Year);
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
            "• Overall Yearly Retention Rate: " +
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

        sb.AppendLine("📅 Monthly Breakdown");
        AppendMonthlyBreakdown(sb, currentRecords);
        sb.AppendLine();

        sb.AppendLine("📈 Year-to-Year Comparison");
        sb.AppendLine("• Previous Year: " + previousStart.Year);

        if (previous == null)
        {
            sb.AppendLine(
                "• No archived stream data exists for the previous year.");
            sb.AppendLine(
                "• Comparisons will appear once both years contain " +
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
            "• Years use each stream's local start date.");
        sb.AppendLine(
            "• Returning and new attendance totals are occurrences summed " +
            "across streams.");
        sb.AppendLine(
            "• The same attendee may count once for every stream they attended.");
        sb.AppendLine(
            "• Unique attendees are counted only once across the entire year.");
        sb.AppendLine(
            "• Retention is the share of the previous year's unique audience " +
            "that returned this year.");
        sb.AppendLine(
            "• Only months containing archived streams appear in the monthly " +
            "breakdown.");
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────");
        sb.AppendLine("💙 Love Life One Day At a Time 💙");
        sb.AppendLine("────────────────────────────────");

        return sb.ToString();
    }

    private string FormatRetention(YearlyTotals totals)
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

    private void AppendMonthlyBreakdown(
        StringBuilder sb,
        List<StreamRecord> records)
    {
        List<IGrouping<DateTime, StreamRecord>> months = records
            .GroupBy(x =>
            {
                DateTime date = GetRecordDate(x);
                return new DateTime(date.Year, date.Month, 1);
            })
            .OrderBy(x => x.Key)
            .ToList();

        foreach (IGrouping<DateTime, StreamRecord> month in months)
        {
            List<StreamRecord> monthRecords = month
                .OrderBy(GetRecordDate)
                .ToList();

            List<string> uniqueAttendees =
                GetUniqueAttendees(monthRecords);

            double totalMinutes = monthRecords.Sum(
                x => Math.Max(0, x.DurationMinutes));

            sb.AppendLine("• " + month.Key.ToString("MMMM"));
            sb.AppendLine(
                "    - Streams: " + monthRecords.Count.ToString("N0"));
            sb.AppendLine(
                "    - Total Stream Time: " +
                FormatDuration(totalMinutes));
            sb.AppendLine(
                "    - Average Attendance: " +
                monthRecords.Average(
                    x => Math.Max(0, x.TotalAttendees)).ToString("0.00"));
            sb.AppendLine(
                "    - Unique Attendees: " +
                uniqueAttendees.Count.ToString("N0"));
            sb.AppendLine(
                "    - Chat Messages: " +
                monthRecords.Sum(
                    x => Math.Max(0, x.TotalMessages)).ToString("N0"));
            sb.AppendLine(
                "    - Follows: " +
                monthRecords.Sum(
                    x => Math.Max(0, x.TotalFollows)).ToString("N0"));
            sb.AppendLine(
                "    - Subscriptions: " +
                monthRecords.Sum(
                    x => Math.Max(0, x.TotalSubs)).ToString("N0"));
            sb.AppendLine(
                "    - Bits: " +
                monthRecords.Sum(
                    x => Math.Max(0, x.TotalBits)).ToString("N0"));
            sb.AppendLine(
                "    - Raids: " +
                monthRecords.Sum(
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
                : "N/A; previous year was 0";
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
        YearlyTotals current,
        YearlyTotals previous)
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
            " | A complete earlier-year baseline is required");
    }

    private string SaveReport(int year, string report)
    {
        string documents =
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string folder = Path.Combine(
            documents,
            "StreamSummaries",
            "Yearly Reports");

        Directory.CreateDirectory(folder);

        string filename =
            "Stream_Suite_Yearly_Report_" +
            year.ToString(CultureInfo.InvariantCulture) +
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

    private class YearlyTotals
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
