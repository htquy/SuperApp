import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'sh_hrm_fe',
      filename: 'remoteEntry.js',
      exposes: {
        './EmployeeList': './src/components/EmployeeList.jsx',
      },
      shared: ['react', 'react-dom']
    })
  ],
  server: {
    port: 3001
  },
  build: {
    target: 'esnext'
  }
})
