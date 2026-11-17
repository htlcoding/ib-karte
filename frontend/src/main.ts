import { createApp } from 'vue'
import 'maplibre-gl/dist/maplibre-gl.css'
import { setWorkerUrl } from 'maplibre-gl'
// Der Kartendienst rechnet Kacheln in einem eigenen Worker; Vite bündelt ihn als eigene Datei.
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import './stil.css'
import App from './App.vue'

setWorkerUrl(workerUrl)
createApp(App).mount('#app')
