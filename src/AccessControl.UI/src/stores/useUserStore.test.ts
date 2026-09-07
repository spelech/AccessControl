import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useUserStore } from './useUserStore';
import { apiClient } from '../api/apiClient';
import type { User, Credential, AccessPolicy } from '../types';

describe('useUserStore', () => {
  beforeEach(() => {
    useUserStore.setState({
      users: [],
      credentials: {},
      policies: {},
      selectedUserId: null,
      isLoading: false,
      error: null,
    });
    vi.restoreAllMocks();
  });

  it('fetches users list into store', async () => {
    const mockUsers: User[] = [
      { id: 'user-1', name: 'Alice', role: 'Admin', isActive: true },
      { id: 'user-2', name: 'Bob', role: 'Member', isActive: true },
    ];

    vi.spyOn(apiClient.users, 'list').mockResolvedValue(mockUsers);

    await useUserStore.getState().fetchUsers();

    const state = useUserStore.getState();
    expect(state.users).toEqual(mockUsers);
    expect(state.isLoading).toBe(false);
  });

  it('sets user PIN and updates credentials map', async () => {
    const mockCred: Credential = {
      id: 'cred-1',
      userId: 'user-1',
      type: 'PIN',
      pinLength: 4,
      label: 'Main PIN',
    };

    vi.spyOn(apiClient.users, 'setPin').mockResolvedValue(mockCred);

    const res = await useUserStore.getState().setUserPin('user-1', '1234', 'Main PIN');

    expect(res).toEqual(mockCred);
    const state = useUserStore.getState();
    expect(state.credentials['user-1']).toContainEqual(mockCred);
  });

  it('saves access policy for user', async () => {
    const mockPolicy: AccessPolicy = {
      id: 'policy-1',
      name: 'Weekday Pass',
      scheduleType: 'WeeklyRecurring',
      daysOfWeek: 31,
      startTime: '08:00',
      endTime: '17:00',
      isEnabled: true,
    };

    vi.spyOn(apiClient.users, 'savePolicy').mockResolvedValue(mockPolicy);

    const res = await useUserStore.getState().savePolicy('user-1', mockPolicy);

    expect(res).toEqual(mockPolicy);
    const state = useUserStore.getState();
    expect(state.policies['user-1']).toContainEqual(mockPolicy);
  });

  it('cleans up credentials and policies on deleteUser', async () => {
    useUserStore.setState({
      users: [{ id: 'user-1', name: 'Alice', role: 'Admin', isActive: true }],
      credentials: { 'user-1': [{ id: 'cred-1', userId: 'user-1', type: 'PIN', pinLength: 4 }] },
      policies: { 'user-1': [{ id: 'pol-1', name: 'Always', scheduleType: 'Always', daysOfWeek: 127, isEnabled: true }] },
      selectedUserId: 'user-1',
    });

    vi.spyOn(apiClient.users, 'delete').mockResolvedValue();

    await useUserStore.getState().deleteUser('user-1');

    const state = useUserStore.getState();
    expect(state.users).toHaveLength(0);
    expect(state.credentials['user-1']).toBeUndefined();
    expect(state.policies['user-1']).toBeUndefined();
    expect(state.selectedUserId).toBeNull();
  });
});
