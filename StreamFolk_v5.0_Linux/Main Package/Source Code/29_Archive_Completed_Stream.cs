/*
==============================================================================
STREAMFOLK - ARCHIVE COMPLETED STREAM
==============================================================================

PURPOSE
Reads streamfolk.analytics.finalSummaryJson after Stream End Protocols finishes and appends
one normalized stream record to streamfolk.analytics.streamHistory. It independently
recalculates true stream retention from attendee-roster overlap so archived
history remains accurate even if an older final-summary formula is present.

PLACEMENT
Add this as a SECOND C# sub-action in the existing Stream End Protocols action,
immediately after the main Stream End C# sub-action.

GLOBALS READ
streamfolk.analytics.finalSummaryJson
streamfolk.analytics.sessionStartLocal
streamfolk.analytics.currentCategory
streamfolk.analytics.currentStreamTitle
streamfolk.analytics.streamHistory
streamfolk.analytics.monthlyHistory (legacy migration only)

GLOBALS WRITTEN
streamfolk.analytics.streamHistory
streamfolk.analytics.lastStreamArchiveJson

SAFETY
This script does not modify streamfolk.analytics.attendanceHistory, streamfolk.analytics.seenUsers, or any active-session
tracker. Duplicate runs for the same finalized stream are ignored. If the old
streamfolk.analytics.monthlyHistory exists and streamHistory is empty, it is copied into
the new general-purpose history without deleting the old variable.
==============================================================================
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class CPHInline
{
    private const string FinalSummaryKey = "streamfolk.analytics.finalSummaryJson";
    private const string SessionStartKey = "streamfolk.analytics.sessionStartLocal";
    private const string CurrentCategoryKey = "streamfolk.analytics.currentCategory";
    private const string CurrentTitleKey = "streamfolk.analytics.currentStreamTitle";
    private const string StreamHistoryKey = "streamfolk.analytics.streamHistory";
    private const string LegacyMonthlyHistoryKey = "streamfolk.analytics.monthlyHistory";
    private const string LastArchiveKey = "streamfolk.analytics.lastStreamArchiveJson";

    public bool Execute()
    {
        string finalJson = CPH.GetGlobalVar<string>(FinalSummaryKey, true);
        if (string.IsNullOrWhiteSpace(finalJson))
        {
            CPH.LogWarn("StreamFolk Archive: streamfolk.analytics.finalSummaryJson is empty. Nothing was archived.");
            return true;
        }

        FinalSummarySnapshot summary;
        try
        {
            summary = JsonConvert.DeserializeObject<FinalSummarySnapshot>(finalJson);
        }
        catch (Exception ex)
        {
            CPH.LogWarn("StreamFolk Archive: Could not read the final summary. Existing stream history was not changed. Error: " + ex.Message);
            return true;
        }

        if (summary == null)
        {
            CPH.LogWarn("StreamFolk Archive: Final summary deserialized as null. Nothing was archived.");
            return true;
        }

        List<StreamRecord> history;
        if (!TryLoadHistory(out history))
            return true;

        DateTime generatedAt = ParseDateOrNow(summary.GeneratedAtLocal);
        DateTime streamStart = ParseDateOrFallback(
            CPH.GetGlobalVar<string>(SessionStartKey, true),
            generatedAt);

        string generatedStamp = generatedAt.ToString("o");
        string startStamp = streamStart.ToString("o");
        string recordId = !string.IsNullOrWhiteSpace(summary.GeneratedAtLocal)
            ? summary.GeneratedAtLocal.Trim()
            : startStamp;

        bool duplicate = history.Any(x =>
            x != null &&
            (
                string.Equals(x.RecordId, recordId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.GeneratedAtLocal, generatedStamp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.StreamStartLocal, startStamp, StringComparison.OrdinalIgnoreCase)
            ));

        if (duplicate)
        {
            CPH.LogWarn("StreamFolk Archive: This completed stream is already stored. Duplicate archive was skipped.");
            return true;
        }

        AttendanceSnapshot attendance = summary.Attendance ?? new AttendanceSnapshot();
        List<string> returningUsers = CleanUserList(attendance.Returning);
        List<string> newUsers = CleanUserList(attendance.Newly);
        List<string> currentUsers = returningUsers
            .Concat(newUsers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int classifiedTotal = returningUsers
            .Concat(newUsers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        int totalAttendees = attendance.TotalAttendees > 0
            ? attendance.TotalAttendees
            : classifiedTotal;

        string category = CleanOrFallback(
            CPH.GetGlobalVar<string>(CurrentCategoryKey, true),
            "Unknown Category");

        string title = CleanOrFallback(
            CPH.GetGlobalVar<string>(CurrentTitleKey, true),
            "");

        DateTime weekStart = GetMonday(streamStart);
        double trueRetentionRate = CalculateTrueRetention(
            history,
            streamStart,
            currentUsers);

        StreamRecord record = new StreamRecord
        {
            RecordId = recordId,
            MonthKey = streamStart.ToString("yyyy-MM"),
            WeekStart = weekStart.ToString("yyyy-MM-dd"),
            StreamDate = streamStart.ToString("yyyy-MM-dd"),
            StreamStartLocal = startStamp,
            GeneratedAtLocal = generatedStamp,
            Category = category,
            StreamTitle = title,
            DurationMinutes = Math.Max(0, summary.DurationMinutes),
            TotalAttendees = Math.Max(0, totalAttendees),
            ReturningAttendees = returningUsers.Count,
            NewAttendees = newUsers.Count,
            ReturningUsers = returningUsers,
            NewUsers = newUsers,
            TotalMessages = Math.Max(0, summary.TotalMessages),
            TotalFollows = Math.Max(0, summary.TotalFollows),
            TotalSubs = Math.Max(0, summary.TotalSubs),
            TotalBits = Math.Max(0, summary.TotalBits),
            TotalRaids = Math.Max(0, summary.TotalRaids),
            StreamRetentionRate = trueRetentionRate
        };

        history.Add(record);
        history = history
            .Where(x => x != null)
            .OrderBy(x => GetRecordDate(x))
            .ToList();

        string historyJson = JsonConvert.SerializeObject(history, Formatting.Indented);
        CPH.SetGlobalVar(StreamHistoryKey, historyJson, true);
        CPH.SetGlobalVar(LastArchiveKey, JsonConvert.SerializeObject(record, Formatting.Indented), true);

        CPH.LogInfo(
            "StreamFolk Archive: Stored " + record.StreamDate +
            " | " + record.Category +
            " | Attendance " + record.TotalAttendees +
            " | True Retention " + (record.StreamRetentionRate * 100.0).ToString("0.0") + "%" +
            " | Week starting " + record.WeekStart + ".");

        return true;
    }

    private bool TryLoadHistory(out List<StreamRecord> history)
    {
        history = new List<StreamRecord>();
        string currentJson = CPH.GetGlobalVar<string>(StreamHistoryKey, true);

        if (!string.IsNullOrWhiteSpace(currentJson))
            return TryDeserializeHistory(currentJson, StreamHistoryKey, out history);

        string legacyJson = CPH.GetGlobalVar<string>(LegacyMonthlyHistoryKey, true);
        if (string.IsNullOrWhiteSpace(legacyJson))
            return true;

        if (!TryDeserializeHistory(legacyJson, LegacyMonthlyHistoryKey, out history))
            return false;

        string migratedJson = JsonConvert.SerializeObject(history, Formatting.Indented);
        CPH.SetGlobalVar(StreamHistoryKey, migratedJson, true);
        CPH.LogInfo("StreamFolk Archive: Migrated legacy streamfolk.analytics.monthlyHistory into streamfolk.analytics.streamHistory. The original variable was preserved.");
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
            CPH.LogWarn(
                "StreamFolk Archive: " + sourceKey + " contains invalid JSON. " +
                "The archive was NOT overwritten. Back up or repair the variable first. Error: " + ex.Message);
            return false;
        }
    }

    private List<string> CleanUserList(List<string> users)
    {
        return (users ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimStart('@'))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private double CalculateTrueRetention(
        List<StreamRecord> history,
        DateTime currentStreamStart,
        List<string> currentUsers)
    {
        StreamRecord previousRecord = (history ?? new List<StreamRecord>())
            .Where(x => x != null)
            .Where(x =>
            {
                DateTime recordDate = GetRecordDate(x);
                return recordDate != DateTime.MinValue &&
                       recordDate < currentStreamStart;
            })
            .OrderByDescending(x => GetRecordDate(x))
            .FirstOrDefault();

        if (previousRecord == null)
            return 0;

        List<string> previousUsers = CleanUserList(
            (previousRecord.ReturningUsers ?? new List<string>())
                .Concat(previousRecord.NewUsers ?? new List<string>())
                .ToList());

        if (previousUsers.Count == 0)
            return 0;

        int retainedViewers = previousUsers
            .Intersect(
                currentUsers ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase)
            .Count();

        return Math.Round(
            (double)retainedViewers / previousUsers.Count,
            3);
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

    private DateTime ParseDateOrNow(string value)
    {
        DateTime parsed;
        return DateTime.TryParse(value, out parsed) ? parsed : DateTime.Now;
    }

    private DateTime ParseDateOrFallback(string value, DateTime fallback)
    {
        DateTime parsed;
        return DateTime.TryParse(value, out parsed) ? parsed : fallback;
    }

    private string CleanOrFallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private class AttendanceSnapshot
    {
        public string Date { get; set; }
        public int TotalAttendees { get; set; }
        public List<string> Returning { get; set; }
        public List<string> Newly { get; set; }
    }

    private class FinalSummarySnapshot
    {
        public string GeneratedAtLocal { get; set; }
        public double DurationMinutes { get; set; }
        public int TotalMessages { get; set; }
        public int TotalFollows { get; set; }
        public int TotalSubs { get; set; }
        public int TotalBits { get; set; }
        public int TotalRaids { get; set; }
        public double RetentionRate { get; set; }
        public AttendanceSnapshot Attendance { get; set; }
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
}
