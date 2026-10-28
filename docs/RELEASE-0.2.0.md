# Windows 0.2.0 / Ubuntu 0.1.3 release validation

Release code: `3292ae2d912bd14fa80ab5c9c7be6e1fd69f886c` (plus a documentation handoff commit). Platforms release independently. Ubuntu remains a preview and need not track future Windows changes.

## Delivered behavior

Grouped workspace/navigation, coherent typography/forms/icons/themes, simpler history controls and segmented CPU/memory views. Real local/SSH network and mounted-volume collection supplies five adaptive persistent resource groups, detail views and additive archive snapshots. Nonlinear chart/number/page motion and cached hover details remain. Windows supports a configurable update source, cancellation/deadlines and tray outside-click dismissal.

Windows confirms once before downloading and verifying, then exits normally, installs silently in its current directory, and restarts. Old UPDATE callers without a PID remain compatible. The installed0.1.x client cannot acquire the new client-side flow until upgraded. Ubuntu upgrades with apt.

## Package identity

- Windows0.2.0: SHA256 `87471115566381cccba2d80ecfdb6b0661130ea54ce59e8089fbb699f955e5aa`; 9398636 bytes.
- Ubuntu0.1.3: SHA256 `873b5202344097ea95e3e641b2792645714d914791d79d59fe2876df31201b9d`; 222790 bytes.
- Both match their own signed metadata. Release metadata signatures are separate from Windows publisher/Authenticode signing; no Authenticode certificate is claimed.

## Executed verification

- Shared QA: ten Python tests, including three independent-platform version tests; six tests execute the actual chart class for leave-frame ordering, stationary hover, navigation cleanup,300-second mapping and hidden canvases.
- Windows: fifteen packaged suites passed for the UI/network/storage candidate. Later updater changes reran module/update checks against the final signed installer and exercised the installed application. Sixty-eight view/font inventories and multi-theme footer geometry passed. Real legacy settings/archive migration, reinstall, uninstall and retention passed under an isolated AppId.
- Automatic update: a native caller closed, the installer waited, silent installation completed, then a new native window opened. A screenshot and JSON checked visible/loaded state, version and exact installation path. Both PID-based and legacy mutex-based protocols passed. External antivirus/disk failure modes are not universally certified.
- Ubuntu: fifteen runtime tests; actual upgrade/reinstall/remove/install;61 native GTK/WebKit actions and eleven screenshots covering local/SSH network/storage, wide/narrow footer, hover across refresh, settings, history and terminal. Config/key preservation used complete hashes; the approximately3.4GB SQLite check used size,mtime_ns and bounded first/last sampled hash, not a complete database byte hash.
- Website: TypeScript/React/Vite build and four Sites tests pass. Real GTK/WebKit checks at1440/834/390 widths verify overflow, separate versions and correct downloads, mobile navigation, release-note spacing and spotlight motion; six screenshots. These are not physical iOS/Safari or all-browser acceptance.
- Public HTTPS: fifteen resources and full Windows/Ubuntu downloads verified without proxy; signed manifest bytes, SHA256 and lengths match. Private QA/config routes are not served.

## Private evidence

Local ignored artifacts/qa contains installed validation `installer-0.2.0-953996503c5340f392643a0be7034a87` and signed-package tests `packaged-0.2.0-90dd4844dfd7493c8e612e46e3f09edc`. Developer native evidence is `/var/lib/aq-server-qa/evidence/codex-0.1.3/`; website evidence is `/var/lib/aq-server-qa/evidence/website-0.2.0/`.

Hermes independently executed the installed Ubuntu0.1.3 native core suite:61 actions and11 screenshots. Parent audit matched the actual action files, all screenshot files and final package hash. Report: `qa/status/ubuntu-hermes-0.1.3.json`; audit: `qa/status/hermes-audit-0.1.3.json`. Result is core-suite-pass-supplemental-incomplete. Two focused attempts failed a telemetry-refresh precondition or an overly strict moving-window tooltip assertion; the third attempt is unknown/interrupted. Theme screenshot timing and narrow viewport observations remain unconfirmed. No confirmed new defect was established. These limits are retained in the raw report; this is not all-platform acceptance. Hermes memory contains the current preview URL; independent platform versions are authoritative in the handoff/report, not verified in its compact memory.

## Access and compatibility

Verified temporary preview: https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/. Quick Tunnel may change hostname. Original IP ingress and the default canonical update endpoint remain a separate known blocker; preview access does not repair them. A reachable user-configured mirror transports the same signed package. Temporary addresses are not embedded in binaries or signed canonical URLs.

Ubuntu24.04/other architectures, physical Linux tray/desktop drag-out/HiDPI and every platform/browser combination remain separate validation. The isolated Windows AppId protects the user's installed application and data.
