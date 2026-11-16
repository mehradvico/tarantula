// The console is a single-page app (like the Pastil panel): it talks to the ingest/read API through
// its own server routes, so the secret read key never reaches the browser.
export default defineNuxtConfig({
  compatibilityDate: '2026-11-01',
  ssr: false,
  devtools: { enabled: false },

  css: ['~/assets/css/tokens.css', '~/assets/css/base.css'],

  app: {
    head: {
      htmlAttrs: { lang: 'fa', dir: 'rtl' },
      title: 'تارانتولا',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#000000' },
        { name: 'color-scheme', content: 'dark' },
      ],
      // Yekan Bakh is a licensed font: its files live in public/fonts and are not committed.
      link: [
        { rel: 'stylesheet', href: '/fonts/yekan-bakh.css' },
        { rel: 'icon', href: '/favicon.ico', sizes: 'any' },
        { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/favicon-32.png' },
        { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon.png' },
        { rel: 'manifest', href: '/manifest.webmanifest' },
      ],
    },
  },

  runtimeConfig: {
    // Server only (never sent to the browser). Override with NUXT_API_BASE / NUXT_READ_KEY.
    apiBase: 'http://localhost:5080',
    readKey: 'rk_dev_local_only_secret',
    public: {
      // The write key is public by design: it only allows sending events.
      writeKey: 'tk_dev_local_only',
      collectUrl: 'http://localhost:5080',
      // Where the built SDK is served from (the console serves it from /sdk in development).
      sdkUrl: 'http://localhost:3100/sdk/tarantula.min.js',
    },
  },

  typescript: { strict: true },
})
