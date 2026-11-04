import React from "react";
import { createRoot } from "react-dom/client";
import App from "./native/NativeApp.jsx";
import "./foundation/tokens.css";
import "./styles.css";
import "./foundation/ui.css";
import "./refinements.css";
import "./native/native.css";
import "./motion-preview5.css";
import "./native/preview5-layout.css";
import "./gpu-inspector-preview5.css";
import "./native/preview6-state.css";

import "./native/readability-preview10.css";
import "./resizable-table.css";
import "./native/usability-preview12.css";

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
import "./appearance/appearance.css";

import './foundation/control-alignment.css';
