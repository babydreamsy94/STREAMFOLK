# Upgrade to the StreamFolk v5.0 namespace

**Fresh install, or already using `streamfolk.analytics.*`? Skip the copy utility.** Import/configure the main package normally. Do not copy stale pre-migration values over an active namespaced installation.

For an older installation still using `SeenUsers`, `AttendanceHistory`, `analytics.*`, or the other old keys:

1. Finish the stream. Close Streamer.bot and back up its whole folder, including saved data. Action exports alone do not contain the live stored histories/counters.
2. Reopen with Twitch disconnected. Stop StreamFolk actions, timers and pending work so none can write while copying.
3. Import this folder's `StreamFolk_5.0_Copy_Existing_Data_Windows.sb` (use the filename matching your platform).
4. In the one-time migration group, run **PREVIEW existing-data namespace migration** manually. Review the log. Preview does not write.
5. If there are no conflicts, run **COPY existing-data to streamfolk.analytics**. Continue only when the log reports COMPLETE. Missing old keys are normal for unused features.
6. Import the platform's main v5.0 package. Update matching actions after reviewing settings; disable any older unmatched duplicates. Command/action/timer IDs are preserved where carried forward, but public defaults replace personal configuration if you overwrite matching actions.
7. Compile, configure, reconnect, and test. Disable or remove the two one-time utility actions afterwards.

The utility handles 44 explicitly mapped names. It copies persisted and temporary stores separately, retains JSON strings/counters/native dictionaries as supplied, leaves the old values intact, and skips equal destinations. **A differing destination stops preflight before any copy.** It never merges conflicting histories. A failed partial copy can be rerun with all tracking still stopped.

If an old and new value differ, preserve both and investigate; do not delete a newer destination to force a copy. Do not rerun copying after normal v5 tracking has resumed.

The main export contains definitions/configuration, not saved community history. Importing it does not itself clear or migrate your saved globals.

For rollback before resuming tracking, restore the backed-up action configuration or complete backup. After live use, the old keys are only a snapshot; reverting to them omits activity since migration. Preserve the current data and reconcile it before rolling back.

`VARIABLE-MAPPING.md`, CSV, and JSON list every old/new name. External scripts or GUIs that read globals directly need those replacements. File-based report fields are unchanged.

Source note: `Source Code/Copy_Existing_Globals.cs` contains both Preview() and Execute(). The imported PREVIEW action uses the same code with Execute() routed to Run(false); COPY routes Execute() to Run(true).
