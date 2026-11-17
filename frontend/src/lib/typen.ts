export type Typ = 'person' | 'org' | 'medium' | 'ereignis'

export interface ArtDef { key: string; name: string; farbe: string; gestrichelt: boolean }
export interface UmfeldDef { key: string; name: string; farbe: string }
export interface KarteOrt { key: string; name: string; lat: number; lon: number; genauigkeit: string; symbol: string | null }
export interface KarteEintrag {
  id: string; name: string; typ: Typ; umfeld: string; form: string; kurz: string
  ort: string | null; ortName: string | null; bild: string | null; bildQuelle: string | null
  /** Gesichtsausschnitt: Mittelpunkt in Prozent, Vergrößerung (1 = ganzes Bild) */
  ausschnitt: { x: number; y: number; zoom: number } | null
}
export interface KarteVerbindung { von: string; nach: string; text: string; status: 'belegt' | 'vorwurf'; art: string }
export interface KarteDaten {
  stand: string; arten: ArtDef[]; umfelder: UmfeldDef[]; orte: KarteOrt[]
  eintraege: KarteEintrag[]; verbindungen: KarteVerbindung[]
}

/** Eine Verbindung aus Sicht eines Eintrags. */
export interface Nachbar extends KarteVerbindung { anderer: string }

export interface EintragDetail { id: string; info: string }
