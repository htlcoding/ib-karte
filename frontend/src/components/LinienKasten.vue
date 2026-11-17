<script setup lang="ts">
import type { KarteEintrag, Nachbar } from '../lib/typen'
import type { Netz } from '../lib/netz'
import EintragBild from './EintragBild.vue'

const props = defineProps<{ a: KarteEintrag; b: KarteEintrag; art: string; liste: Nachbar[]; netz: Netz }>()
const def = props.netz.art.get(props.art)!
const name = (id: string) => props.netz.eintrag.get(id)!.name
</script>

<template>
  <article class="kasten linie">
    <div class="paar">
      <EintragBild :eintrag="a" :farbe="netz.farbe(a)" klein />
      <span class="strich" :style="{ borderTopColor: def.farbe, borderTopStyle: def.gestrichelt ? 'dashed' : 'solid' }"></span>
      <EintragBild :eintrag="b" :farbe="netz.farbe(b)" klein />
    </div>
    <div class="namen"><span>{{ a.name }}</span><span>{{ b.name }}</span></div>
    <div class="art"><span class="lin" :style="{ borderTopColor: def.farbe, borderTopStyle: def.gestrichelt ? 'dashed' : 'solid' }"></span>{{ def.name }}</div>
    <ul>
      <li v-for="(v, i) in liste" :key="i">{{ name(v.von) }} – {{ v.text }} – {{ name(v.nach) }}</li>
    </ul>
  </article>
</template>
