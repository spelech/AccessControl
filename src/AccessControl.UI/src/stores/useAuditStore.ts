import { create } from 'zustand';
import { apiClient, buildUrl } from '../api/apiClient';
import type { AccessLog } from '../types';

export interface AuditFilter {
  doorId?: string;
  userId?: string;
  method?: string;
  eventType?: string;
  limit?: number;
}

interface AuditState {
  logs: AccessLog[];
  filter: AuditFilter;
  isLoading: boolean;
  isLive: boolean;
  error: string | null;

  fetchLogs: () => Promise<void>;
  setFilter: (newFilter: Partial<AuditFilter>) => void;
  clearFilter: () => void;
  addLogEvent: (event: AccessLog) => void;
  toggleLive: (enabled?: boolean) => void;
  startLiveStream: () => void;
  stopLiveStream: () => void;
}

let activeEventSource: EventSource | null = null;

export const useAuditStore = create<AuditState>((set, get) => ({
  logs: [],
  filter: { limit: 100 },
  isLoading: false,
  isLive: true,
  error: null,

  fetchLogs: async () => {
    set({ isLoading: true, error: null });
    try {
      const logs = await apiClient.logs.list(get().filter);
      set({ logs, isLoading: false });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to fetch access logs';
      set({ error: message, isLoading: false });
    }
  },

  setFilter: (newFilter: Partial<AuditFilter>) => {
    const updated = { ...get().filter, ...newFilter };
    set({ filter: updated });
    get().fetchLogs();
  },

  clearFilter: () => {
    set({ filter: { limit: 100 } });
    get().fetchLogs();
  },

  addLogEvent: (event: AccessLog) => {
    set((state) => {
      // Check if event matches current filters
      const { doorId, userId, method, eventType } = state.filter;
      if (doorId && event.accessPointId !== doorId) return state;
      if (userId && event.userId !== userId) return state;
      if (method && event.method !== method) return state;
      if (eventType && event.eventType !== eventType) return state;

      // Prepend event, keep max 200 items in memory
      const logs = [event, ...state.logs.filter((l) => l.id !== event.id)].slice(0, 200);
      return { logs };
    });
  },

  toggleLive: (enabled?: boolean) => {
    const next = enabled !== undefined ? enabled : !get().isLive;
    set({ isLive: next });
    if (next) {
      get().startLiveStream();
    } else {
      get().stopLiveStream();
    }
  },

  startLiveStream: () => {
    if (typeof window === 'undefined' || typeof EventSource === 'undefined') {
      return;
    }
    if (activeEventSource) {
      return;
    }

    try {
      const streamUrl = buildUrl('/api/logs/stream');
      activeEventSource = new EventSource(streamUrl);

      activeEventSource.onmessage = (e) => {
        if (!e.data || e.data.startsWith(':')) return;
        try {
          const log = JSON.parse(e.data) as AccessLog;
          if (log && log.id) {
            get().addLogEvent(log);
          }
        } catch {
          // Ignore invalid or heartbeat frames
        }
      };

      activeEventSource.onerror = () => {
        // EventSource will automatically attempt reconnection
      };
    } catch {
      // Ignore initial connection errors
    }
  },

  stopLiveStream: () => {
    if (activeEventSource) {
      activeEventSource.close();
      activeEventSource = null;
    }
  },
}));
