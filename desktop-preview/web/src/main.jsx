import React from "react";
import { createRoot } from "react-dom/client";
import App from "./native/NativeApp.jsx";
import "./styles.css";
import "./refinements.css";
import "./native/native.css";
import "./motion-preview5.css";
import "./native/preview5-layout.css";
import "./gpu-inspector-preview5.css";
import "./native/preview6-state.css";

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
