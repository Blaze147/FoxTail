# FoxTail

A portable Windows desktop log viewer built with .NET 10 and Windows Presentation Foundation (WPF). All projects use Microsoft framework components only: there are no third-party NuGet package references, embedded browser, WebView runtime, or web server. This is an independent implementation inspired by the workflow of BareTail.

The desktop interface requires Windows. The platform-independent log reader, highlighting engine, settings, and core regression checks can run on macOS for development.

## Run on Windows

Install the .NET 10 SDK, then from this directory:

```powershell
dotnet run --project src/FoxTail.Desktop
# Or open a log immediately:
dotnet run --project src/FoxTail.Desktop -- "C:\Logs\application.log"
```

Open one or more files with **Open files…**. To generate a changing sample log, run `python scripts/demo-log.py` in a second terminal and open the printed path (Python is optional and used only by this development helper).

## Features

- Live local-file monitoring, polling every 200 ms. Each tab monitors independently.
- **Follow tail** controls automatic scrolling. Turn it off to read earlier lines while monitoring continues. **Pause monitoring** stops reads for all tabs; resume catches up from the saved position.
- Ordered, enabled/disabled highlighting rules with literal or regular-expression matching and optional case sensitivity. **The first matching enabled rule wins**, supplying both the foreground and background for the entire line. Use ↑/↓ to change precedence.
- Rule editor with sample preview, color hex inputs, validation, and JSON import/export. Changes apply to all tabs. Cancel discards the editor's changes.
- Adjustable font size, line wrapping, numbered rows, full path tooltips, selected-line copy, and multiple tabs.
- Handles incomplete lines, split UTF-8 sequences, CRLF/LF, file truncation, and common rename/recreate log rotation. Missing or locked files retry automatically.
- Bounded large-file tail viewing: initial load reads the last 2 MiB, each poll reads up to 256 KiB per file, and each tab retains up to 20,000 lines or 16,777,216 text characters (about 32 MiB of UTF-16 payload). Individual lines are capped at 64 Ki characters and visibly marked when truncated. WPF list rows are virtualized and recycled.

## Portable Windows build

From PowerShell on Windows with the .NET 10 SDK installed:

```powershell
./scripts/publish.ps1
# ARM Windows:
./scripts/publish.ps1 -Runtime win-arm64
```

Unzip `artifacts/FoxTail-win-x64.zip` into a writable folder and run `FoxTail.Desktop.exe`. The self-contained executable includes the Microsoft .NET and WPF runtime; the destination PC needs no SDK or separate .NET installation. Native Microsoft libraries extract to the operating system's temporary cache on first launch. No installer or administrator rights are required. The executable is unsigned.

You can also cross-build the Windows package on this Mac:

```sh
./scripts/publish.sh win-x64
# Or:
./scripts/publish.sh win-arm64
```

The shell publishing script uses the system `zip` utility. Both scripts create the ZIP from a fresh staging folder. `EnableWindowsTargeting` in the desktop and UI-test projects lets the .NET SDK download Microsoft's Windows targeting packs and compile on macOS; it does not make WPF run on macOS. Trimming is disabled for WPF compatibility.

## Settings and rules

`foxtail.settings.json` is saved beside the executable. Keep the app in a writable folder so rules and display preferences travel with it. Development runs save into the build output directory. If loading fails, defaults are used with a startup warning; save failures appear in the status bar. Settings include `RetainedLines` (1,000–100,000), `FontSize`, `Wrap`, and the ordered `Rules` array. Rule export writes only the array; import replaces the editor's list and takes effect when you click **Apply rules**.

Default rules highlight errors, warnings, and success messages. Color values use `#RRGGBB`. Regex rules have a 25 ms match timeout; timeouts skip that rule. Rules are global to all tabs. There is no telemetry or automatic network file discovery.

Settings and exported rules from the previous desktop implementation use the same format. Settings recovered from the superseded Mac build are in `artifacts/saved-settings/foxtail.settings.json`; copy that file beside the Windows executable if you want to carry them over. The old Mac app and its binaries have been removed.

## Scope and limits

This version is a tail viewer. It does not index or browse the entire history of a large file; the earliest displayed row may start near its end and row numbers count the observed lines in the current read session. Older rows are discarded as the retention limits are reached. While paused, data that is rotated away cannot be recovered. Rotation detection uses file size, creation time, and a short prefix; unusual rewrites that preserve all of these can go undetected. Text is decoded as UTF-8 (ASCII works directly); other encodings are not selectable. BareTail compatibility is not claimed.

## Validate

On macOS or Windows:

```sh
dotnet build FoxTail.slnx -c Release
dotnet run --project tests/FoxTail.Tests -c Release
```

On Windows only:

```powershell
dotnet run --project tests/FoxTail.UiTests -c Release
```

The dependency-free core test executable checks real temporary-file append, partial-line completion, split UTF-8, truncation, rotation/replacement, recovery, bounded large-file reading, long-line limits, highlighting precedence, matching validation, and settings persistence (13 checks).

The WPF UI test executable uses a real STA dispatcher and modal windows to exercise rule editing, precedence moves, add/remove, validation, Apply, Cancel, display retention, rotation, foreground/background colors, and display refresh (four grouped checks). It uses no external test framework. The WPF solution and UI checks have been cross-compiled on macOS; native WPF UI checks and a Windows launch smoke test must still be run on Windows. Earlier cross-platform UI results do not validate this WPF replacement.

## Structure

- `src/FoxTail.Core`: platform-independent tail reader, highlighting, settings.
- `src/FoxTail.Desktop`: native WPF desktop UI, XAML log-row template and theme.
- `tests/FoxTail.Tests`: executable core regression checks; runs on macOS and Windows.
- `tests/FoxTail.UiTests`: native WPF UI regression checks; runs on Windows only.
- `scripts`: Windows portable publishing and a sample-log generator.
