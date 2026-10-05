# Optional Diaper Check

This add-on is separate from the main analytics package and intended for consenting adult communities where its theme fits. The main package works without it.

1. Install/configure the main Linux package first.
2. Import `StreamFolk_5.0_Optional_Diaper_Check_Linux.sb` from this folder.
3. Review the response text and compile its C# sub-action.
4. Enable the Diaper Check action and `!check` command when ready. Both start disabled.

The add-on reads `streamfolk.analytics.seenUsers`; it needs the v5 main package or the equivalent migrated attendance system. No legacy `SeenUsers` fallback is used. It shares the main analytics queue; importing it preserves that queue's ID.
