# Complete StreamFolk global-variable mapping

All **44** distinct global names covered by this release are listed below. These names are case-sensitive. Every operational read/write uses the persisted store (`true`). The separate one-time utility can also preserve identically named temporary values.

| Old global variable | New global variable | Note |
| --- | --- | --- |
| `AttendanceHistory` | `streamfolk.analytics.attendanceHistory` |  |
| `FirstDates` | `streamfolk.analytics.firstDates` | Native Dictionary<string, string>; winner/date entry names are unchanged. |
| `FirstStats` | `streamfolk.analytics.firstStats` | Native Dictionary<string, int>; entry names are unchanged. |
| `HugStats` | `streamfolk.analytics.hugStats` |  |
| `PatCounts` | `streamfolk.analytics.patCounts` |  |
| `PatGiven` | `streamfolk.analytics.patGiven` |  |
| `PatStats` | `streamfolk.analytics.patStats` | Reset-only legacy slot; actual Pats counters are patCounts and patGiven. |
| `SeenUsers` | `streamfolk.analytics.seenUsers` |  |
| `StreamFolkText.LastSentUtc` | `streamfolk.analytics.text.lastSentUtc` | Text-message cooldown timestamp. |
| `analytics.attendanceSummaryJson` | `streamfolk.analytics.attendanceSummaryJson` |  |
| `analytics.bits` | `streamfolk.analytics.bits` |  |
| `analytics.chatMessagesByUser` | `streamfolk.analytics.chatMessagesByUser` |  |
| `analytics.chatTotalMessages` | `streamfolk.analytics.chatTotalMessages` |  |
| `analytics.currentCategory` | `streamfolk.analytics.currentCategory` |  |
| `analytics.currentStreamTitle` | `streamfolk.analytics.currentStreamTitle` |  |
| `analytics.finalSummaryJson` | `streamfolk.analytics.finalSummaryJson` |  |
| `analytics.follows` | `streamfolk.analytics.follows` |  |
| `analytics.lastStreamArchiveJson` | `streamfolk.analytics.lastStreamArchiveJson` |  |
| `analytics.latestMonthlyReport` | `streamfolk.analytics.latestMonthlyReport` |  |
| `analytics.latestMonthlyReportEnd` | `streamfolk.analytics.latestMonthlyReportEnd` |  |
| `analytics.latestMonthlyReportMonth` | `streamfolk.analytics.latestMonthlyReportMonth` |  |
| `analytics.latestMonthlyReportPath` | `streamfolk.analytics.latestMonthlyReportPath` |  |
| `analytics.latestMonthlyReportStart` | `streamfolk.analytics.latestMonthlyReportStart` |  |
| `analytics.latestWeeklyReport` | `streamfolk.analytics.latestWeeklyReport` |  |
| `analytics.latestWeeklyReportEnd` | `streamfolk.analytics.latestWeeklyReportEnd` |  |
| `analytics.latestWeeklyReportPath` | `streamfolk.analytics.latestWeeklyReportPath` |  |
| `analytics.latestWeeklyReportStart` | `streamfolk.analytics.latestWeeklyReportStart` |  |
| `analytics.latestYearlyReport` | `streamfolk.analytics.latestYearlyReport` |  |
| `analytics.latestYearlyReportEnd` | `streamfolk.analytics.latestYearlyReportEnd` |  |
| `analytics.latestYearlyReportPath` | `streamfolk.analytics.latestYearlyReportPath` |  |
| `analytics.latestYearlyReportStart` | `streamfolk.analytics.latestYearlyReportStart` |  |
| `analytics.latestYearlyReportYear` | `streamfolk.analytics.latestYearlyReportYear` |  |
| `analytics.monthlyHistory` | `streamfolk.analytics.monthlyHistory` | Older history format; migrated readers still use it under this new name when needed. |
| `analytics.raids` | `streamfolk.analytics.raids` |  |
| `analytics.sessionStartLocal` | `streamfolk.analytics.sessionStartLocal` |  |
| `analytics.streamDeckStatsJson` | `streamfolk.analytics.streamDeckStatsJson` |  |
| `analytics.streamDeckTestMode` | `streamfolk.analytics.streamDeckTestMode` |  |
| `analytics.streamDeckTestStartedLocal` | `streamfolk.analytics.streamDeckTestStartedLocal` |  |
| `analytics.streamHistory` | `streamfolk.analytics.streamHistory` |  |
| `analytics.subs` | `streamfolk.analytics.subs` |  |
| `analytics.subsDetailed` | `streamfolk.analytics.subsDetailed` |  |
| `analytics.totalBits` | `streamfolk.analytics.totalBits` |  |
| `analytics.totalFollows` | `streamfolk.analytics.totalFollows` |  |
| `analytics.totalSubs` | `streamfolk.analytics.totalSubs` |  |

## What is not a global name

Dictionary entries such as `alice_total`, `alice_streak`, `FirstWinner_2026-10-03`, usernames, report JSON properties, Twitch arguments such as `userLogin`, and `%_source%` remain unchanged. Namespacing those would change the stored data or external event/report schema.

Old names appear deliberately in this mapping and the **separate copying utility**. There are no old-global reads, aliases or dual writes in the operational build. Existing old values are retained only as a pre-migration snapshot.

See `GLOBAL-DEPENDENCIES.md` for every action that reads or writes each new global.
