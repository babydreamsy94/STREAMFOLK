<img width="1920" height="1080" alt="StreamFolk" src="https://github.com/user-attachments/assets/2def0f20-cba3-4a39-8781-887a7eb66a3b" />

# STREAMFOLK v5.0

> **Built By Streamers. Powered by Community.**
>
> Created by **[babydreamsy](https://www.twitch.tv/babydreamsy)**

[![Version](https://img.shields.io/badge/version-5.0.0-856ed6)](https://github.com/babydreamsy94/STREAMFOLK/releases)
[![Streamer.bot](https://img.shields.io/badge/Streamer.bot-v1.0.7%20stable-01d7fb)](https://streamer.bot/downloads)
[![Platform](https://img.shields.io/badge/platform-Twitch-9146FF)](https://www.twitch.tv/)
[![Download](https://img.shields.io/badge/download-100%25%20FREE-2ea44f)](https://github.com/babydreamsy94/STREAMFOLK/releases)

StreamFolk is a community-focused Streamer.bot system for Twitch attendance, participation, support tracking, retention, stream summaries, and long-term reports. It is designed to complement Twitch's official analytics rather than replace platform-controlled financial, payout, advertising, or concurrent-viewer data.

## What's new in v5.0

- **Present Viewers attendance** can record identifiable quiet attendees without requiring them to chat.
- **Bot Welcomer is separate from attendance**, so background attendance can remain silent until a viewer reaches First Words.
- **Community support can confirm attendance while live**, including follows, Bits/Cheers, subscriptions, resubscriptions, gift-sub gifters, gift bombs, and raids.
- **Gift recipients are not automatically counted as attendees** merely because somebody gifted them a subscription.
- **Gift-sub reports retain gifter attribution** while counting each actual subscription only once.
- **JSON-backed analytics are fail-closed** in the audited trackers: malformed persisted data is logged and preserved instead of being overwritten by a fresh empty dictionary.
- **Hugs, Pats, BONK, stats, and leaderboard parsing is hardened** for Streamer.bot's Starts-With `rawInput` behavior and manual/test-trigger input.
- **Stream End guards invalid session start data** instead of producing a year-1 duration.
- **Send Streamer a Text remains disabled by default** and now includes a code-side safety cooldown/refund path in addition to reward-side controls.
- **Optional Diaper Check is case-insensitive and corruption-safe** when reading `SeenUsers`.

See **[AUDIT_FIXES.md](./AUDIT_FIXES.md)** for the independent-audit disposition and **[VERIFICATION_REPORT.md](./VERIFICATION_REPORT.md)** for package verification.

## Requirements

- **Streamer.bot v1.0.7 stable**
- A Twitch broadcaster account connected to Streamer.bot
- An active internet connection for Twitch/EventSub/API features
- A backup of your current Streamer.bot setup before importing or overwriting actions

Windows is the primary native target. A separate Linux/Wine build is prepared with Wine-oriented .NET Framework reference paths; Streamer.bot under Wine should still be treated as experimental and tested in the destination prefix.

## Core features

- Attendance Tracker using Present Viewers, First Words, support events, raids, and an optional Attendance Check reward
- Returning/new attendee summaries and true attendee-overlap retention
- Chat message, Bits, follow, raid, subscription, gift subscription, gift bomb, and category tracking
- Stream Start / Stream End protocols and saved per-stream summaries
- Weekly, monthly, and yearly reports
- FIRST! tracker, stats, streaks, and milestones
- Hugs and Pats trackers, personal stats, and leaderboards
- BITE! and BONK! attendance-aware community commands
- Stream Deck live stats
- Broadcaster reset utilities
- Optional Send Streamer a Text integration
- Separate optional Diaper Check add-on

## Attendance model

`SeenUsers` remains the authoritative current-stream attendance record. StreamFolk attendance means identifiable community presence/activity that Streamer.bot can observe; it is **not** Twitch's concurrent viewer count and cannot identify every anonymous or logged-out viewer.

A viewer can enter attendance through Present Viewers or another supported event and stay counted for that session even if they later disappear from the current viewer list. Bot Welcomer handles visible First Words welcomes separately.

## Audit hardening

The September 29, 2026 independent v4.1 review was re-checked against the newer v5 source before changes were applied. Findings that were real were fixed; recommendations based on an incorrect `rawInput` assumption were not copied mechanically. The v5 package also fixes the same stripped-input issue in Pats stats/leaderboard that the original audit did not call out.

The package verification pass preserves the original v5 action, command, queue, and timer IDs and validates that readable C# copies match the code embedded in the generated Streamer.bot imports.

## Download

StreamFolk releases are distributed through **GitHub Releases**:

**https://github.com/babydreamsy94/STREAMFOLK/releases**

The v5 release package contains Windows and Linux/Wine imports, readable C# source, setup guides, feature summaries, the audit resolution, and verification report.

## Safety and privacy

The public package is blank-slate. Do not commit configured email addresses, App Passwords, SMS gateways, phone information, local private paths, channel-point reward IDs, Stream Deck identifiers, or private analytics histories back to the public repository.

Back up Streamer.bot before upgrades and test Stream Start/End, support events, gift handling, attendance, reports, and any optional integrations before the first production stream.

---

**Built By Streamers. Powered by Community.**
