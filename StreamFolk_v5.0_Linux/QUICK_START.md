# StreamFolk v5.0 — Linux quick start

Built By Streamers. Powered by Community.

**Use Streamer.bot 1.0.7 stable.** Download it from https://streamer.bot/downloads. Use only this platform package in one installation.

1. Back up Streamer.bot and its data, go offline, disconnect Twitch, and stop old StreamFolk actions/timers.
2. Existing pre-namespace installation: read [Upgrade Tools/UPGRADE.md](Upgrade%20Tools/UPGRADE.md) and preview/copy your globals first. Fresh or already-migrated installation: skip copying.
3. Import `Main Package/StreamFolk_5.0_Main_Package_Linux.sb`. Expect **30 actions, 16 commands, 2 queues, and 1 disabled timer**. There is no auto-run action.
4. Connect your broadcaster/bot accounts. In the source exclusion arrays, replace `botname` with your separate bot login or remove it. Review messages and compile each Execute C# sub-action. Read `PLATFORM_LINUX.txt` for references.
5. Enable Twitch Present Viewers Live Update. Choose its update interval in Streamer.bot. Attendance Check is a manual fallback; bind only your own Attendance Check reward to Attendance Tracker.
6. For FIRST!, create a reward and bind it to FIRST! Tracker. Creator reward IDs are intentionally absent.
7. Review Start and End, then enable both. They save/report the session and post chat messages; `ManageChatModes=false` leaves chat modes alone. If wanted, enable chat management in both scripts after reviewing its behavior.
8. Enable the community commands you want. The four reset commands enforce the connected broadcaster in code; report commands should stay restricted to the broadcaster/trusted moderators.
9. Optional Stream Deck: enter all wanted Status Indicator IDs, enable Stream Deck Stats, then enable its three-second timer. Optional SMS: configure it privately; both SMS switches remain off otherwise.
10. Reconnect and test a short stream. Confirm one welcome, one entry per attendee, accurate chat counts, and the text/JSON summary under Documents/StreamSummaries. Generate a period report from the resulting archive.

Bot Welcomer is already called after attendance on First Words. Do not add another First Words trigger to it. Support events are connected to both their analytics tracker and the attendance handler; the recipient of a gifted sub is not counted only because of the gift.

Diaper Check is a separate, disabled optional add-on under `Optional Packages/Diaper Check`. The main package works without it.

The detailed [README](README.md), feature summaries, and release-root verification report describe behavior, defaults, migration, and testing limits. An import over an existing action can replace personal settings; retain your backup and inspect the preview.
