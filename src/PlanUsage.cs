using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace ClaudeUsageTray
{
    class Limit
    {
        public double Percent;
        public DateTime? ResetsAt;   // local time; null when the window hasn't started

        // Percentage as of `now`: once the reset time has passed the window is empty,
        // even if the next poll hasn't happened yet.
        public double PercentAt(DateTime now)
        {
            return ResetsAt.HasValue && ResetsAt.Value <= now ? 0 : Percent;
        }

        public bool IsRunning(DateTime now)
        {
            return ResetsAt.HasValue && ResetsAt.Value > now;
        }

        // How much of a window of the given length has passed, 0..100; -1 when none is running.
        public double ElapsedPercent(DateTime now, TimeSpan length)
        {
            if (!IsRunning(now)) return -1;
            return Math.Max(0, 100 - (ResetsAt.Value - now).TotalMinutes / length.TotalMinutes * 100);
        }
    }

    class PlanUsage
    {
        public static readonly TimeSpan SessionLength = TimeSpan.FromHours(5);

        public Limit Session, Weekly;
        public DateTime Fetched;
    }

    // Reads the plan-limit percentages Claude shows in /usage and on claude.ai.
    // Authenticates with the login Claude Code already saved in .credentials.json: the file is
    // re-read on every poll and only ever read, and the token is only sent to api.anthropic.com.
    // Expired tokens are NOT refreshed here, because refreshing rotates Claude Code's own login.
    // Not thread-safe; the caller polls from one worker at a time.
    class PlanUsageClient
    {
        const string Url = "https://api.anthropic.com/api/oauth/usage";
        static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

        readonly string credentialsPath;
        DateTime nextPoll = DateTime.MinValue;

        public PlanUsage Last { get; private set; }
        public string Error { get; private set; }

        public PlanUsageClient(string claudeConfigDir)
        {
            credentialsPath = Path.Combine(claudeConfigDir, ".credentials.json");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public void Poll(bool force)
        {
            DateTime now = DateTime.Now;
            if (!force && now < nextPoll) return;
            nextPoll = now + PollInterval;
            try
            {
                Last = Fetch();
                Error = null;
            }
            catch (PlanUsageException ex)
            {
                Error = ex.Message;
                if (ex.RetryAfter > PollInterval) nextPoll = now + ex.RetryAfter;
            }
        }

        PlanUsage Fetch()
        {
            string token = ReadToken();
            string body;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(Url);
                req.Headers["Authorization"] = "Bearer " + token;
                req.Headers["anthropic-beta"] = "oauth-2025-04-20";
                req.Accept = "application/json";
                req.UserAgent = "claude-usage-tray";
                req.Timeout = 15000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    body = reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp == null) throw new PlanUsageException("Can't reach Anthropic (offline?)");
                int code = (int)resp.StatusCode;
                if (code == 401 || code == 403)
                    throw new PlanUsageException("Claude login expired: open Claude Code to refresh it");
                if (code == 429)
                    throw new PlanUsageException("Rate limited; retrying in 5 min", TimeSpan.FromMinutes(5));
                throw new PlanUsageException("Usage request failed (HTTP " + code + ")");
            }

            var json = new JavaScriptSerializer().DeserializeObject(body) as Dictionary<string, object>;
            if (json == null) throw new PlanUsageException("Unexpected usage response");
            var usage = new PlanUsage();
            usage.Session = ParseLimit(json, "five_hour");
            usage.Weekly = ParseLimit(json, "seven_day");
            usage.Fetched = DateTime.Now;
            if (usage.Session == null) throw new PlanUsageException("Unexpected usage response");
            return usage;
        }

        string ReadToken()
        {
            if (!File.Exists(credentialsPath))
                throw new PlanUsageException("Not logged in to Claude Code (no saved login found)");
            Dictionary<string, object> oauth;
            try
            {
                var json = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(credentialsPath)) as Dictionary<string, object>;
                object o;
                oauth = json != null && json.TryGetValue("claudeAiOauth", out o) ? o as Dictionary<string, object> : null;
            }
            catch (Exception)
            {
                throw new PlanUsageException("Couldn't read the saved Claude Code login");
            }
            object token, expiresAt;
            if (oauth == null || !oauth.TryGetValue("accessToken", out token) || !(token is string))
                throw new PlanUsageException("Not logged in to Claude Code with a Claude account");
            if (oauth.TryGetValue("expiresAt", out expiresAt) &&
                DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(expiresAt)) <= DateTimeOffset.UtcNow)
                throw new PlanUsageException("Claude login expired: open Claude Code to refresh it");
            return (string)token;
        }

        static Limit ParseLimit(Dictionary<string, object> json, string key)
        {
            object o;
            var d = json.TryGetValue(key, out o) ? o as Dictionary<string, object> : null;
            if (d == null) return null;
            var limit = new Limit();
            object v;
            if (d.TryGetValue("utilization", out v) && v != null) limit.Percent = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            DateTimeOffset resets;
            if (d.TryGetValue("resets_at", out v) && v is string &&
                DateTimeOffset.TryParse((string)v, CultureInfo.InvariantCulture, DateTimeStyles.None, out resets))
            {
                // Resets jitter around the minute (10:39:59.67, 10:40:00.31, ...);
                // round to the nearest minute like Claude's UI (4:10 PM).
                DateTime local = resets.LocalDateTime.AddSeconds(30);
                limit.ResetsAt = local.AddTicks(-(local.Ticks % TimeSpan.TicksPerMinute));
            }
            return limit;
        }
    }

    class PlanUsageException : Exception
    {
        public readonly TimeSpan RetryAfter;

        public PlanUsageException(string message) : this(message, TimeSpan.Zero) { }

        public PlanUsageException(string message, TimeSpan retryAfter) : base(message)
        {
            RetryAfter = retryAfter;
        }
    }
}
