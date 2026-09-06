import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useAuditStore } from './useAuditStore';
import { apiClient } from '../api/apiClient';
import type { AccessLog } from '../types';

describe('useAuditStore', () => {
  beforeEach(() => {
    useAuditStore.setState({
      logs: [],
      filter: { limit: 100 },
      isLoading: false,
      isLive: true,
      error: null,
    });
    vi.restoreAllMocks();
  });

  it('fetches logs and populates store', async () => {
    const mockLogs: AccessLog[] = [
      {
        id: 'log-1',
        accessPointId: 'door-1',
        eventType: 'Unlocked',
        method: 'RingKeypad',
        timestamp: '2026-09-06T00:00:00Z',
      },
    ];

    vi.spyOn(apiClient.logs, 'list').mockResolvedValue(mockLogs);

    await useAuditStore.getState().fetchLogs();

    const state = useAuditStore.getState();
    expect(state.logs).toEqual(mockLogs);
    expect(state.isLoading).toBe(false);
  });

  it('updates filters and calls fetchLogs', async () => {
    const spy = vi.spyOn(apiClient.logs, 'list').mockResolvedValue([]);

    useAuditStore.getState().setFilter({ doorId: 'door-front', method: 'RingKeypad' });

    expect(useAuditStore.getState().filter.doorId).toBe('door-front');
    expect(useAuditStore.getState().filter.method).toBe('RingKeypad');
    expect(spy).toHaveBeenCalled();
  });

  it('adds live log event that satisfies active filters', () => {
    useAuditStore.setState({
      logs: [
        {
          id: 'log-old',
          accessPointId: 'door-1',
          eventType: 'Locked',
          method: 'Manual',
          timestamp: '2026-09-06T00:00:00Z',
        },
      ],
      filter: { doorId: 'door-1' },
    });

    const newEvent: AccessLog = {
      id: 'log-new',
      accessPointId: 'door-1',
      eventType: 'Unlocked',
      method: 'RingKeypad',
      timestamp: '2026-09-06T00:01:00Z',
    };

    useAuditStore.getState().addLogEvent(newEvent);

    const state = useAuditStore.getState();
    expect(state.logs).toHaveLength(2);
    expect(state.logs[0].id).toBe('log-new');
  });

  it('drops live log event that does not match active filter', () => {
    useAuditStore.setState({
      logs: [],
      filter: { doorId: 'door-front' },
    });

    const irrelevantEvent: AccessLog = {
      id: 'log-back',
      accessPointId: 'door-back',
      eventType: 'Unlocked',
      method: 'RingKeypad',
      timestamp: '2026-09-06T00:01:00Z',
    };

    useAuditStore.getState().addLogEvent(irrelevantEvent);

    const state = useAuditStore.getState();
    expect(state.logs).toHaveLength(0);
  });

  it('starts live stream and handles incoming SSE messages', () => {
    let messageHandler: ((e: { data: string }) => void) | null = null;
    let closed = false;

    class MockEventSource {
      url: string;
      onmessage: ((e: { data: string }) => void) | null = null;
      onerror: (() => void) | null = null;

      constructor(url: string) {
        this.url = url;
        messageHandler = (e: { data: string }) => {
          if (this.onmessage) this.onmessage(e);
        };
      }

      close() {
        closed = true;
      }
    }

    vi.stubGlobal('EventSource', MockEventSource);

    useAuditStore.getState().startLiveStream();

    const sampleLog: AccessLog = {
      id: 'log-sse-1',
      accessPointId: 'door-1',
      eventType: 'Unlocked',
      method: 'Manual',
      timestamp: '2026-09-06T12:00:00Z',
    };

    // Simulate SSE message arrival
    expect(messageHandler).not.toBeNull();
    messageHandler!({ data: JSON.stringify(sampleLog) });

    expect(useAuditStore.getState().logs).toContainEqual(sampleLog);

    // Stop stream and verify close() is called
    useAuditStore.getState().stopLiveStream();
    expect(closed).toBe(true);

    vi.unstubAllGlobals();
  });
});
