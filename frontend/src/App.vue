<script setup lang="ts">
import { onMounted, ref, shallowRef } from 'vue'
import { ladeKarte } from './lib/api'
import { Netz } from './lib/netz'
import NetzKarte from './components/NetzKarte.vue'
import SuchFeld from './components/SuchFeld.vue'
import LegendeBox from './components/LegendeBox.vue'

const netz = shallowRef<Netz | null>(null)
const fehler = ref<string | null>(null)
const karte = ref<InstanceType<typeof NetzKarte> | null>(null)

async function laden() {
  fehler.value = null
  try { netz.value = new Netz(await ladeKarte()) }
  catch (e) { fehler.value = e instanceof Error ? e.message : String(e) }
}
onMounted(laden)
</script>

<template>
  <main class="seite">
    <h1 class="unsichtbar">Netzwerk der Identitären Bewegung – Karte</h1>
    <template v-if="netz">
      <NetzKarte ref="karte" :netz="netz" />
      <SuchFeld :netz="netz" @waehle="(id) => karte?.waehle(id, true)" />
      <LegendeBox :netz="netz" />
    </template>
    <div v-else class="status">
      <template v-if="fehler">
        <p>Die Karte konnte nicht geladen werden: {{ fehler }}</p>
        <button type="button" @click="laden">Erneut versuchen</button>
      </template>
      <p v-else>Karte wird geladen …</p>
    </div>
  </main>
</template>
