import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { DoorSetupWizard } from './DoorSetupWizard';
import { apiClient } from '../../api/apiClient';
import { useSettingsStore, DEFAULT_SETTINGS } from '../../stores/useSettingsStore';

describe('DoorSetupWizard component', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    useSettingsStore.setState({
      settings: { ...DEFAULT_SETTINGS },
      transports: [],
      detectedNodes: [],
      lastTestResult: null,
      isLoading: false,
      isSaving: false,
      isTesting: false,
      error: null,
      saveSuccessMessage: null,
    });
    vi.spyOn(apiClient.settings, 'get').mockResolvedValue({
      settings: { ...DEFAULT_SETTINGS },
      transports: [],
    });
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

  it('renders modal with default topics and submits door creation', async () => {
    vi.spyOn(apiClient.discovery, 'getTopics').mockResolvedValue([
      { topic: 'zwave/front_door', deviceType: 'lock', description: 'Front Lock' },
    ]);

    const onSave = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(
      <DoorSetupWizard isOpen={true} onClose={onClose} onSave={onSave} />
    );

    expect(screen.getByText(/1-Click Door Setup Wizard/i)).toBeDefined();

    // Enter name
    const nameInput = screen.getByPlaceholderText(/e\.g\. Front Door/i);
    fireEvent.change(nameInput, { target: { value: 'Back Patio Door' } });

    // Click submit
    const submitBtn = screen.getByRole('button', { name: /Create Access Point/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
        name: 'Back Patio Door',
        lockProviderType: 'AugustZWave',
        autoLockEnabled: true,
      }));
      expect(onClose).toHaveBeenCalled();
    });
  });

  it('allows picking detected Z-Wave nodes in WebSocket mode and submits correctly', async () => {
    useSettingsStore.setState({
      settings: {
        ...DEFAULT_SETTINGS,
        zWaveTransportType: 'WebSocket',
      },
      detectedNodes: [
        { nodeId: 39, name: 'Front Door Lock', deviceType: 'lock', model: 'Allegion BE469ZP' },
        { nodeId: 40, name: 'Laundry Room Keypad', deviceType: 'keypad', model: 'Ring 4AK1SZ' },
      ],
    });

    vi.spyOn(apiClient.discovery, 'getTopics').mockResolvedValue([]);

    const onSave = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(
      <DoorSetupWizard isOpen={true} onClose={onClose} onSave={onSave} />
    );

    // Pick detected lock (Node 39)
    const lockBtn = screen.getByRole('button', { name: /Node 39: Front Door Lock/i });
    expect(lockBtn).toBeDefined();
    fireEvent.click(lockBtn);

    // Pick detected keypad (Node 40)
    const keypadBtn = screen.getByRole('button', { name: /Node 40: Laundry Room Keypad/i });
    expect(keypadBtn).toBeDefined();
    fireEvent.click(keypadBtn);

    // Verify name was auto-populated from the lock
    const nameInput = screen.getByPlaceholderText(/e\.g\. Front Door/i) as HTMLInputElement;
    expect(nameInput.value).toBe('Front Door Lock');

    // Submit
    const submitBtn = screen.getByRole('button', { name: /Create Access Point/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
        name: 'Front Door Lock',
        lockProviderType: 'ZWaveWebSocket',
        lockConfigJson: JSON.stringify({ topic: 'node_39', nodeId: 39 }),
        keypadProviderType: 'ZWaveKeypad',
        keypadConfigJson: JSON.stringify({ topic: 'node_40', nodeId: 40 }),
      }));
      expect(onClose).toHaveBeenCalled();
    });
  });

  it('pre-populates existing door with Z-Wave node ID from config JSON', async () => {
    useSettingsStore.setState({
      settings: {
        ...DEFAULT_SETTINGS,
        zWaveTransportType: 'WebSocket',
      },
    });

    const initialDoor = {
      id: 'door-123',
      name: 'Garage Side Door',
      lockProviderType: 'ZWaveWebSocket',
      lockConfigJson: JSON.stringify({ topic: 'node_39', nodeId: 39 }),
      keypadProviderType: 'ZWaveKeypad',
      keypadConfigJson: JSON.stringify({ topic: 'node_40', nodeId: 40 }),
      autoLockEnabled: true,
      autoLockDaySeconds: 180,
      autoLockNightSeconds: 45,
      retryOnFailure: true,
    };

    render(
      <DoorSetupWizard
        isOpen={true}
        initialData={initialDoor}
        onClose={vi.fn()}
        onSave={vi.fn()}
      />
    );

    expect(screen.getByText(/Configure Access Point/i)).toBeDefined();
    const nameInput = screen.getByPlaceholderText(/e\.g\. Front Door/i) as HTMLInputElement;
    expect(nameInput.value).toBe('Garage Side Door');

    const updateBtn = screen.getByRole('button', { name: /Update Access Point/i });
    expect(updateBtn).toBeDefined();
  });
});
