export const APP_VERSION = '0.1.0'
export const APP_RELEASE_DATE = '2026-11-16'

export interface ChangeGroup {
  type: 'new' | 'improved' | 'fixed'
  title: string
  items: string[]
}

export const CHANGELOG: ChangeGroup[] = [
  {
    type: 'new',
    title: 'قابلیت‌های جدید',
    items: [
      'نمای کلی با شمارش زنده‌ی Userهای آنلاین و صفحه‌های فعلی',
      'کنسول با طراحی Liquid Glass مشکی-نارنجی و فونت یکان بخ',
      'کنسول سرور-به-سرور به API وصل می‌شود؛ کلید خواندن هرگز به مرورگر نمی‌رسد',
    ],
  },
]
