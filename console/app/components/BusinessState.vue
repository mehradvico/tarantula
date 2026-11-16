<template>
  <div v-if="state === 'loading'" class="note" role="status"><Loader2 :size="22" class="spin" /><span>در حال خواندن داده‌ی کسب‌وکار…</span></div>
  <div v-else-if="state === 'not_connected'" class="note warn" role="status">
    <DatabaseZap :size="24" />
    <div>
      <strong>پایگاه‌داده‌ی کسب‌وکار وصل نیست</strong>
      <p>رشته‌ی اتصال کاربر فقط‌خواندنی را در User Secrets بگذارید: <code dir="ltr">Pastil:ConnectionString</code></p>
    </div>
  </div>
  <div v-else-if="state === 'error'" class="note bad" role="alert">
    <TriangleAlert :size="24" />
    <div>
      <strong>خواندن داده انجام نشد</strong>
      <p>API یا پایگاه‌داده در دسترس نیست.</p>
    </div>
    <button type="button" class="retry" @click="emit('retry')">تلاش دوباره</button>
  </div>
</template>

<script setup lang="ts">
import { DatabaseZap, Loader2, TriangleAlert } from 'lucide-vue-next'
import type { BusinessState } from '~/composables/useBusiness'

defineProps<{ state: BusinessState }>()
const emit = defineEmits<{ retry: [] }>()
</script>

<style scoped>
.note { display: flex; align-items: center; gap: 14px; padding: 18px 20px; border-radius: var(--r-lg); background: rgb(255 255 255 / 0.05); color: var(--label-2); font-size: var(--t-subhead); }
.note p { margin: 2px 0 0; color: var(--label-2); }
.note strong { color: var(--label); }
.note.warn { background: rgb(255 214 10 / 0.1); color: var(--warn); }
.note.bad { background: rgb(255 69 58 / 0.12); color: var(--bad); }
.retry { margin-inline-start: auto; min-height: var(--target); padding-inline: 18px; border-radius: var(--r-pill); background: var(--tint); color: var(--on-tint); font-weight: 800; }
.spin { animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
@media (prefers-reduced-motion: reduce) { .spin { animation: none; } }
code { font-family: ui-monospace, Consolas, monospace; background: rgb(255 255 255 / 0.08); padding: 1px 6px; border-radius: 6px; }
</style>
