<template>
  <div class="page">
    <div class="intro">
      <h2 class="large-title">نمای کلی</h2>
      <p class="sub">وضعیت زنده‌ی پروژه‌ی <strong>Demo site</strong></p>
    </div>

    <!-- Business data from the product's own database (Pastil), read-only -->
    <section class="biz" aria-labelledby="biz-h">
      <h3 id="biz-h" class="card-title">کسب‌وکار (۳۰ روز اخیر)</h3>
      <BusinessState :state="bizState" @retry="bizReload" />
      <div v-if="bizData" class="biz-grid">
        <NuxtLink to="/users" class="card kpi"><p class="kl">User</p><p class="kv tabular">{{ nf.format(bizData.users) }}</p><p class="kh">{{ nf.format(bizData.newUsers7Days) }} جدید در ۷ روز · {{ nf.format(bizData.buyers30Days) }} خریدار</p></NuxtLink>
        <NuxtLink to="/vendors" class="card kpi"><p class="kl">Vendor</p><p class="kv tabular">{{ nf.format(vendorTotal) }}</p><p class="kh">{{ nf.format(vendorVisible) }} نمایان در سایت · {{ nf.format(bizData.providers) }} Provider</p></NuxtLink>
        <div class="card kpi"><p class="kl">فروش فروشگاه</p><p class="kv tabular">{{ nf.format(bizData.shop30Days.count) }}</p><p class="kh">{{ nf.format(bizData.shop30Days.amount) }} تومان</p></div>
        <div class="card kpi"><p class="kl">سبد رهاشده (۷ روز)</p><p class="kv tabular">{{ nf.format(bizData.abandonedCarts7Days.count) }}</p><p class="kh">{{ nf.format(bizData.abandonedCarts7Days.amount) }} تومان روی میز</p></div>
      </div>
    </section>

    <div class="grid">
      <!-- Live users -->
      <section class="card hero" aria-labelledby="live-h">
        <header class="card-head">
          <div>
            <h3 id="live-h" class="card-title">همین لحظه آنلاین‌اند</h3>
            <p class="muted">Userهایی که در {{ windowLabel }} اخیر رویدادی فرستاده‌اند</p>
          </div>
          <SegmentedControl v-model="windowSeconds" :options="windows" label="بازه‌ی زمانی" @update:model-value="setWindow" />
        </header>

        <div class="count-row">
          <p class="big tabular" aria-live="polite">{{ snapshot ? nf.format(snapshot.onlineUsers) : '—' }}</p>
          <div class="status" :class="status">
            <span class="dot" />
            <span>{{ statusText }}</span>
          </div>
        </div>

        <p class="muted">
          وارد‌شده: <strong class="tabular">{{ snapshot ? nf.format(snapshot.signedInUsers) : '—' }}</strong>
          <span class="sep">·</span> آخرین به‌روزرسانی: <strong class="tabular">{{ updatedAt }}</strong>
        </p>

        <LiveSparkline :values="history" />
        <p v-if="status === 'error'" class="error" role="alert">اتصال به API برقرار نشد: {{ message }}</p>
      </section>

      <!-- Top pages -->
      <section class="card" aria-labelledby="pages-h">
        <header class="card-head">
          <div>
            <h3 id="pages-h" class="card-title">صفحه‌های فعلی</h3>
            <p class="muted">هر User روی آخرین صفحه‌ای که دیده شمرده می‌شود</p>
          </div>
        </header>
        <ul v-if="snapshot?.topPages.length" class="pages">
          <li v-for="p in snapshot.topPages" :key="p.path">
            <div class="page-row">
              <span class="path" dir="ltr" :title="p.path">{{ p.path }}</span>
              <span class="n tabular">{{ nf.format(p.users) }}</span>
            </div>
            <div class="bar"><i :style="{ width: `${(p.users / maxUsers) * 100}%` }" /></div>
          </li>
        </ul>
        <div v-else class="empty"><Activity :size="30" /><span>هنوز صفحه‌ی فعالی دیده نشده.</span></div>
      </section>

      <!-- Install -->
      <section class="card install" aria-labelledby="install-h">
        <header class="card-head">
          <div>
            <h3 id="install-h" class="card-title">نصب SDK روی سایت</h3>
            <p class="muted">این کد را قبل از بسته شدن <code dir="ltr">&lt;/body&gt;</code> بگذارید. کلید نوشتن عمومی است و فقط اجازه‌ی ارسال رویداد می‌دهد.</p>
          </div>
          <button type="button" class="copy" @click="copy">
            <component :is="copied ? Check : Copy" :size="17" />{{ copied ? 'کپی شد' : 'کپی' }}
          </button>
        </header>
        <pre dir="ltr" class="code"><code>{{ snippet }}</code></pre>
        <p class="muted">تا وقتی User رضایت ندهد (<code dir="ltr">setConsent</code>) هیچ رویدادی ارسال نمی‌شود.</p>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Activity, Check, Copy } from 'lucide-vue-next'
