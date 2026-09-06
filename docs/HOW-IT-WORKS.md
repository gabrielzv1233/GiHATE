# How GiHATE works

GiHATE does not uninstall GiMATE or GIGABYTE Control Center and does not inject code into either process.

It disables only the detected vendor-defined HID child collection using `CM_Disable_DevNode` with `CM_DISABLE_PERSIST`, then maps the surviving keyboard scan code through `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout\Scancode Map`.

F24 is the default target because it is a real Windows key that is rarely produced by physical keyboards and is easy to bind in PowerToys.

GiHATE preserves unrelated existing Scancode Map entries and remembers the previous mapping for its source scan code so Restore does not replace the entire registry value.

The portable executable stores machine-wide state in `C:\ProgramData\GiHATE\config.json`.
