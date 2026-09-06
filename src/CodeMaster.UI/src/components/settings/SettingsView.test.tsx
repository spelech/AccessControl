import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { SettingsView } from './SettingsView';
import { useSettingsStore, DEFAULT_SETTINGS } from '../../stores/useSettingsStore';
import { apiClient } from '../../api/apiClient';

describe('SettingsView component', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(apiClient.settings, 'get').mockResolvedValue({
      settings: { ...DEFAULT_SETTINGS },
      transports: [
        {
          transportId: 'zwave_ws',
          displayName: 'Z-Wave JS WebSocket',
          status: 'Connected',
          isConnected: true,
          details: 'Node 39 (Allegion BE469ZP), Node 40 (Ring Keypad v2)',
        },
        {
          transportId: 'mqtt_broker',
          displayName: 'Mosquitto MQTT',
          status: 'Connected',
          isConnected: true,
          details: '10.0.0.10:8100',
        },
      ],
    });

    useSettingsStore.setState({
      settings: { ...DEFAULT_SETTINGS },
      transports: [
        {
          transportId: 'zwave_ws',
          displayName: 'Z-Wave JS WebSocket',
          status: 'Connected',
          isConnected: true,
          details: 'Node 39 (Allegion BE469ZP), Node 40 (Ring Keypad v2)',
        },
        {
          transportId: 'mqtt_broker',
          displayName: 'Mosquitto MQTT',
          status: 'Connected',
          isConnected: true,
          details: '10.0.0.10:8100',
        },
      ],
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
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders system settings, Ingress context, and MCP tools', () => {
    render(<SettingsView />);

    expect(screen.getByText(/System & Connectivity Settings/i)).toBeDefined();
    expect(screen.getByText(/Ingress & Routing Context/i)).toBeDefined();
    expect(screen.getByText(/Model Context Protocol \(MCP\) Gateway/i)).toBeDefined();
    expect(screen.getByText(/codemaster__list_doors/i)).toBeDefined();
    expect(screen.getByText(/codemaster__unlock_door/i)).toBeDefined();
    expect(screen.getByText(/Hardware Security Architecture/i)).toBeDefined();
  });

  it('allows toggling between WebSocket and MQTT modes', async () => {
    render(<SettingsView />);

    // Initially in WebSocket mode
    expect(screen.getByLabelText(/Z-Wave JS WebSocket Server URL/i)).toBeDefined();

    // Click MQTT toggle button
    const mqttToggle = screen.getByRole('radio', { name: /MQTT \(Mosquitto\)/i });
    fireEvent.click(mqttToggle);

    await waitFor(() => {
      expect(screen.getByLabelText(/Z-Wave MQTT Topic Prefix/i)).toBeDefined();
    });

    // Switch back to WebSocket
    const wsToggle = screen.getByRole('radio', { name: /WebSocket \(Direct\)/i });
    fireEvent.click(wsToggle);

    await waitFor(() => {
      expect(screen.getByLabelText(/Z-Wave JS WebSocket Server URL/i)).toBeDefined();
    });
  });

  it('tests WebSocket connection and renders detected nodes on success', async () => {
    vi.spyOn(apiClient.settings, 'testConnection').mockResolvedValue({
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
    });

    render(<SettingsView />);

    const testBtn = screen.getByRole('button', { name: /Test Connection/i });
    fireEvent.click(testBtn);

    await waitFor(() => {
      expect(apiClient.settings.testConnection).toHaveBeenCalled();
      expect(screen.getByText(/Latency: 42ms/i)).toBeDefined();
      expect(screen.getByText(/15.15.3/)).toBeDefined();
      expect(screen.getByText(/3.2.1/)).toBeDefined();
      expect(screen.getByRole('cell', { name: /Node 39/i })).toBeDefined();
      expect(screen.getByRole('cell', { name: /Front Door Lock/i })).toBeDefined();
      expect(screen.getByRole('cell', { name: /Node 40/i })).toBeDefined();
      expect(screen.getByRole('cell', { name: /Laundry Room Keypad/i })).toBeDefined();
    });
  });

  it('renders error banner when connection test fails', async () => {
    vi.spyOn(apiClient.settings, 'testConnection').mockRejectedValue(
      new Error('WebSocket connection refused at ws://10.0.0.10:8106')
    );

    render(<SettingsView />);

    const testBtn = screen.getByRole('button', { name: /Test Connection/i });
    fireEvent.click(testBtn);

    await waitFor(() => {
      expect(screen.getByText(/Connection Failed/i)).toBeDefined();
      expect(
        screen.getAllByText(/WebSocket connection refused at ws:\/\/10.0.0.10:8106/i).length
      ).toBeGreaterThanOrEqual(1);
    });
  });


  it('saves settings when clicking Save & Apply', async () => {
    vi.spyOn(apiClient.settings, 'update').mockResolvedValue({
      success: true,
      message: 'Settings updated successfully',
    });

    render(<SettingsView />);

    const hostInput = screen.getByLabelText(/Broker Host/i);
    fireEvent.change(hostInput, { target: { value: '10.0.0.15' } });

    const saveBtn = screen.getByRole('button', { name: /Save & Apply/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(apiClient.settings.update).toHaveBeenCalledWith(
        expect.objectContaining({
          mqttHost: '10.0.0.15',
        })
      );
      expect(screen.getByText(/Settings updated successfully/i)).toBeDefined();
    });
  });
});
