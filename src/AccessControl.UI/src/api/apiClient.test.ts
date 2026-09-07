import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { getBasePath, buildUrl, apiClient } from './apiClient';
import type { DiscoveredContactSensor, SnifferResult } from '../types';

describe('apiClient and Ingress base path resolution', () => {
  const originalWindow = global.window;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    // reset global window
    global.window = originalWindow;
  });

  it('resolves empty base path by default when window.__ACCESSCONTROL_BASE_PATH__ is not set', () => {
    const base = getBasePath();
    expect(base).toBe('');
    expect(buildUrl('/api/doors')).toBe('/api/doors');
  });

  it('resolves base path from window.__ACCESSCONTROL_BASE_PATH__ for Home Assistant Ingress', () => {
    if (typeof global.window === 'undefined') {
      (global as unknown as { window: Window & typeof globalThis }).window = {} as Window & typeof globalThis;
    }
    (global.window as unknown as { __ACCESSCONTROL_BASE_PATH__?: string }).__ACCESSCONTROL_BASE_PATH__ = '/api/hassio_ingress/token123/';
    const base = getBasePath();
    expect(base).toBe('/api/hassio_ingress/token123');
    expect(buildUrl('/api/doors')).toBe('/api/hassio_ingress/token123/api/doors');
    delete (global.window as unknown as { __ACCESSCONTROL_BASE_PATH__?: string }).__ACCESSCONTROL_BASE_PATH__;
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

  it('fetches settings and calls GET /api/settings', async () => {
    const mockSettingsRes = {
      settings: { zWaveTransportType: 'WebSocket', zWaveWebSocketUrl: 'ws://10.0.0.10:8106' },
      transports: [],
    };

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockSettingsRes,
    });

    const res = await apiClient.settings.get();
    expect(res).toEqual(mockSettingsRes);
    expect(global.fetch).toHaveBeenCalledWith('/api/settings', expect.objectContaining({
      headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
    }));
  });

  it('updates settings and calls PUT /api/settings', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ success: true, message: 'Settings saved' }),
    });

    const res = await apiClient.settings.update({ zWaveTransportType: 'Mqtt' });
    expect(res.success).toBe(true);
    expect(global.fetch).toHaveBeenCalledWith('/api/settings', expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ zWaveTransportType: 'Mqtt' }),
    }));
  });

  it('tests connection and calls POST /api/settings/test-connection', async () => {
    const mockTestRes = {
      success: true,
      latencyMs: 35,
      driverVersion: '15.15.3',
      serverVersion: '3.2.1',
      nodeCount: 2,
      detectedNodes: [
        { nodeId: 39, name: 'Front Door Lock', deviceType: 'lock', model: 'Allegion BE469ZP' }
      ],
      message: 'Connected',
    };

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockTestRes,
    });

    const res = await apiClient.settings.testConnection({ transportType: 'WebSocket', endpointUrl: 'ws://10.0.0.10:8106' });
    expect(res).toEqual(mockTestRes);
    expect(global.fetch).toHaveBeenCalledWith('/api/settings/test-connection', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ transportType: 'WebSocket', endpointUrl: 'ws://10.0.0.10:8106' }),
    }));
  });

  it('discovery.getSensors fetches strictly filtered contact sensors', async () => {
    const mockSensors: DiscoveredContactSensor[] = [
      {
        topic: 'zigbee2mqtt/front_door_contact',
        deviceName: 'Front Door Contact',
        integration: 'Zigbee2MQTT',
        model: 'Aqara MCCGQ11LM',
        currentState: 'closed',
        lastSeen: '2026-09-07T12:00:00Z',
      },
    ];
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockSensors,
    });

    const result = await apiClient.discovery.getSensors();
    expect(result).toEqual(mockSensors);
    expect(global.fetch).toHaveBeenCalledWith('/api/discovery/sensors', expect.any(Object));
  });

  it('discovery.sniff calls sniff endpoint with since timestamp', async () => {
    const mockSnifferResult: SnifferResult = {
      detected: true,
      event: {
        topic: 'zigbee2mqtt/front_door_contact',
        deviceName: 'Front Door Contact',
        model: 'Aqara MCCGQ11LM',
        state: 'OPEN',
        timestamp: '2026-09-07T12:05:00Z',
      },
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockSnifferResult,
    });

    const result = await apiClient.discovery.sniff('2026-09-07T12:04:50Z');
    expect(result.detected).toBe(true);
    expect(result.event?.state).toBe('OPEN');
    expect(global.fetch).toHaveBeenCalledWith(
      '/api/discovery/sniff?since=2026-09-07T12%3A04%3A50Z',
      expect.any(Object)
    );
  });
});

