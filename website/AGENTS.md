# Prototype Instructions

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.

## AQ Server website
Use React 18, TypeScript, Vite, Tailwind CSS, lucide-react. Typography: Inter and Playfair Display, with Microsoft YaHei UI fallback for Chinese. Keep the first section dark and full viewport. Preserve the two-image soft circular pointer reveal, reduced-motion behavior and responsive phone/tablet/desktop layouts. CTA must download an existing installer; release history must use readable line spacing. Never publish credentials, signing private keys or monitoring archives.

Platform releases are independent: read src/release-versions.json for Windows and Ubuntu versions. Display each platform version and update date beside its download, with separate release notes. Windows 0.2.0 and retained Ubuntu 0.1.3 must not be presented as matching versions. Future Windows releases do not imply an Ubuntu update.

## Website layout fixes 2026-10-29
Keep each platform requirement directly under its download button in normal flow. Keep Windows/Ubuntu release cards together in .release-stack; never let a third direct grid item wrap into the introduction column. On phones the product image follows the download groups instead of covering them. Keep public platform versions independent and retain signed manifests and existing installer filenames. New AsterHub icon proposals remain separate until selected.

## Windows preview channel 2026-10-29
Show 0.3.0-preview.4 explicitly as the new Windows workbench preview with a separate installer and signed windows-preview.json. Retain stable 0.2.1 download and stable.json unchanged. Ubuntu 0.1.3 remains independent. Preview and stable data are separate. Do not promise automatic migration of saved credentials between them.

## AsterHub scroll experience 2026-10-29
Use current preview.4 screenshots with synthetic, explicitly labelled example data. Never reuse the former AQ Server workbench images for the current Windows preview. The primary route is /asterhub/. Keep previous release URLs and signed manifests working. Desktop chapters change a sticky product image as the reader scrolls; tablet and phone use the same chapters with inline images, without scroll capture. Keep the soft pointer reveal, keyboard controls and reduced-motion support. Use Chinese primary copy, English secondary copy. Do not imply Ubuntu has current Windows features.

## Approved silver glass brand 2026-10-29
Use the selected smooth silver glass icon from outputs/asterhub-zheguang-glass-v4 for the site header, favicon and app brand. Tablet and phone inline chapter images and text use subtle, non-linear entrances once per page, without scroll capture or looping; reduced-motion shows content immediately. Preserve the desktop sticky chapter images and the hero pointer reveal.

## Preview.5 polish delivery 2026-10-29
Current Windows preview is 0.3.0-preview.5; use actual preview.5 screenshots with the same explicitly labelled synthetic data. Preserve historical downloads and signatures. Settings actions are contextual, dense CPU grids wrap, and number/curve geometry remains stable.
