# Claude Usage Tray

A small Windows tray app that shows your Claude plan limits (session % and weekly %, the same numbers as claude.ai and `/usage`), plus Claude Code token and cost figures from your local logs.

## Build

It uses the C# compiler that ships with Windows (.NET Framework 4.x), so there's no SDK to install:

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

The output is `bin\ClaudeUsageTray.exe`.

## Use

Run `bin\ClaudeUsageTray.exe`. The badge may start in the tray overflow (`^`); drag it onto the taskbar to keep it visible.

- **Badge**
  - By default it shows **how much of your current session limit you've used**, e.g. `20`. It turns red at 90% or more.
  - Right-click → **Tray icon shows** to show one of these instead:
    - weekly limit %;
    - session time elapsed %;
    - today's Claude Code cost or tokens.
- **Left-click** opens a panel showing:
  - the session and weekly limits as progress bars, with their reset times;
  - how much of the 5-hour session has passed;
  - Claude Code tokens and estimated cost for this session, today, the last 7 days, this month, and today by model.
- **Refreshing**
  - Plan limits are fetched every 2 minutes, or right away when you click **Refresh**.
  - Local log figures update a few seconds after Claude Code writes to its logs.

## Where the data comes from

- **Plan limits**
  - The app uses the login Claude Code already saved in `%USERPROFILE%\.claude\.credentials.json` to call `https://api.anthropic.com/api/oauth/usage`. This is the endpoint behind `/usage`.
  - Anthropic doesn't document this endpoint, so it may change.
  - The file is only read, never written. The token is sent only to `api.anthropic.com` and is never logged or stored.
  - The app does **not** refresh an expired login, because that would rotate Claude Code's own credentials. If the panel says the login expired, open Claude Code and the app picks up the refreshed login on its next poll.
- **Local figures**
  - They are read from `%USERPROFILE%\.claude\projects\**\*.jsonl`.
  - They only cover Claude Code. Chats on claude.ai and in the desktop or mobile apps count toward your limits, but they don't appear in these figures.
  - Cost is an estimate at API list prices (`src/Pricing.cs`). You can override the prices with a `pricing.json` next to the exe:
    `{ "claude-opus-5": { "input": 5, "output": 25, "cacheRead": 0.5 } }`
- **System changes:** the app doesn't touch the registry and doesn't start with Windows.
