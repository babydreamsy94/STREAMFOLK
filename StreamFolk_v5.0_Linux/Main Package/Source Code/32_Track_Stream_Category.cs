/*
███████╗████████╗██████╗ ███████╗ █████╗ ███╗   ███╗     ███████╗██╗   ██╗██╗████████╗███████╗
██╔════╝╚══██╔══╝██╔══██╗██╔════╝██╔══██╗████╗ ████║     ██╔════╝██║   ██║██║╚══██╔══╝██╔════╝
███████╗   ██║   ██████╔╝█████╗  ███████║██╔████╔██║     ███████╗██║   ██║██║   ██║   █████╗
╚════██║   ██║   ██╔══██╗██╔══╝  ██╔══██║██║╚██╔╝██║     ╚════██║██║   ██║██║   ██║   ██╔══╝
███████║   ██║   ██║  ██║███████╗██║  ██║██║ ╚═╝ ██║     ███████║╚██████╔╝██║   ██║   ███████╗
╚══════╝   ╚═╝   ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝     ╚══════╝ ╚═════╝ ╚═╝   ╚═╝   ╚══════╝

==============================================================================
TRACK CURRENT CATEGORY - FEATURE SUMMARY
==============================================================================

==============================================================================
🧠 PURPOSE
==============================================================================
Stores the stream's current Twitch category and title so completed stream
records can include that information in weekly reports and future historical
reports.

This action helps StreamFolk identify which category and stream title were
active when a completed stream was archived.
==============================================================================
🔄 SYSTEM FLOW
==============================================================================
1. Runs when the Twitch stream goes online or when the stream information is
   updated.
2. Searches the available Streamer.bot arguments for the current category.
3. Saves the detected category to streamfolk.analytics.currentCategory.
4. Searches the available arguments for the current stream title.
5. Saves the detected title to streamfolk.analytics.currentStreamTitle.
6. Leaves the previously saved value unchanged when Streamer.bot does not
   supply a replacement value.
==============================================================================
📊 VARIABLE REFERENCE
==============================================================================
GLOBALS WRITTEN

streamfolk.analytics.currentCategory
- Stores the most recently detected Twitch category.

streamfolk.analytics.currentStreamTitle
- Stores the most recently detected Twitch stream title.

SUPPORTED CATEGORY ARGUMENTS

- gameName
- game
- categoryName
- category

SUPPORTED TITLE ARGUMENTS

- status
- title
- streamTitle
==============================================================================
🎨 USER CUSTOMIZATION
==============================================================================
- No required customization is needed for normal use.

- Attach this action to both:
  - Twitch > Channel > Stream Online
  - Twitch > General > Stream Update

- Keep the global-variable names unchanged unless the Archive Completed Stream
  script is also updated to use the same replacement names.

- Additional argument names can be added to GetFirstArg if a future
  Streamer.bot version supplies the category or title under a different key.

- The wording of log messages can be customized without changing the tracking
  logic.
==============================================================================
⚠️ TROUBLESHOOTING
==============================================================================
- If a weekly report shows "Unknown Category", confirm this action ran before
  Stream End.

- Confirm that the action has both the Stream Online and Stream Update
  triggers.

- Check the Streamer.bot log for the warning that no category argument was
  supplied.

- A missing category does not erase the previously saved category.

- If the category changes during the broadcast, the completed stream record
  uses the most recently detected category and title.

*/
using System;

public class CPHInline
{
    private const string CurrentCategoryKey = "streamfolk.analytics.currentCategory";
    private const string CurrentTitleKey = "streamfolk.analytics.currentStreamTitle";

    public bool Execute()
    {
        string category = GetFirstArg("gameName", "game", "categoryName", "category");
        string title = GetFirstArg("status", "title", "streamTitle");

        if (!string.IsNullOrWhiteSpace(category))
        {
            category = category.Trim();
            CPH.SetGlobalVar(CurrentCategoryKey, category, true);
            CPH.LogInfo("StreamFolk Monthly Report: Current category saved as '" + category + "'.");
        }
        else
        {
            CPH.LogWarn("StreamFolk Monthly Report: No category argument was supplied. The previously saved category was left unchanged.");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            title = title.Trim();
            CPH.SetGlobalVar(CurrentTitleKey, title, true);
        }

        return true;
    }

    private string GetFirstArg(params string[] keys)
    {
        if (args == null)
            return null;

        foreach (string key in keys)
        {
            if (!args.ContainsKey(key) || args[key] == null)
                continue;

            string value = args[key].ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
