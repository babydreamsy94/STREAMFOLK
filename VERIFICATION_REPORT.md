# StreamFolk v5.0 — Verification Report

Generated: September 29, 2026

## Result

**172/172 automated package checks passed. 0 failed.**

These checks validate the generated Streamer.bot export envelope/JSON, object counts and IDs, embedded/readable source parity, platform reference transforms, C# lexical structure, and the presence of the audited safeguards. They are static/package verification; they do not replace importing into Streamer.bot v1.0.7 and running live Twitch event tests.

## Release invariants

- Main package: 29 actions, 16 commands, 2 queues, 1 timer.
- Optional Diaper Check: one action and one command.
- Original v5 action/command/queue/timer IDs preserved.
- Windows and Linux/Wine packages share the same application logic; Linux changes only platform-oriented package metadata/reference paths.
- All readable C# copies exactly match the source embedded in their corresponding generated `.sb` file.

## Audit checks

- PASS — Windows: main counts = 29 actions / 16 commands / 2 queues / 1 timer
- PASS — Windows: main IDs preserved
- PASS — Windows: addon IDs preserved
- PASS — Linux: main counts = 29 actions / 16 commands / 2 queues / 1 timer
- PASS — Linux: main IDs preserved
- PASS — Linux: addon IDs preserved
- PASS — v5 Attendance retains fail-closed JSON update path
- PASS — Bits: malformed persisted dictionary fails closed
- PASS — Chat: malformed persisted dictionary fails closed
- PASS — Follow: malformed persisted dictionary fails closed
- PASS — BONK/Hugs/Pats affected commands: arguments are guarded
- PASS — Hugs/Pats stats and leaderboards: stripped/full `rawInput` parsing supported
- PASS — Gift subs: no synthetic gifter subscription
- PASS — Gift subs: gifter attribution retained
- PASS — Gift bombs: child Gift Subscription events skipped
- PASS — Stream End: missing/invalid start gives zero duration
- PASS — Stream End: legacy synthetic gift slices excluded
- PASS — SMS: disabled by default
- PASS — SMS: 10-minute code-side cooldown
- PASS — SMS: cooldown refund path
- PASS — Diaper Check: arguments guarded, case-insensitive attendance, corrupt SeenUsers fails closed, dual-shape target parsing
- PASS — No remaining unguarded direct `args["user"]` casts in main scripts

## SHA-256

- **Windows main:** `0151675d66ad063c6313d460734aa7d383338dea3c158a9a985299b7688addab`
- **Windows addon:** `9e184b40617aaa91c5236bd89223eafb70e8aa2d152050355f7ea7741a7298d0`
- **Linux main:** `d3bccc903d5384b88af48887c98e10ae178ae2d60e30d2a55a81318a775ddf3b`
- **Linux addon:** `3f750756f5c57fbdf20a17b7794200d0cc7fe22265840284b2da6bcea41696d5`

## Required final runtime rehearsal

Before publishing or replacing a production StreamFolk installation, import the appropriate package into a backed-up Streamer.bot v1.0.7 stable instance and exercise:

- Present Viewers + First Words/Bot Welcomer
- Bits, chat, follow, raid, subscription, individual gift, and gift-bomb triggers
- Hugs/Pats/BITE/BONK command inputs with and without targets
- Stream Start → Stream End normal flow and Stream End without a valid start timestamp
- Send Streamer a Text with the feature disabled, configured, and within the safety cooldown
- Optional Diaper Check with mixed-case target names
- Saved report generation and subsequent weekly/monthly/yearly aggregation

That rehearsal is the remaining environment-dependent validation because Streamer.bot's CPH runtime and Twitch event payloads are not executable inside this packaging environment.
