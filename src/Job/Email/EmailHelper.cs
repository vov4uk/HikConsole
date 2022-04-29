﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Mail;
using System.Text;
using Hik.Api;
using Newtonsoft.Json;
using NLog;

namespace Job.Email
{
    [ExcludeFromCodeCoverage]
    public class EmailHelper : IEmailHelper
    {

        static EmailConfig Settings { get;}
        static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        static EmailHelper()
        {
#if RELEASE
            string configPath = Path.Combine(Environment.CurrentDirectory, "email.json");
            Settings = JsonConvert.DeserializeObject<EmailConfig>(File.ReadAllText(configPath));
#endif
        }

        public void Send(Exception ex, string alias = null, string hikJobDetails = null)
        {
            string errorDetails = string.Empty;
            if (ex is HikException hikEx)
            {
                errorDetails = $@"<ul>
  <li>Error Code : {hikEx.ErrorCode}</li>
  <li>{hikEx.ErrorMessage}</li>
</ul>";
            }

            var msg = BuildBody(errorDetails, hikJobDetails, ex.Message, ex.ToString());
            var subject = $"{alias ?? "Hik.Web"} {(ex as HikException)?.ErrorMessage ?? ex.Message}".Replace('\r', ' ').Replace('\n', ' ');
            Send(subject, msg);
        }

        public void Send(string subject, string body)
        {
            try
            {
#if DEBUG
                Logger.Error(body);
#elif RELEASE
                if (Settings != null)
                {
                    using var mail = new MailMessage
                    {
                        From = new MailAddress(Settings.UserName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true,
                    };

                    mail.To.Add(Settings.Receiver);
                    using var smtp = new SmtpClient(Settings.Server, Settings.Port)
                    {
                        Credentials = new System.Net.NetworkCredential(Settings.UserName, Settings.Password),
                        EnableSsl = true,
                    };
                    smtp.Send(mail);
                }
#endif
            }
            catch (Exception e)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Failed to Sent Email");
                sb.AppendLine($"Local exception {e}");
                sb.AppendLine($"With subject : {subject} and body : {body}");
                Logger.Error(sb.ToString());
            }
        }

        private static string BuildBody(string details, string hikJobDetails, string message, string callStack)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
<style>
table {{
      font - family: arial, sans-serif;
  border-collapse: collapse;
  width: 100%;
}}

td, th {{
      border: 1px solid #dddddd;
  text-align: left;
  padding: 8px;
}}

tr:nth-child(even) {{
      background - color: #dddddd;
}}
</style>
</head>
<body>

{hikJobDetails}

{details}

<h2>Call stack</h2>
<p>{message}</p>

<pre>
{callStack}
</pre>
</body>
</html>";
        }
    }
}