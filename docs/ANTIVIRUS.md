# Executable metadata and antivirus notes

GiHATE changes low-level Windows keyboard and HID configuration, so an unsigned build may occasionally receive a heuristic false positive from antivirus or SmartScreen products.

The project does **not** attempt to hide, obfuscate, pack, or bypass security products.

## Embedded executable metadata

Release builds include normal Windows/.NET metadata such as:

- Product: `GiHATE`
- Company / author: `Gabrielzv1233`
- Description: `GIGABYTE GiMATE hardware button disabler and programmable key remapper.`
- Copyright
- Assembly, file, product, and informational versions
- Repository/project URL
- English-US neutral language metadata

Single-file compression is explicitly disabled so the executable is not unnecessarily packed.

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
