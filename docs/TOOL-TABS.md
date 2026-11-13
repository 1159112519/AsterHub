# Resource tool tabs

Interaction reference: TUFFUI Tab Panels, official repository yurigdesigner/tuffui, tree 589cbb03ee1b06deaf02b78dde5395db99832397. Reviewed registry/tab-panels/tab-panels.tsx and docs.ts; its license is MIT (2026 TUFFUI contributors).

AsterHub uses its own React/CSS implementation in desktop/web/src/foundation/ToolNavigation.jsx. No TUFFUI source, assets, package, motion library or transitive dependency is bundled. The reference informs the moving underline, roving keyboard navigation and lazy panel behavior. Existing AsterHub workspace/query caches retain state; only the selected tool is rendered.

Regression: sparse CPU sample matching, independent memory endpoint matching, remote historical NVIDIA path provenance, native tab keyboard and indicator alignment, DPI/window chrome interaction.
