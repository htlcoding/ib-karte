import type { Feature, FeatureCollection, MultiPolygon, Polygon, Position } from 'geojson'

type Land = Feature<Polygon | MultiPolygon, { n: string }>

function imRing(x: number, y: number, ring: Position[]) {
  let drin = false
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, yi] = ring[i], [xj, yj] = ring[j]
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi || 1e-12) + xi) drin = !drin
  }
  return drin
}

function polygone(f: Land): Position[][][] {
  return f.geometry.type === 'Polygon' ? [f.geometry.coordinates] : f.geometry.coordinates
}

/** Abstand (in Grad, Länge nach Breite gestaucht) von einem Punkt zur nächsten Kante eines Rings. */
function abstand(x: number, y: number, ring: Position[]) {
  const k = Math.cos((y * Math.PI) / 180)
  let min = Infinity
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const ax = ring[j][0] * k, ay = ring[j][1], bx = ring[i][0] * k, by = ring[i][1], px = x * k
    const dx = bx - ax, dy = by - ay, l = dx * dx + dy * dy
    const s = l ? Math.max(0, Math.min(1, ((px - ax) * dx + (y - ay) * dy) / l)) : 0
    min = Math.min(min, Math.hypot(px - ax - s * dx, y - ay - s * dy))
  }
  return min
}

/**
 * Land eines Punktes. Liegt er knapp außerhalb jeder Fläche (Hafen- und Küstenstädte wie Kopenhagen oder Livorno
 * fallen bei vereinfachten Umrissen ins Wasser), zählt das nächstgelegene Land im Umkreis von etwa 30 km.
 */
export function landVon(laender: Land[], lon: number, lat: number) {
  const drin = laender.find((f) => polygone(f).some((p) => imRing(lon, lat, p[0]) && !p.slice(1).some((h) => imRing(lon, lat, h))))
  if (drin) return drin
  let best: Land | undefined, min = 0.3
  for (const f of laender) for (const p of polygone(f)) {
    const d = abstand(lon, lat, p[0])
    if (d < min) { min = d; best = f }
  }
  return best
}

/** Ring, der über die Datumsgrenze läuft (würde sonst quer über die Karte gezeichnet). */
const ueberDatumsgrenze = (r: Position[]) => Math.max(...r.map((p) => p[0])) - Math.min(...r.map((p) => p[0])) > 300

/**
 * Abdeckung für alle Länder ohne Einträge: eine Weltfläche, aus der die Länder mit Einträgen ausgespart sind,
 * dazu die Umrisse dieser Länder.
 */
export function abdeckung(alle: FeatureCollection, punkte: [number, number][]) {
  const laender = alle.features as Land[]
  const mit = new Set<Land>()
  for (const [lon, lat] of punkte) { const l = landVon(laender, lon, lat); if (l) mit.add(l) }
  const loecher: Position[][] = []
  // Ringe über die Datumsgrenze an 180° abschneiden statt quer über die Karte zu zeichnen.
  for (const l of mit) for (const p of polygone(l)) loecher.push(ueberDatumsgrenze(p[0]) ? p[0].map((q) => (q[0] < 0 ? [180, q[1]] : q)) : p[0])
  const welt: Position[] = [[-180, -85], [180, -85], [180, 85], [-180, 85], [-180, -85]]
  const flaeche: Feature<Polygon> = { type: 'Feature', properties: {}, geometry: { type: 'Polygon', coordinates: [welt, ...loecher] } }
  const umrisse: FeatureCollection = { type: 'FeatureCollection', features: loecher.map((r) => ({ type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: r } })) }
  return { flaeche, umrisse }
}
