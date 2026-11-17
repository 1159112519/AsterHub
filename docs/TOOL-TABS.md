# Resource tool navigation

Interaction reference: TUFFUI Segmented Control, official registry at https://tuffui.com/r/segmented-control.json (reviewed 2026-11-17), MIT License, copyright 2026 TUFFUI contributors.

AsterHub keeps its own React/CSS implementation in desktop/web/src/foundation/ToolNavigation.jsx. The reference informs the capsule container, sliding raised selection and instant keyboard selection. No additional motion package is required. Resource tabs retain tablist/tab/tabpanel semantics, Home/End and arrow navigation, a stable actions container, lazy panels and existing query caches. The system title bar is unchanged.

CPU analysis measures chart geometry before rendering; static analysis is memoized across live captures. Core history uses bounded visible rows above 32 cores, cached locale formatters and keyboard scrolling. This avoids rendering thousands of offscreen cells.

Regression covers sparse CPU samples, memory endpoint gaps, recorded remote GPU path provenance, tab keyboard/pill alignment, chart first-frame geometry and a 256-core replay. Custom-range axis bounds are copied when locked, isolated by metric/device/range and released on unlock.

资源工具切换栏按 2026-11-12 修订4：38px 总高、30px 选项、14/20 常规文字；暗轨道与亮选中块，选中背景滑动，文字不位移。
