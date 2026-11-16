<template>
  <div class="seg" role="radiogroup" :aria-label="label" :style="{ '--n': options.length, '--i': index }">
    <span class="thumb" aria-hidden="true" />
    <button
      v-for="o in options"
      :key="o.value"
      type="button"
      role="radio"
      class="opt"
      :class="{ on: o.value === modelValue }"
      :aria-checked="o.value === modelValue"
      @click="emit('update:modelValue', o.value)"
    >{{ o.label }}</button>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{ modelValue: number; options: { label: string; value: number }[]; label: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()
const index = computed(() => Math.max(0, props.options.findIndex((o) => o.value === props.modelValue)))
</script>

<style scoped>
/* iOS segmented control: a pill track with a sliding thumb. */
.seg { position: relative; display: grid; grid-template-columns: repeat(var(--n), 1fr); padding: 3px; border-radius: var(--r-md); background: rgb(118 118 128 / 0.24); min-width: min(240px, 100%); max-width: 100%; }
.thumb {
  position: absolute; inset-block: 3px; inset-inline-start: 3px;
  width: calc((100% - 6px) / var(--n));
  border-radius: calc(var(--r-md) - 3px);
  background: var(--surface-3);
  box-shadow: 0 2px 8px rgb(0 0 0 / 0.4);
  transform: translateX(calc(var(--i) * -100%));
  transition: transform 0.35s var(--ease);
}
.opt { position: relative; z-index: 1; min-height: 38px; padding-inline: 12px; border-radius: calc(var(--r-md) - 3px); color: var(--label-2); font-size: var(--t-subhead); font-weight: 600; transition: color 0.2s ease; }
.opt.on { color: var(--label); }
</style>
