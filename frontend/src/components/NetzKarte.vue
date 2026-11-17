<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import { AttributionControl, Map as KarteMap, NavigationControl, Popup, type GeoJSONSource, type MapGeoJSONFeature, type Offset } from 'maplibre-gl'
import type { Feature, FeatureCollection, LineString, Point } from 'geojson'
import type { Nachbar } from '../lib/typen'
import type { Netz } from '../lib/netz'
import { ABSTAND, GROESSE, grundSymbole, markerAusBild, markerBild, registriere } from '../lib/symbole'
import { abdeckung } from '../lib/geo'
import InfoKasten from './InfoKasten.vue'
import LinienKasten from './LinienKasten.vue'
import OrtKasten from './OrtKasten.vue'
import type { KarteOrt } from '../lib/typen'

const props = defineProps<{ netz: Netz }>()
const netz = props.netz

const STIL = 'https://tiles.openfreemap.org/styles/positron'
const HINTERGRUND = 'rgb(242,243,240)'
const mobil = matchMedia('(max-width: 600px)').matches

// Markergröße je Zoomstufe; der Abstand in der Spirale wächst und schrumpft mit.
const STUFEN: [number, number][] = [[6, 0.32], [9, 0.5], [11, 0.78], [12, 1]]
const GROESSE_EXPR = ['interpolate', ['linear'], ['zoom'], ...STUFEN.flat()] as any
function markerSkala(z: number) {
  if (z <= STUFEN[0][0]) return STUFEN[0][1]
  for (let i = 1; i < STUFEN.length; i++) {
    const [z1, s1] = STUFEN[i - 1], [z2, s2] = STUFEN[i]
    if (z <= z2) return s1 + ((z - z1) / (z2 - z1)) * (s2 - s1)
  }
  return STUFEN[STUFEN.length - 1][1]
}

const behaelter = ref<HTMLDivElement>()
let map: KarteMap
const sel = ref<string | null>(null)
const kastenZiel = ref<HTMLElement | null>(null)
const linienZiel = ref<HTMLElement | null>(null)
const linie = shallowRef<{ partner: string; art: string; liste: Nachbar[] } | null>(null)
const selEintrag = computed(() => (sel.value ? netz.eintrag.get(sel.value)! : null))
let infoPopup: Popup | null = null
let linienPopup: Popup | null = null
let ortPopup: Popup | null = null
const ortZiel = ref<HTMLElement | null>(null)
const ortWahl = shallowRef<KarteOrt | null>(null)
/** Vom Nutzer verschobener Versatz des Info-Kästchens (Pixel) */
let zug = { x: 0, y: 0 }
let tipp: Popup
let wechsel = false
const LEER: FeatureCollection = { type: 'FeatureCollection', features: [] }

// ---------- Lage auf dem Bildschirm (Ort + Versatz in der Spirale) ----------
function versatz(id: string, z = map.getZoom()) {
  const l = netz.lage.get(id)!, s = markerSkala(z)
  return { x: l.dx * ABSTAND * s, y: l.dy * ABSTAND * s }
}
function bildschirm(id: string) {
  const l = netz.lage.get(id)!, p = map.project([l.lon, l.lat]), o = versatz(id)
  return { x: p.x + o.x, y: p.y + o.y }
}
const merc = (lon: number, lat: number, z: number) => {
  const w = 512 * 2 ** z, s = Math.sin((lat * Math.PI) / 180)
  return { x: ((lon + 180) / 360) * w, y: (0.5 - Math.log((1 + s) / (1 - s)) / (4 * Math.PI)) * w }
}
const unmerc = (x: number, y: number, z: number) => {
  const w = 512 * 2 ** z
  return [(x / w) * 360 - 180, (Math.atan(Math.sinh(Math.PI * (1 - (2 * y) / w))) * 180) / Math.PI] as [number, number]
}

