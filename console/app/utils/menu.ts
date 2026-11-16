import type { Component } from 'vue'
import { Filter, Flame, LayoutDashboard, Megaphone, Settings, Sparkles, Store, Users, Video, Eye, KeyRound } from 'lucide-vue-next'

export interface MenuItem {
  id: string
  title: string
  icon: Component
  /** Route of a finished page. Without it the item is a group or a page that is not built yet. */
  to?: string
  /** Not built yet: shown dimmed with a "soon" badge and not navigable. */
  soon?: boolean
  /** At most two levels, as Apple's sidebar guidance recommends. */
  children?: MenuItem[]
}

export const MENU: MenuItem[] = [
  { id: 'overview', title: 'نمای کلی', icon: LayoutDashboard, to: '/' },
  {
    id: 'behavior',
    title: 'رفتار',
    icon: Eye,
    children: [
      { id: 'replay', title: 'ریپلی جلسه', icon: Video, soon: true },
      { id: 'heatmap', title: 'هیت‌مپ', icon: Flame, soon: true },
      { id: 'funnels', title: 'قیف و مسیر', icon: Filter, soon: true },
    ],
  },
  {
    id: 'people',
    title: 'افراد',
    icon: Users,
    children: [
      { id: 'users', title: 'User', icon: Users, to: '/users' },
      { id: 'vendors', title: 'Vendor', icon: Store, to: '/vendors' },
    ],
  },
  {
    id: 'marketing',
    title: 'بازاریابی',
    icon: Megaphone,
    children: [
      { id: 'weaver', title: 'تارباف', icon: Megaphone, soon: true },
      { id: 'copilot', title: 'مشاور رشد', icon: Sparkles, soon: true },
    ],
  },
  {
    id: 'settings',
    title: 'تنظیمات',
    icon: Settings,
    children: [{ id: 'keys', title: 'پروژه‌ها و کلیدها', icon: KeyRound, soon: true }],
  },
]

/** Flat list of leaf items, for search. */
export function leafItems(items: MenuItem[] = MENU): MenuItem[] {
  return items.flatMap((i) => (i.children?.length ? leafItems(i.children) : [i]))
}
