import type { Map as KarteMap } from 'maplibre-gl'
import type { KarteEintrag } from './typen'

/** Pixeldichte der erzeugten Bilder (scharf auf hochauflösenden Bildschirmen). */
const PR = 2

/** Größen in CSS-Pixeln bei voller Zoomstufe. */
export const GROESSE = { person: 26, org: 16, medium: 18, ereignis: 13 } as const
/** Abstand zwischen Einträgen am selben Ort (Spirale), in CSS-Pixeln bei voller Größe. */
export const ABSTAND = 15

// Strichsymbole auf einem 24er-Raster.
export const PFADE = {
  // Unbekanntes Profilbild: Kopf und Schultern (gefüllt)
  silhouette: 'M12 4.2a3.9 3.9 0 1 1 0 7.8a3.9 3.9 0 0 1 0-7.8z M4.6 20.2c.4-4 3.6-6.4 7.4-6.4s7 2.4 7.4 6.4z',
  zeitung: 'M4 5h12v14H6.5A2.5 2.5 0 0 1 4 16.5z M16 9h4v8a2 2 0 0 1-2 2h-2 M7 8.5h6 M7 12h6 M7 15.5h4',
  kalender: 'M4 6h16v14H4z M4 10h16 M8 3.5v4 M16 3.5v4',
  parlament: 'M3 20.5h18 M4.5 17.5h15 M6 17.5v-7 M10 17.5v-7 M14 17.5v-7 M18 17.5v-7 M3.5 10h17L12 4.5z',
  rathaus: 'M3 20.5h18 M5 20.5V11h14v9.5 M10.5 20.5v-4h3v4 M9 11V7.5l3-4 3 4V11 M8 14h1.5 M14.5 14h1.5',
  keller: 'M3 6.5h5v4h4v4h4v4h5 M18 3.5v6 M15.5 7l2.5 2.5L20.5 7',
  parteizentrale: 'M4 20.5V8h10v12.5 M2.5 20.5h19 M14 12h5v8.5 M7 11h1 M10 11h1 M7 14h1 M10 14h1 M7 17h1 M10 17h1 M16 3v6 M16 3.5h4l-1 1.5 1 1.5h-4',
  gericht: 'M12 4v16 M8 20h8 M5.5 7.5h13 M5.5 7.5 3 13.5a2.6 2.6 0 0 0 5 0z M18.5 7.5 16 13.5a2.6 2.6 0 0 0 5 0z',
  haus: 'M3 11.5 12 4.5l9 7 M5.5 10v10.5h13V10 M10 20.5v-5.5h4v5.5',
} as const

export type OrtSymbol = 'parlament' | 'rathaus' | 'keller' | 'parteizentrale' | 'gericht' | 'haus'

function leinwand(w: number, h: number) {
  const c = document.createElement('canvas')
  c.width = Math.ceil(w * PR); c.height = Math.ceil(h * PR)
  const g = c.getContext('2d')!
  g.scale(PR, PR)
  return { c, g }
}

function glyphe(g: CanvasRenderingContext2D, pfad: string, x: number, y: number, s: number, farbe: string, fuellen = false, strich = 1.7) {
  g.save()
  g.translate(x, y); g.scale(s / 24, s / 24)
  const p = new Path2D(pfad)
  if (fuellen) { g.fillStyle = farbe; g.fill(p) } else {
    g.strokeStyle = farbe; g.lineWidth = strich; g.lineCap = 'round'; g.lineJoin = 'round'; g.stroke(p)
  }
  g.restore()
}

function eckig(g: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  g.beginPath(); g.roundRect(x, y, w, h, r)
}

/** Marker für einen Eintrag ohne eigenes Bild. */
export function markerBild(typ: KarteEintrag['typ'], farbe: string) {
  const s = GROESSE[typ]
  if (typ === 'person') {
    const { c, g } = leinwand(s, s)
    g.beginPath(); g.arc(s / 2, s / 2, s / 2 - 0.5, 0, Math.PI * 2)
    g.fillStyle = farbe; g.fill(); g.strokeStyle = 'rgba(0,0,0,.28)'; g.lineWidth = 1; g.stroke()
    g.save(); g.beginPath(); g.arc(s / 2, s / 2, s / 2 - 1, 0, Math.PI * 2); g.clip()
    glyphe(g, PFADE.silhouette, s * 0.1, s * 0.16, s * 0.8, 'rgba(255,255,255,.92)', true)
    g.restore()
    return c
  }
  if (typ === 'ereignis') {
    const d = s * Math.SQRT2
    const { c, g } = leinwand(d, d)
    g.translate(d / 2, d / 2); g.rotate(Math.PI / 4)
    g.fillStyle = farbe; g.fillRect(-s / 2, -s / 2, s, s)
    g.strokeStyle = 'rgba(0,0,0,.35)'; g.lineWidth = 1; g.strokeRect(-s / 2 + .5, -s / 2 + .5, s - 1, s - 1)
    return c
  }
  const { c, g } = leinwand(s, s)
  eckig(g, .5, .5, s - 1, s - 1, 2); g.fillStyle = farbe; g.fill(); g.strokeStyle = 'rgba(0,0,0,.35)'; g.lineWidth = 1; g.stroke()
  if (typ === 'medium') glyphe(g, PFADE.zeitung, 1.5, 1.5, s - 3, '#ffffff', false, 2)
  return c
}

