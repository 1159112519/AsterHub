import {setLanguage} from './i18n/index.js';
import {native} from './native/bridge.js';
import React from "react";
import { createRoot } from "react-dom/client";
import App from "./native/NativeApp.jsx";
import "./foundation/tokens.css";
import "./styles.css";
import "./foundation/ui.css";
import "./refinements.css";
import "./native/native.css";
import "./motion-layout.css";
import "./native/layout.css";
import "./gpu-inspector.css";
import "./native/state.css";

import "./native/readability.css";
import "./resizable-table.css";
import "./native/usability.css";

// The launch shell stays up until each interface font weight is available.
// Mounting first would allow fallback glyphs to move when a popup is opened.
async function mount(){
 if(native.available){try{const preferences=await native.request('desktop.settings');setLanguage(preferences.language)}catch(error){console.error('Language preferences unavailable',error)}}
 const faces=['400 14px Inter','400 14px "HarmonyOS Sans SC"','500 14px "HarmonyOS Sans SC"','700 14px "HarmonyOS Sans SC"'];
 const results=await Promise.allSettled(faces.map((face,i)=>document.fonts.load(face,i?'设置':'AsterHub 0123456789')));
 if(results.some(r=>r.status==='rejected'||!r.value.length))console.error('Unable to load an AsterHub interface font');
 createRoot(document.getElementById('root')).render(<React.StrictMode><App/></React.StrictMode>);
}
void mount();
import "./appearance/appearance.css";

import './foundation/control-alignment.css';

import './i18n/english-layout.css';
