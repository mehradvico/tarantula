<template>
  <div class="page">
    <div class="intro">
      <h2 class="large-title">Vendor</h2>
      <p class="sub">فروشگاه‌ها، کلینیک‌ها، آرایشگاه‌ها، مربی‌ها، پانسیون‌ها و مدرسه‌ها؛ هر کدام با علامت‌هایی که می‌گوید اول سراغ کدام بروید</p>
    </div>

    <BusinessState :state="state" @retry="reload" />

    <template v-if="data">
      <div class="toolbar">
        <div class="seg-scroll"><SegmentedControl v-model="kind" :options="kinds" label="نوع Vendor" /></div>
        <p class="count"><strong class="tabular">{{ nf.format(shown.length) }}</strong> Vendor ·
          <strong class="tabular">{{ nf.format(attention) }}</strong> نیازمند توجه</p>
      </div>

      <section class="card" aria-label="فهرست Vendorها">
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th scope="col">Vendor</th>
                <th scope="col">نوع</th>
                <th scope="col">شهر</th>
                <th scope="col">امتیاز</th>
                <th scope="col">فروش ۳۰ روز</th>
                <th scope="col">علامت‌ها</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="v in shown" :key="`${v.facts.kind}-${v.facts.id}`">
                <th scope="row" class="name">{{ v.facts.name || '—' }}</th>
                <td>{{ KIND_LABEL[v.facts.kind] }}</td>
                <td>{{ v.facts.city ?? '—' }}</td>
                <td class="tabular">{{ v.facts.ratingCount ? `${nf.format(Math.round(v.facts.rating * 10) / 10)} (${nf.format(v.facts.ratingCount)})` : '—' }}</td>
                <td class="tabular">{{ sales(v) }}</td>
                <td>
                  <span v-if="!v.signals.length" class="ok">سالم</span>
                  <ul v-else class="tags"><li v-for="s in v.signals" :key="s" :class="tone(s)">{{ SIGNAL_LABEL[s] ?? s }}</li></ul>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { KIND_LABEL, SIGNAL_LABEL, useBusiness, type VendorRow, type VendorsReport } from '~/composables/useBusiness'

definePageMeta({ title: 'Vendor' })

const nf = new Intl.NumberFormat('fa-IR')
const { data, state, reload } = useBusiness<VendorsReport>('vendors')

const kind = ref('all')
const kinds = [{ label: 'همه', value: 'all' }, ...Object.entries(KIND_LABEL).map(([value, label]) => ({ label, value }))]
const shown = computed(() => (data.value?.vendors ?? []).filter((v) => kind.value === 'all' || v.facts.kind === kind.value))
const attention = computed(() => shown.value.filter((v) => v.signals.length).length)

const sales = (v: VendorRow) =>
  v.facts.kind === 'school' ? '—' : `${nf.format(v.facts.sales30DaysCount)} · ${nf.format(v.facts.sales30DaysAmount)} تومان`
const tone = (s: string) => (s === 'low_rating' || s === 'no_sales_30d' ? 'warn' : 'bad')
</script>

<style scoped>
.page { max-width: 1180px; margin-inline: auto; display: grid; grid-template-columns: minmax(0, 1fr); gap: 16px; }
.large-title { font-size: var(--t-large-title); font-weight: 900; line-height: 1.2; }
.sub { margin-top: 4px; color: var(--label-2); font-size: var(--t-callout); line-height: 1.7; }
.seg-scroll { max-width: 100%; overflow-x: auto; scrollbar-width: none; }
.toolbar { display: flex; align-items: center; justify-content: space-between; gap: 14px; flex-wrap: wrap; }
.count { color: var(--label-2); font-size: var(--t-subhead); }
.count strong { color: var(--label); }
.card { padding: 8px 12px 12px; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: var(--t-subhead); min-width: 720px; }
th { text-align: start; color: var(--label-3); font-weight: 700; font-size: var(--t-footnote); padding: 12px 10px; }
td, th[scope='row'] { padding: 12px 10px; border-top: 1px solid var(--separator-soft); vertical-align: middle; }
.name { color: var(--label); font-weight: 700; font-size: var(--t-subhead); }
.ok { color: var(--ok); font-weight: 700; }
.tags { margin: 0; padding: 0; list-style: none; display: flex; flex-wrap: wrap; gap: 6px; }
.tags li { padding: 3px 10px; border-radius: var(--r-pill); font-size: var(--t-caption); font-weight: 700; }
.tags .bad { background: rgb(255 69 58 / 0.14); color: #ff9b94; }
.tags .warn { background: rgb(255 214 10 / 0.12); color: var(--warn); }
@media (max-width: 767px) { .large-title { font-size: var(--t-title-1); } }
</style>
