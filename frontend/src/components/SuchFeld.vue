<script setup lang="ts">
import { computed, ref } from 'vue'
import type { Netz } from '../lib/netz'

const props = defineProps<{ netz: Netz }>()
const emit = defineEmits<{ waehle: [id: string] }>()

const q = ref('')
const aktiv = ref(0)
const offen = ref(false)

const treffer = computed(() => {
  const s = q.value.trim().toLowerCase()
  if (s.length < 2) return []
  const woerter = s.split(/\s+/)
  return props.netz.daten.eintraege
    .filter((e) => props.netz.lage.has(e.id))
    .filter((e) => woerter.every((w) => (e.name + ' ' + (e.ortName ?? '')).toLowerCase().includes(w)))
    .map((e) => {
      const n = e.name.toLowerCase()
      const rang = [n === s ? 0 : 1, e.typ === 'ereignis' ? 1 : 0, n.split(/[\s(„'-]+/).some((x) => x.startsWith(woerter[0])) ? 0 : 1, -(props.netz.nachbarn.get(e.id)?.length ?? 0)]
      return { e, rang }
    })
    .sort((a, b) => { for (let i = 0; i < 4; i++) if (a.rang[i] !== b.rang[i]) return a.rang[i] - b.rang[i]; return a.e.name.localeCompare(b.e.name, 'de') })
    .slice(0, 12)
    .map((x) => x.e)
})

const TYP = { person: 'Person', org: 'Organisation', medium: 'Medium', ereignis: 'Ereignis' } as const

function nimm(id: string) {
  q.value = props.netz.eintrag.get(id)!.name
  offen.value = false
  ;(document.activeElement as HTMLElement | null)?.blur()
  emit('waehle', id)
}
function taste(e: KeyboardEvent) {
  if (e.key === 'ArrowDown') { aktiv.value = Math.min(aktiv.value + 1, treffer.value.length - 1); e.preventDefault() }
  else if (e.key === 'ArrowUp') { aktiv.value = Math.max(aktiv.value - 1, 0); e.preventDefault() }
  else if (e.key === 'Enter' && treffer.value[aktiv.value]) nimm(treffer.value[aktiv.value].id)
  else if (e.key === 'Escape') offen.value = false
}
</script>

<template>
  <div class="suche" role="search">
    <label>
      <span class="unsichtbar">Suche</span>
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>
      <input v-model="q" type="search" placeholder="Name oder Ort suchen" autocomplete="off" role="combobox"
        aria-controls="treffer" :aria-expanded="offen && treffer.length > 0"
        @input="offen = true; aktiv = 0" @focus="offen = true" @keydown="taste" />
    </label>
    <ul v-if="offen && q.trim().length >= 2" id="treffer" class="treffer" role="listbox">
      <li v-if="!treffer.length" class="leer">Kein Treffer</li>
      <li v-for="(e, i) in treffer" :key="e.id" role="option" :aria-selected="i === aktiv" @mousedown.prevent="nimm(e.id)">
        <span class="sym" :class="e.typ" :style="{ '--c': netz.farbe(e) }"></span>
        <span>{{ e.name }}<small>{{ TYP[e.typ] }}{{ e.ortName ? ' · ' + e.ortName : '' }}</small></span>
      </li>
    </ul>
  </div>
</template>
