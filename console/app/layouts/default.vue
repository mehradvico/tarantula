<template>
  <div class="shell">
    <a href="#content" class="skip">رفتن به محتوا</a>

    <AppSidebar v-model:expanded="expanded" :sheet-open="sheet" />
    <Transition name="fade"><div v-if="sheet" class="scrim" @click="sheet = false" /></Transition>

    <div class="main" :class="{ rail: !expanded }">
      <AppHeader :title="title" @menu="sheet = true" />
      <main id="content" class="content"><slot /></main>
    </div>

    <MobileTabBar @menu="sheet = true" @search="openSearch" />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()
const title = computed(() => String(route.meta.title ?? 'تارانتولا'))

const isPhone = ref(false)
const userExpanded = ref(true)
const sheet = ref(false)

// On phones the sidebar is always a full sheet; on larger screens people can collapse it to a rail.
const expanded = computed({
  get: () => isPhone.value || userExpanded.value,
  set: (v: boolean) => (userExpanded.value = v),
})

const { connect, close } = useLive()

function measure() {
  isPhone.value = window.innerWidth < 768
}
function openSearch() {
  document.querySelector<HTMLButtonElement>('button[aria-label="جستجو"]')?.click()
}

onMounted(() => {
  measure()
  window.addEventListener('resize', measure)
  connect()
})
onBeforeUnmount(() => {
  window.removeEventListener('resize', measure)
  close()
})
watch(() => route.fullPath, () => (sheet.value = false))
</script>

<style scoped>
.shell { min-height: 100dvh; }
.skip { position: fixed; inset-block-start: -60px; inset-inline-start: 12px; z-index: 300; padding: 10px 16px; border-radius: var(--r-md); background: var(--tint); color: var(--on-tint); font-weight: 700; transition: inset-block-start 0.2s ease; }
.skip:focus { inset-block-start: 12px; }

/* The content layer extends underneath the floating bars: the main area is padded, the bars float on top. */
.main {
  min-height: 100dvh;
  padding: var(--gap);
  padding-inline-start: calc(var(--sidebar-w) + var(--gap) * 2);
  transition: padding-inline-start 0.55s var(--ease);
}
.main.rail { padding-inline-start: calc(var(--sidebar-rail) + var(--gap) * 2); }
.content { padding: 28px 8px 120px; }

.scrim { position: fixed; inset: 0; z-index: 150; background: rgb(0 0 0 / 0.5); -webkit-backdrop-filter: blur(4px); backdrop-filter: blur(4px); }
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

@media (max-width: 767px) {
  .main, .main.rail { padding-inline-start: var(--gap); }
  .content { padding-inline: 2px; }
}
</style>
