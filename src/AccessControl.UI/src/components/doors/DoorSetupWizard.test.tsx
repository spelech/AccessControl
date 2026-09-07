import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { DoorSetupWizard } from './DoorSetupWizard';
import { apiClient } from '../../api/apiClient';
import { useSettingsStore, DEFAULT_SETTINGS } from '../../stores/useSettingsStore';
import type { DiscoveredContactSensor, SnifferResult } from '../../types';

describe('DoorSetupWizard component', () => {
  const mockSensors: DiscoveredContactSensor[] = [
    {
      topic: 'zigbee2mqtt/front_door_contact',
      deviceName: 'Front Door Contact',
      integration: 'Zigbee2MQTT',
      model: 'Aqara MCCGQ11LM',
      currentState: 'Closed',
      lastSeen: '2026-09-07T12:00:00Z',
    },
    {
      topic: 'homeassistant/binary_sensor/patio_door/state',
      deviceName: 'Patio Door Sensor',
      integration: 'Home Assistant',
      model: 'Ring Contact Sensor v2',
      currentState: 'Open',
      lastSeen: '2026-09-07T12:01:00Z',
    },
  ];

  beforeEach(() => {
    vi.restoreAllMocks();
    useSettingsStore.setState({
      settings: { ...DEFAULT_SETTINGS, zWaveTransportType: 'WebSocket' },
      transports: [],
      detectedNodes: [
        { nodeId: 39, name: 'Front Door Lock', deviceType: 'lock', model: 'Allegion BE469ZP' },
        { nodeId: 40, name: 'Laundry Room Keypad', deviceType: 'keypad', model: 'Ring 4AK1SZ' },
      ],
      lastTestResult: null,
      isLoading: false,
      isSaving: false,
      isTesting: false,
      error: null,
      saveSuccessMessage: null,
    });

    vi.spyOn(apiClient.settings, 'get').mockResolvedValue({
      settings: { ...DEFAULT_SETTINGS, zWaveTransportType: 'WebSocket' },
      transports: [],
    });

    vi.spyOn(apiClient.discovery, 'getSensors').mockResolvedValue(mockSensors);
    vi.spyOn(apiClient.discovery, 'sniff').mockResolvedValue({ detected: false });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders nothing when isOpen is false', () => {
    const { container } = render(
      <DoorSetupWizard isOpen={false} onClose={vi.fn()} onSave={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('Direct Z-Wave JS mode renders node cards and completely omits any MQTT topic inputs from the DOM', async () => {
    render(<DoorSetupWizard isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    // Node 39 lock card should be visible
    expect(screen.getByText(/Node 39: Front Door Lock/i)).toBeDefined();
    expect(screen.getByText(/Allegion BE469ZP/i)).toBeDefined();

    // Verify there are NO MQTT topic inputs for the lock in Direct Z-Wave mode
    expect(screen.queryByPlaceholderText(/zwave\/front_door/i)).toBeNull();
    expect(screen.queryByText(/Provider Type/i)).toBeNull();
  });

  it('Selecting integrated lock node card defaults to Built-In Lock Keypad', async () => {
    render(<DoorSetupWizard isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    // Built-In Keypad should be active by default with explicit explanatory note
    expect(
      screen.getByText(/Uses Node 39 hardware keypad\. PIN slots sync directly\./i)
    ).toBeDefined();

    // Clicking Node 39 card updates name if empty
    const nodeCard = screen.getByText(/Node 39: Front Door Lock/i);
    fireEvent.click(nodeCard);

    const nameInput = screen.getByPlaceholderText(/e\.g\. Front Door/i) as HTMLInputElement;
    expect(nameInput.value).toBe('Front Door Lock');
  });

  it('allows switching between Direct Z-Wave and MQTT Lock modes', async () => {
    render(<DoorSetupWizard isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    // Switch to MQTT Lock
    const mqttBtn = screen.getByRole('button', { name: /MQTT Lock/i });
    fireEvent.click(mqttBtn);

    // Now MQTT inputs should be present
    expect(screen.getByPlaceholderText(/zwave\/front_door/i)).toBeDefined();
    expect(screen.getByLabelText(/Provider Type/i)).toBeDefined();

    // Switch back to Direct Z-Wave
    const directBtn = screen.getByRole('button', { name: /Direct Z-Wave JS/i });
    fireEvent.click(directBtn);

    expect(screen.queryByPlaceholderText(/zwave\/front_door/i)).toBeNull();
  });

  it('MqttContact renders friendly sensor options and no raw button swarms', async () => {
    render(<DoorSetupWizard isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    await waitFor(() => {
      expect(apiClient.discovery.getSensors).toHaveBeenCalled();
    });

    // Check that dropdown contains formatted friendly name options
    const select = screen.getByLabelText(/Discovered Contact Sensor/i) as HTMLSelectElement;
    expect(select).toBeDefined();

    const options = Array.from(select.options).map((o) => o.text);
    expect(options).toContain(
      'Front Door Contact — Aqara MCCGQ11LM (Zigbee2MQTT) [Closed]'
    );
    expect(options).toContain(
      'Patio Door Sensor — Ring Contact Sensor v2 (Home Assistant) [Open]'
    );

    // Ensure raw button cloud is NOT rendered
    expect(
      screen.queryByRole('button', { name: 'zigbee2mqtt/front_door_contact' })
    ).toBeNull();
  });

  it('Clicking Listen for Activity triggers sniffing and selects the detected topic on event arrival', async () => {
    const snifferResult: SnifferResult = {
      detected: true,
      event: {
        topic: 'zigbee2mqtt/patio_door_contact',
        deviceName: 'Patio Door Sensor',
        model: 'Aqara MCCGQ11LM',
        state: 'OPEN',
        timestamp: '2026-09-07T12:05:00Z',
      },
    };

    const sniffSpy = vi.spyOn(apiClient.discovery, 'sniff').mockResolvedValue(snifferResult);

    render(<DoorSetupWizard isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    const listenBtn = screen.getByRole('button', { name: /⚡ Listen for Activity/i });
    fireEvent.click(listenBtn);

    await waitFor(() => {
      expect(sniffSpy).toHaveBeenCalled();
      expect(screen.getByText(/Detected Activity on Patio Door Sensor \(OPEN\)/i)).toBeDefined();
    });
  });

  it('Submits clean AccessPoint payload in Direct Z-Wave mode with Built-In Keypad', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(<DoorSetupWizard isOpen={true} onClose={onClose} onSave={onSave} />);

    // Enter door name
    const nameInput = screen.getByPlaceholderText(/e\.g\. Front Door/i);
    fireEvent.change(nameInput, { target: { value: 'Main Front Entry' } });

    // Submit form
    const submitBtn = screen.getByRole('button', { name: /Create Access Point/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Main Front Entry',
          lockProviderType: 'ZWaveWebSocket',
          lockConfigJson: JSON.stringify({ topic: 'node_39', nodeId: 39 }),
          keypadProviderType: 'BuiltInKeypad',
          keypadConfigJson: JSON.stringify({ nodeId: 39, type: 'built_in' }),
          autoLockEnabled: true,
          autoLockDaySeconds: 300,
          autoLockNightSeconds: 60,
          retryOnFailure: true,
        })
      );
      expect(onClose).toHaveBeenCalled();
    });
  });

  it('Pre-populates existing door with Z-Wave node ID from config JSON and saves update', async () => {
    const initialDoor = {
      id: 'door-123',
      name: 'Garage Side Door',
      lockProviderType: 'ZWaveWebSocket',
      lockConfigJson: JSON.stringify({ topic: 'node_39', nodeId: 39 }),
      keypadProviderType: 'ZWaveKeypad',
      keypadConfigJson: JSON.stringify({ topic: 'node_40', nodeId: 40 }),
      doorSensorProviderType: 'GenericMqttContact',
      doorSensorConfigJson: JSON.stringify({ topic: 'zigbee2mqtt/garage_contact' }),
      autoLockEnabled: true,
      autoLockDaySeconds: 180,
      autoLockNightSeconds: 45,
      retryOnFailure: true,
    };

    const onSave = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(
      <DoorSetupWizard
        isOpen={true}
        initialData={initialDoor}
        onClose={onClose}
        onSave={onSave}
      />
    );

    expect(screen.getByText(/Configure Access Point/i)).toBeDefined();
    const nameInput = screen.getByPlaceholderText(/e\.g\. Front Door/i) as HTMLInputElement;
    expect(nameInput.value).toBe('Garage Side Door');

    const updateBtn = screen.getByRole('button', { name: /Update Access Point/i });
    fireEvent.click(updateBtn);

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'door-123',
          name: 'Garage Side Door',
          lockProviderType: 'ZWaveWebSocket',
          autoLockDaySeconds: 180,
          autoLockNightSeconds: 45,
        })
      );
      expect(onClose).toHaveBeenCalled();
    });
  });
});
