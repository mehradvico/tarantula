<template>
  <header class="header glass">
    <div class="start">
      <button type="button" class="icon-btn menu-btn" aria-label="باز کردن منو" @click="emit('menu')"><Menu :size="21" /></button>
      <span class="accent" aria-hidden="true" />
      <h1 class="title">{{ title }}</h1>

      <GlassPopover label="تغییرات نسخه" align="start" class="hide-sm">
        <template #trigger="{ open, toggle }">
          <button type="button" class="chip" :class="{ on: open }" :aria-expanded="open" @click="toggle">
            <span dir="ltr">v{{ APP_VERSION }}</span><ChevronDown :size="14" class="chev" :class="{ flip: open }" />
          </button>
        </template>
        <div class="pop-head">
          <div><strong>تغییرات نسخه <span dir="ltr">{{ APP_VERSION }}</span></strong><p>{{ releaseDate }}</p></div>
          <span class="badge-ok"><Rocket :size="13" />آخرین نسخه</span>
        </div>
        <div class="changelog">
          <div v-for="g in CHANGELOG" :key="g.type" class="group">
            <span class="group-title"><Sparkles :size="14" />{{ g.title }}</span>
            <ul><li v-for="(t, i) in g.items" :key="i">{{ t }}</li></ul>
          </div>
        </div>
      </GlassPopover>
    </div>

    <div class="end">
      <NuxtLink to="/" class="live" :class="status" :title="liveTitle" :aria-label="liveTitle">
        <span class="dot" /><span class="live-text hide-sm">{{ liveLabel }}</span>
        <strong v-if="snapshot" class="live-count tabular">{{ nf.format(snapshot.onlineUsers) }}</strong>
      </NuxtLink>

      <GlassPopover label="جستجو" ref="searchPop">
        <template #trigger="{ toggle }">
          <button type="button" class="icon-btn" aria-label="جستجو" title="جستجو (Ctrl+K)" @click="toggle(); focusSearch()"><Search :size="20" /></button>
        </template>
        <div class="search-box">
          <Search :size="17" class="search-ic" />
          <input ref="searchInput" v-model.trim="q" type="search" placeholder="رفتن به…" aria-label="جستجو" @keydown.enter="goFirst" />
        </div>
        <ul class="results">
          <li v-for="r in results" :key="r.id">
            <button type="button" class="result" :disabled="r.soon" @click="go(r)">
              <component :is="r.icon" :size="19" /><span>{{ r.title }}</span><em v-if="r.soon">به‌زودی</em>
            </button>
          </li>
          <li v-if="!results.length" class="none">موردی پیدا نشد</li>
        </ul>
      </GlassPopover>

      <GlassPopover label="اعلان‌ها">
        <template #trigger="{ open, toggle }">
          <button type="button" class="icon-btn" :class="{ on: open }" aria-label="اعلان‌ها" title="اعلان‌ها" @click="toggle"><Bell :size="20" /></button>
        </template>
        <div class="pop-head"><div><strong>اعلان‌ها</strong><p>رویدادهای نیازمند بررسی</p></div></div>
        <div class="empty"><BellOff :size="30" /><span>اعلان جدیدی وجود ندارد.</span></div>
      </GlassPopover>

      <GlassPopover label="حساب کاربری">
        <template #trigger="{ open, toggle }">
          <button type="button" class="profile" :class="{ on: open }" :aria-expanded="open" @click="toggle">
            <span class="avatar">م</span>
            <span class="who hide-sm"><strong>مدیر پلتفرم</strong><small>دسترسی کامل (محلی)</small></span>
            <ChevronDown :size="15" class="chev hide-sm" :class="{ flip: open }" />
          </button>
        </template>
        <div class="hero">
          <span class="avatar big">م</span>
          <strong>مدیر پلتفرم</strong>
          <span class="role"><ShieldCheck :size="13" />دسترسی کامل (محلی)</span>
        </div>
        <ul class="menu">
          <li><div class="menu-item soon"><Settings :size="19" /><span><strong>تنظیمات حساب</strong><small>به‌زودی</small></span></div></li>
          <li><div class="menu-item soon"><LogOut :size="19" /><span><strong>خروج</strong><small>با افزودن ورود به حساب فعال می‌شود</small></span></div></li>
        </ul>
      </GlassPopover>
    </div>
  </header>
</template>

