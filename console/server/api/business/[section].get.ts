// Business data proxy: the browser never sees the secret read key. Only the three known sections are forwarded.
const SECTIONS = new Set(['overview', 'users', 'vendors'])

export default defineEventHandler(async (event) => {
  const section = String(getRouterParam(event, 'section') ?? '')
  if (!SECTIONS.has(section)) {
    throw createError({ statusCode: 404, statusMessage: 'unknown_section' })
  }

  const { apiBase, readKey } = useRuntimeConfig(event)
  const kind = typeof getQuery(event).kind === 'string' ? String(getQuery(event).kind) : ''
  const url = new URL(`${apiBase}/v1/business/${section}`)
  if (section === 'vendors' && kind) url.searchParams.set('kind', kind)

  setHeader(event, 'Cache-Control', 'no-store')
  try {
    return await $fetch(url.toString(), { headers: { 'X-Tarantula-Read-Key': String(readKey) } })
  } catch (e) {
    // Pass the API's own error code through (for example business_not_connected) instead of a generic 500.
    const err = e as { statusCode?: number; data?: { error?: string } }
    throw createError({ statusCode: err.statusCode ?? 502, statusMessage: err.data?.error ?? 'api_unreachable' })
  }
})
