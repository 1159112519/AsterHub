# Prototype Instructions

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.

## AQ Server website
Use React 18, TypeScript, Vite, Tailwind CSS, lucide-react. Typography: Inter and Playfair Display, with Microsoft YaHei UI fallback for Chinese. Keep the first section dark and full viewport. Preserve the two-image soft circular pointer reveal, reduced-motion behavior and responsive phone/tablet/desktop layouts. CTA must download an existing installer; release history must use readable line spacing. Never publish credentials, signing private keys or monitoring archives.

Platform releases are independent: read src/release-versions.json for Windows and Ubuntu versions. Display each platform version and update date beside its download, with separate release notes. Windows 0.2.0 and retained Ubuntu 0.1.3 must not be presented as matching versions. Future Windows releases do not imply an Ubuntu update.
