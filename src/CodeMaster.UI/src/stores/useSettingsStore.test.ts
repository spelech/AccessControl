import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { useSettingsStore, DEFAULT_SETTINGS } from './useSettingsStore';
import { apiClient } from '../api/apiClient';
import type { SettingsResponse, TestConnectionResult } from '../types';

describe('useSettingsStore', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    useSettingsStore.setState({
      settings: DEFAULT_SETTINGS,
      transports: [],
      detectedNodes: [],
      lastTestResult: null,
      isLoading: false,
      isSaving: false,
      isTesting: false,
      error: null,
      saveSuccessMessage: null,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with default settings and empty state', () => {
    const state = useSettingsStore.getState();
    expect(state.settings.zWaveTransportType).toBe('WebSocket');
    expect(state.settings.zWaveWebSocketUrl).toBe('ws://10.0.0.10:8106');
    expect(state.transports).toEqual([]);
    expect(state.detectedNodes).toEqual([]);
    expect(state.isLoading).toBe(false);
  });

  it('fetches settings successfully and updates state', async () => {
    const mockResponse: SettingsResponse = {
      settings: {
        zWaveTransportType: 'WebSocket',
        zWaveWebSocketUrl: 'ws://10.0.0.10:8106',
        zWaveMqttPrefix: 'zwave',
        mqttHost: '10.0.0.10',
        mqttPort: 8100,
        mqttUsername: 'codemaster',
        mqttPassword: '',
        appriseUrl: 'discord://webhook',
      },
      transports: [
        {
          transportId: 'zwave_ws',
          displayName: 'Z-Wave JS WebSocket',
          status: 'Connected',
          isConnected: true,
          details: 'Node 39 (Allegion BE469ZP)',
        },
      ],
    };

    vi.spyOn(apiClient.settings, 'get').mockResolvedValue(mockResponse);

    await useSettingsStore.getState().fetchSettings();

    const state = useSettingsStore.getState();
    expect(state.isLoading).toBe(false);
    expect(state.settings.mqttUsername).toBe('codemaster');
    expect(state.settings.appriseUrl).toBe('discord://webhook');
    expect(state.transports).toHaveLength(1);
    expect(state.transports[0].transportId).toBe('zwave_ws');
  });

  it('handles fetchSettings failure', async () => {
    vi.spyOn(apiClient.settings, 'get').mockRejectedValue(new Error('Network error'));

    await useSettingsStore.getState().fetchSettings();

    const state = useSettingsStore.getState();
    expect(state.isLoading).toBe(false);
    expect(state.error).toBe('Network error');
  });

  it('updates form settings locally', () => {
    useSettingsStore.getState().updateSettings({
      zWaveTransportType: 'Mqtt',
      mqttHost: '192.168.1.50',
    });

    const state = useSettingsStore.getState();
    expect(state.settings.zWaveTransportType).toBe('Mqtt');
    expect(state.settings.mqttHost).toBe('192.168.1.50');
  });

  it('saves settings successfully', async () => {
    vi.spyOn(apiClient.settings, 'update').mockResolvedValue({
      success: true,
      message: 'Saved successfully',
    });

    const success = await useSettingsStore.getState().saveSettings();

    expect(success).toBe(true);
    const state = useSettingsStore.getState();
    expect(state.isSaving).toBe(false);
    expect(state.saveSuccessMessage).toBe('Saved successfully');
  });

  it('handles saveSettings failure', async () => {
    vi.spyOn(apiClient.settings, 'update').mockRejectedValue(new Error('Save failed'));

    const success = await useSettingsStore.getState().saveSettings();

    expect(success).toBe(false);
    const state = useSettingsStore.getState();
    expect(state.isSaving).toBe(false);
    expect(state.error).toBe('Save failed');
  });

  it('tests connection and stores detected nodes on success', async () => {
    const mockResult: TestConnectionResult = {
      success: true,
      latencyMs: 42,
      driverVersion: '15.15.3',
      serverVersion: '3.2.1',
      nodeCount: 30,
      detectedNodes: [
        { nodeId: 39, name: 'Front Door Lock', deviceType: 'lock', model: 'Allegion BE469ZP' },
        { nodeId: 40, name: 'Laundry Room Keypad', deviceType: 'keypad', model: 'Ring 4AK1SZ' },
      ],
      message: 'Connected',
    };

    vi.spyOn(apiClient.settings, 'testConnection').mockResolvedValue(mockResult);

    const result = await useSettingsStore.getState().testConnection();

    expect(result.success).toBe(true);
    const state = useSettingsStore.getState();
    expect(state.isTesting).toBe(false);
    expect(state.lastTestResult).toEqual(mockResult);
    expect(state.detectedNodes).toHaveLength(2);
    expect(state.detectedNodes[0].nodeId).toBe(39);
  });

  it('handles testConnection failure', async () => {
    vi.spyOn(apiClient.settings, 'testConnection').mockRejectedValue(new Error('Connection timed out'));

    const result = await useSettingsStore.getState().testConnection();

    expect(result.success).toBe(false);
    const state = useSettingsStore.getState();
    expect(state.isTesting).toBe(false);
    expect(state.error).toBe('Connection timed out');
    expect(state.lastTestResult?.success).toBe(false);
  });

  it('clears messages correctly', () => {
    useSettingsStore.setState({
      error: 'An error occurred',
      saveSuccessMessage: 'Success!',
    });

    useSettingsStore.getState().clearMessages();

    const state = useSettingsStore.getState();
    expect(state.error).toBeNull();
    expect(state.saveSuccessMessage).toBeNull();
  });

  it('sets detected nodes directly', () => {
    const nodes = [
      { nodeId: 10, name: 'Test Lock', deviceType: 'lock', model: 'Yale Assure' },
    ];
    useSettingsStore.getState().setDetectedNodes(nodes);

    expect(useSettingsStore.getState().detectedNodes).toEqual(nodes);
  });
});
