import { create } from 'zustand';
import { apiClient } from '../api/apiClient';
import type {
  SystemSettings,
  TransportInfo,
  DetectedZWaveNode,
  TestConnectionRequest,
  TestConnectionResult,
} from '../types';

export const DEFAULT_SETTINGS: SystemSettings = {
  zWaveTransportType: 'WebSocket',
  zWaveWebSocketUrl: 'ws://10.0.0.10:8106',
  zWaveMqttPrefix: 'zwave',
  mqttHost: '10.0.0.10',
  mqttPort: 8100,
  mqttUsername: '',
  mqttPassword: '',
  appriseUrl: '',
};

export interface SettingsState {
  settings: SystemSettings;
  transports: TransportInfo[];
  detectedNodes: DetectedZWaveNode[];
  lastTestResult: TestConnectionResult | null;
  isLoading: boolean;
  isSaving: boolean;
  isTesting: boolean;
  error: string | null;
  saveSuccessMessage: string | null;

  fetchSettings: () => Promise<void>;
  updateSettings: (patch: Partial<SystemSettings>) => void;
  saveSettings: () => Promise<boolean>;
  testConnection: (request?: Partial<TestConnectionRequest>) => Promise<TestConnectionResult>;
  clearMessages: () => void;
  setDetectedNodes: (nodes: DetectedZWaveNode[]) => void;
}

export const useSettingsStore = create<SettingsState>((set, get) => ({
  settings: DEFAULT_SETTINGS,
  transports: [],
  detectedNodes: [],
  lastTestResult: null,
  isLoading: false,
  isSaving: false,
  isTesting: false,
  error: null,
  saveSuccessMessage: null,

  fetchSettings: async () => {
    set({ isLoading: true, error: null });
    try {
      const res = await apiClient.settings.get();
      set({
        settings: { ...DEFAULT_SETTINGS, ...res.settings },
        transports: res.transports || [],
        isLoading: false,
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to load system settings';
      set({ error: message, isLoading: false });
    }
  },

  updateSettings: (patch: Partial<SystemSettings>) => {
    set((state) => ({
      settings: { ...state.settings, ...patch },
      saveSuccessMessage: null,
    }));
  },

  saveSettings: async () => {
    const current = get().settings;
    set({ isSaving: true, error: null, saveSuccessMessage: null });
    try {
      const res = await apiClient.settings.update(current);
      set({
        isSaving: false,
        saveSuccessMessage: res.message || 'Settings saved and applied successfully',
      });
      return true;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save system settings';
      set({ error: message, isSaving: false });
      return false;
    }
  },

  testConnection: async (request?: Partial<TestConnectionRequest>) => {
    const current = get().settings;
    const transportType = request?.transportType ?? current.zWaveTransportType;
    const endpointUrl =
      request?.endpointUrl ??
      (transportType === 'WebSocket'
        ? current.zWaveWebSocketUrl
        : `mqtt://${current.mqttHost}:${current.mqttPort}`);

    set({ isTesting: true, error: null });
    try {
      const result = await apiClient.settings.testConnection({
        transportType,
        endpointUrl,
      });
      set({
        isTesting: false,
        lastTestResult: result,
        detectedNodes:
          result.detectedNodes && result.detectedNodes.length > 0
            ? result.detectedNodes
            : get().detectedNodes,
      });
      return result;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Connection test failed';
      const failedResult: TestConnectionResult = {
        success: false,
        message,
      };
      set({
        isTesting: false,
        lastTestResult: failedResult,
        error: message,
      });
      return failedResult;
    }
  },

  clearMessages: () => {
    set({ error: null, saveSuccessMessage: null });
  },

  setDetectedNodes: (nodes: DetectedZWaveNode[]) => {
    set({ detectedNodes: nodes });
  },
}));
