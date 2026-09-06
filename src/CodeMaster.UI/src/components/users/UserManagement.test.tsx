import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { UserManagement } from './UserManagement';
import { useUserStore } from '../../stores/useUserStore';
import type { User } from '../../types';

describe('UserManagement component', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  const mockUsers: User[] = [
    { id: 'u-1', name: 'Steven Pelech', role: 'Admin', isActive: true },
    { id: 'u-2', name: 'Guest DogWalker', role: 'Guest', isActive: true },
  ];

  it('renders users list and action controls', () => {
    useUserStore.setState({ users: mockUsers });

    render(<UserManagement />);

    expect(screen.getByText('Access Credentials & Users')).toBeDefined();
    expect(screen.getByText('Steven Pelech')).toBeDefined();
    expect(screen.getByText('Guest DogWalker')).toBeDefined();
  });

  it('opens add user modal and submits creation', async () => {
    useUserStore.setState({ users: [] });
    const createSpy = vi.spyOn(useUserStore.getState(), 'createUser').mockResolvedValue({
      id: 'u-new',
      name: 'New Contractor',
      role: 'Service',
      isActive: true,
    });

    render(<UserManagement />);

    const addBtn = screen.getByRole('button', { name: /Add User \/ Guest/i });
    fireEvent.click(addBtn);

    const nameInput = screen.getByPlaceholderText(/e\.g\. John Doe/i);
    fireEvent.change(nameInput, { target: { value: 'New Contractor' } });

    const createSubmit = screen.getByRole('button', { name: 'Create User' });
    fireEvent.click(createSubmit);

    expect(createSpy).toHaveBeenCalledWith(expect.objectContaining({
      name: 'New Contractor',
    }));
  });

  it('displays error banner inside modal when submitting an invalid short PIN', async () => {
    useUserStore.setState({ users: mockUsers });
    const setPinSpy = vi.spyOn(useUserStore.getState(), 'setUserPin');

    render(<UserManagement />);

    // Open PIN modal
    const assignBtn = screen.getAllByRole('button', { name: /Assign PIN/i })[0];
    fireEvent.click(assignBtn);

    expect(screen.getByText('Set PIN & Schedule')).toBeDefined();

    // Enter invalid short PIN (2 digits)
    const pinInput = screen.getByPlaceholderText('••••');
    fireEvent.change(pinInput, { target: { value: '12' } });

    const submitBtn = screen.getByRole('button', { name: /Save & Sync Keypads/i });
    fireEvent.submit(submitBtn.closest('form')!);

    // Error banner should appear inside the modal
    expect(screen.getByRole('alert')).toBeDefined();
    expect(screen.getByText(/PIN must be between 4 and 8 numeric digits/i)).toBeDefined();

    // Should NOT call the API
    expect(setPinSpy).not.toHaveBeenCalled();
  });

  it('displays backend error message inside modal when API call fails', async () => {
    useUserStore.setState({ users: mockUsers });
    vi.spyOn(useUserStore.getState(), 'setUserPin').mockRejectedValue(
      new Error('API Error [400] Bad Request: {"error":"PIN must be between 4 and 8 numeric digits (0-9)."}')
    );

    render(<UserManagement />);

    const assignBtn = screen.getAllByRole('button', { name: /Assign PIN/i })[0];
    fireEvent.click(assignBtn);

    const pinInput = screen.getByPlaceholderText('••••');
    fireEvent.change(pinInput, { target: { value: '1234' } });

    const submitBtn = screen.getByRole('button', { name: /Save & Sync Keypads/i });
    fireEvent.submit(submitBtn.closest('form')!);

    // Modal stays open and displays the parsed backend error
    await screen.findByRole('alert');
    expect(screen.getByText('PIN must be between 4 and 8 numeric digits (0-9).')).toBeDefined();
    expect(screen.getByText('Set PIN & Schedule')).toBeDefined();
  });
});
