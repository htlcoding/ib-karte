import type { EintragDetail, KarteDaten } from './typen'

/** Vorzeige-Version ohne Server: Daten kommen aus statischen Dateien (VITE_STATISCH=1). */
const STATISCH = import.meta.env.VITE_STATISCH === '1'
const BASIS = import.meta.env.BASE_URL

export async function ladeKarte(signal?: AbortSignal): Promise<KarteDaten> {
  const url = STATISCH ? `${BASIS}statisch/karte.json` : '/api/karte'
  const r = await fetch(url, { signal, headers: { Accept: 'application/json' } })
  if (!r.ok) throw new Error(`Kartendaten nicht verfügbar (HTTP ${r.status})`)
  return r.json()
}

const details = new Map<string, Promise<EintragDetail>>()
let texte: Promise<Record<string, string>> | null = null

/** Voller Beschreibungstext eines Eintrags (einmal geladen, dann gemerkt). */
export function ladeDetail(id: string): Promise<EintragDetail> {
  if (STATISCH) {
    texte ??= fetch(`${BASIS}statisch/texte.json`).then((r) => {
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      return r.json()
    }).catch((e) => { texte = null; throw e })
    return texte.then((t) => ({ id, info: t[id] ?? '' }))
  }
  if (!details.has(id)) {
    details.set(id, fetch(`/api/eintraege/${encodeURIComponent(id)}`).then((r) => {
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      return r.json()
    }).catch((e) => { details.delete(id); throw e }))
  }
  return details.get(id)!
}
