import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { DoorSetupWizard } from './DoorSetupWizard';
import { apiClient } from '../../api/apiClient';

describe('DoorSetupWizard component', () => {
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
});
