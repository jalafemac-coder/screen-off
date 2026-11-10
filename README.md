# ScreenOff

Turn off the display on Windows without putting the machine to sleep.

Windows has a timeout for blanking the screen but no built-in command to do it
on demand. Pressing the power button can be reconfigured to blank the screen,
and locking the workstation blanks it after the lock timeout, but neither is
immediate and neither lets you say "in twenty minutes". This is a single small
executable that does both: blank now, or blank after a delay you choose.

The system keeps running the whole time — downloads, builds, playback, and
remote sessions are unaffected. Moving the mouse or pressing a key brings the
display back.

## Usage

Run with no arguments to get a picker: an immediate option, presets from one
minute to two hours, and a free-form minute field. The immediate option blanks
the display straight away; any delay instead opens a small always-on-top
countdown in the lower corner of whichever display the pointer is on, and that
window can extend itself by five minutes, fire early, or call the whole thing
off.

From the command line:

| Command | Effect |
| --- | --- |
| `ScreenOff.exe` | Open the delay picker |
| `ScreenOff.exe 0` or `ScreenOff.exe now` | Blank the display immediately |
| `ScreenOff.exe 25` | Blank it after 25 minutes |
| `ScreenOff.exe 90s` | Suffixes: `s` seconds, `m` minutes, `h` hours |
| `ScreenOff.exe 1.5h` | Fractional values are accepted |
| `ScreenOff.exe --lang=en` | Force the interface language (`en` or `zh`) |
| `ScreenOff.exe --help` | Show usage |

A bare number means minutes. The delay is capped at 24 hours. A delay below one
second is raised to one second rather than rounded down to zero, so a request
like `0.4s` still gets a countdown window and so still has a way out.

Anything unparseable, an unrecognized option, or a second delay value shows the
usage dialog and exits with a non-zero code: 2 for a bad argument, 1 if a
picker or countdown was dismissed, 0 otherwise. `--lang` is read before the rest
of the command line, so it applies to the usage and error text of the same
invocation no matter where it appears.

Without `--lang`, the interface follows the operating system's UI language:
Chinese when that is Chinese, English otherwise. Every string lives in
`src/Strings.cs` if you want to change or extend the wording.

## Build

No SDK required. The C# compiler bundled with the .NET Framework is used, and
it is present on every supported Windows build.

```powershell
.\build.ps1
```

The executable lands in `bin\ScreenOff.exe`. `-Platform` (`anycpu`, `x64`,
`x86`) and `-OutputDir` are available if you need them.

## Install

```powershell
.\install.ps1
```

This puts two shortcuts on the desktop: *Screen Off Timer* (the picker, bound to
<kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd>) and *Screen Off* (immediate, bound
to <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Q</kbd>). Override with `-TimerHotkey`,
`-ImmediateHotkey`, `-TimerName`, `-ImmediateName`, or pass `-SkipImmediate` for
just the picker. An empty hotkey string creates the shortcut without one.

A shortcut hotkey is handled by the shell, which only watches the desktop and
the Start menu — moving the shortcut elsewhere silently disables its hotkey.

To uninstall, delete the `.lnk` files from the desktop and remove the folder.

## How it works

The display is switched by broadcasting `WM_SYSCOMMAND` with `SC_MONITORPOWER`
to every top-level window.

The broadcast goes out through `SendMessageTimeout` rather than `SendMessage`.
`SendMessage` to `HWND_BROADCAST` is synchronous and blocks on the first window
that does not pump its message queue, so the process can stay alive indefinitely
after the display has already gone dark — a stuck process per invocation, each
one holding a lock on the executable. `SMTO_ABORTIFHUNG` with a two second
timeout skips those windows instead.

When the blanking is triggered by the user, the broadcast is held back briefly.
A key release or a mouse click is itself an input event, and an input event
arriving right after the broadcast turns the display straight back on. This
applies to all three user-driven routes — a hotkey, the picker's immediate
option, and the countdown's "off now" button. A countdown reaching zero has no
such gesture to wait for and is not delayed.

Control sizes are written for 96 DPI and multiplied by the display's scale
factor at startup. `AutoScaleMode.Font` cannot do this for a form built in code:
it compares the runtime font against a design-time baseline that only the form
designer records, so for hand-written forms the factor is always 1 and literal
pixel sizes never grow, while point-sized fonts do.

`--help` and the error paths use dialogs rather than console output because the
program is built as a Windows application: a console subsystem binary would
flash a terminal window on every hotkey press.

## Icon

`assets/ScreenOff.ico` is generated, not hand-drawn:

```powershell
.\assets\make-icon.ps1
```

Each size is rendered at its own resolution instead of downscaling one bitmap,
so the 16px frame stays readable. Frames are stored as uncompressed DIBs;
PNG-compressed frames are legal in an ICO but are not read reliably by
`System.Drawing.Icon` or by the compiler's resource embedder. The 256px frame is
deliberately omitted — uncompressed, it would cost more than the entire program,
and the shell scales the 128px frame for that view.

## Requirements

Windows 8.1 or later. .NET Framework 4.x is in-box from Windows 8 onward, but
the application manifest declares compatibility with Windows 8.1 and Windows
10/11 only, so an older build runs under compatibility shims.

No administrator rights, no installer, no network access, no registry writes.
The program's only persistent footprint is whatever shortcuts `install.ps1`
creates.

## Layout

```
build.ps1              compile to bin\ScreenOff.exe
install.ps1            create desktop shortcuts with hotkeys
assets/make-icon.ps1   regenerate the application icon
assets/ScreenOff.ico   generated icon
src/Program.cs         entry point, argument parsing, delay parsing
src/Forms.cs           delay picker and countdown windows
src/Display.cs         the monitor-power broadcast
src/Strings.cs         all user-visible text
src/ScreenOff.manifest DPI awareness and supported-OS declarations
```

## License

MIT. See [LICENSE](LICENSE).
