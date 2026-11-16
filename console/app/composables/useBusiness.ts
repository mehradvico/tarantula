export type BusinessState = 'loading' | 'ready' | 'not_connected' | 'error'

export interface VendorKindCount { kind: VendorKindName; total: number; visible: number }
export interface Sales { count: number; amount: number; siteShare: number }
export interface Overview {
  users: number
  newUsers7Days: number
  newUsers30Days: number
  providers: number
  buyers30Days: number
  vendors: VendorKindCount[]
  shop30Days: Sales
  reserves30Days: Sales
  abandonedCarts7Days: { count: number; amount: number }
}

export type VendorKindName = 'store' | 'clinic' | 'groomer' | 'coach' | 'pansion' | 'school'

export interface UsersReport {
  total: number
  buyers: number
  repeatBuyers: number
  signups30Days: { day: string; count: number }[]
  sources: { source: number; name: string; users: number }[]
  topSpenders: { userId: number; purchases: number; spent: number }[]
}

export interface VendorRow {
  facts: {
    kind: VendorKindName
    id: number
    name: string
    city: string | null
    active: boolean
    approved: boolean | null
    showToSite: boolean
    rating: number
    ratingCount: number
    createdAt: string | null
    sales30DaysCount: number
    sales30DaysAmount: number
  }
  signals: string[]
}
export interface VendorsReport { total: number; needAttention: number; vendors: VendorRow[] }

/** Persian labels for the stable codes the API returns. The words User and Vendor stay in English everywhere. */
export const KIND_LABEL: Record<VendorKindName, string> = {
  store: 'فروشگاه',
  clinic: 'کلینیک',
  groomer: 'آرایشگاه',
  coach: 'مربی',
  pansion: 'پانسیون',
  school: 'مدرسه',
}

export const SIGNAL_LABEL: Record<string, string> = {
  not_approved: 'تأیید‌نشده',
  inactive: 'غیرفعال',
  hidden: 'مخفی از سایت',
  no_sales_30d: 'بدون فروش ۳۰ روز',
  low_rating: 'امتیاز پایین',
}

export const SOURCE_LABEL: Record<string, string> = {
  Unspecified: 'نامشخص',
  Clinic: 'کلینیک',
  PetShop: 'پت‌شاپ',
  Acquaintances: 'آشنایان',
  SocialMedia: 'شبکه‌های اجتماعی',
  Other: 'سایر',
}

/**
 * Loads one section of the business data through the console's own server route.
 * The data is somebody else's production system, so it is loaded once per visit, not polled.
 */
export function useBusiness<T>(section: 'overview' | 'users' | 'vendors', query?: Ref<Record<string, string>> | Record<string, string>) {
  const data = ref<T | null>(null) as Ref<T | null>
  const state = ref<BusinessState>('loading')

  async function load() {
    state.value = 'loading'
    try {
      data.value = await $fetch<T>(`/api/business/${section}`, { query: unref(query) })
      state.value = 'ready'
    } catch (e) {
      const err = e as { statusMessage?: string; statusCode?: number }
      state.value = err.statusMessage === 'business_not_connected' ? 'not_connected' : 'error'
    }
  }

  onMounted(load)
  return { data, state, reload: load }
}
