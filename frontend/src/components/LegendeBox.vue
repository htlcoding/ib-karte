<script setup lang="ts">
import type { Netz } from '../lib/netz'
import { PFADE } from '../lib/symbole'

defineProps<{ netz: Netz }>()
const ORTE = [
  ['parlament', 'Parlament, Landtag'], ['rathaus', 'Rathaus'], ['keller', 'Kellerlokal'],
  ['parteizentrale', 'Parteizentrale'], ['gericht', 'Gericht'], ['haus', 'Haus, Zentrum'],
] as const
</script>

<template>
  <details class="legende">
    <summary>Legende</summary>
    <div class="inhalt">
      <h4>Einträge</h4>
      <div v-for="u in netz.daten.umfelder" :key="u.key" class="zeile"><span class="sym" :style="{ '--c': u.farbe }"></span>{{ u.name }}</div>
      <div class="zeile formen"><span class="sym person" style="--c:#777"></span>Person <span class="sym" style="--c:#777"></span>Organisation <span class="sym ereignis" style="--c:#777"></span>Ereignis</div>
      <h4>Orte</h4>
      <div v-for="[s, n] in ORTE" :key="s" class="zeile">
        <svg class="ortsym" viewBox="0 0 24 24" aria-hidden="true"><path :d="PFADE[s]" /></svg>{{ n }}
      </div>
      <h4>Verbindungen</h4>
      <div v-for="a in netz.daten.arten" :key="a.key" class="zeile">
        <span class="lin" :style="{ borderTopColor: a.farbe, borderTopStyle: a.gestrichelt ? 'dashed' : 'solid' }"></span>{{ a.name }}
      </div>
      <p>Einen Punkt anklicken, um seine Verbindungen zu sehen. Eine Linie anklicken, um die Verbindung erklärt zu bekommen. Dicke Linien stehen für mehrere Verbindungen.</p>
    </div>
  </details>
</template>
