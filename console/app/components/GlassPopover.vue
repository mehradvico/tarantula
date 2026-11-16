<template>
  <div ref="root" class="pop">
    <slot name="trigger" :open="open" :toggle="toggle" />
    <Transition name="pop">
      <div v-if="open" class="pop-panel glass glass-strong" :class="[`align-${align}`]" role="dialog" :aria-label="label">
        <slot :close="close" />
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{ label: string; align?: 'start' | 'end' }>(), { align: 'end' })

const open = ref(false)
const root = ref<HTMLElement | null>(null)

const toggle = () => (open.value = !open.value)
const close = () => (open.value = false)

function onDocClick(e: MouseEvent) {
  if (open.value && root.value && !root.value.contains(e.target as Node)) close()
}
function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') close()
}

onMounted(() => {
  document.addEventListener('click', onDocClick)
  document.addEventListener('keydown', onKey)
})
onBeforeUnmount(() => {
  document.removeEventListener('click', onDocClick)
  document.removeEventListener('keydown', onKey)
})

defineExpose({ close, open, toggle })
void props
</script>

<style scoped>
.pop { position: relative; }
.pop-panel {
  position: absolute;
  top: calc(100% + 10px);
  z-index: 60;
  min-width: 300px;
  max-width: min(380px, calc(100vw - 24px));
  overflow: hidden;
  border-radius: var(--r-lg);
}
.align-end { inset-inline-end: 0; }
.align-start { inset-inline-start: 0; }

.pop-enter-active, .pop-leave-active { transition: opacity 0.2s ease, transform 0.35s var(--ease); transform-origin: top; }
.pop-enter-from, .pop-leave-to { opacity: 0; transform: translateY(-8px) scale(0.98); }

@media (max-width: 767px) {
  .pop { position: static; }
  .pop-panel { position: fixed; inset-inline: 12px; top: calc(var(--header-h) + 24px); max-width: none; }
}
</style>
