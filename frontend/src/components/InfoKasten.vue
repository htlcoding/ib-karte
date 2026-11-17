<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { KarteEintrag, Nachbar } from '../lib/typen'
import { type Netz, unterzeile } from '../lib/netz'
import { ladeDetail } from '../lib/api'
import EintragBild from './EintragBild.vue'

const props = defineProps<{ eintrag: KarteEintrag; netz: Netz }>()
const emit = defineEmits<{ waehle: [id: string] }>()

const text = ref<string | null>(null)
const ganz = ref(false)
const KURZ = 600

watch(() => props.eintrag.id, (id) => {
  text.value = null; ganz.value = false
  ladeDetail(id).then((d) => { if (d.id === props.eintrag.id) text.value = d.info }).catch(() => {})
}, { immediate: true })

/** Fließtext: voller Beschreibungstext, bei langen Texten zunächst gekürzt. */
const absatz = computed(() => {
  const t = text.value ?? props.eintrag.kurz
  if (ganz.value || t.length <= KURZ + 80) return t
  const cut = t.lastIndexOf('. ', KURZ)
  return t.slice(0, cut > 200 ? cut + 1 : KURZ) + ' …'
})
const kannMehr = computed(() => !!text.value && text.value.length > KURZ + 80)

const sortiert = (l: Nachbar[]) => [...l].sort((a, b) => props.netz.eintrag.get(a.anderer)!.name.localeCompare(props.netz.eintrag.get(b.anderer)!.name, 'de'))
const gruppen = computed(() => {
  const belegt = props.netz.belegt(props.eintrag.id)
  return props.netz.artReihe
    .map((k) => ({ art: props.netz.art.get(k)!, key: k, liste: sortiert(belegt.filter((n) => n.art === k)) }))
    .filter((g) => g.liste.length)
})
const vorwuerfe = computed(() => sortiert((props.netz.nachbarn.get(props.eintrag.id) ?? []).filter((n) => n.status === 'vorwurf')))
const name = (id: string) => props.netz.eintrag.get(id)!.name
</script>

<template>
  <article class="kasten">
    <header class="kopf">
      <EintragBild :eintrag="eintrag" :farbe="netz.farbe(eintrag)" />
      <div>
        <h3>{{ eintrag.name }}</h3>
        <div class="unter">{{ unterzeile(netz, eintrag) }}</div>
      </div>
    </header>

    <p class="text">{{ absatz }}</p>
    <button v-if="kannMehr" class="mehr" type="button" @click="ganz = !ganz">{{ ganz ? 'Weniger anzeigen' : 'Weiterlesen' }}</button>

    <h4>Verbindungen</h4>
    <p v-if="!gruppen.length" class="leise">Keine belegten Verbindungen.</p>
    <section v-for="g in gruppen" :key="g.key">
      <div class="art"><span class="lin" :style="{ borderTopColor: g.art.farbe, borderTopStyle: g.art.gestrichelt ? 'dashed' : 'solid' }"></span>{{ g.art.name }}</div>
      <ul>
        <li v-for="(n, i) in g.liste" :key="i">
          <button class="wer" type="button" @click="emit('waehle', n.anderer)">{{ name(n.anderer) }}</button>
          <span class="was">{{ n.text }}</span>
        </li>
      </ul>
    </section>

    <template v-if="vorwuerfe.length">
      <h4 class="zweit">Nicht bestätigt</h4>
      <p class="leise">Als Vorwurf erfasst und nicht eingezeichnet.</p>
      <ul>
        <li v-for="(n, i) in vorwuerfe" :key="i">
          <button class="wer" type="button" @click="emit('waehle', n.anderer)">{{ name(n.anderer) }}</button>
          <span class="was">{{ n.text }}</span>
        </li>
      </ul>
    </template>
  </article>
</template>
