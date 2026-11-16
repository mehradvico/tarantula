<template>
  <aside class="sidebar glass" :class="{ expanded, 'sheet-open': sheetOpen }" aria-label="منوی اصلی">
    <div class="brand">
      <NuxtLink to="/" class="brand-link" aria-label="تارانتولا، بازگشت به نمای کلی">
        <Transition name="swap" mode="out-in">
          <img v-if="expanded" key="full" src="/logo.png" alt="Tarantula" class="logo-full" />
          <img v-else key="mark" src="/logo-mark.png" alt="Tarantula" class="logo-mark" width="52" height="52" />
        </Transition>
      </NuxtLink>
    </div>

    <Transition name="reveal">
      <label v-if="expanded" class="search">
        <Search :size="17" class="search-icon" aria-hidden="true" />
        <input v-model.trim="query" type="search" placeholder="جستجو در منو" aria-label="جستجو در منو" />
      </label>
    </Transition>

    <nav class="nav" aria-label="منوی اصلی">
      <Transition name="reveal">
        <div v-if="expanded" class="section-title"><span>منوی اصلی</span><i /></div>
      </Transition>

      <SidebarItem
        v-for="(item, i) in items"
        :key="item.id"
        :item="item"
        :expanded="expanded"
        :style="{ '--i': i }"
        @request-expand="emit('update:expanded', true)"
      />

      <div v-if="!items.length" class="empty"><SearchX :size="26" /><span>موردی پیدا نشد</span></div>
    </nav>

    <div class="foot">
      <button type="button" class="collapse" :aria-label="expanded ? 'بستن منو' : 'باز کردن منو'" :title="expanded ? 'بستن منو' : 'باز کردن منو'" @click="emit('update:expanded', !expanded)">
        <span class="icon"><component :is="expanded ? PanelRightClose : PanelRightOpen" :size="21" /></span>
        <Transition name="reveal"><span v-if="expanded" class="copy"><strong>جمع‌وجور</strong><small>نمایش فقط آیکن‌ها</small></span></Transition>
      </button>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { PanelRightClose, PanelRightOpen, Search, SearchX } from 'lucide-vue-next'
import { MENU, type MenuItem } from '~/utils/menu'

const props = defineProps<{ expanded: boolean; sheetOpen: boolean }>()
const emit = defineEmits<{ 'update:expanded': [value: boolean] }>()

const query = ref('')

const filter = (list: MenuItem[], q: string): MenuItem[] =>
  list.reduce<MenuItem[]>((acc, item) => {
    const children = item.children ? filter(item.children, q) : []
    if (item.title.toLocaleLowerCase('fa').includes(q) || children.length) acc.push({ ...item, children: item.children ? children : undefined })
    return acc
  }, [])

const items = computed(() => (query.value ? filter(MENU, query.value.toLocaleLowerCase('fa')) : MENU))
void props
</script>

<style scoped>
/* Floating glass: sits above the content layer with a gap on every side, concentric radii. */
.sidebar {
  position: fixed;
  inset-block: var(--gap);
  inset-inline-start: var(--gap); /* start = the right edge in RTL, like the Pastil panel */
  z-index: 100;
  width: var(--sidebar-rail);
  display: flex;
  flex-direction: column;
  border-radius: var(--r-xl);
  overflow: hidden;
  transition: width 0.55s var(--ease);
}
.sidebar.expanded { width: var(--sidebar-w); }

.brand { display: flex; align-items: center; justify-content: center; min-height: 84px; padding: 12px; border-bottom: 1px solid var(--separator-soft); }
.brand-link { display: grid; place-items: center; width: 100%; height: 60px; border-radius: var(--r-lg); }
/* The logo has a pure black background; "screen" blending makes that black disappear into the glass. */
.logo-full { width: 190px; max-width: 100%; height: auto; mix-blend-mode: screen; }
.logo-mark { width: 52px; height: 52px; object-fit: contain; }

.search { position: relative; display: block; margin: 12px 12px 4px; }
.search input {
  width: 100%; height: var(--target); padding-inline: 40px 14px;
  border: 1px solid var(--separator-soft); border-radius: var(--r-md);
  background: rgb(255 255 255 / 0.06); outline: none; font-size: var(--t-subhead);
  transition: border-color 0.2s ease, background-color 0.2s ease;
}
.search input::placeholder { color: var(--label-3); }
.search input:focus { border-color: var(--tint); background: var(--tint-fill); }
.search-icon { position: absolute; inset-inline-start: 14px; top: 50%; transform: translateY(-50%); color: var(--label-3); pointer-events: none; }

.nav { flex: 1; min-height: 0; padding: 8px 10px 14px; overflow-y: auto; overflow-x: hidden; scrollbar-width: none; display: flex; flex-direction: column; gap: 2px; }
.nav::-webkit-scrollbar { display: none; }
.nav > :deep(.item) { animation: arrive 0.5s var(--ease) both; animation-delay: calc(var(--i, 0) * 30ms); }

.section-title { display: flex; align-items: center; gap: 10px; padding: 6px 10px 8px; color: var(--label-3); font-size: var(--t-caption); font-weight: 700; white-space: nowrap; }
.section-title i { flex: 1; height: 1px; background: linear-gradient(to left, var(--separator-soft), transparent); }

.empty { display: grid; justify-items: center; gap: 8px; padding: 32px 8px; color: var(--label-3); font-size: var(--t-footnote); }

.foot { padding: 10px; border-top: 1px solid var(--separator-soft); }
.collapse { display: flex; align-items: center; width: 100%; min-height: 52px; border-radius: var(--r-md); color: var(--label-2); text-align: start; transition: background-color 0.2s ease, color 0.2s ease; }
.collapse:hover { background: var(--tint-fill); color: var(--tint-text); }
.collapse .icon { display: grid; place-items: center; flex: 0 0 48px; }
.sidebar:not(.expanded) .collapse { justify-content: center; }
.sidebar:not(.expanded) .collapse .icon { flex-basis: 100%; }
.copy { display: flex; flex-direction: column; gap: 2px; white-space: nowrap; line-height: 1.25; }
.copy strong { font-size: var(--t-subhead); }
.copy small { color: var(--label-3); font-size: var(--t-caption); }

.swap-enter-active, .swap-leave-active { transition: opacity 0.18s ease, transform 0.3s var(--ease); }
.swap-enter-from { opacity: 0; transform: scale(0.88); }
.swap-leave-to { opacity: 0; transform: scale(1.05); }
.reveal-enter-active, .reveal-leave-active { transition: opacity 0.2s ease, transform 0.4s var(--ease); }
.reveal-enter-from, .reveal-leave-to { opacity: 0; transform: translateX(14px); }

@keyframes arrive { from { opacity: 0; transform: translateX(10px); } to { opacity: 1; transform: none; } }

/* Phones: the sidebar becomes a sheet that slides in from the trailing edge. */
@media (max-width: 767px) {
  .sidebar {
    width: min(var(--sidebar-w), 88vw);
    transform: translateX(calc(100% + var(--gap) * 2));
    transition: transform 0.5s var(--ease);
    z-index: 200;
  }
  .sidebar.sheet-open { transform: none; }
  .foot { display: none; }
}
</style>