/** Marker aus einem hinterlegten Bild (Foto oder Logo). */
export function markerAusBild(img: HTMLImageElement | ImageBitmap, typ: KarteEintrag['typ'], farbe: string, ausschnitt?: KarteEintrag['ausschnitt']) {
  const s = typ === 'person' ? GROESSE.person : 22
  const { c, g } = leinwand(s, s)
  g.save()
  if (typ === 'person') { g.beginPath(); g.arc(s / 2, s / 2, s / 2 - 0.5, 0, Math.PI * 2) } else eckig(g, .5, .5, s - 1, s - 1, 2)
  g.fillStyle = '#ffffff'; g.fill(); g.clip()
  const w = img.width, h = img.height
  if (typ === 'person') {
    // Quadratischer Ausschnitt um den Gesichtsmittelpunkt; ohne Angabe: Bildmitte, obere Hälfte bevorzugt.
    const a = ausschnitt ?? { x: 50, y: 35, zoom: 1 }
    const seite = Math.min(w, h) / Math.max(1, a.zoom)
    const sx = Math.min(Math.max((a.x / 100) * w - seite / 2, 0), w - seite), sy = Math.min(Math.max((a.y / 100) * h - seite / 2, 0), h - seite)
    g.drawImage(img, sx, sy, seite, seite, 0, 0, s, s)
  }
  else { const k = Math.min((s - 4) / w, (s - 4) / h); g.drawImage(img, (s - w * k) / 2, (s - h * k) / 2, w * k, h * k) }
  g.restore()
  g.lineWidth = 1.5; g.strokeStyle = farbe
  if (typ === 'person') { g.beginPath(); g.arc(s / 2, s / 2, s / 2 - 0.75, 0, Math.PI * 2) } else eckig(g, .75, .75, s - 1.5, s - 1.5, 2)
  g.stroke()
  return c
}

/** Auswahlring, liegt unter dem Marker. */
function ringBild(form: 'rund' | 'eckig' | 'raute') {
  const s = form === 'rund' ? GROESSE.person + 10 : form === 'raute' ? (GROESSE.ereignis + 8) * Math.SQRT2 : 28
  const { c, g } = leinwand(s, s)
  const zeichne = (r: number, farbe: string) => {
    g.fillStyle = farbe
    if (form === 'rund') { g.beginPath(); g.arc(s / 2, s / 2, r, 0, Math.PI * 2); g.fill() }
    else if (form === 'eckig') { eckig(g, s / 2 - r, s / 2 - r, 2 * r, 2 * r, 4); g.fill() }
    else { g.save(); g.translate(s / 2, s / 2); g.rotate(Math.PI / 4); g.fillRect(-r / Math.SQRT2, -r / Math.SQRT2, 2 * r / Math.SQRT2, 2 * r / Math.SQRT2); g.restore() }
  }
  zeichne(s / 2, '#1a1a1a'); zeichne(s / 2 - 2.5, '#ffffff')
  return c
}

/** Symbol für einen wichtigen Ort (Parlament, Rathaus, Keller …). */
export function ortBild(symbol: OrtSymbol) {
  const s = 30
  const { c, g } = leinwand(s, s)
  eckig(g, .5, .5, s - 1, s - 1, 5); g.fillStyle = '#ffffff'; g.fill(); g.strokeStyle = '#4a4a4a'; g.lineWidth = 1; g.stroke()
  glyphe(g, PFADE[symbol], 5, 5, 20, '#262626', false, 1.6)
  return c
}

export function bildDaten(c: HTMLCanvasElement) {
  return { width: c.width, height: c.height, data: new Uint8Array(c.getContext('2d')!.getImageData(0, 0, c.width, c.height).data.buffer) }
}

export function registriere(map: KarteMap, name: string, c: HTMLCanvasElement) {
  if (map.hasImage(name)) map.removeImage(name)
  map.addImage(name, bildDaten(c), { pixelRatio: PR })
}

export function grundSymbole(map: KarteMap) {
  registriere(map, 'ring-rund', ringBild('rund'))
  registriere(map, 'ring-eckig', ringBild('eckig'))
  registriere(map, 'ring-raute', ringBild('raute'))
  for (const s of Object.keys(PFADE) as (keyof typeof PFADE)[])
    if (['parlament', 'rathaus', 'keller', 'parteizentrale', 'gericht', 'haus'].includes(s)) registriere(map, 'ort-' + s, ortBild(s as OrtSymbol))
}
