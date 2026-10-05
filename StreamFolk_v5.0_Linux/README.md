# STREAMFOLK v5.0

> Built By Streamers. Powered by Community.
>
> Created by [babydreamsy](https://www.twitch.tv/babydreamsy).

StreamFolk is a free collection of Streamer.bot actions and C# scripts for Twitch attendance, community interactions, support events, and local stream reports.

This public release uses **`streamfolk.analytics.*` for every operational global variable**. It is prepared for **Streamer.bot 1.0.7 stable**, verified as the current stable release on October 5, 2026. No alpha or beta is required.

## Download and start

[Quick start](QUICK_START.md) · [Full changelog](CHANGELOG.txt) · [Upgrade guide](Upgrade%20Tools/UPGRADE.md)

Get the package from the [StreamFolk GitHub repository](https://github.com/babydreamsy94/STREAMFOLK) or its [Releases page](https://github.com/babydreamsy94/STREAMFOLK/releases).

Choose **one** platform folder. Each has `START_HERE.txt`, `QUICK_START.md`, platform notes, the main import, readable C# source, feature summaries, optional Diaper Check, and upgrade tools.

| Folder | Environment |
| --- | --- |
| `StreamFolk_v5.0_Windows` | Streamer.bot 1.0.7 stable on Windows |
| `StreamFolk_v5.0_Linux` | The same stable application through Wine; Linux support is experimental upstream |

The platform packages use the same logic and IDs. Their framework reference paths differ. **Do not import both into one installation.**

## What changed from v4.1 to v5.0

Every operational global now uses `streamfolk.analytics.*`, with a manual preview/copy utility and a 44-key mapping for existing installations. The main package now has 30 actions and 31 C# modules, adding the separate Bot Welcomer and eight-stat Stream Deck feature.

The release also includes Present Viewers and support-event attendance, gifter details, a single first-chat welcome route, and automatic connected-broadcaster configuration. The working migrated build's report/GUI data fields remain intact.

The [complete changelog](CHANGELOG.txt) retains the original release history and adds the v5.0 summary and entry above v4.1. Earlier release descriptions remain historical and have not been rewritten.

## What it records

- Cumulative attendance, new/returning attendees, and long-term attendance dates.
- Logged-in presence reported by Streamer.bot Present Viewers, first-chat participation, optional Attendance Check redemptions, and eligible support-event actors.
- Chat totals, unique chatters, messages per minute, follows, Bits, raids, subscriptions, resubscriptions, gift subscriptions, and gifter labels.
- Attendee-overlap retention, category/title, duration, individual stream summaries, and weekly/monthly/yearly reports.
- FIRST! wins and streaks, Hugs and Pats statistics/leaderboards, and BITE/BONK commands.
- Eight optional Stream Deck statistics and optional email-to-SMS notifications.

Attendance is a cumulative record of identifiable accounts observed during a session. It is not concurrent viewership, and it cannot identify every anonymous or silent viewer. Receiving a gifted subscription does not, by itself, make someone an attendee. A raid records the raider; it cannot enumerate everybody arriving with that raid.

## Package contents

| Component, per platform | Included |
| --- | --- |
| Main package | 30 actions, 16 commands, 2 blocking queues, 1 disabled timer |
| Main source and feature summaries | 31 C# modules and 31 matching summaries |
| Optional Diaper Check | 1 action, 1 command, 1 C# module and summary |
| Optional upgrade tools | 2 manual actions sharing 1 copy-utility source; 44-key mapping |

Stream End Protocols contains two C# modules: the final-summary workflow, followed by the completed-stream archive. True Retention is integrated into summaries, the archive, period reports, and Stream Deck statistics.

## Public defaults

- No saved attendance, personal histories, credentials, email/gateway addresses, creator reward bindings, personal sound paths, or configured Stream Deck button IDs are included.
- Broadcaster identity comes from the connected Twitch broadcaster account. Replace `botname` in the exclusion arrays with your own separate bot login, or remove that entry if unused.
- All 16 main commands start disabled. Stream Start, Stream End, Send Streamer a Text, Stream Deck Stats, and the Stream Deck timer also start disabled until configured.
- Both SMS switches default off. The optional Diaper Check action and command start disabled.
- `ManageChatModes` defaults false in Start and End. Set it true in both only if you want the original automatic unlocking/locking and chat-clear behavior.
- Reset commands independently verify the connected broadcaster in C#. Report commands are restricted to the broadcaster/trusted moderators; review permissions before enabling them.
- Public imports contain no auto-run action. The separate copy utility runs only when explicitly invoked.

## Install

1. Back up the complete Streamer.bot installation and its saved data. Import between streams with Twitch disconnected and existing StreamFolk actions/timers stopped.
2. If upgrading from old global names, follow `Upgrade Tools/UPGRADE.md` before activating v5.0. A fresh installation skips the copying utility. An installation already using the namespace also skips it.
3. Import your platform's main `.sb` file using Streamer.bot's Import dialog. Review 30 actions, 16 commands, 2 queues, and the disabled timer. Preserve the supplied command/action/timer links.
4. Connect the broadcaster and, if used, bot account. Review exclusions, messages, feature summaries, and command permissions.
5. Enable Present Viewers Live Update in Twitch settings. Bind your channel's optional Attendance Check and FIRST! rewards to the specific actions; no creator reward IDs are shipped.
6. Open and compile the C# sub-actions. Enable Stream Start and Stream End when their messages/settings are ready, then enable the community commands you want. Optional SMS/Deck/Diaper Check can remain disabled.
7. Reconnect and test a short stream: Start, attendance, chat, an interaction, and End. Confirm the text/JSON summary and archive before relying on reports live.

Existing custom installations should review incoming action IDs and settings in the import preview. Updating a matching action can replace its settings; a separate unmatched older tracker can remain active and double-count. Keep only one active set of trackers. The public package does not erase old globals or uninstall old actions.

## Event connections

Attendance Tracker receives First Words, Present Viewers, Follow, Cheer, Subscription, Resubscription, Gift Subscription, Gift Bomb, and Raid. Connect your optional Attendance Check reward to it separately.

On First Words it records attendance, then calls Bot Welcomer immediately. Bot Welcomer deliberately has no independent First Words trigger; that prevents a duplicate greeting and ensures it reads the recorded attendee.

The individual chat/follow/Bits/raid/sub trackers remain responsible for their own analytics. Attendance ignores anonymous support and gifted-recipient notifications. Support-event attendance checks the broadcaster's live status through Twitch; test-mode events are ignored by the attendance handler.

## Commands

| Command | Purpose |
| --- | --- |
| `!bite`, `!bonk` | Attendance-aware interactions |
| `!hug`, `!hstats`, `!hboard` | Hugs interaction, stats, leaderboard |
| `!pat`, `!pstats`, `!pboard` | Pats interaction, stats, leaderboard |
| `!fstats` | FIRST! statistics |
| `!wreport [date]` | Week containing the date; defaults to the newest archived stream's week |
| `!mreport [month]` | Current month by default; accepts `previous` or `yyyy-MM` |
| `!yreport [year]` | Current year by default; accepts `previous` or `yyyy` |
| `!resetattendance` | Clear only the session roster; broadcaster only |
| `!resetfirst` | Clear complete FIRST! history; broadcaster only |
| `!resethugs`, `!resetpats` | Clear the corresponding complete interaction history; broadcaster only |

FIRST! itself is a reward action. Reset Variables is a manual maintenance action with no public command. The optional add-on supplies `!check` separately.

## Reports, retention, and the GUI

Reports use the installing account's Documents folder:

| Output | Location beneath Documents |
| --- | --- |
| Stream text summary | `StreamSummaries/Summary_yyyy-MM-dd_HH-mm-ss.txt` |
| Companion stream JSON | `StreamSummaries/json/Summary_yyyy-MM-dd.json` |
| Period reports | `StreamSummaries/Weekly Reports`, `Monthly Reports`, `Yearly Reports` |

The date-only companion JSON holds the latest saved summary for that date; timestamped text summaries and archived stream records retain the individual sessions. Wine resolves Documents through its configured prefix.

The report JSON property names and data model remain compatible with the migrated build, including `StreamCategory`, `SubsPerUserDetailed`, nested `Attendance`, and `RetentionRate`. Archived records keep `Category`. Only the globals holding those values changed names; an external GUI that reads globals directly must use the supplied mapping. A file-based GUI does not need a field rename. No standalone GUI application is bundled.

True Retention is the fraction of the previous eligible attendee roster that returns. Period reports compare unique rosters for their respective periods. It is not the ratio of two attendance totals. Preserve the archive for useful comparisons.

## Upgrading and preserving history

`Upgrade Tools/UPGRADE.md` describes previewing and copying the 44 mapped globals. The utility preserves types, copies persisted/temporary stores separately, leaves the old keys intact, skips equal destinations, and stops before any copy if a destination conflicts. It never guesses which history is newer.

All normal operation uses `streamfolk.analytics.*`; there are no legacy-key writes or automatic legacy-key fallbacks. The mapping and manual utility intentionally contain old names. The older monthly archive *format* fallback remains under `streamfolk.analytics.monthlyHistory`.

## Troubleshooting

- **Compilation errors:** use 1.0.7 stable, check the References list with Find Refs, and read your platform notes. Include System/System.Core plus Newtonsoft.Json, and System.Net.Http for Attendance Tracker. System.Net namespaces are supplied by System.dll; an extra System.Net.dll is not required.
- **No start/end report:** enable the actions, verify their Stream Online/Offline triggers and connected broadcaster, and inspect Action History/logs. End must execute its summary module before its archive module.
- **Duplicate welcome:** keep only the Attendance Tracker First Words route; remove a manually added independent trigger from Bot Welcomer.
- **Duplicate counters:** disable the older duplicate tracker/action set. Do not import both platform packages.
- **Missing quiet attendee:** Present Viewers is an incomplete observable signal. An Attendance Check reward provides a manual fallback.
- **Interrupted stream:** preserve existing globals/reports before clearing or merging anything. Reset Variables is a manual per-session clear; it does not merge data or recover a lost report.
- **No Stream Deck output:** configure each Status Indicator ID and enable both the action and timer. Empty IDs intentionally produce no button updates.

## Verification

See `../VERIFICATION_REPORT.md` at the release root. All 32 operational modules plus the copy utility compiled against .NET Framework 4.8 reference assemblies with documented CPH test doubles. Simulations passed 1,432 assertions; report model declarations match the working migration.

This package was assembled and checked programmatically. It has not been imported into a native Windows/Wine Streamer.bot instance in this environment, and live Twitch, Stream Deck, pinning, and SMS services were not exercised here. Complete the local import/compile and short-stream check above. Linux/Wine remains experimental according to Streamer.bot.

## Official references

- [Stable release and changelog](https://streamer.bot/changelogs/v1.0.7)
- [Streamer.bot downloads](https://streamer.bot/downloads)
- [Import and export](https://docs.streamer.bot/guide/core/import-export)
- [Linux/Wine installation](https://docs.streamer.bot/get-started/installation/linux)
- [Connected broadcaster API](https://docs.streamer.bot/api/csharp/methods/twitch/user/twitch-get-broadcaster)
- [Pinned-message duration API](https://docs.streamer.bot/api/csharp/methods/twitch/chat/twitch-update-pinned-message-duration)
