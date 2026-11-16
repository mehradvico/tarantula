export interface LivePage {
  path: string
  users: number
}

export interface LiveSnapshot {
  onlineUsers: number
  signedInUsers: number
  windowSeconds: number
  topPages: LivePage[]
  at: string
}

export type LiveStatus = 'connecting' | 'live' | 'error'

const SAMPLES = 60

/**
 * One shared live connection for the whole console. The browser talks to the console's own server
 * (/api/live/stream), which adds the secret read key and forwards the event stream.
 */
export function useLive() {
  const windowSeconds = useState<number>('live-window', () => 300)
  const snapshot = useState<LiveSnapshot | null>('live-snapshot', () => null)
  const status = useState<LiveStatus>('live-status', () => 'connecting')
  const message = useState<string>('live-message', () => '')
  const history = useState<number[]>('live-history', () => [])

  let source: EventSource | null = null

  function close() {
    source?.close()
    source = null
  }

  function connect() {
    close()
    status.value = 'connecting'
    message.value = ''
    history.value = []

    source = new EventSource(`/api/live/stream?window=${windowSeconds.value}`)
    source.addEventListener('snapshot', (e) => {
      const data = JSON.parse((e as MessageEvent<string>).data) as LiveSnapshot
      snapshot.value = data
      status.value = 'live'
      history.value = [...history.value, data.onlineUsers].slice(-SAMPLES)
    })
    source.addEventListener('error', (e) => {
      const data = (e as MessageEvent<string>).data
      if (data) {
        // A real error event from the API (for example a wrong read key).
        status.value = 'error'
        message.value = (JSON.parse(data) as { error: string }).error
        close()
      } else {
        // The connection dropped; EventSource retries by itself.
        status.value = 'connecting'
      }
    })
  }

  function setWindow(seconds: number) {
    windowSeconds.value = seconds
    connect()
  }

  return { windowSeconds, snapshot, status, message, history, connect, close, setWindow }
}