// ---------- Daten für die Kartenebenen ----------
function iconName(id: string) {
  const e = netz.eintrag.get(id)!
  return geladeneBilder.has(id) ? 'b-' + id : `m-${e.typ}-${e.umfeld}`
}
const geladeneBilder = new Set<string>()
function eintragDaten(): FeatureCollection<Point> {
  return {
    type: 'FeatureCollection',
    features: [...netz.lage].map(([id, l]) => {
      const e = netz.eintrag.get(id)!
      return {
        type: 'Feature', geometry: { type: 'Point', coordinates: [l.lon, l.lat] },
        properties: {
          id, icon: iconName(id), ring: e.typ === 'person' ? 'ring-rund' : e.typ === 'ereignis' ? 'ring-raute' : 'ring-eckig',
          off: [l.dx * ABSTAND, l.dy * ABSTAND], rang: e.typ === 'person' ? 0 : 1, name: e.name,
        },
      }
    }),
  }
}
function ortDaten(): FeatureCollection<Point> {
  return {
    type: 'FeatureCollection',
    features: netz.daten.orte.filter((o) => o.symbol).map((o) => ({
      type: 'Feature', geometry: { type: 'Point', coordinates: [o.lon, o.lat] }, properties: { name: o.name, icon: 'ort-' + o.symbol, key: o.key },
    })),
  }
}

// ---------- Linien der Auswahl ----------
const kurve = (art: string) => [0, 0.08, -0.08, 0.16, -0.16, 0.24, -0.24][netz.artReihe.indexOf(art)] ?? 0
let linienGruppen = new Map<string, { partner: string; art: string; liste: Nachbar[] }>()
function zeichneLinien() {
  const quelle = map.getSource('linien') as GeoJSONSource | undefined
  if (!quelle) return
  if (!sel.value) { quelle.setData(LEER); return }
  linienGruppen = new Map()
  for (const n of netz.belegt(sel.value)) {
    if (!netz.lage.has(n.anderer) || n.anderer === sel.value) continue
    const key = n.anderer + '|' + n.art
    if (!linienGruppen.has(key)) linienGruppen.set(key, { partner: n.anderer, art: n.art, liste: [] })
    linienGruppen.get(key)!.liste.push(n)
  }
  const a = bildschirm(sel.value)
  const features: Feature<LineString>[] = []
  for (const [key, g] of linienGruppen) {
    const b = bildschirm(g.partner), k = 0.12 + kurve(g.art)
    const m = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }, d = { x: b.x - a.x, y: b.y - a.y }
    const c = { x: m.x - d.y * k, y: m.y + d.x * k }
    const coords: [number, number][] = []
    for (let i = 0; i <= 24; i++) {
      const t = i / 24, u = 1 - t
      const p = map.unproject([u * u * a.x + 2 * u * t * c.x + t * t * b.x, u * u * a.y + 2 * u * t * c.y + t * t * b.y])
      coords.push([p.lng, p.lat])
    }
    const art = netz.art.get(g.art)!
    features.push({
      type: 'Feature', geometry: { type: 'LineString', coordinates: coords },
      properties: { key, farbe: art.farbe, gestrichelt: art.gestrichelt, breite: 2 + Math.min(4, 1.4 * (g.liste.length - 1)), partner: g.partner },
    })
  }
  quelle.setData({ type: 'FeatureCollection', features })
}

// ---------- Hervorheben ----------
function hervorheben() {
  if (!map.getLayer('eintraege')) return
  if (sel.value) {
    const hl = [sel.value, ...netz.belegt(sel.value).map((n) => n.anderer)]
    map.setPaintProperty('eintraege', 'icon-opacity', 0.16)
    map.setPaintProperty('orte', 'icon-opacity', 0.45)
    map.setFilter('eintraege-hervor', ['in', ['get', 'id'], ['literal', hl]])
    map.setFilter('eintraege-ring', ['==', ['get', 'id'], sel.value])
  } else {
    map.setPaintProperty('eintraege', 'icon-opacity', 1)
    map.setPaintProperty('orte', 'icon-opacity', 1)
    map.setFilter('eintraege-hervor', ['==', ['get', 'id'], ''])
    map.setFilter('eintraege-ring', ['==', ['get', 'id'], ''])
  }
}

