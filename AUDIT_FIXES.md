# StreamFolk v5.0 — Independent Audit Resolution

This document records how the September 29, 2026 independent v4.1 audit was
handled when preparing the newer StreamFolk v5.0 package.

The audit was **not applied mechanically**. Each finding was re-checked against
the v5 blank-slate source first, because v5 already contains a newer attendance
pipeline and other changes that were not present in the audited v4.1 tree.

| # | Audit finding | v5 disposition |
|---|---|---|
| 1 | Corrupt JSON can be overwritten by empty dictionaries | **Addressed where applicable.** v5 Attendance already failed closed. Bits, Chat, Follow, Hugs/Pats and related writers were hardened so a failed load is never saved over. |
| 2 | Unguarded `args["user"]` casts | **Addressed.** BONK/Hugs/Pats affected scripts use safe lookup/fallbacks. |
| 3 | Unhandled JSON exceptions | **Addressed.** Read-only commands log/skip; writers fail closed rather than resetting. |
| 4 | Command target parsing | **Addressed with corrected scope.** Hugs and Pats stats/leaderboards are fixed for stripped `rawInput`; dual-shape parsing also supports manual/full-message input. The v4.1 audit's claim that normal PAT/Diaper input includes the command word was not adopted. |
| 5 | Diaper Check case mismatch | **Addressed.** Target and stored keys are normalized/case-insensitive and JSON load is guarded. |
| 6 | Gift subs double-counted | **Addressed.** Actual subscriptions are counted once; gifter attribution is kept in detailed gift labels. Gift-bomb child gift events are skipped by the individual handler. |
| 7 | Absurd duration without Stream Start | **Addressed.** Invalid/missing/future start time produces 0 minutes plus a warning. |
| 8 | Send Streamer a Text spam vector | **Defense-in-depth added.** The feature remains a disabled-by-default channel-point reward, not an unrestricted chat command. A 10-minute code cooldown and redemption refund path were added. |
| 9 | Dead GitHub Actions updater workflow | **Repository fix.** The obsolete workflow is absent from the v5 branch because the UPDATER project no longer exists. |
| 10 | Broken root README links | **Repository fix.** The v5 README points to the v5 release/download structure instead of dead v4.1 root-relative paths. |

## Additional issue found during the v5 pass

The Pats leaderboard/stats scripts had the same stripped-`rawInput` indexing
pattern identified in the Hugs equivalents. They are corrected in v5 as well.

## Data-preservation rule used in this release

For any persisted dictionary that an action may later save:

1. Missing/blank storage is a valid empty state.
2. Malformed nonblank JSON is **not** treated as an empty state.
3. The action logs the failure and skips the event.
4. It does not overwrite the malformed persisted value.

That preserves the original data for troubleshooting/recovery instead of turning
a recoverable corruption incident into permanent data loss.
