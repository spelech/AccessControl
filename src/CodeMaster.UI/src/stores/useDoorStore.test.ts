import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useDoorStore } from './useDoorStore';
import { apiClient } from '../api/apiClient';

describe('useDoorStore', () => {
  beforeEach(() => {
    useDoorStore.setState({
      doors: [],
      selectedDoorId: null,
      autoLockCountdowns: {},
      isOperating: {},
      isLoading: false,
      error: null,
    });
    vi.restoreAllMocks();
  });

  it('fetches doors and updates store state', async () => {
    const mockDoors = [
      {
        id: 'door-1',
        name: 'Front Door',
        lockProviderType: 'AugustZWave',
        lockConfigJson: '{}',
        autoLockEnabled: true,
        autoLockDaySeconds: 300,
        autoLockNightSeconds: 60,
        retryOnFailure: true,
        remainingCountdownSeconds: 120,
      },
    ];

    vi.spyOn(apiClient.doors, 'list').mockResolvedValue(mockDoors);

    await useDoorStore.getState().fetchDoors();

    const state = useDoorStore.getState();
    expect(state.doors).toEqual(mockDoors);
    expect(state.autoLockCountdowns['door-1']).toBe(120);
    expect(state.isLoading).toBe(false);
  });

  it('handles unlockDoor with optimistic state update and countdown start', async () => {
    useDoorStore.setState({
      doors: [
        {
          id: 'door-1',
          name: 'Front Door',
          lockProviderType: 'AugustZWave',
          lockConfigJson: '{}',
          lockState: 'Locked',
          autoLockEnabled: true,
          autoLockDaySeconds: 300,
          autoLockNightSeconds: 60,
          retryOnFailure: true,
        },
      ],
    });

    vi.spyOn(apiClient.doors, 'unlock').mockResolvedValue({ success: true });

    const result = await useDoorStore.getState().unlockDoor('door-1');

    expect(result).toBe(true);
    const state = useDoorStore.getState();
    expect(state.doors[0].lockState).toBe('Unlocked');
    expect(state.autoLockCountdowns['door-1']).toBe(300);
    expect(state.isOperating['door-1']).toBe(false);
  });

  it('handles lockDoor with optimistic state update and countdown reset', async () => {
    useDoorStore.setState({
      doors: [
        {
          id: 'door-1',
          name: 'Front Door',
          lockProviderType: 'AugustZWave',
          lockConfigJson: '{}',
          lockState: 'Unlocked',
          autoLockEnabled: true,
          autoLockDaySeconds: 300,
          autoLockNightSeconds: 60,
          retryOnFailure: true,
        },
      ],
      autoLockCountdowns: { 'door-1': 150 },
    });

    vi.spyOn(apiClient.doors, 'lock').mockResolvedValue({ success: true });

    const result = await useDoorStore.getState().lockDoor('door-1');

    expect(result).toBe(true);
    const state = useDoorStore.getState();
    expect(state.doors[0].lockState).toBe('Locked');
    expect(state.autoLockCountdowns['door-1']).toBe(0);
    expect(state.isOperating['door-1']).toBe(false);
  });

  it('ticks countdowns accurately each second', () => {
    useDoorStore.setState({
      autoLockCountdowns: {
        'door-1': 10,
        'door-2': 1,
        'door-3': 0,
      },
    });

    useDoorStore.getState().tickCountdowns();

    const state = useDoorStore.getState();
    expect(state.autoLockCountdowns['door-1']).toBe(9);
    expect(state.autoLockCountdowns['door-2']).toBe(0);
    expect(state.autoLockCountdowns['door-3']).toBe(0);
  });

  it('selects and deletes doors properly', async () => {
    useDoorStore.setState({
      doors: [
        {
          id: 'door-1',
          name: 'Door 1',
          lockProviderType: 'AugustZWave',
          lockConfigJson: '{}',
          autoLockEnabled: true,
          autoLockDaySeconds: 300,
          autoLockNightSeconds: 60,
          retryOnFailure: true,
        },
      ],
      selectedDoorId: 'door-1',
    });

    vi.spyOn(apiClient.doors, 'delete').mockResolvedValue();

    await useDoorStore.getState().deleteDoor('door-1');

    const state = useDoorStore.getState();
    expect(state.doors).toHaveLength(0);
    expect(state.selectedDoorId).toBeNull();
  });
});
