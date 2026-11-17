import type { EintragDetail, KarteDaten } from './typen'

export async function ladeKarte(signal?: AbortSignal): Promise<KarteDaten> {
  const r = await fetch('/api/karte', { signal, headers: { Accept: 'application/json' } })
  if (!r.ok) throw new Error(`Kartendaten nicht verfügbar (HTTP ${r.status})`)
  return r.json()
}

const details = new Map<string, Promise<EintragDetail>>()

/** Voller Beschreibungstext eines Eintrags (einmal geladen, dann gemerkt). */
export function ladeDetail(id: string): Promise<EintragDetail> {
  if (!details.has(id)) {
    details.set(id, fetch(`/api/eintraege/${encodeURIComponent(id)}`).then((r) => {
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      return r.json()
    }).catch((e) => { details.delete(id); throw e }))
  }
  return details.get(id)!
}
