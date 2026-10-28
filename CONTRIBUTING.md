# Contributions

Please keep the app offline, lightweight and compatible with C# 5/.NET Framework 4.x.

1. Fork the repository and create a branch.
2. Run `build.cmd -Test`; do not exercise real shutdown commands in tests.
3. Add regression cases for solar/date/time-zone changes. Specify coordinates, date, Windows time-zone ID, DST offset, expected time and the reference source.
4. Keep runtime configuration, local logs, secrets, screenshots containing personal desktop information and generated binaries out of Git.
5. Open a PR describing the change and test results. UI changes should include cropped, non-personal screenshots.

Source layout:
- `src/AutoDayNight.cs`: tray app, theme/autostart APIs, battery prompt, icon rendering and CLI.
- `src/AppConfig.cs`: configuration, legacy migration, bounds and local logs.
- `src/LocationForm.cs`: location/time-zone setup.
- `src/SolarSchedule.cs`: UTC solar calculations, polar states and transitions.
- `tests/TestRunner.cs`: isolated regression tests.

Run `build.cmd -Test -Package` to reproduce the Windows release archive and SHA-256 checksum.
