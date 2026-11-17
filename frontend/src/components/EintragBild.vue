<script setup lang="ts">
import { computed, ref } from 'vue'
import type { KarteEintrag } from '../lib/typen'
import { kuerzel } from '../lib/netz'
import { PFADE } from '../lib/symbole'

const props = defineProps<{ eintrag: KarteEintrag; farbe: string; klein?: boolean }>()
const fehler = ref(false)
const bild = computed(() => (props.eintrag.bild && !fehler.value ? props.eintrag.bild : null))
const rund = computed(() => props.eintrag.typ === 'person')
/** Gesichtsausschnitt per CSS: Bild vergrößern und um den angegebenen Mittelpunkt verschieben. */
const bildStil = computed(() => {
  const a = props.eintrag.ausschnitt
  if (!a || props.eintrag.typ !== 'person') return props.eintrag.typ === 'person' ? { objectPosition: '50% 35%' } : {}
  return { objectPosition: `${a.x}% ${a.y}%`, transform: `scale(${a.zoom})`, transformOrigin: `${a.x}% ${a.y}%` }
})
</script>

<template>
  <div class="bild" :class="{ rund, klein, mitbild: !!bild }" :style="{ '--c': farbe }">
    <img v-if="bild" :src="bild" alt="" :style="bildStil" @error="fehler = true" />
    <svg v-else-if="eintrag.typ === 'person'" viewBox="0 0 24 24" aria-hidden="true"><path :d="PFADE.silhouette" fill="currentColor" /></svg>
    <svg v-else-if="eintrag.typ === 'medium'" viewBox="0 0 24 24" aria-hidden="true" class="strich"><path :d="PFADE.zeitung" /></svg>
    <svg v-else-if="eintrag.typ === 'ereignis'" viewBox="0 0 24 24" aria-hidden="true" class="strich"><path :d="PFADE.kalender" /></svg>
    <span v-else aria-hidden="true">{{ kuerzel(eintrag.name) }}</span>
  </div>
</template>

<style scoped>
.bild { width: 58px; height: 58px; flex: none; display: grid; place-items: center; background: var(--c); color: #fff; border-radius: 3px; overflow: hidden; font: 600 19px var(--sans); }
.bild.rund { border-radius: 50%; }
.bild.klein { width: 36px; height: 36px; font-size: 13px; }
.bild.mitbild { background: #fff; box-shadow: inset 0 0 0 1.5px var(--c); }
.bild img { width: 100%; height: 100%; object-fit: cover; }
.bild:not(.rund) img { object-fit: contain; padding: 4px; }
.bild svg { width: 82%; height: 82%; }
.bild.rund svg { width: 92%; height: 92%; margin-top: 14%; opacity: .92; }
.bild svg.strich { width: 62%; height: 62%; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }
</style>
