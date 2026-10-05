// MANUAL, ONE-TIME COPY UTILITY. This is the only action allowed to read old keys.
// Stop StreamFolk events and import/run this before the migrated operational build.
// Preview() is read-only. Execute() copies missing keys. Neither deletes old data.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private static readonly object MigrationLock = new object();
    private static readonly string[,] Names = new string[,]
    {
        { "AttendanceHistory", "streamfolk.analytics.attendanceHistory" },
        { "FirstDates", "streamfolk.analytics.firstDates" },
        { "FirstStats", "streamfolk.analytics.firstStats" },
        { "HugStats", "streamfolk.analytics.hugStats" },
        { "PatCounts", "streamfolk.analytics.patCounts" },
        { "PatGiven", "streamfolk.analytics.patGiven" },
        { "PatStats", "streamfolk.analytics.patStats" },
        { "SeenUsers", "streamfolk.analytics.seenUsers" },
        { "StreamFolkText.LastSentUtc", "streamfolk.analytics.text.lastSentUtc" },
        { "analytics.attendanceSummaryJson", "streamfolk.analytics.attendanceSummaryJson" },
        { "analytics.bits", "streamfolk.analytics.bits" },
        { "analytics.chatMessagesByUser", "streamfolk.analytics.chatMessagesByUser" },
        { "analytics.chatTotalMessages", "streamfolk.analytics.chatTotalMessages" },
        { "analytics.currentCategory", "streamfolk.analytics.currentCategory" },
        { "analytics.currentStreamTitle", "streamfolk.analytics.currentStreamTitle" },
        { "analytics.finalSummaryJson", "streamfolk.analytics.finalSummaryJson" },
        { "analytics.follows", "streamfolk.analytics.follows" },
        { "analytics.lastStreamArchiveJson", "streamfolk.analytics.lastStreamArchiveJson" },
        { "analytics.latestMonthlyReport", "streamfolk.analytics.latestMonthlyReport" },
        { "analytics.latestMonthlyReportEnd", "streamfolk.analytics.latestMonthlyReportEnd" },
        { "analytics.latestMonthlyReportMonth", "streamfolk.analytics.latestMonthlyReportMonth" },
        { "analytics.latestMonthlyReportPath", "streamfolk.analytics.latestMonthlyReportPath" },
        { "analytics.latestMonthlyReportStart", "streamfolk.analytics.latestMonthlyReportStart" },
        { "analytics.latestWeeklyReport", "streamfolk.analytics.latestWeeklyReport" },
        { "analytics.latestWeeklyReportEnd", "streamfolk.analytics.latestWeeklyReportEnd" },
        { "analytics.latestWeeklyReportPath", "streamfolk.analytics.latestWeeklyReportPath" },
        { "analytics.latestWeeklyReportStart", "streamfolk.analytics.latestWeeklyReportStart" },
        { "analytics.latestYearlyReport", "streamfolk.analytics.latestYearlyReport" },
        { "analytics.latestYearlyReportEnd", "streamfolk.analytics.latestYearlyReportEnd" },
        { "analytics.latestYearlyReportPath", "streamfolk.analytics.latestYearlyReportPath" },
        { "analytics.latestYearlyReportStart", "streamfolk.analytics.latestYearlyReportStart" },
        { "analytics.latestYearlyReportYear", "streamfolk.analytics.latestYearlyReportYear" },
        { "analytics.monthlyHistory", "streamfolk.analytics.monthlyHistory" },
        { "analytics.raids", "streamfolk.analytics.raids" },
        { "analytics.sessionStartLocal", "streamfolk.analytics.sessionStartLocal" },
        { "analytics.streamDeckStatsJson", "streamfolk.analytics.streamDeckStatsJson" },
        { "analytics.streamDeckTestMode", "streamfolk.analytics.streamDeckTestMode" },
        { "analytics.streamDeckTestStartedLocal", "streamfolk.analytics.streamDeckTestStartedLocal" },
        { "analytics.streamHistory", "streamfolk.analytics.streamHistory" },
        { "analytics.subs", "streamfolk.analytics.subs" },
        { "analytics.subsDetailed", "streamfolk.analytics.subsDetailed" },
        { "analytics.totalBits", "streamfolk.analytics.totalBits" },
        { "analytics.totalFollows", "streamfolk.analytics.totalFollows" },
        { "analytics.totalSubs", "streamfolk.analytics.totalSubs" },
    };

    private class CopyItem
    {
        public string OldName;
        public string NewName;
        public object Value;
        public bool Persisted;
    }

    public bool Preview() { return Run(false); }
    public bool Execute() { return Run(true); }

    private bool Run(bool apply)
    {
        lock (MigrationLock)
        {
            var plan = new List<CopyItem>();
            var conflicts = new List<string>();
            int already = 0;
            int absent = 0;
            try
            {
                foreach (bool persisted in new bool[] { true, false })
                {
                    var snapshot = Snapshot(persisted);
                    for (int i = 0; i < Names.GetLength(0); i++)
                    {
                        string oldName = Names[i, 0];
                        string newName = Names[i, 1];
                        object oldValue;
                        object newValue;
                        if (!snapshot.TryGetValue(oldName, out oldValue)) { absent++; continue; }
                        if (snapshot.TryGetValue(newName, out newValue))
                        {
                            if (SameValue(oldValue, newValue)) already++;
                            else conflicts.Add((persisted ? "Persisted: " : "Temporary: ") + newName);
                            continue;
                        }
                        plan.Add(new CopyItem { OldName=oldName, NewName=newName, Value=oldValue, Persisted=persisted });
                    }
                }
                if (conflicts.Count > 0)
                {
                    CPH.LogError("StreamFolk migration STOPPED before copying: existing destination values differ. " +
                        "No values were overwritten. Keep StreamFolk stopped and compare these keys: " + string.Join(", ", conflicts));
                    return false;
                }
                CPH.LogInfo("StreamFolk migration " + (apply ? "preflight" : "PREVIEW") + ": " + plan.Count +
                    " missing destinations to copy, " + already + " already equal, " + absent +
                    " old keys absent across both stores. Missing old keys are normal for unused features.");
                if (!apply) return true;

                // Check snapshots again before the first write. StreamFolk events must remain stopped.
                foreach (CopyItem item in plan)
                {
                    var current = Snapshot(item.Persisted);
                    object value;
                    if (!current.TryGetValue(item.OldName, out value) || !SameValue(item.Value, value) ||
                        current.ContainsKey(item.NewName))
                        throw new InvalidOperationException("Variables changed after preflight; keep actions stopped and run again.");
                }
                int copied = 0;
                foreach (CopyItem item in plan)
                {
                    // Pass the original object to preserve strings, numbers and native FIRST dictionaries.
                    CPH.SetGlobalVar(item.NewName, item.Value, item.Persisted);
                    var after = Snapshot(item.Persisted);
                    object value;
                    if (!after.TryGetValue(item.NewName, out value) || !SameValue(item.Value, value))
                        throw new InvalidOperationException("Copy verification failed for " + item.NewName);
                    copied++;
                }
                CPH.LogInfo("StreamFolk migration COMPLETE: verified " + copied + " copies; " + already +
                    " destinations were already equal. Old values are retained for rollback. " +
                    "Import the migrated operational build before re-enabling StreamFolk. Disable/delete this utility afterwards.");
                return true;
            }
            catch (Exception ex)
            {
                // An interrupted copy is safely resumable: successful destinations are compared on rerun.
                // Never roll back by deleting keys that another action may have updated.
                CPH.LogError("StreamFolk migration INCOMPLETE (" + ex.GetType().Name +
                    "). Old values are retained. Keep StreamFolk stopped; inspect the globals, then run again. " +
                    "Already verified destinations will be skipped if equal; conflicting destinations are never overwritten.");
                return false;
            }
        }
    }

    private Dictionary<string, object> Snapshot(bool persisted)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in CPH.GetGlobalVarValues(persisted))
            result.Add(item.VariableName, item.Value);
        return result;
    }

    private bool SameValue(object a, object b)
    {
        if (a == null || b == null) return a == null && b == null;
        // No string-to-JSON or string-to-number coercion: storage type matters.
        if (a.GetType() != b.GetType()) return false;
        return JToken.DeepEquals(JToken.FromObject(a), JToken.FromObject(b));
    }
}