<script setup lang="ts">
import { Bell, BellOff, ChevronDown, LogOut, Menu, Rocket, Search, Settings, ShieldCheck, Sparkles } from 'lucide-vue-next'
import { APP_RELEASE_DATE, APP_VERSION, CHANGELOG } from '~/data/changelog'
import { leafItems, type MenuItem } from '~/utils/menu'

defineProps<{ title: string }>()
const emit = defineEmits<{ menu: [] }>()

const { snapshot, status } = useLive()
const nf = new Intl.NumberFormat('fa-IR')

const releaseDate = new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric', month: 'long', day: 'numeric' }).format(new Date(APP_RELEASE_DATE))
const liveLabel = computed(() => ({ live: 'زنده', connecting: 'در حال اتصال', error: 'خطا' })[status.value])
const liveTitle = computed(() => (status.value === 'live' ? 'Userهای آنلاین، همین لحظه' : liveLabel.value))

const q = ref('')
const searchInput = ref<HTMLInputElement | null>(null)
const searchPop = ref<{ close: () => void } | null>(null)
const results = computed(() => leafItems().filter((i) => !q.value || i.title.toLocaleLowerCase('fa').includes(q.value.toLocaleLowerCase('fa'))))

function focusSearch() {
  nextTick(() => setTimeout(() => searchInput.value?.focus(), 50))
}
function go(item: MenuItem) {
  if (item.soon || !item.to) return
  searchPop.value?.close()
  q.value = ''
  navigateTo(item.to)
}
function goFirst() {
  const first = results.value.find((r) => !r.soon)
  if (first) go(first)
}

function onKey(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault()
    ;(searchPop.value as unknown as { toggle: () => void } | null)?.toggle()
    focusSearch()
  }
}
onMounted(() => document.addEventListener('keydown', onKey))
onBeforeUnmount(() => document.removeEventListener('keydown', onKey))
</script>

<style scoped>
/* The header is a floating Liquid Glass toolbar; content scrolls underneath it. */
.header {
  position: sticky;
  top: var(--gap);
  z-index: 90;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  height: var(--header-h);
  padding-inline: 14px 12px;
  border-radius: var(--r-xl);
}
.start, .end { display: flex; align-items: center; gap: 10px; min-width: 0; }
.accent { width: 4px; height: 26px; border-radius: 2px; background: var(--tint); }
.title { font-size: var(--t-title-2); font-weight: 800; letter-spacing: 0; white-space: nowrap; }

.icon-btn { display: grid; place-items: center; width: var(--target); height: var(--target); border-radius: var(--r-md); color: var(--label-2); background: rgb(255 255 255 / 0.06); border: 1px solid var(--separator-soft); transition: background-color 0.2s ease, color 0.2s ease, border-color 0.2s ease; }
.icon-btn:hover, .icon-btn.on { background: var(--tint-fill); color: var(--tint-text); border-color: rgb(255 106 0 / 0.35); }
.menu-btn { display: none; }

.chip { display: flex; align-items: center; gap: 6px; height: 36px; padding-inline: 12px; border-radius: var(--r-pill); background: rgb(255 255 255 / 0.06); border: 1px solid var(--separator-soft); color: var(--label-2); font-size: var(--t-footnote); font-weight: 600; }
.chip:hover, .chip.on { background: var(--tint-fill); color: var(--tint-text); }
.chev { transition: transform 0.3s var(--ease); }
.chev.flip { transform: rotate(180deg); }

.live { display: flex; align-items: center; gap: 8px; height: 40px; padding-inline: 14px; border-radius: var(--r-pill); background: rgb(255 255 255 / 0.06); border: 1px solid var(--separator-soft); font-size: var(--t-footnote); font-weight: 600; color: var(--label-2); }
.live .dot { width: 9px; height: 9px; border-radius: 50%; background: var(--warn); }
.live.live .dot { background: var(--ok); animation: pulse 1.8s infinite; }
.live.error .dot { background: var(--bad); }
.live-count { color: var(--tint-text); font-size: var(--t-callout); font-weight: 800; }
@keyframes pulse { 0% { box-shadow: 0 0 0 0 rgb(48 209 88 / 0.6); } 70% { box-shadow: 0 0 0 9px rgb(48 209 88 / 0); } 100% { box-shadow: 0 0 0 0 rgb(48 209 88 / 0); } }

