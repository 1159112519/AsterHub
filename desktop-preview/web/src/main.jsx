import React from "react";
import { createRoot } from "react-dom/client";
import App from "./native/NativeApp.jsx";
import "./styles.css";
import "./refinements.css";
import "./native/native.css";

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
