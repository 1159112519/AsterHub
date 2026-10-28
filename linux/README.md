# AQ Server Ubuntu edition

Ubuntu 22.04 amd64 is the target for live verification. Ubuntu 24.04 and other architectures require separate validation. Python >=3.10; system packages provide aiohttp, Paramiko, GTK/WebKit and Chinese fonts.

Install: sudo apt install ./AQServer-0.1.3-ubuntu-all.deb
Open from the applications menu, or run: aq-server --desktop
Keep collection across logins: systemctl --user enable --now aq-server
Run without a desktop: aq-server --serve

The workbench listens only at 127.0.0.1:19871. It requires the current user's protected access key and is not an unauthenticated public dashboard. The native launcher supplies that key and removes it from the page address after opening. Do not reverse-proxy the workbench to the public website.

Configuration: ~/.config/aq-server/settings.json
Private bootstrap key and SSH known_hosts: ~/.config/aq-server/
Archive: ~/.local/share/aq-server/history.sqlite3
The XDG_CONFIG_HOME and XDG_DATA_HOME variables override these roots. File/terminal operations use the current Linux user's permissions. Passwords remain in memory, never in the package or settings. SSH host fingerprints require confirmation. Use your own private key path to reconnect unattended after restart.

All enabled devices collect independently of selected page/device. History snapshots contain GPU processes and usernames when the driver/OS makes them readable. NVIDIA monitoring uses the installed nvidia-smi; a GPU is optional. Memory type/frequency are shown only when known, never inferred. Unsupported sensor fields are marked unavailable.

File/folder uploads are copies, never moves. Existing names are rejected rather than silently overwritten. Downloads retain sources; directories download as ZIP and symlinks inside ZIP trees are skipped. Newly uploaded unchanged files may be undone. Drag-download support depends on the browser/GTK host; the download control is always available. Empty directories are not currently uploaded by the browser file picker.

Closing the desktop window leaves its local collection service running. Stop the user service with systemctl --user stop aq-server if enabled. Package removal does not delete user configuration or history. Linux system tray, cross-host clipboard transfer, native drag-to-desktop parity and automatic package upgrades are not in this initial Ubuntu release. This does not replace or emulate the Windows WPF runtime.

For headless QA, the root/operator reads the dedicated test account's access.key directly into the browser driver, never prints it, operates the real workbench and writes evidence to the private QA folder. A successful HTTP status does not count as UI acceptance.
