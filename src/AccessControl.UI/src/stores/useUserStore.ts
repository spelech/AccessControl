import { create } from 'zustand';
import { apiClient } from '../api/apiClient';
import type { User, Credential, AccessPolicy } from '../types';

interface UserState {
  users: User[];
  credentials: Record<string, Credential[]>;
  policies: Record<string, AccessPolicy[]>;
  selectedUserId: string | null;
  isLoading: boolean;
  error: string | null;

  fetchUsers: () => Promise<void>;
  selectUser: (id: string | null) => void;
  createUser: (user: Partial<User>) => Promise<User>;
  updateUser: (id: string, user: Partial<User>) => Promise<User>;
  deleteUser: (id: string) => Promise<void>;
  fetchCredentials: (userId: string) => Promise<Credential[]>;
  setUserPin: (userId: string, pin: string, label?: string) => Promise<Credential>;
  deleteCredential: (userId: string, credentialId: string) => Promise<void>;
  fetchPolicies: (userId: string) => Promise<AccessPolicy[]>;
  savePolicy: (userId: string, policy: Partial<AccessPolicy>) => Promise<AccessPolicy>;
}

export const useUserStore = create<UserState>((set, get) => ({
  users: [],
  credentials: {},
  policies: {},
  selectedUserId: null,
  isLoading: false,
  error: null,

  fetchUsers: async () => {
    set({ isLoading: true, error: null });
    try {
      const users = await apiClient.users.list();
      set({ users, isLoading: false });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to fetch users';
      set({ error: message, isLoading: false });
    }
  },

  selectUser: (id: string | null) => {
    set({ selectedUserId: id });
    if (id) {
      get().fetchCredentials(id);
      get().fetchPolicies(id);
    }
  },

  createUser: async (userData: Partial<User>) => {
    set({ isLoading: true, error: null });
    try {
      const created = await apiClient.users.create(userData);
      set((state) => ({
        users: [...state.users, created],
        isLoading: false,
      }));
      return created;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to create user';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  updateUser: async (id: string, userData: Partial<User>) => {
    set({ isLoading: true, error: null });
    try {
      const updated = await apiClient.users.update(id, userData);
      set((state) => ({
        users: state.users.map((u) => (u.id === id ? updated : u)),
        isLoading: false,
      }));
      return updated;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to update user';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  deleteUser: async (id: string) => {
    set({ isLoading: true, error: null });
    try {
      await apiClient.users.delete(id);
      set((state) => {
        const nextCreds = { ...state.credentials };
        delete nextCreds[id];
        const nextPolicies = { ...state.policies };
        delete nextPolicies[id];

        return {
          users: state.users.filter((u) => u.id !== id),
          selectedUserId: state.selectedUserId === id ? null : state.selectedUserId,
          credentials: nextCreds,
          policies: nextPolicies,
          isLoading: false,
        };
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to delete user';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  fetchCredentials: async (userId: string) => {
    try {
      const creds = await apiClient.users.getCredentials(userId);
      set((state) => ({
        credentials: { ...state.credentials, [userId]: creds },
      }));
      return creds;
    } catch {
      return [];
    }
  },

  setUserPin: async (userId: string, pin: string, label?: string) => {
    set({ isLoading: true, error: null });
    try {
      const cred = await apiClient.users.setPin(userId, pin, label);
      set((state) => {
        const existing = state.credentials[userId] || [];
        const filtered = existing.filter((c) => c.type !== 'PIN');
        return {
          credentials: {
            ...state.credentials,
            [userId]: [...filtered, cred],
          },
          isLoading: false,
        };
      });
      return cred;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to set PIN';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  deleteCredential: async (userId: string, credentialId: string) => {
    try {
      await apiClient.users.deleteCredential(userId, credentialId);
      set((state) => ({
        credentials: {
          ...state.credentials,
          [userId]: (state.credentials[userId] || []).filter((c) => c.id !== credentialId),
        },
      }));
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to delete credential';
      set({ error: message });
      throw err;
    }
  },

  fetchPolicies: async (userId: string) => {
    try {
      const pols = await apiClient.users.getPolicies(userId);
      set((state) => ({
        policies: { ...state.policies, [userId]: pols },
      }));
      return pols;
    } catch {
      return [];
    }
  },

  savePolicy: async (userId: string, policy: Partial<AccessPolicy>) => {
    set({ isLoading: true, error: null });
    try {
      const saved = await apiClient.users.savePolicy(userId, policy);
      set((state) => ({
        policies: {
          ...state.policies,
          [userId]: [
            ...(state.policies[userId] || []).filter((p) => p.id !== saved.id),
            saved,
          ],
        },
        isLoading: false,
      }));
      return saved;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save policy';
      set({ error: message, isLoading: false });
      throw err;
    }
  },
}));
