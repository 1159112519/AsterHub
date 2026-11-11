# Module branches and integration

Use one short-lived feature branch for each independently reviewable upgrade. A local worktree isolates the working directory; the GitHub branch stores its commits.

## Branches
- `main`: reviewed stable source; merging does not itself authorize a product release.
- `integration`: staging branch for combining approved module upgrades. Initially points at the confirmed public main baseline.
- `feature/<module>-<upgrade>`: one bounded module upgrade, normally based on the agreed integration baseline.

## Workflow
1. Confirm the common source baseline before development. A published binary's version label does not prove that the repository contains matching source.
2. Create/reuse an isolated worktree and feature branch. Keep user data, local paths, credentials, generated installers and unrelated edits out of commits.
3. Add targeted regression coverage, run the affected tests and record unverified boundaries.
4. Push only the feature commits and open a Draft PR targeting `integration`. Explain dependencies and baseline differences. Do not merge a private whole-workspace snapshot as a shortcut.
5. After review and baseline alignment, merge approved module PRs into `integration` and test the combined tree. Recheck shared-file conflicts and behavior, not just patch application.
6. Open a separate integration-to-main PR when the combined result is accepted. Version changes, installers, GitHub Releases, website downloads and application update channels require their own explicit release authorization.

## Initial source-baseline gap
At setup, public `desktop-preview/VERSION` is `0.3.0-preview.23` while a newer local candidate is `0.4.3.2`. The storage native-enumeration PR therefore stays Draft and carries only the bounded scanner/test/workflow changes. Reconcile the product source baseline before marking it ready for final integration; do not bulk-push unreviewed local snapshots to close the gap.

Do not create empty placeholder branches for unimplemented features. Open module branches as real work begins. Preserve active worktrees while their PRs remain under review.
