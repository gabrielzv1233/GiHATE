# Advanced, verification, backups, and launch modes

## Better state tracking

GiHATE stores restart-required state with an identifier for the Windows boot where the change was requested. This lets the UI distinguish:

- device detected but not applied
- restart required on the current boot
- restart happened and verification is ready
- configured and healthy
- configured but HID/scancode state does not match
- revert staged but not applied yet

## Post-reboot verification

After Apply, Restore, Revert, or backup import, GiHATE creates a scheduled task named:

```text
GiHATE Verify After Restart
```

It uses an `ONLOGON` trigger with a short delay. A raw `ONSTART` task runs before an interactive desktop session exists, so it cannot reliably show the small verification window the user asked for.

The task launches the same portable executable with:

```text
GiHATE.exe --verify --from-task
```

If the task fires on the same Windows boot because the user only logged out/in, GiHATE keeps the task and continues to report that a restart is required. Once a newer boot is detected, the verifier checks the expected state and removes the task.

The verifier shows:

- GiMATE vendor HID enabled/disabled/unknown
- source scan code and expected destination mapping
- Windows restart state
- whether a known GIGABYTE/GiMATE listener process is running

The listener-process list currently includes:

```text
GiMATE
GimateServiceHelper
GCC
GCCService
GService
GIGABYTE Control Center
```

If none are running, GiHATE explicitly warns that a button test may be inconclusive even when the registry/device state looks correct.

## Restart-later watcher

If the user chooses **Restart later**, GiHATE starts a small background instance:

```text
GiHATE.exe --pending-watch
```

It listens only for the detected GiMATE keyboard scan code. If that button is pressed while the same Windows boot still has pending GiHATE changes, it shows a Windows notification reminding the user that the new behavior cannot be judged until Windows restarts.

The watcher exits at reboot and uses a global mutex to avoid duplicate watcher processes.

## Logs

Logs live in:

```text
C:\ProgramData\GiHATE\logs\
```

The current session is:

```text
latest.log
```

Older normal/verification sessions are renamed to timestamped files such as:

```text
2026-09-06_12-34-56.log
```

The beginning of every new log includes:

- GiHATE version
- full informational version
- short Git commit ID
- SHA256 of the running EXE
- executable path
- Windows version
- process architecture
- elevation state
- current boot-session identifier
- config/log paths

Use **Advanced -> Diagnostics -> Copy log file** to put `latest.log` on the Windows clipboard as an actual file attachment.

## Backup export/import

Advanced can export a `.gihate.json` backup containing:

- GiHATE config/profile
- detected keyboard/vendor HID identity
- original mapping information
- complete raw Windows `Scancode Map` value
- vendor HID persistent `CONFIGFLAGS`
- export version and commit metadata

Import restores the full saved Scancode Map, not just GiHATE's source-code entry. This makes it a real system-state backup but also means importing an old backup can replace keyboard mappings that were changed after the backup was made.

Import always marks Windows as requiring a restart and schedules verification.

## Developer launch modes

```text
--verify
```

Open the compact verification UI manually. A completed post-reboot verification may clear pending verification state.

```text
--verify-debug
```

Open the same verifier without clearing pending restart/task state. Useful for testing the UI repeatedly without rebooting.

```text
--pending-watch
```

Run only the restart-required GiMATE-button notification watcher.

```text
--advanced
```

Launch the normal main window and automatically open Advanced.
