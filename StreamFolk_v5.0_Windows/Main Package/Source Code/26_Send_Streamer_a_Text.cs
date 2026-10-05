using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;

public class CPHInline
{
    // This action is intentionally a Channel Point Reward action, not a public
    // chat command. Attach your own reward after configuring and enabling this optional action.
    private const bool SmsEnabled = false;
    private const string SenderGmailAddress = "";
    private const string GoogleAppPassword = "";
    private const string SmsGatewayAddress = "";
    private const string OptionalSoundPath = "";

    private const int MaximumMessageLength = 500;
    private const int CooldownMinutes = 10;
    private const string LastSentKey = "streamfolk.analytics.text.lastSentUtc";

    public bool Execute()
    {
        if (args == null)
        {
            CPH.LogWarn("StreamFolk Text: args is null; skipping.");
            return true;
        }

        if (!SmsEnabled)
        {
            CPH.LogWarn("StreamFolk Text: SMS is disabled. Configure the placeholders, then set SmsEnabled to true.");
            return true;
        }

        if (HasPlaceholder(SenderGmailAddress) ||
            HasPlaceholder(GoogleAppPassword) ||
            HasPlaceholder(SmsGatewayAddress))
        {
            CPH.LogWarn("StreamFolk Text: a required email-to-SMS placeholder has not been configured.");
            return true;
        }

        if (!CooldownElapsed())
        {
            CPH.LogInfo("StreamFolk Text: safety cooldown active; message not sent.");
            RefundRedemptionIfPossible();
            return true;
        }

        string smsBody = GetArg("rawInput");
        if (string.IsNullOrWhiteSpace(smsBody))
            return true;

        smsBody = smsBody.Trim().Replace("\r", " ").Replace("\n", " ");
        if (smsBody.Length > MaximumMessageLength)
            smsBody = smsBody.Substring(0, MaximumMessageLength);

        string sender = GetArg("userLogin");
        if (string.IsNullOrWhiteSpace(sender)) sender = GetArg("userName");
        if (string.IsNullOrWhiteSpace(sender)) sender = GetArg("user");
        if (string.IsNullOrWhiteSpace(sender)) sender = "a viewer";

        smsBody += "\n- Sent by " + sender.Trim();

        try
        {
            using (SmtpClient client = new SmtpClient("smtp.gmail.com", 587))
            using (MailMessage mail = new MailMessage())
            {
                client.Credentials = new NetworkCredential(SenderGmailAddress, GoogleAppPassword);
                client.EnableSsl = true;
                mail.From = new MailAddress(SenderGmailAddress);
                mail.To.Add(SmsGatewayAddress);
                mail.Body = smsBody;
                client.Send(mail);
            }

            CPH.SetGlobalVar(LastSentKey, DateTime.UtcNow.ToString("o"), true);

            if (!string.IsNullOrWhiteSpace(OptionalSoundPath) && File.Exists(OptionalSoundPath))
            {
                Thread.Sleep(4000);
                CPH.PlaySound(OptionalSoundPath, 0.5f);
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn("StreamFolk Text: message could not be sent. " + ex.Message);
        }

        return true;
    }

    private string GetArg(string key)
    {
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return null;
        return args[key].ToString();
    }

    private bool HasPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.IndexOf("YOUR_", StringComparison.OrdinalIgnoreCase) >= 0
            || value.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
    }

    private bool CooldownElapsed()
    {
        string lastSentStr = CPH.GetGlobalVar<string>(LastSentKey, true);
        DateTime lastSent;
        if (!DateTime.TryParse(lastSentStr, out lastSent))
            return true;
        return (DateTime.UtcNow - lastSent.ToUniversalTime()).TotalMinutes >= CooldownMinutes;
    }

    private void RefundRedemptionIfPossible()
    {
        string rewardId = GetArg("rewardId");
        string redemptionId = GetArg("redemptionId");
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(redemptionId))
            return;

        try
        {
            CPH.TwitchRedemptionCancel(rewardId, redemptionId);
        }
        catch
        {
            CPH.LogWarn("StreamFolk Text: cooldown redemption could not be refunded automatically.");
        }
    }
}
