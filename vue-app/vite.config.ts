import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import { default as vue } from '@vitejs/plugin-vue'
import VueRouter from 'unplugin-vue-router/vite'
import { defineConfig } from 'vite'

export default defineConfig({
    base: '/',
    build: {
        outDir: '../Resources/Raw/wwwroot',
        emptyOutDir: true,
    },
    plugins: [
        VueRouter(),
        vue(),
        tailwindcss(),
    ],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url)),
        },
    },
    server: {
        host: '0.0.0.0',
        port: 5173,
        strictPort: true,
        watch: {
            usePolling: true,
            interval: 100
        },
        hmr: {
            host: 'localhost',
            protocol: 'ws',
            port: 5173,
            clientPort: 5173,
        },
    },
})