// ---------- Info-Kästchen ----------
function popupVersatz(id: string): Offset {
  const o = versatz(id), e = netz.eintrag.get(id)!, r = (GROESSE[e.typ] / 2) * markerSkala(map.getZoom()) + 6
  const p = (x: number, y: number): [number, number] => [o.x + x + zug.x, o.y + y + zug.y]
  return {
    center: p(0, 0), top: p(0, r), bottom: p(0, -r), left: p(r, 0), right: p(-r, 0),
    'top-left': p(r * 0.7, r * 0.7), 'top-right': p(-r * 0.7, r * 0.7), 'bottom-left': p(r * 0.7, -r * 0.7), 'bottom-right': p(-r * 0.7, -r * 0.7),
  }
}
/** Schiebt die Karte so weit, dass ein Kästchen ganz sichtbar ist. */
function einpassen(p: Popup | null) {
  const el = p?.getElement()
  if (!el) return
  const r = el.getBoundingClientRect(), k = map.getContainer().getBoundingClientRect(), rand = 12
  let dx = 0, dy = 0
  if (r.top < k.top + rand + (mobil ? 56 : 0)) dy = r.top - k.top - rand - (mobil ? 56 : 0)
  else if (r.bottom > k.bottom - rand - 22) dy = r.bottom - k.bottom + rand + 22
  if (r.left < k.left + rand) dx = r.left - k.left - rand
  else if (r.right > k.right - rand - 52) dx = r.right - k.right + rand + 52
  if (dx || dy) map.panBy([dx, dy], { duration: 250 })
}
/** Kästchen mit der Maus oder dem Finger verschieben (am Kopf bzw. an freien Stellen anfassen). */
function ziehbar(popup: Popup, versatzNeu: (d: { x: number; y: number }) => Offset) {
  const el = popup.getElement()
  let start: { x: number; y: number; d: { x: number; y: number } } | null = null
  const d = { x: 0, y: 0 }
  el.addEventListener('pointerdown', (ev) => {
    const t = ev.target as HTMLElement
    if (t.closest('button, a, input, summary, .was, .text, li') && !t.closest('.ziehgriff')) return
    start = { x: ev.clientX, y: ev.clientY, d: { ...d } }
    el.setPointerCapture(ev.pointerId)
    el.classList.add('zieht')
    ev.preventDefault(); ev.stopPropagation()
  })
  el.addEventListener('pointermove', (ev) => {
    if (!start) return
    d.x = start.d.x + ev.clientX - start.x; d.y = start.d.y + ev.clientY - start.y
    el.classList.add('verschoben')
    popup.setOffset(versatzNeu(d))
  })
  const ende = (ev: PointerEvent) => { if (!start) return; start = null; el.classList.remove('zieht'); try { el.releasePointerCapture(ev.pointerId) } catch { /* schon frei */ } }
  el.addEventListener('pointerup', ende); el.addEventListener('pointercancel', ende)
}

const beobachter = new ResizeObserver(() => {
  infoPopup?.setLngLat(infoPopup.getLngLat())
  linienPopup?.setLngLat(linienPopup.getLngLat())
})

function zeigeInfo(id: string) {
  const l = netz.lage.get(id)!
  const div = document.createElement('div')
  wechsel = true; infoPopup?.remove(); linienPopup?.remove(); wechsel = false
  infoPopup = new Popup({ closeOnClick: false, focusAfterOpen: false, maxWidth: mobil ? 'calc(100vw - 32px)' : '372px', offset: popupVersatz(id), className: 'info' })
    .setLngLat([l.lon, l.lat]).setDOMContent(div).addTo(map)
  infoPopup.on('close', () => { if (!wechsel) abwaehlen() })
  zug = { x: 0, y: 0 }
  ziehbar(infoPopup, (d) => { zug = d; return popupVersatz(id) })
  kastenZiel.value = div
  beobachter.observe(div)
  nextTick(() => requestAnimationFrame(() => einpassen(infoPopup)))
}

function zeigeLinie(key: string, wo: [number, number]) {
  const g = linienGruppen.get(key)
  if (!g || !sel.value) return
  linie.value = g
  const div = document.createElement('div')
  wechsel = true; infoPopup?.remove(); linienPopup?.remove(); wechsel = false
  kastenZiel.value = null
  linienPopup = new Popup({ closeOnClick: false, focusAfterOpen: false, maxWidth: mobil ? 'calc(100vw - 32px)' : '340px', offset: 10, className: 'info' })
    .setLngLat(wo).setDOMContent(div).addTo(map)
  linienZiel.value = div
  ziehbar(linienPopup, (d) => [d.x, d.y + 10])
  beobachter.observe(div)
  nextTick(() => requestAnimationFrame(() => einpassen(linienPopup)))
}

