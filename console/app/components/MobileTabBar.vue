<template>
  <nav class="tabbar glass" :class="{ compact }" aria-label="ناوبری سریع">
    <NuxtLink to="/" class="tab" active-class="active"><LayoutDashboard :size="22" /><span>نمای کلی</span></NuxtLink>
    <button type="button" class="tab" @click="emit('search')"><Search :size="22" /><span>جستجو</span></button>
    <button type="button" class="tab" @click="emit('menu')"><Menu :size="22" /><span>منو</span></button>
  </nav>
</template>

<script setup lang="ts">
import { LayoutDashboard, Menu, Search } from 'lucide-vue-next'

const emit = defineEmits<{ menu: []; search: [] }>()

// iOS 26 tab bars shrink while scrolling down to favour content, and expand again on scroll up.
const compact = ref(false)
let last = 0
function onScroll() {
  const y = window.scrollY
  compact.value = y > last && y > 60
  last = y
}
onMounted(() => window.addEventListener('scroll', onScroll, { passive: true }))
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))
</script>

<style scoped>
.tabbar { display: none; }

@media (max-width: 767px) {
  .tabbar {
    position: fixed;
    inset-inline: 0;
    margin-inline: auto;
    bottom: calc(var(--gap) + env(safe-area-inset-bottom));
    z-index: 150;
    display: flex;
    width: min(380px, calc(100vw - 24px));
    padding: 6px;
    border-radius: var(--r-pill);
    transition: width 0.45s var(--ease), padding 0.45s var(--ease);
  }
  .tabbar.compact { width: 220px; }
  .tab { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 3px; min-height: 54px; border-radius: var(--r-pill); color: var(--label-2); font-size: var(--t-caption); font-weight: 600; transition: background-color 0.2s ease, color 0.2s ease; }
  .tab.active { background: var(--tint-fill-strong); color: var(--tint-text); }
  .tabbar.compact .tab { min-height: 46px; }
  .tabbar.compact .tab span { display: none; }
}
</style>
