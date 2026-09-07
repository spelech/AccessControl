import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { DoorCard } from './DoorCard';
import { useDoorStore } from '../../stores/useDoorStore';
import type { AccessPoint } from '../../types';

describe('DoorCard component', () => {
  const sampleDoor: AccessPoint = {
    id: 'door-front',
    name: 'Front Door',
    lockProviderType: 'AugustZWave',
    lockConfigJson: '{}',
    keypadProviderType: 'RingKeypad',
    autoLockEnabled: true,
    autoLockDaySeconds: 300,
    autoLockNightSeconds: 60,
    retryOnFailure: true,
    lockState: 'Locked',
    contactState: 'Closed',
    remainingCountdownSeconds: 0,
  };

  afterEach(() => {
    cleanup();
  });

  it('renders door name, provider types, and status badges', () => {
    render(<DoorCard door={sampleDoor} />);

    expect(screen.getByText('Front Door')).toBeDefined();
    expect(screen.getByText(/AugustZWave • RingKeypad/)).toBeDefined();
    expect(screen.getByText(/Locked/)).toBeDefined();
    expect(screen.getByText(/Door Closed/)).toBeDefined();
  });

  it('invokes unlockDoor when unlock button is clicked', async () => {
    const unlockSpy = vi.spyOn(useDoorStore.getState(), 'unlockDoor').mockResolvedValue(true);

    render(<DoorCard door={sampleDoor} />);

    const unlockBtn = screen.getByRole('button', { name: /^Unlock$/i });
    fireEvent.click(unlockBtn);

    expect(unlockSpy).toHaveBeenCalledWith('door-front');
  });

  it('invokes lockDoor when lock button is clicked', async () => {
    const lockSpy = vi.spyOn(useDoorStore.getState(), 'lockDoor').mockResolvedValue(true);

    render(<DoorCard door={{ ...sampleDoor, lockState: 'Unlocked' }} />);

    const lockBtn = screen.getByRole('button', { name: /^Lock$/i });
    fireEvent.click(lockBtn);

    expect(lockSpy).toHaveBeenCalledWith('door-front');
  });
});
