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
});