function zeigeOrt(key: string) {
  const o = netz.daten.orte.find((x) => x.key === key)
  if (!o) return
  wechsel = true; ortPopup?.remove(); wechsel = false
  ortWahl.value = o
  const div = document.createElement('div')
  ortPopup = new Popup({ closeOnClick: false, focusAfterOpen: false, maxWidth: mobil ? 'calc(100vw - 32px)' : '340px', offset: 20, className: 'info' })
    .setLngLat([o.lon, o.lat]).setDOMContent(div).addTo(map)
  ortPopup.on('close', () => { if (!wechsel) { ortZiel.value = null; ortWahl.value = null } })
  ortZiel.value = div
  ziehbar(ortPopup, (d) => [d.x, d.y + 20])
  beobachter.observe(div)
  nextTick(() => requestAnimationFrame(() => einpassen(ortPopup)))
}

// ---------- Auswahl ----------
function waehle(id: string, fliegen = false) {
  if (!netz.lage.has(id)) return
  if (ortPopup) { wechsel = true; ortPopup.remove(); wechsel = false; ortPopup = null; ortZiel.value = null; ortWahl.value = null }
  sel.value = id
  linie.value = null
  hervorheben()
  const oeffnen = () => { zeichneLinien(); zeigeInfo(id) }
  if (fliegen) {
    const z = Math.max(map.getZoom(), 15), l = netz.lage.get(id)!, p = merc(l.lon, l.lat, z), o = versatz(id, z)
    const h = map.getContainer().clientHeight
    map.flyTo({ center: unmerc(p.x + o.x, p.y + o.y - h * (mobil ? 0.2 : 0.24), z), zoom: z, duration: 800, essential: true })
    map.once('moveend', oeffnen)
  } else oeffnen()
  history.replaceState(null, '', '#' + encodeURIComponent(id))
}
function abwaehlen() {
  if (!sel.value) return
  sel.value = null
  linie.value = null
  wechsel = true; infoPopup?.remove(); linienPopup?.remove(); wechsel = false
  infoPopup = linienPopup = null
  kastenZiel.value = linienZiel.value = null
  zeichneLinien(); hervorheben()
  history.replaceState(null, '', location.pathname + location.search)
}
defineExpose({ waehle })
// Testzugang für automatisierte Prüfungen, nur mit ?debug in der Adresse.
if (new URLSearchParams(location.search).has('debug')) (window as any).__karte = { get map() { return map }, waehle, bildschirm, linien: () => linienGruppen }

// ---------- Karte aufbauen ----------
function bilderLaden() {
  for (const e of netz.daten.eintraege) {
    if (!e.bild || !netz.lage.has(e.id)) continue
    const img = new Image()
    img.crossOrigin = 'anonymous'
    img.onload = () => {
      try {
        registriere(map, 'b-' + e.id, markerAusBild(img, e.typ, netz.farbe(e), e.ausschnitt))
        geladeneBilder.add(e.id)
        ;(map.getSource('eintraege') as GeoJSONSource).setData(eintragDaten())
      } catch { /* Bild ohne CORS-Freigabe: Standardsymbol bleibt */ }
    }
    img.src = e.bild
  }
}

/** Beschriftungen der Grundkarte auf Deutsch (sofern im Kartenmaterial vorhanden). */
function deutsch() {
  for (const l of map.getStyle().layers) {
    if (l.type !== 'symbol') continue
    const feld = map.getLayoutProperty(l.id, 'text-field')
    if (feld && JSON.stringify(feld).includes('name')) map.setLayoutProperty(l.id, 'text-field', ['coalesce', ['get', 'name:de'], ['get', 'name:latin'], ['get', 'name']])
  }
}

