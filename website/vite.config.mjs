import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
export default defineConfig({ plugins:[react()], base:"./", build:{outDir:"dist/client"}, server:{host:"127.0.0.1",port:8783,strictPort:true} });
