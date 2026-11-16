import type { Sender, WireEvent } from './types';

export interface QueueOptions {
  sender: Sender;
  maxBatchSize: number;
  /** Oldest events are dropped beyond this, so a dead network never grows memory without limit. */
  maxQueueSize?: number;
}

/** Buffers events and sends them in batches. A failed batch goes back to the front and is retried later. */
export class EventQueue {
  private buffer: WireEvent[] = [];
  private flushing: Promise<void> | null = null;
  private readonly maxQueueSize: number;

  constructor(private readonly options: QueueOptions) {
    this.maxQueueSize = options.maxQueueSize ?? 500;
  }

  get size(): number {
    return this.buffer.length;
  }

  push(event: WireEvent): void {
    this.buffer.push(event);
    if (this.buffer.length > this.maxQueueSize) {
      this.buffer.splice(0, this.buffer.length - this.maxQueueSize);
    }
    if (this.buffer.length >= this.options.maxBatchSize) void this.flush();
  }

  clear(): void {
    this.buffer = [];
  }

  /** Sends everything queued. Concurrent calls share one run. Returns true when the queue ended empty. */
  async flush(unloading = false): Promise<boolean> {
    if (this.flushing) {
      await this.flushing;
      if (this.buffer.length === 0) return true;
    }

    const run = this.drain(unloading);
    this.flushing = run;
    try {
      await run;
    } finally {
      this.flushing = null;
    }
    return this.buffer.length === 0;
  }

  private async drain(unloading: boolean): Promise<void> {
    while (this.buffer.length > 0) {
      const batch = this.buffer.splice(0, this.options.maxBatchSize);
      let done = false;
      try {
        done = await this.options.sender(batch, { unloading });
      } catch {
        done = false;
      }
      if (!done) {
        this.buffer.unshift(...batch);
        if (this.buffer.length > this.maxQueueSize) this.buffer.length = this.maxQueueSize;
        return;
      }
    }
  }
}