function ebenen() {
  const gemeinsam = {
    'icon-size': GROESSE_EXPR, 'icon-offset': ['array', 'number', 2, ['get', 'off']] as any,
    'icon-allow-overlap': true, 'icon-ignore-placement': true,
  }
  map.addSource('abdeckung', { type: 'geojson', data: LEER })
  map.addLayer({ id: 'abdeckung', type: 'fill', source: 'abdeckung', paint: { 'fill-color': HINTERGRUND } })
  map.addSource('laender-rand', { type: 'geojson', data: LEER })
  map.addLayer({ id: 'abdeckung-rand', type: 'line', source: 'laender-rand', paint: { 'line-color': '#b9bbb6', 'line-width': 0.8 } })

  map.addSource('linien', { type: 'geojson', data: LEER })
  map.addLayer({ id: 'linien-rand', type: 'line', source: 'linien', layout: { 'line-cap': 'round' }, paint: { 'line-color': '#ffffff', 'line-opacity': 0.85, 'line-width': ['+', ['get', 'breite'], 2.5] } })
  map.addLayer({ id: 'linien', type: 'line', source: 'linien', filter: ['!', ['get', 'gestrichelt']], layout: { 'line-cap': 'round' }, paint: { 'line-color': ['get', 'farbe'], 'line-width': ['get', 'breite'] } })
  map.addLayer({ id: 'linien-strich', type: 'line', source: 'linien', filter: ['get', 'gestrichelt'], paint: { 'line-color': ['get', 'farbe'], 'line-width': ['get', 'breite'], 'line-dasharray': [2.5, 2.5] } })
  map.addLayer({ id: 'linien-treffer', type: 'line', source: 'linien', paint: { 'line-color': '#000000', 'line-opacity': 0, 'line-width': 16 } })

  map.addSource('eintraege', { type: 'geojson', data: eintragDaten() })
  map.addLayer({ id: 'eintraege-ring', type: 'symbol', source: 'eintraege', filter: ['==', ['get', 'id'], ''], layout: { ...gemeinsam, 'icon-image': ['get', 'ring'] } })
  map.addLayer({ id: 'eintraege', type: 'symbol', source: 'eintraege', layout: { ...gemeinsam, 'icon-image': ['get', 'icon'], 'symbol-sort-key': ['get', 'rang'] }, paint: { 'icon-opacity': 1, 'icon-opacity-transition': { duration: 150 } } })
  map.addSource('orte', { type: 'geojson', data: ortDaten() })
  map.addLayer({ id: 'orte', type: 'symbol', source: 'orte', layout: { 'icon-image': ['get', 'icon'], 'icon-size': ['interpolate', ['linear'], ['zoom'], 6, 0.5, 10, 0.8, 12, 1], 'icon-allow-overlap': true, 'icon-ignore-placement': true } })
  map.addLayer({
    id: 'orte-namen', type: 'symbol', source: 'orte', minzoom: 13.5,
    layout: { 'text-field': ['get', 'name'], 'text-font': ['Noto Sans Regular'], 'text-size': 12, 'text-anchor': 'bottom', 'text-offset': [0, -1.6], 'text-optional': true },
    paint: { 'text-color': '#333333', 'text-halo-color': '#ffffff', 'text-halo-width': 1.5 },
  })

  map.addLayer({ id: 'eintraege-hervor', type: 'symbol', source: 'eintraege', filter: ['==', ['get', 'id'], ''], layout: { ...gemeinsam, 'icon-image': ['get', 'icon'], 'symbol-sort-key': ['get', 'rang'] } })
}

async function abdeckungLaden() {
  try {
    const welt = await (await fetch(`${import.meta.env.BASE_URL}laender.geojson`)).json()
    const punkte = netz.daten.orte.map((o) => [o.lon, o.lat] as [number, number])
    const { flaeche, umrisse } = abdeckung(welt, punkte)
    ;(map.getSource('abdeckung') as GeoJSONSource).setData(flaeche)
    ;(map.getSource('laender-rand') as GeoJSONSource).setData(umrisse)
  } catch { /* ohne Abdeckung bleibt die volle Grundkarte */ }
}

