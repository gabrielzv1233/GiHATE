# Limitations and risks

## Scancode Map is system-wide

Windows' `Scancode Map` is not device-specific. If GiHATE maps source scan `0x59` to F24, **any connected keyboard that produces scan `0x59` is also affected**.

## Restart required

Windows loads Scancode Map during boot. Applying, changing, or restoring the mapping does not fully take effect until Windows restarts. GiHATE defaults to **Restart now**, while still allowing Restart later.

## Hardware implementations differ

Other GIGABYTE/AORUS models may use different scan codes, HID reports, or entirely different ACPI/WMI/driver paths. The guided detector must succeed before Apply is enabled.

## Administrative access

Changing HKLM keyboard mappings and PnP device state requires Administrator privileges, so GiHATE requests elevation when launched.
