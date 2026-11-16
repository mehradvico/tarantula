// Server-Sent Events proxy: the browser never sees the secret read key.
export default defineEventHandler((event) => {
  const { apiBase, readKey } = useRuntimeConfig(event)
  const raw = Number(getQuery(event).window)
  const windowSeconds = Number.isFinite(raw) && raw > 0 ? Math.floor(raw) : 300

  setHeader(event, 'Cache-Control', 'no-cache, no-transform')
  setHeader(event, 'X-Accel-Buffering', 'no')

  return sendProxy(event, `${apiBase}/v1/live/stream?window=${windowSeconds}`, {
    headers: { 'X-Tarantula-Read-Key': String(readKey), accept: 'text/event-stream' },
  })
})
