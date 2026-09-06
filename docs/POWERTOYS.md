# Using the GiMATE replacement key with PowerToys

GiHATE uses **F24 by default** as the replacement for the physical GiMATE button.

F24 is useful because Windows supports F13-F24, but most keyboards do not have those keys physically, so it is unlikely to collide with normal input.

## Simple key remaps

If you only want the GiMATE button to act like another normal key or shortcut, open:

**PowerToys -> Keyboard Manager -> Remap a key**

Set `F24` as the source and choose the key or shortcut you want it to send.

## Launching an app or opening a URL

PowerToys treats **Start App** and **Open URI** as shortcut actions. Shortcut mappings must begin with a modifier key such as Shift, Ctrl, Alt, or Win.

Because the physical GiMATE replacement is only F24, use a two-stage mapping:

1. Open **Remap a key**.
2. Map `F24` -> `Shift + F24`.
3. Open **Remap a shortcut**.
4. Add `Shift + F24` as the source shortcut.
5. Set the destination to **Start App** or **Open URI**.

This lets the physical GiMATE button trigger any application or URI without GiHATE needing to remain running.

## Example: launch PowerShell

The original tested setup uses:

```text
GiMATE button
    -> F24        (GiHATE)
    -> Shift+F24  (PowerToys Remap a key)
    -> PowerShell (PowerToys Remap a shortcut -> Start App)
```

For Windows PowerShell, the application can be:

```text
powershell.exe
```

For PowerShell 7, use the installed `pwsh.exe` path instead.

## Important

PowerToys must be running with Keyboard Manager enabled for PowerToys remaps/actions to work. GiHATE itself does not need to remain open after its machine-wide configuration has been applied and Windows has restarted.
