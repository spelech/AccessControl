import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import { LiveEventFeed } from './LiveEventFeed';
import { useAuditStore } from '../../stores/useAuditStore';
import type { AccessLog } from '../../types';

describe('LiveEventFeed component', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders access events feed table', async () => {
    const mockEvents: AccessLog[] = [
      {
        id: 'ev-1',
        accessPointId: 'door-1',
        accessPointName: 'Front Door',
        userName: 'Steve',
        eventType: 'Unlocked',
        method: 'RingKeypad',
        timestamp: new Date().toISOString(),
        details: 'Valid PIN 4821 entered',
      },
    ];

    useAuditStore.setState({ logs: mockEvents });

    render(<LiveEventFeed />);

    expect(screen.getByText(/Access & Security Audit Trail/i)).toBeDefined();
    expect(screen.getByText('Front Door')).toBeDefined();
    expect(screen.getByText('Steve')).toBeDefined();
    expect(screen.getByText('RingKeypad')).toBeDefined();
    expect(screen.getByText('Valid PIN 4821 entered')).toBeDefined();
  });
});
