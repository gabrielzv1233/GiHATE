# Detection and safety

GiHATE does not blindly disable a hardcoded device instance ID.

The wizard asks the user to press the GiMATE button three times. It looks for a repeated keyboard scan code and a repeated report from a separate vendor-defined HID collection. The HID candidate must use a vendor-defined usage page (`0xFF00` or above), and its PnP instance ID must differ from the keyboard interface.

Before saving the profile, GiHATE asks the user to press a normal built-in keyboard key such as A or Space. The check passes only if the normal key comes from the detected keyboard interface and the candidate vendor HID collection does not emit traffic for that normal key.

If detection is ambiguous, GiHATE refuses to save a profile rather than guessing.