const PUNKTE = ['eintraege-hervor', 'eintraege', 'orte']
onMounted(() => {
  map = new KarteMap({
    container: behaelter.value!, style: STIL, center: mobil ? [16.357, 48.2] : [16.347, 48.199], zoom: mobil ? 11 : 12,
    minZoom: 2.5, maxZoom: 18, attributionControl: false, dragRotate: false, pitchWithRotate: false, touchPitch: false,
  })
  map.touchZoomRotate.disableRotation()
  map.keyboard.disableRotation()
  map.addControl(new NavigationControl({ showCompass: false }), 'top-right')
  map.addControl(new AttributionControl({
    compact: true,
    customAttribution: `Stand ${netz.daten.stand} · Eine Verbindung ist keine Aussage über Mitgliedschaft oder Gesinnung · Personen stehen symbolisch bei ihrer Funktion, nicht an ihrer Wohnadresse · Es gilt die Unschuldsvermutung`,
  }), 'bottom-right')
  map.on('error', (e) => console.error('Kartenfehler:', e.error?.message ?? e))
  tipp = new Popup({ closeButton: false, closeOnClick: false, className: 'tipp', offset: 14 })

  map.on('load', async () => {
    // Am Handy den Quellenhinweis zunächst eingeklappt zeigen (ⓘ öffnet ihn).
    if (mobil) map.getContainer().querySelector('.maplibregl-ctrl-attrib')?.classList.remove('maplibregl-compact-show')
    await document.fonts.ready
    deutsch()
    grundSymbole(map)
    for (const e of netz.daten.eintraege) {
      const name = `m-${e.typ}-${e.umfeld}`
      if (!map.hasImage(name)) registriere(map, name, markerBild(e.typ, netz.farbe(e)))
    }
    ebenen()
    hervorheben()
    abdeckungLaden()
    bilderLaden()
    const h = decodeURIComponent(location.hash.slice(1))
    if (h && netz.lage.has(h)) waehle(h, true)
  })

  map.on('click', (ev) => {
    const p = map.queryRenderedFeatures(ev.point, { layers: PUNKTE.filter((l) => l !== 'orte') })
    if (p.length) { waehle(p[0].properties.id as string); return }
    const g = map.queryRenderedFeatures(ev.point, { layers: ['orte'] })
    if (g.length) { zeigeOrt(g[0].properties.key as string); return }
    const l = sel.value ? map.queryRenderedFeatures(ev.point, { layers: ['linien-treffer'] }) : []
    if (l.length) { zeigeLinie(l[0].properties.key as string, [ev.lngLat.lng, ev.lngLat.lat]); return }
    abwaehlen()
  })
  map.on('mousemove', (ev) => {
    const f: MapGeoJSONFeature[] = map.queryRenderedFeatures(ev.point, { layers: [...PUNKTE, ...(sel.value ? ['linien-treffer'] : [])] })
    map.getCanvas().style.cursor = f.length ? 'pointer' : ''
    if (!f.length) { tipp.remove(); return }
    const g = f[0], text = g.layer.id === 'linien-treffer' ? netz.eintrag.get(g.properties.partner as string)!.name : (g.properties.name as string)
    tipp.setLngLat(ev.lngLat).setText(text).addTo(map)
  })
  map.on('mouseout', () => tipp.remove())

  // Während des Zoomens Linien und Kästchen an die mitwachsenden Abstände anpassen.
  let geplant = false
  map.on('zoom', () => {
    if (geplant) return
    geplant = true
    requestAnimationFrame(() => {
      geplant = false
      if (sel.value) { zeichneLinien(); infoPopup?.setOffset(popupVersatz(sel.value)) }
    })
  })
  window.addEventListener('keydown', taste)
})
function taste(e: KeyboardEvent) { if (e.key === 'Escape') abwaehlen() }
onBeforeUnmount(() => { window.removeEventListener('keydown', taste); beobachter.disconnect(); map?.remove() })
</script>

<template>
  <div ref="behaelter" class="karte" role="region" aria-label="Karte des Netzwerks"></div>
  <Teleport v-if="kastenZiel && selEintrag" :to="kastenZiel">
    <InfoKasten :eintrag="selEintrag" :netz="netz" @waehle="(id) => waehle(id, true)" />
  </Teleport>
  <Teleport v-if="ortZiel && ortWahl" :to="ortZiel">
    <OrtKasten :ort="ortWahl" :netz="netz" @waehle="(id) => waehle(id, true)" />
  </Teleport>
  <Teleport v-if="linienZiel && linie && selEintrag" :to="linienZiel">
    <LinienKasten :a="selEintrag" :b="netz.eintrag.get(linie.partner)!" :art="linie.art" :liste="linie.liste" :netz="netz" />
  </Teleport>
</template>
