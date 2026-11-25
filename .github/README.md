# Sucrose (hardened personal fork)

This is a **personal, security-hardened fork** of [Sucrose](https://github.com/Taiizor/Sucrose), the open-source wallpaper engine for Windows by [Taiizor](https://github.com/Taiizor). It is maintained for my own use and is **not affiliated with, endorsed by, or contributing back to** the upstream project. Please do not open issues about this fork upstream.

All credit for the original application goes to the upstream authors. This fork stays under the same license (GPL-3.0, see [LICENSE](../LICENSE)).

## What this fork changes

The goal is to reduce the attack surface of malicious wallpapers and remove everything that is not needed to display a wallpaper.

### Wallpaper hardening
- **Disabled wallpaper types:** Url, YouTube and Application wallpapers no longer launch (remote JavaScript and arbitrary executables). Gif, Video and Web remain.
- **Web wallpapers are local-only** (WebView2 engine): requests to remote hosts, popups, navigation away, permission prompts (camera, mic, location) and password autofill are blocked.
- **Dangerous Chromium flags removed:** `--disable-web-security`, `--allow-file-access*`, `--allow-universal-access-from-files`, fake media-permission prompts, plugin flags and more.
- **TLS:** invalid certificates are rejected (WebView2, CefSharp, shared HTTP client); only TLS 1.2/1.3 is allowed.
- **Local wallpaper servers** (`LocalHttpServer`, `WatsonWebServer`): path traversal fixed, wildcard CORS removed, GET/HEAD only, loopback host names only.
- **No global keyboard capture** in the engines. Mouse input is kept for interactive wallpapers.

### Removed or disabled network features
- Telemetry, online pings and error reporting (Reportdog); defaults are off.
- Discord Rich Presence.
- The auto-updater (it downloaded and ran installers). Update by rebuilding.
- Store download/search telemetry, theme share and report uploads.
- External ping/DNS lookups in the network status API (the ping value stays at 0).
- All shared HTTP traffic is limited to **HTTPS GET/HEAD**; anything that sends data out, or targets `/telemetry/`, `/report/` or `/upload/` paths, is refused.

### What you lose
- Existing Url, YouTube and Application wallpapers.
- Web wallpapers that fetch anything from the internet (CDNs, APIs, embeds), and wallpapers that need camera, microphone or keyboard input.
- Auto-update, Discord presence, theme sharing/reporting, the ping stat.

Wallpaper downloads from the store (GitHub and the Soferity mirror) still work.

## Build

Requirements: Windows 10/11, the .NET 11 SDK (see [global.json](../global.json)).

```powershell
dotnet restore src/Sucrose.slnx
dotnet build src/Sucrose.slnx -c Release -p:PlatformTarget=x64
```

To produce a runnable package that bundles the .NET runtime:

```powershell
cd .build
powershell -NoProfile -ExecutionPolicy Bypass -File .\Sucrose.ps1 -Configuration Release -PlatformTarget x64 -PublishBaseDir $PWD
```

The package is written to `src/Sucrose/Package`. Start `Sucrose.Launcher.exe` from the extracted/published folder.

## Moving the build to another PC

If Windows asks whether you want to open files that are "not secure", and the app sits in the tray without opening, the files carry a "downloaded from the internet" mark and the launcher's child programs get blocked. Unblock the folder first:

```powershell
Get-ChildItem .\Sucrose-x64 -Recurse -File | Unblock-File
```

## Not built or verified

The hardening changes compile and the full package builds, but they have been tested only lightly. Expect rough edges in less common wallpapers.

## Upstream

- Original project: https://github.com/Taiizor/Sucrose
- Original documentation and wiki: see the upstream repository.
