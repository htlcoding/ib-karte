import type { KarteDaten, KarteEintrag, Nachbar, Typ } from './typen'

/** Aufbereitete Kartendaten: Nachschlagetabellen, Farben, Nachbarn, Lage der Punkte. */
export class Netz {
  readonly eintrag = new Map<string, KarteEintrag>()
  readonly nachbarn = new Map<string, Nachbar[]>()
  readonly farbeUmfeld = new Map<string, string>()
  readonly nameUmfeld = new Map<string, string>()
  readonly art = new Map<string, { name: string; farbe: string; gestrichelt: boolean }>()
  readonly artReihe: string[]
  /** Lage in Längen-/Breitengrad und Versatz (Einheitsspirale) je Eintrag mit Ort */
  readonly lage = new Map<string, { lon: number; lat: number; dx: number; dy: number }>()

  readonly daten: KarteDaten

  constructor(daten: KarteDaten) {
    this.daten = daten
    for (const u of daten.umfelder) { this.farbeUmfeld.set(u.key, u.farbe); this.nameUmfeld.set(u.key, u.name) }
    for (const a of daten.arten) this.art.set(a.key, a)
    this.artReihe = daten.arten.map((a) => a.key)
    for (const e of daten.eintraege) { this.eintrag.set(e.id, e); this.nachbarn.set(e.id, []) }
    for (const v of daten.verbindungen) {
      this.nachbarn.get(v.von)?.push({ ...v, anderer: v.nach })
      this.nachbarn.get(v.nach)?.push({ ...v, anderer: v.von })
    }
    this.anordnen()
  }

  farbe(e: KarteEintrag) { return this.farbeUmfeld.get(e.umfeld) ?? '#777777' }
  umfeldName(e: KarteEintrag) { return this.nameUmfeld.get(e.umfeld) ?? e.umfeld }
  belegt(id: string) { return (this.nachbarn.get(id) ?? []).filter((n) => n.status === 'belegt') }

  /** Einträge am selben Ort liegen in einer Spirale um den Ort (Versatz in Bildschirm-Pixeln, siehe Karte). */
  private anordnen() {
    const rang: Record<Typ, number> = { org: 0, medium: 1, ereignis: 2, person: 3 }
    const orte = new Map(this.daten.orte.map((o) => [o.key, o]))
    const gruppen = new Map<string, KarteEintrag[]>()
    for (const e of this.daten.eintraege) {
      if (!e.ort || !orte.has(e.ort)) continue
      if (!gruppen.has(e.ort)) gruppen.set(e.ort, [])
      gruppen.get(e.ort)!.push(e)
    }
    for (const [key, g] of gruppen) {
      const o = orte.get(key)!
      g.sort((a, b) => rang[a.typ] - rang[b.typ] || this.nachbarn.get(b.id)!.length - this.nachbarn.get(a.id)!.length || a.name.localeCompare(b.name, 'de'))
      g.forEach((e, i) => {
        // Hat der Ort ein eigenes Symbol, bleibt die Mitte dafür frei.
        const j = o.symbol ? i + 1 : i
        const r = (j ? Math.sqrt(j + 0.4) : 0) + (o.symbol ? 0.95 : 0), w = j * 2.39996
        this.lage.set(e.id, { lon: o.lon, lat: o.lat, dx: r * Math.cos(w), dy: r * Math.sin(w) })
      })
    }
  }
}

export function initialen(name: string) {
  const w = name.replace(/\([^)]*\)/g, '').replace(/[„“"'.]/g, '').trim().split(/\s+/).filter((x) => /^[A-ZÄÖÜ]/.test(x))
  return ((w[0] ?? name)[0] + (w.length > 1 ? w[w.length - 1][0] : '')).toUpperCase()
}

export function kuerzel(name: string) {
  const m = name.match(/\(([A-ZÄÖÜ][A-Za-zÄÖÜ0-9-]{1,6})\)/)
  if (m) return m[1].slice(0, 4)
  const w = name.replace(/\([^)]*\)/g, '').replace(/[„“"'.]/g, '').trim().split(/\s+/)
  if (w.length === 1 && w[0].length <= 4) return w[0]
  return w.filter((x) => /^[A-ZÄÖÜ0-9]/.test(x)).slice(0, 3).map((x) => x[0]).join('') || name[0]
}

export function unterzeile(netz: Netz, e: KarteEintrag) {
  if (e.typ === 'person') return netz.umfeldName(e)
  const form = e.form === 'Medium' || e.form.startsWith('Ereignis') ? ({ medium: 'Medium', ereignis: 'Ereignis', org: 'Organisation', person: '' }[e.typ]) : e.form
  return form + (e.ortName ? ', ' + e.ortName : '')
}