.profile { display: flex; align-items: center; gap: 10px; min-height: var(--target); padding: 4px 6px 4px 10px; border-radius: var(--r-pill); border: 1px solid transparent; }
.profile:hover, .profile.on { background: rgb(255 255 255 / 0.07); border-color: var(--separator-soft); }
.avatar { display: grid; place-items: center; width: 38px; height: 38px; border-radius: 50%; background: var(--tint); color: var(--on-tint); font-weight: 900; }
.avatar.big { width: 64px; height: 64px; font-size: 1.6rem; box-shadow: 0 0 0 3px rgb(255 255 255 / 0.25); }
.who { display: flex; flex-direction: column; gap: 1px; text-align: start; }
.who strong { font-size: var(--t-subhead); }
.who small { color: var(--label-3); font-size: var(--t-caption); }

/* popover content */
.pop-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 14px 16px; border-bottom: 1px solid var(--separator-soft); }
.pop-head strong { font-size: var(--t-headline); }
.pop-head p { margin-top: 2px; color: var(--label-3); font-size: var(--t-footnote); }
.badge-ok { display: inline-flex; align-items: center; gap: 5px; padding: 5px 10px; border-radius: var(--r-pill); background: rgb(48 209 88 / 0.14); color: var(--ok); font-size: var(--t-caption); font-weight: 700; white-space: nowrap; }
.changelog { padding: 14px 16px; display: grid; gap: 14px; max-height: 320px; overflow-y: auto; }
.group-title { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: var(--r-pill); background: var(--tint-fill); color: var(--tint-text); font-size: var(--t-footnote); font-weight: 700; }
.group ul { margin: 8px 0 0; padding: 0; list-style: none; display: grid; gap: 6px; }
.group li { position: relative; padding-inline-start: 14px; color: var(--label-2); font-size: var(--t-subhead); line-height: 1.5; }
.group li::before { content: ''; position: absolute; inset-inline-start: 0; top: 0.6em; width: 5px; height: 5px; border-radius: 50%; background: var(--label-4); }

.empty { display: grid; justify-items: center; gap: 10px; padding: 36px 16px; color: var(--label-3); font-size: var(--t-subhead); }

.search-box { position: relative; padding: 12px; border-bottom: 1px solid var(--separator-soft); }
.search-box input { width: 100%; height: var(--target); padding-inline: 40px 12px; border-radius: var(--r-md); border: 1px solid var(--separator-soft); background: rgb(255 255 255 / 0.06); outline: none; font-size: var(--t-callout); }
.search-box input:focus { border-color: var(--tint); }
.search-ic { position: absolute; inset-inline-start: 26px; top: 50%; transform: translateY(-50%); color: var(--label-3); }
.results { margin: 0; padding: 8px; list-style: none; max-height: 320px; overflow-y: auto; }
.result { display: flex; align-items: center; gap: 12px; width: 100%; min-height: var(--target); padding-inline: 12px; border-radius: var(--r-md); text-align: start; font-size: var(--t-callout); }
.result:hover:not(:disabled) { background: var(--tint-fill); color: var(--tint-text); }
.result:disabled { color: var(--label-3); cursor: default; }
.result em { margin-inline-start: auto; padding: 2px 8px; border-radius: var(--r-pill); background: rgb(255 255 255 / 0.08); font-size: var(--t-caption); font-style: normal; }
.none { padding: 20px; text-align: center; color: var(--label-3); font-size: var(--t-subhead); }

.hero { display: grid; justify-items: center; gap: 8px; padding: 22px 16px 18px; text-align: center; background: radial-gradient(circle at 15% -10%, rgb(255 255 255 / 0.18), transparent 45%), linear-gradient(135deg, #b84a00, var(--tint) 55%, #b84a00); color: #fff; }
.hero strong { font-size: var(--t-headline); }
.role { display: inline-flex; align-items: center; gap: 6px; padding: 4px 12px; border-radius: var(--r-pill); background: rgb(0 0 0 / 0.25); font-size: var(--t-caption); font-weight: 700; }
.menu { margin: 0; padding: 8px; list-style: none; display: grid; gap: 4px; }
.menu-item { display: flex; align-items: center; gap: 12px; min-height: 52px; padding: 6px 12px; border-radius: var(--r-md); }
.menu-item.soon { color: var(--label-3); }
.menu-item span { display: flex; flex-direction: column; gap: 2px; }
.menu-item strong { font-size: var(--t-subhead); font-weight: 600; }
.menu-item small { font-size: var(--t-caption); }

@media (max-width: 767px) {
  .header { padding-inline: 10px; }
  .menu-btn { display: grid; }
  .accent, .hide-sm { display: none !important; }
  .title { font-size: var(--t-headline); }
  .live-text { display: none; }
}
</style>
