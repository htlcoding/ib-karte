<script setup lang="ts">
import { computed } from 'vue'
import type { KarteOrt } from '../lib/typen'
import type { Netz } from '../lib/netz'
import { PFADE } from '../lib/symbole'

const props = defineProps<{ ort: KarteOrt; netz: Netz }>()
const emit = defineEmits<{ waehle: [id: string] }>()

const SYMBOLNAME: Record<string, string> = {
  parlament: 'Parlament', rathaus: 'Rathaus / Amtshaus', keller: 'Lokal', parteizentrale: 'Parteizentrale', gericht: 'Gericht', haus: 'Haus / Zentrum',
}
const hier = computed(() => props.netz.daten.eintraege.filter((e) => e.ort === props.ort.key).sort((a, b) => a.name.localeCompare(b.name, 'de')))
const einrichtungen = computed(() => hier.value.filter((e) => e.typ !== 'person'))
const personen = computed(() => hier.value.filter((e) => e.typ === 'person'))
</script>

<template>
  <article class="kasten ortkasten">
    <header class="kopf ziehgriff">
      <div class="ortbild" aria-hidden="true">
        <svg viewBox="0 0 24 24"><path :d="PFADE[(ort.symbol ?? 'haus') as keyof typeof PFADE]" /></svg>
      </div>
      <div>
        <h3>{{ ort.name }}</h3>
        <div class="unter">{{ ort.symbol ? SYMBOLNAME[ort.symbol] : 'Ort' }}</div>
      </div>
    </header>
    <p v-if="ort.adresse" class="text">{{ ort.adresse }}</p>

    <template v-if="einrichtungen.length">
      <h4>Hier verortet</h4>
      <ul>
        <li v-for="e in einrichtungen" :key="e.id">
          <button class="wer" type="button" @click="emit('waehle', e.id)">{{ e.name }}</button>
          <span v-if="e.ortGrund" class="was leise">{{ e.ortGrund }}</span>
        </li>
      </ul>
    </template>
    <template v-if="personen.length">
      <h4 :class="{ zweit: einrichtungen.length }">Personen mit Funktion hier</h4>
      <p class="leise">Symbolisch bei ihrer Funktion, nicht an ihrer Wohnadresse.</p>
      <ul>
        <li v-for="e in personen" :key="e.id">
          <button class="wer" type="button" @click="emit('waehle', e.id)">{{ e.name }}</button>
        </li>
      </ul>
    </template>
    <p v-if="!hier.length" class="leise">Keine Einträge direkt an diesem Ort.</p>
  </article>
</template>
