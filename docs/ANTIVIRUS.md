# Executable metadata, SmartScreen, and antivirus notes

GiHATE changes low-level Windows keyboard and HID configuration, so an unsigned build may occasionally receive a heuristic false positive from antivirus or SmartScreen products.

The project does **not** attempt to hide, obfuscate, pack, inject into other processes, or bypass security products.

## Embedded executable metadata

Release builds include normal Windows/.NET metadata such as:

- Product: `GiHATE`
- Company / author: `Gabrielzv1233`
- File description: `GIGABYTE GiMATE hardware button disabler and programmable key remapper.`
- Copyright
- Assembly, file, product, and informational versions
- Repository/project URL
- English-US neutral language metadata
- A Windows manifest identifying `Gabrielzv1233.GiHATE` and Windows 10/11 compatibility

Single-file compression is explicitly disabled so the executable is not unnecessarily packed.

## SmartScreen / UAC publisher field

The **Publisher** field shown by Windows SmartScreen or the UAC elevation prompt does not come from the `Company`, `Authors`, `Product`, or file-description metadata above.

Windows considers that field verified only when the executable has a trusted **Authenticode code-signing signature**. An unsigned GitHub Actions build can therefore still show:

```text
Publisher: Unknown publisher
```

even though Explorer's file properties correctly show `Gabrielzv1233`, `GiHATE`, the version, and the description.

Self-signing a certificate does not solve this for normal users because their PCs do not trust that certificate. A public release needs a publicly trusted code-signing certificate or a compatible trusted signing service to show a verified publisher on other computers.

## Reproducibility and transparency

- Source is public in this repository.
- The GitHub Actions build is defined in the repository.
- Deterministic compilation is enabled.
- GiHATE does not install a background service, inject into other processes, or remain running after applying a configuration.
- Machine-wide state is stored at `C:\ProgramData\GiHATE\config.json`.

## Code signing

Metadata can help reduce simplistic false positives, but it cannot guarantee that antivirus or SmartScreen will trust an executable.

Authenticode code signing is the strongest additional step for publisher identity and reputation. Official releases may be signed in the future if a suitable signing certificate or trusted signing service is available.

Do not disable antivirus protection just to run GiHATE. If a build is flagged, verify that it came from this repository, inspect the source/build workflow, and submit the file to the antivirus vendor as a false positive when appropriate.
