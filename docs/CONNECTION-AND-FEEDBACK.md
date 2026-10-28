# SSH authentication, feedback, and official updates

Windows 0.2.1 adds four connection modes under Settings > Connection:

- Password: enter the login password; empty-password accounts may leave it empty. Optional saved passwords use Windows DPAPI for the current Windows user. If unchecked, the password is held only for this running application session and must be entered again after restart.
- Private key: select a private key file. Existing OpenSSH and terminal behavior remains available for agent/passphrase prompts.
- Automatic: use OpenSSH configuration, default keys and aliases. The file pane supports readable unencrypted keys; agent-only or proxy configurations can require the terminal's system SFTP command.
- No authentication: only for a server that explicitly permits the SSH `none` method. This does not bypass server authentication. Use Password for empty-password accounts.

The managed password/no-auth transport shares host-key validation between collection, connection test, process identity operations, SFTP, and terminal. It follows accept-new trust on first use and rejects changed or revoked keys. Review `~/.ssh/known_hosts` and independently verify fingerprints before accepting any deliberate replacement. Password-based jump-host and multifactor flows are currently unsupported. Private-key passphrases and platform credentials are not included in feedback.

About > Feedback collects category, title and description. Contact and system/display information are optional. The first version has no automatic screenshot, configuration, history, or terminal log attachment. A successful HTTP response is accepted only with a valid saved-feedback receipt. Cancellation/network failures preserve form text. Feedback files are write-only from the public API.

Server storage: `/var/lib/aq-server-feedback/inbox/YYYY-MM-DD/FB-*.json` (UTC date; directories 0700 and files 0600), owned by the isolated `aqserver-feedback` service account. The Python standard-library service listens on loopback 18772, and only `/aqserver/api/feedback` is proxied. Reports have bounded fields and size, per-client hourly limits, and bounded storage; reaching capacity rejects new reports instead of deleting existing reports. Keep backups and triage access private.

Feedback HTTPS endpoint discovery is published at `website/public/releases/services.json` and verified using the embedded release public key. Renew the signed discovery before expiry and whenever the public HTTPS origin changes using `scripts/sign-services.ps1`. The key must stay outside the repository and website. Browser requests are not an intake UI and cannot read the inbox.

Windows updates use a fixed official signed manifest on this repository's main branch. Legacy per-user `update-source.json` overrides are ignored and there is no source editor. The exact installer URL, size, SHA256 and version are signed; downloads are not rebased onto another origin. New releases point to versioned GitHub Release assets. Signing is integrity protection and is not equivalent to hiding publicly available source or preventing recompilation.

The SSH.NET terminal resize adapter is isolated in `SshTerminalSession`: the pinned dependency exposes window-change on its internal channel. Updating SSH.NET requires running actual terminal input/output/resize regressions in addition to normal tests.
