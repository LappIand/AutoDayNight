# AutoDayNight

**Light at sunrise. Dark at sunset.** A lightweight Windows tray app that computes daily solar events offline for your own location.

[中文](README.md) · [Download](https://github.com/LappIand/AutoDayNight/releases) · [Issues](https://github.com/LappIand/AutoDayNight/issues)

## Features

- First-run **location setup**: choose a preset or enter any latitude/longitude; no city is silently assumed.
- Choose the location's **Windows time zone**, or follow the device zone. DST is applied at the event instant, including transition days.
- Offline NOAA/Meeus solar calculations: **no network calls, telemetry, geolocation uploads or API keys**.
- Synchronize the theme when the app starts at sign-in. Missing sunrise while the PC is off is handled on next launch.
- Preserve manual overrides between scheduled boundaries. Resuming automatic mode, waking the PC, or changing its clock synchronizes again.
- Polar day/night handling and future seasonal transitions.
- Black sun on the light taskbar, near-white crescent on the dark taskbar; embedded multi-size icons.
- Cached astronomy and a 30-second poll; typically a few dozen MB of memory.
- Optional full-screen low-battery prompt with a shutdown countdown. **Disabled by default on new installations.** Existing settings are preserved on upgrade.

## Install: Windows 10 / 11

1. Download `AutoDayNight-v1.0.0-windows.zip` from [Releases](https://github.com/LappIand/AutoDayNight/releases) and extract it fully.
2. Run `install.cmd`. The installer copies the app to `%LOCALAPPDATA%\AutoDayNight`, registers current-user sign-in startup, and launches it. No administrator access is required.
3. Choose your city/coordinates and time zone in the first-run dialog, check the sunrise/sunset preview, and save.
4. Look for the sun/crescent in the tray overflow area.

Requires the Windows-supplied .NET Framework 4.x, not the .NET SDK. Cancelling first-run setup leaves solar switching pending; configure the location through the tray menu later. Installation upgrades preserve existing coordinates and preferences.

## Location and time zones

The tray's **Location settings…** item supports presets plus custom coordinates everywhere:

- Latitude: north positive, south negative (`-90..90`).
- Longitude: east positive, west negative (`-180..180`).
- Select the time zone **of your configured location**. If following Windows, ensure the device zone is correct.
- Presets include Chinese cities and Tokyo, Singapore, New Delhi, London, Berlin, New York, Los Angeles, Sydney, Auckland, Honolulu and Tromso.
- Times are displayed in the configured zone, not permanently in Beijing time. City coordinates are approximate centers.
- Moving or travelling requires updating the location manually. The app does not track IP/GPS location.

Main tray controls and location setup adapt to Chinese/English UI culture; some diagnostics are bilingual.

## Low-battery shutdown prompt: opt in

Enable it in the tray menu if desired. On battery, below 20% by default, it asks whether to shut down. No response in 60 seconds requests `shutdown.exe /s /t 0` (no forced application termination). Unsaved work may prevent Windows from shutting down normally.

Cancel with the button, Esc or closing the window. Restoring AC power, reaching the threshold, or disabling the feature also cancels a running countdown. One prompt per discharge episode; no-battery devices remain silent.

`AutoDayNight.exe --test-lowbatt 15 8` exercises the prompt without a real shutdown.

## Configuration / privacy

Configuration and a size-capped local log live in `%LOCALAPPDATA%\AutoDayNight`. The repository's `config.ini` is a **public first-run template**, not a user's real location.

```ini
location_configured=1
city=New York
lat=40.7128
lon=-74.0060
time_zone=Eastern Standard Time
auto=1
sunrise_offset_min=0
sunset_offset_min=0
low_batt_alert=0
low_batt_percent=20
low_batt_grace_sec=60
```

Empty `time_zone` follows Windows. Positive offsets delay events, negative offsets advance them (`-180..180` minutes). Battery threshold: `1..99`; countdown: `10..3600` seconds. Restart after manual edits; tray location changes apply immediately. Legacy configurations with both valid coordinates migrate automatically.

No telemetry or uploads. Redact local diagnostic data before sharing an issue. `AUTODAYNIGHT_DATA_DIR` optionally selects another data folder and is used for isolated tests.

## Build and test

```powershell
.\build.cmd
.\build.cmd -Test
.\build.cmd -Test -Package
```

Uses the system Framework64/Framework `csc.exe`, C# 5, and no external packages. Tests use isolated data and do not change the real theme, registry, startup or shutdown state. They cover reference solar times, DST transitions, the date line, southern seasons, polar states, offsets crossing midnight, config migration and UI construction.

The EXE is built in the project root; the release ZIP and `SHA256SUMS.txt` go to ignored `artifacts/`. Binaries are distributed through Releases, not committed to Git.

CLI:

```text
AutoDayNight.exe --status
AutoDayNight.exe --test-calc
AutoDayNight.exe --location
AutoDayNight.exe --set dark
AutoDayNight.exe --calc 2026-03-08 40.7128 -74.0060 "Eastern Standard Time"
AutoDayNight.exe --dump-icons OUTPUT_DIR
```

`--calc` performs calculations only. Restart an existing tray instance after changing location through CLI. GUI executable output is also written to `%TEMP%\AutoDayNight.out`.

An optional GitHub Actions build definition is provided as [docs/windows-ci.example.yml](docs/windows-ci.example.yml). It is not installed as an active workflow because the publishing OAuth identity lacks the workflow scope.

## Accuracy / limitations

UTC solar coordinates are computed using NOAA/Meeus formulas and converted at each instant to the selected time zone. The -0.833° horizon incorporates nominal refraction and solar radius; elevation, mountains and actual atmospheric conditions are not modeled. Accuracy is suitable for theme scheduling, not navigation/precision observation. Errors can grow near polar-season boundaries.

Online switching occurs on the next 30-second poll; Windows sign-in startup can be delayed. There is no watchdog. Lock-screen theming is outside this app; older applications may read the theme only when restarted.

Formula reference: [NOAA calculation details](https://gml.noaa.gov/grad/solcalc/calcdetails.html). Independently implemented, not affiliated with NOAA.

## Uninstall / license

Run `uninstall.cmd` to remove the installed app, sign-in startup, configuration and logs. It does not restore your previous theme.

[MIT license](LICENSE). Contributions welcome: [CONTRIBUTING.md](CONTRIBUTING.md).
