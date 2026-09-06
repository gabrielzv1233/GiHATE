# Tested hardware

## Original development machine

GiHATE was developed and tested on:

- Laptop: **AORUS Master 16 AM6H**
- GIGABYTE keyboard device: `VID_0414&PID_8100&MI_00`
- Dedicated GiMATE keyboard scan code: `0x59`
- Windows virtual key before remapping: `VK_CLEAR` (`0x0C`)
- GiMATE vendor HID collection: `VID_0414&PID_8100&MI_02&Col04`
- Vendor usage page: `0xFF02`
- Observed button report: `04 00 00 91`

On this machine, PowerToys could see the button as **Clear**, but changing the Clear mapping did not stop GiMATE from launching.

The same physical button produced two paths: a keyboard scan `0x59`, plus the separate vendor HID report `04 00 00 91`. Null-remapping scan `0x59` did not stop GiMATE. Disabling only `MI_02&Col04` did stop GiMATE while scan `0x59` continued to arrive, allowing it to be reused as F24.

This exact layout must not be assumed for every GIGABYTE laptop. GiHATE's guided detector exists so other machines can be verified before anything is disabled.