import type { Overview } from '~/composables/useBusiness'

definePageMeta({ title: 'نمای کلی' })

const nf = new Intl.NumberFormat('fa-IR')
const { data: bizData, state: bizState, reload: bizReload } = useBusiness<Overview>('overview')
const vendorTotal = computed(() => bizData.value?.vendors.reduce((n, v) => n + v.total, 0) ?? 0)
const vendorVisible = computed(() => bizData.value?.vendors.reduce((n, v) => n + v.visible, 0) ?? 0)
const { windowSeconds, snapshot, status, message, history, setWindow } = useLive()
const config = useRuntimeConfig()

const windows = [
  { label: '۱ دقیقه', value: 60 },
  { label: '۵ دقیقه', value: 300 },
  { label: '۱۵ دقیقه', value: 900 },
]
const windowLabel = computed(() => windows.find((w) => w.value === windowSeconds.value)?.label ?? '')
const maxUsers = computed(() => Math.max(1, ...(snapshot.value?.topPages.map((p) => p.users) ?? [1])))
const statusText = computed(() => ({ live: 'زنده', connecting: 'در حال اتصال…', error: 'قطع شد' })[status.value])
const updatedAt = computed(() => (snapshot.value ? new Date(snapshot.value.at).toLocaleTimeString('fa-IR') : '—'))

const snippet = computed(
  () => `<script src="${config.public.sdkUrl}"><\/script>
<script>
  tarantula.init({
    key: '${config.public.writeKey}',
    endpoint: '${config.public.collectUrl}',
  });
  // After the visitor agrees to tracking:
  // tarantula.setConsent('full');   // 'anonymous' | 'analytics' | 'full'
<\/script>`,
)

const copied = ref(false)
async function copy() {
  try {
    await navigator.clipboard.writeText(snippet.value)
    copied.value = true
    setTimeout(() => (copied.value = false), 1800)
  } catch {
    copied.value = false
  }
}
</script>

<style scoped>
.page { max-width: 1180px; margin-inline: auto; }
.intro { margin-bottom: 22px; }
.large-title { font-size: var(--t-large-title); font-weight: 900; line-height: 1.2; }
.sub { margin-top: 4px; color: var(--label-2); font-size: var(--t-callout); }
.sub strong { color: var(--tint-text); }

