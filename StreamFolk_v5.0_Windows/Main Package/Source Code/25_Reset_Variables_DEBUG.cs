// SAFE RESET ACTION
// Clears ONLY per-stream variables.
// Does NOT touch streamfolk.analytics.attendanceHistory or any long-term analytics.

public class CPHInline
{
    public bool Execute()
    {
        // --- Per-stream attendance ---
        CPH.SetGlobalVar("streamfolk.analytics.seenUsers", "{}", true);

        // --- Chat analytics ---
        CPH.SetGlobalVar("streamfolk.analytics.chatMessagesByUser", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.chatTotalMessages", 0, true);

        // --- Subs analytics ---
        CPH.SetGlobalVar("streamfolk.analytics.subs", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.subsDetailed", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.totalSubs", 0, true);

        // --- Follows analytics ---
        CPH.SetGlobalVar("streamfolk.analytics.follows", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.totalFollows", 0, true);

        // --- Bits analytics ---
        CPH.SetGlobalVar("streamfolk.analytics.bits", "{}", true);
        CPH.SetGlobalVar("streamfolk.analytics.totalBits", 0, true);

        // --- Raids analytics ---
        CPH.SetGlobalVar("streamfolk.analytics.raids", "[]", true);

        // OPTIONAL: Clear final summary (remove if you want to keep last summary)
        // CPH.SetGlobalVar("streamfolk.analytics.finalSummaryJson", "{}", true);

        CPH.LogInfo("SAFE RESET COMPLETE: All per-stream variables cleared. streamfolk.analytics.attendanceHistory preserved.");
        CPH.SendMessage("/me 🔄 Safe Reset Complete! All per-stream data has been cleared without touching long-term attendance history.");

        return true;
    }
}
