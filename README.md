# Claude Usage Tray

A small Windows tray app that shows your Claude plan limits: the session % and weekly % shown on claude.ai and in Claude Code's `/usage`.

## Build

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

The output is `bin\ClaudeUsageTray.exe`, a single exe of about 25 KB.

### Nothing to install

The build uses the C# compiler that is already part of Windows: `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. It comes with .NET Framework 4.x, which every Windows 10 and 11 machine has.

- **No SDK, IDE or package manager.** You don't need the .NET SDK, Visual Studio, NuGet, Node or Python.
- **No third-party code.** The app only uses parts of .NET that come with Windows (WinForms, System.Drawing and the built-in JSON parser). There are no downloaded libraries whose code you'd have to trust.
- **Easy to audit.** A few hundred lines of C# in `src/` compile directly into the exe you run, with no packages or generated code in between.
- **No runtime to install.** The exe runs on the .NET Framework already in Windows.

The trade-off is that the code is limited to C# 5 syntax (for example no `$"..."` strings), and the UI is WinForms rather than WinUI.

### Windows SmartScreen warning

**If you build the exe yourself, Windows won't warn you when you run it.** If you download a pre-built copy, Windows will probably show "Windows protected your PC".

- **Why:** SmartScreen only checks files that carry the **Mark of the Web**, a hidden `Zone.Identifier` tag that browsers and email clients add to downloaded files.
  - An exe compiled on your own machine has no such tag, so Windows treats it as your own file.
  - A downloaded copy has the tag. It is also unsigned and unknown to Microsoft's reputation service, so SmartScreen blocks it until you click **More info → Run anyway**.
- **Recommended:** clone the repo and build it yourself. Git doesn't add the Mark of the Web, you get no warning, and you know the exe matches the source. That matters for an app that reads your Claude login.
- **If you downloaded the exe**, you can clear the tag by either:
  - right-click → Properties → tick **Unblock**, or
  - running `Unblock-File .\ClaudeUsageTray.exe`.

  Only do this if you trust where the file came from.
- Microsoft Defender antivirus still scans the exe either way; the Mark of the Web only controls the SmartScreen reputation prompt.

## Use

Run `bin\ClaudeUsageTray.exe`. The badge may start in the tray overflow (`^`); drag it onto the taskbar to keep it visible.

- **Badge:** by default it shows **how much of your current session limit you've used**, e.g. `25`. It turns red at 90% or more.
- **Hover** shows a tooltip with the session and weekly percentages and their reset times.
- **Left-click** opens a panel showing:
  - the session limit used, with its reset time and a time-elapsed bar;
  - the weekly limit used, with its reset time.
- **Right-click** for these menu items:
  - **Refresh now**;
  - **Tray icon shows**: session limit %, weekly limit % or session time elapsed %;
  - **Exit**.
- **Refreshing:** limits are fetched every 2 minutes, or right away when you click **Refresh**. Countdowns update every minute.

## Where the data comes from

- The app uses the login Claude Code already saved in `%USERPROFILE%\.claude\.credentials.json` to call `https://api.anthropic.com/api/oauth/usage`. This is the endpoint behind `/usage`.
- The numbers cover all your Claude use (claude.ai, desktop, mobile and Claude Code), because they come from Anthropic.
- If the panel says the login expired, open Claude Code. The app picks up the refreshed login on its next poll.

## Security & privacy

Your Claude login gives access to your account, so here is exactly what the app does with it. Each point can be checked against the source.

### It does not store your credentials
- `ReadToken()` in `src/PlanUsage.cs` reads the access token into a local variable for one request and then drops it. Nothing keeps it between polls; the file is read again each time.
- The app has no code that writes files, uses the registry, or logs anything, so the token is never saved, logged or shown on screen.
- Error messages are fixed text such as "Claude login expired", so the token can't leak through them.
- .NET can't wipe strings from memory, so the token stays in the app's memory until .NET cleans it up. That adds no real exposure, because any program running as you can already read `.credentials.json`.

### It only uses the login that's already on your computer
- It reads `%USERPROFILE%\.claude\.credentials.json`, or the same file in `%CLAUDE_CONFIG_DIR%` if that variable is set.
- Only `claudeAiOauth.accessToken` and `expiresAt` are used. The **refresh token is never read or used**.
- The app never asks you to type a token or password and doesn't look for credentials anywhere else.

### It only reads
- **On disk:** it reads one file, `.credentials.json`, in read-only mode. It doesn't read your Claude Code conversations or anything else. It writes nothing to disk and doesn't touch the registry.
- **On the network:** one `GET https://api.anthropic.com/api/oauth/usage` request, made every 2 minutes or when you click Refresh. That is the only address the app contacts. The request only reads your usage and changes nothing on your account. It uses HTTPS with Windows' normal certificate checks.
- **No token refresh:** an expired login is never refreshed, because refreshing would write new credentials and could sign Claude Code out.
- **No other programs:** the app never launches anything.

### Things to be aware of
- **Undocumented endpoint:** the usage endpoint is the one behind Claude Code's `/usage`, but Anthropic doesn't document it. It could change or stop working.
- **Anthropic's terms:** the app reuses Claude Code's login to read your own usage, which is what `/usage` itself does. Anthropic's terms limit how subscription logins are used outside its own apps. Read them and decide for yourself before using this.
- **Unsigned exe:** the build isn't code-signed, so a downloaded copy triggers SmartScreen (see [Windows SmartScreen warning](#windows-smartscreen-warning)). Build it yourself from source if you want to be sure what you're running.
- **System changes:** the app doesn't start with Windows and never changes system settings.