.biz { display: grid; gap: 12px; margin-bottom: 16px; }
.biz-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 16px; }
.kpi { padding: 18px 20px; display: grid; gap: 4px; color: inherit; text-decoration: none; }
a.kpi { transition: transform 0.2s var(--ease), background-color 0.2s ease; }
a.kpi:hover { background-color: var(--surface-2); }
.kl { color: var(--label-2); font-size: var(--t-subhead); }
.kv { font-size: 2.2rem; font-weight: 900; color: var(--tint); line-height: 1.15; }
.kh { color: var(--label-3); font-size: var(--t-footnote); line-height: 1.6; }
@media (max-width: 960px) { .biz-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 520px) { .biz-grid { grid-template-columns: minmax(0, 1fr); } }
.grid { display: grid; grid-template-columns: 1.7fr 1fr; gap: 16px; }
/* Grid items may shrink below their content, so long code lines scroll inside the card instead of widening the page. */
.grid > * { min-width: 0; }
.hero { grid-row: span 1; }
.install { grid-column: 1 / -1; }
.card { padding: 24px; display: flex; flex-direction: column; gap: 14px; }
.card-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
.card-title { font-size: var(--t-headline); font-weight: 800; }
.muted { color: var(--label-2); font-size: var(--t-subhead); line-height: 1.6; }
.muted strong { color: var(--label); }
.sep { margin-inline: 6px; color: var(--label-4); }

.count-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.big { font-size: clamp(4.5rem, 11vw, 7.5rem); font-weight: 900; line-height: 1; color: var(--tint); letter-spacing: -0.02em; }
.status { display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px; border-radius: var(--r-pill); background: rgb(255 255 255 / 0.07); color: var(--label-2); font-size: var(--t-footnote); font-weight: 700; }
.status .dot { width: 9px; height: 9px; border-radius: 50%; background: var(--warn); }
.status.live { color: var(--ok); background: rgb(48 209 88 / 0.12); }
.status.live .dot { background: var(--ok); animation: pulse 1.8s infinite; }
.status.error { color: var(--bad); background: rgb(255 69 58 / 0.12); }
.status.error .dot { background: var(--bad); }
.error { padding: 12px 14px; border-radius: var(--r-md); background: rgb(255 69 58 / 0.12); color: #ff9b94; font-size: var(--t-subhead); }
@keyframes pulse { 0% { box-shadow: 0 0 0 0 rgb(48 209 88 / 0.6); } 70% { box-shadow: 0 0 0 9px rgb(48 209 88 / 0); } 100% { box-shadow: 0 0 0 0 rgb(48 209 88 / 0); } }

.pages { margin: 0; padding: 0; list-style: none; display: grid; gap: 14px; }
.page-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.path { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: left; font-size: var(--t-callout); }
.n { font-weight: 800; color: var(--tint-text); font-size: var(--t-callout); }
.bar { height: 6px; margin-top: 8px; border-radius: 3px; background: rgb(255 255 255 / 0.08); overflow: hidden; }
.bar i { display: block; height: 100%; border-radius: 3px; background: var(--tint); transition: width 0.6s var(--ease); }
.empty { display: grid; justify-items: center; gap: 10px; padding: 28px 8px; color: var(--label-3); font-size: var(--t-subhead); }

.copy { display: inline-flex; align-items: center; gap: 8px; min-height: var(--target); padding-inline: 18px; border-radius: var(--r-pill); background: var(--tint); color: var(--on-tint); font-weight: 800; font-size: var(--t-subhead); transition: background-color 0.2s ease, transform 0.2s var(--ease); }
.copy:hover { background: var(--tint-text); }
.copy:active { transform: scale(0.97); }
.code { margin: 0; padding: 16px 18px; border-radius: var(--r-md); background: #000; border: 1px solid var(--separator-soft); overflow-x: auto; font-size: var(--t-footnote); line-height: 1.7; color: #ffd9bd; font-family: ui-monospace, 'Cascadia Code', Consolas, monospace; }
code { font-family: ui-monospace, 'Cascadia Code', Consolas, monospace; font-size: 0.9em; background: rgb(255 255 255 / 0.08); padding: 1px 6px; border-radius: 6px; }

@media (max-width: 960px) { .grid { grid-template-columns: minmax(0, 1fr); } }
@media (max-width: 767px) { .card { padding: 18px; } .large-title { font-size: var(--t-title-1); } }
</style>
