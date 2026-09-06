import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { getBasePath, buildUrl, apiClient } from './apiClient';

describe('apiClient and Ingress base path resolution', () => {
  const originalWindow = global.window;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    // reset global window
    global.window = originalWindow;
  });

  it('resolves empty base path by default when window.__CODEMASTER_BASE_PATH__ is not set', () => {
    const base = getBasePath();
    expect(base).toBe('');
    expect(buildUrl('/api/doors')).toBe('/api/doors');
  });

  it('resolves base path from window.__CODEMASTER_BASE_PATH__ for Home Assistant Ingress', () => {
    if (typeof global.window === 'undefined') {
      (global as unknown as { window: Window & typeof globalThis }).window = {} as Window & typeof globalThis;
    }
    (global.window as unknown as { __CODEMASTER_BASE_PATH__?: string }).__CODEMASTER_BASE_PATH__ = '/api/hassio_ingress/token123/';
    const base = getBasePath();
    expect(base).toBe('/api/hassio_ingress/token123');
    expect(buildUrl('/api/doors')).toBe('/api/hassio_ingress/token123/api/doors');
    delete (global.window as unknown as { __CODEMASTER_BASE_PATH__?: string }).__CODEMASTER_BASE_PATH__;
  });

  it('fetches doors successfully through apiClient', async () => {
    const mockDoors = [
      { id: 'door-1', name: 'Front Door', lockProviderType: 'AugustZWave', autoLockEnabled: true, autoLockDaySeconds: 300, autoLockNightSeconds: 60, retryOnFailure: true, lockConfigJson: '{}' }
    ];

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockDoors,
    });

    const doors = await apiClient.doors.list();
    expect(doors).toEqual(mockDoors);
    expect(global.fetch).toHaveBeenCalledWith('/api/doors', expect.objectContaining({
      headers: expect.objectContaining({
        'Content-Type': 'application/json',
      }),
    }));
  });

  it('builds query parameters correctly for audit logs', async () => {
    const mockLogs = [
      { id: 'log-1', accessPointId: 'door-1', eventType: 'Unlocked', method: 'RingKeypad', timestamp: '2026-09-06T00:00:00Z' }
    ];

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockLogs,
    });

    const logs = await apiClient.logs.list({ doorId: 'door-1', method: 'RingKeypad', limit: 50 });
    expect(logs).toEqual(mockLogs);
    expect(global.fetch).toHaveBeenCalledWith('/api/logs?doorId=door-1&method=RingKeypad&limit=50', expect.any(Object));
  });

  it('throws informative error on non-ok HTTP responses', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 404,
      statusText: 'Not Found',
      text: async () => 'Resource not found',
    });

    await expect(apiClient.doors.get('invalid-id')).rejects.toThrow('API Error [404] Not Found: Resource not found');
  });
});
