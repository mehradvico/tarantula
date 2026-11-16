<template>
  <svg class="spark" viewBox="0 0 300 90" preserveAspectRatio="none" role="img" aria-label="روند تعداد Userهای آنلاین در نمونه‌های اخیر">
    <defs>
      <linearGradient id="spark-fill" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="#ff6a00" stop-opacity="0.35" />
        <stop offset="1" stop-color="#ff6a00" stop-opacity="0" />
      </linearGradient>
    </defs>
    <template v-if="points.length > 1">
      <polygon :points="area" fill="url(#spark-fill)" />
      <polyline :points="line" fill="none" stroke="#ff6a00" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke" />
    </template>
    <line v-else x1="0" y1="80" x2="300" y2="80" stroke="rgb(235 235 245 / 0.18)" stroke-width="1" stroke-dasharray="4 4" vector-effect="non-scaling-stroke" />
  </svg>
</template>

<script setup lang="ts">
const props = defineProps<{ values: number[] }>()

const W = 300
const H = 90
const PAD = 8

const points = computed(() => {
  const v = props.values
  if (v.length < 2) return []
  const max = Math.max(1, ...v)
  // RTL: time runs from right (oldest) to left (newest), like the rest of the interface.
  return v.map((n, i) => [W - (i / (v.length - 1)) * W, H - PAD - (n / max) * (H - PAD * 2)] as const)
})
const line = computed(() => points.value.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' '))
const area = computed(() => (points.value.length ? `${line.value} 0,${H} ${W},${H}` : ''))
</script>

<style scoped>
.spark { width: 100%; height: 90px; display: block; }
</style>
