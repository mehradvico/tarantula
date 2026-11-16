<template>
  <div class="page">
    <div class="intro">
      <h2 class="large-title">User</h2>
      <p class="sub">از داده‌ی واقعی پاستیل؛ فقط شناسه و عدد، بدون نام و شماره</p>
    </div>

    <BusinessState :state="state" @retry="reload" />

    <template v-if="data">
      <div class="kpis">
        <div class="card kpi"><p class="label">همه‌ی Userها</p><p class="value tabular">{{ nf.format(data.total) }}</p></div>
        <div class="card kpi"><p class="label">خریدار (تا امروز)</p><p class="value tabular">{{ nf.format(data.buyers) }}</p><p class="hint">{{ percent(data.buyers, data.total) }} از Userها</p></div>
        <div class="card kpi"><p class="label">خریدار تکراری</p><p class="value tabular">{{ nf.format(data.repeatBuyers) }}</p><p class="hint">{{ percent(data.repeatBuyers, data.buyers) }} از خریداران</p></div>
      </div>

      <div class="grid">
        <section class="card" aria-labelledby="signup-h">
          <h3 id="signup-h" class="card-title">ثبت‌نام ۳۰ روز اخیر</h3>
          <div v-if="data.signups30Days.length" class="bars" role="img" :aria-label="`ثبت‌نام روزانه، بیشترین ${maxSignup}`">
            <div v-for="d in data.signups30Days" :key="d.day" class="bar-col" :title="`${d.day}: ${d.count}`">
              <i :style="{ height: `${Math.max(4, (d.count / maxSignup) * 100)}%` }" />
            </div>
          </div>
          <p class="hint">بیشترین در یک روز: <strong class="tabular">{{ nf.format(maxSignup) }}</strong></p>
        </section>

        <section class="card" aria-labelledby="src-h">
          <h3 id="src-h" class="card-title">از کجا آمده‌اند</h3>
          <ul class="list">
            <li v-for="s in data.sources" :key="s.source">
              <div class="row"><span>{{ SOURCE_LABEL[s.name] ?? s.name }}</span><span class="n tabular">{{ nf.format(s.users) }}</span></div>
              <div class="bar"><i :style="{ width: `${(s.users / data.total) * 100}%` }" /></div>
            </li>
          </ul>
        </section>

        <section class="card wide" aria-labelledby="top-h">
          <h3 id="top-h" class="card-title">بیشترین خرید</h3>
          <div class="table-wrap">
            <table>
              <thead><tr><th scope="col">شناسه‌ی User</th><th scope="col">تعداد خرید</th><th scope="col">جمع (تومان)</th></tr></thead>
              <tbody>
                <tr v-for="u in data.topSpenders" :key="u.userId">
                  <td class="tabular" dir="ltr">#{{ u.userId }}</td>
                  <td class="tabular">{{ nf.format(u.purchases) }}</td>
                  <td class="tabular">{{ nf.format(u.spent) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { SOURCE_LABEL, useBusiness, type UsersReport } from '~/composables/useBusiness'

definePageMeta({ title: 'User' })

const nf = new Intl.NumberFormat('fa-IR')
const { data, state, reload } = useBusiness<UsersReport>('users')
const maxSignup = computed(() => Math.max(1, ...(data.value?.signups30Days.map((d) => d.count) ?? [1])))
const percent = (a: number, b: number) => (b ? `${nf.format(Math.round((a / b) * 100))}٪` : '—')
</script>

<style scoped>
.page { max-width: 1180px; margin-inline: auto; display: grid; grid-template-columns: minmax(0, 1fr); gap: 16px; }
.intro { margin-bottom: 6px; }
.large-title { font-size: var(--t-large-title); font-weight: 900; line-height: 1.2; }
.sub { margin-top: 4px; color: var(--label-2); font-size: var(--t-callout); }
.kpis { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }
.grid { display: grid; grid-template-columns: 1.4fr 1fr; gap: 16px; }
.grid > * { min-width: 0; }
.wide { grid-column: 1 / -1; }
.card { padding: 22px; display: flex; flex-direction: column; gap: 12px; }
.card-title { font-size: var(--t-headline); font-weight: 800; }
.label { color: var(--label-2); font-size: var(--t-subhead); }
.value { font-size: 2.6rem; font-weight: 900; color: var(--tint); line-height: 1.1; }
.hint { color: var(--label-3); font-size: var(--t-footnote); }
.hint strong { color: var(--label-2); }
.bars { display: flex; align-items: flex-end; gap: 4px; height: 150px; }
.bar-col { flex: 1; height: 100%; display: flex; align-items: flex-end; }
.bar-col i { width: 100%; border-radius: 4px 4px 2px 2px; background: var(--tint); opacity: 0.9; }
.list { margin: 0; padding: 0; list-style: none; display: grid; gap: 14px; }
.row { display: flex; justify-content: space-between; gap: 12px; font-size: var(--t-callout); }
.n { font-weight: 800; color: var(--tint-text); }
.bar { height: 6px; margin-top: 8px; border-radius: 3px; background: rgb(255 255 255 / 0.08); overflow: hidden; }
.bar i { display: block; height: 100%; background: var(--tint); }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: var(--t-callout); }
th { text-align: start; color: var(--label-3); font-weight: 700; font-size: var(--t-footnote); padding: 8px 10px; }
td { padding: 12px 10px; border-top: 1px solid var(--separator-soft); }
@media (max-width: 960px) { .grid, .kpis { grid-template-columns: minmax(0, 1fr); } }
@media (max-width: 767px) { .large-title { font-size: var(--t-title-1); } .card { padding: 18px; } }
</style>
