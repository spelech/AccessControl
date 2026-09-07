import { create } from 'zustand';
import { apiClient } from '../api/apiClient';
import type { AccessPoint, LockStateType, DoorContactStateType } from '../types';

interface DoorState {
  doors: AccessPoint[];
  selectedDoorId: string | null;
  autoLockCountdowns: Record<string, number>;
  isOperating: Record<string, boolean>;
  isLoading: boolean;
  error: string | null;

  fetchDoors: () => Promise<void>;
  selectDoor: (id: string | null) => void;
  unlockDoor: (id: string) => Promise<boolean>;
  lockDoor: (id: string) => Promise<boolean>;
  createDoor: (door: Partial<AccessPoint>) => Promise<AccessPoint>;
  updateDoor: (id: string, door: Partial<AccessPoint>) => Promise<AccessPoint>;
  deleteDoor: (id: string) => Promise<void>;
  updateDoorState: (id: string, lockState: LockStateType, contactState?: DoorContactStateType, countdown?: number) => void;
  tickCountdowns: () => void;
}

export const useDoorStore = create<DoorState>((set, get) => ({
  doors: [],
  selectedDoorId: null,
  autoLockCountdowns: {},
  isOperating: {},
  isLoading: false,
  error: null,

  fetchDoors: async () => {
    set({ isLoading: true, error: null });
    try {
      const doors = await apiClient.doors.list();
      const countdowns: Record<string, number> = {};
      doors.forEach((door) => {
        if (door.remainingCountdownSeconds !== undefined) {
          countdowns[door.id] = door.remainingCountdownSeconds;
        }
      });

      set({
        doors,
        autoLockCountdowns: { ...get().autoLockCountdowns, ...countdowns },
        isLoading: false,
      });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to fetch doors';
      set({ error: message, isLoading: false });
    }
  },

  selectDoor: (id: string | null) => {
    set({ selectedDoorId: id });
  },

  unlockDoor: async (id: string) => {
    set((state) => ({
      isOperating: { ...state.isOperating, [id]: true },
    }));

    try {
      await apiClient.doors.unlock(id);
      // Optimistic update
      set((state) => ({
        doors: state.doors.map((d) =>
          d.id === id
            ? { ...d, lockState: 'Unlocked' as LockStateType }
            : d
        ),
        isOperating: { ...state.isOperating, [id]: false },
        autoLockCountdowns: {
          ...state.autoLockCountdowns,
          [id]: state.doors.find((d) => d.id === id)?.autoLockDaySeconds || 300,
        },
      }));
      return true;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to unlock door';
      set((state) => ({
        error: message,
        isOperating: { ...state.isOperating, [id]: false },
      }));
      return false;
    }
  },

  lockDoor: async (id: string) => {
    set((state) => ({
      isOperating: { ...state.isOperating, [id]: true },
    }));

    try {
      await apiClient.doors.lock(id);
      // Optimistic update
      set((state) => ({
        doors: state.doors.map((d) =>
          d.id === id
            ? { ...d, lockState: 'Locked' as LockStateType }
            : d
        ),
        isOperating: { ...state.isOperating, [id]: false },
        autoLockCountdowns: {
          ...state.autoLockCountdowns,
          [id]: 0,
        },
      }));
      return true;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to lock door';
      set((state) => ({
        error: message,
        isOperating: { ...state.isOperating, [id]: false },
      }));
      return false;
    }
  },

  createDoor: async (doorData: Partial<AccessPoint>) => {
    set({ isLoading: true, error: null });
    try {
      const created = await apiClient.doors.create(doorData);
      set((state) => ({
        doors: [...state.doors, created],
        isLoading: false,
      }));
      return created;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to create door';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  updateDoor: async (id: string, doorData: Partial<AccessPoint>) => {
    set({ isLoading: true, error: null });
    try {
      const updated = await apiClient.doors.update(id, doorData);
      set((state) => ({
        doors: state.doors.map((d) => (d.id === id ? updated : d)),
        isLoading: false,
      }));
      return updated;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to update door';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  deleteDoor: async (id: string) => {
    set({ isLoading: true, error: null });
    try {
      await apiClient.doors.delete(id);
      set((state) => ({
        doors: state.doors.filter((d) => d.id !== id),
        selectedDoorId: state.selectedDoorId === id ? null : state.selectedDoorId,
        isLoading: false,
      }));
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to delete door';
      set({ error: message, isLoading: false });
      throw err;
    }
  },

  updateDoorState: (id: string, lockState: LockStateType, contactState?: DoorContactStateType, countdown?: number) => {
    set((state) => ({
      doors: state.doors.map((d) =>
        d.id === id
          ? {
              ...d,
              lockState,
              ...(contactState !== undefined ? { contactState } : {}),
            }
          : d
      ),
      autoLockCountdowns:
        countdown !== undefined
          ? { ...state.autoLockCountdowns, [id]: countdown }
          : state.autoLockCountdowns,
    }));
  },

  tickCountdowns: () => {
    set((state) => {
      const updated = { ...state.autoLockCountdowns };
      let changed = false;

      for (const [doorId, seconds] of Object.entries(updated)) {
        if (seconds > 0) {
          updated[doorId] = seconds - 1;
          changed = true;
        }
      }

      return changed ? { autoLockCountdowns: updated } : state;
    });
  },
}));
