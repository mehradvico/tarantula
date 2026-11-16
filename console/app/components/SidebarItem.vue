<template>
  <div class="item">
    <!-- group with children (disclosure) -->
    <template v-if="hasChildren">
      <button
        type="button"
        class="row"
        :class="{ active: activeTree, rail: !expanded }"
        :aria-expanded="expanded ? open : false"
        :title="expanded ? undefined : item.title"
        @click="toggle"
      >
        <span class="icon"><component :is="item.icon" :size="21" :stroke-width="2" /></span>
        <Transition name="label">
          <span v-if="expanded" class="label">{{ item.title }}</span>
        </Transition>
        <Transition name="label">
          <ChevronDown v-if="expanded" :size="16" class="chev" :class="{ flip: open }" />
        </Transition>
      </button>

      <Transition name="sub">
        <div v-if="open && expanded" class="children">
          <SidebarItem v-for="child in item.children" :key="child.id" :item="child" :expanded="true" />
        </div>
      </Transition>
    </template>

    <!-- finished page -->
    <NuxtLink v-else-if="item.to && !item.soon" :to="item.to" class="row" :class="{ rail: !expanded }" active-class="active" :title="expanded ? undefined : item.title">
      <span class="icon"><component :is="item.icon" :size="21" :stroke-width="2" /></span>
      <Transition name="label"><span v-if="expanded" class="label">{{ item.title }}</span></Transition>
    </NuxtLink>

    <!-- not built yet -->
    <div v-else class="row soon" :class="{ rail: !expanded }" aria-disabled="true" :title="expanded ? undefined : `${item.title} (به‌زودی)`">
      <span class="icon"><component :is="item.icon" :size="21" :stroke-width="2" /></span>
      <Transition name="label">
        <span v-if="expanded" class="label">{{ item.title }}<em>به‌زودی</em></span>
      </Transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ChevronDown } from 'lucide-vue-next'
import type { MenuItem } from '~/utils/menu'

const props = defineProps<{ item: MenuItem; expanded: boolean }>()
const emit = defineEmits<{ 'request-expand': [] }>()

const route = useRoute()
const open = ref(false)

const hasChildren = computed(() => !!props.item.children?.length)
const isSame = (i: MenuItem) => !!i.to && (route.path === i.to || (i.to !== '/' && route.path.startsWith(`${i.to}/`)))
const hasActive = (i: MenuItem): boolean => !!i.children?.some((c) => isSame(c) || hasActive(c))
const activeTree = computed(() => isSame(props.item) || hasActive(props.item))

function toggle() {
  if (!props.expanded) {
    emit('request-expand')
    open.value = true
    return
  }
  open.value = !open.value
}

watch(
  () => [route.path, props.expanded],
  () => {
    if (props.expanded && activeTree.value) open.value = true
    if (!props.expanded) open.value = false
  },
  { immediate: true },
)
</script>

<style scoped>
.item { width: 100%; }

.row {
  display: flex;
  align-items: center;
  width: 100%;
  min-height: var(--target);
  gap: 4px;
  border-radius: var(--r-md);
  color: var(--label-2);
  font-size: var(--t-callout);
  font-weight: 600;
  text-align: start;
  transition: background-color 0.2s ease, color 0.2s ease;
}
.row:hover { background: rgb(255 255 255 / 0.07); color: var(--label); }
.row.active { background: var(--tint-fill-strong); color: var(--tint-text); }
.row.rail { justify-content: center; }

.icon { display: grid; place-items: center; flex: 0 0 48px; height: 44px; }
.row.rail .icon { flex-basis: 100%; }
.label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.chev { margin-inline-end: 12px; color: var(--label-3); transition: transform 0.3s var(--ease); flex: 0 0 auto; }
.chev.flip { transform: rotate(180deg); }

.soon { cursor: default; color: var(--label-3); }
.soon:hover { background: transparent; color: var(--label-3); }
.soon em {
  margin-inline-start: 8px;
  padding: 2px 8px;
  border-radius: var(--r-pill);
  background: rgb(255 255 255 / 0.08);
  color: var(--label-3);
  font-size: var(--t-caption);
  font-style: normal;
  font-weight: 600;
}

.children { margin: 2px 22px 6px 0; padding-inline-end: 10px; border-inline-end: 1px solid var(--separator-soft); }
.children .row { min-height: 42px; font-size: var(--t-subhead); font-weight: 500; }
.children .icon { flex-basis: 40px; height: 40px; }

.label-enter-active, .label-leave-active { transition: opacity 0.2s ease, transform 0.35s var(--ease); }
.label-enter-from, .label-leave-to { opacity: 0; transform: translateX(10px); }
.sub-enter-active, .sub-leave-active { max-height: 600px; overflow: hidden; transition: max-height 0.4s var(--ease), opacity 0.22s ease; }
.sub-enter-from, .sub-leave-to { max-height: 0; opacity: 0; }
</style>
