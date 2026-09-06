import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { ScheduleEditor } from './ScheduleEditor';
import type { AccessPolicy } from '../../types';

describe('ScheduleEditor component', () => {
  afterEach(() => {
    cleanup();
  });
  it('renders all schedule type buttons', () => {
    const policy: Partial<AccessPolicy> = {
      scheduleType: 'Always',
      daysOfWeek: 127,
    };
    const onChange = vi.fn();

    render(<ScheduleEditor policy={policy} onChange={onChange} />);

    expect(screen.getByRole('button', { name: /Always 24\/7/i })).toBeDefined();
    expect(screen.getByRole('button', { name: /Recurring/i })).toBeDefined();
    expect(screen.getByRole('button', { name: /Date Range/i })).toBeDefined();
    expect(screen.getByRole('button', { name: /One-Time Pass/i })).toBeDefined();
  });

  it('switches to WeeklyRecurring and toggles day pills', () => {
    const policy: Partial<AccessPolicy> = {
      scheduleType: 'WeeklyRecurring',
      daysOfWeek: 31, // Mon-Fri
    };
    const onChange = vi.fn();

    render(<ScheduleEditor policy={policy} onChange={onChange} />);

    // Toggle Saturday (bit 32)
    const satBtn = screen.getByTitle('Saturday');
    fireEvent.click(satBtn);

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({
      daysOfWeek: 31 | 32, // 63
    }));
  });

  it('selects weekday preset', () => {
    const policy: Partial<AccessPolicy> = {
      scheduleType: 'WeeklyRecurring',
      daysOfWeek: 127,
    };
    const onChange = vi.fn();

    render(<ScheduleEditor policy={policy} onChange={onChange} />);

    const weekdaysBtn = screen.getByRole('button', { name: /Weekdays/i });
    fireEvent.click(weekdaysBtn);

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({
      daysOfWeek: 31,
    }));
  });

  it('renders one-time pass counter when OneTime is selected', () => {
    const policy: Partial<AccessPolicy> = {
      scheduleType: 'OneTime',
      remainingUses: 1,
    };
    const onChange = vi.fn();

    render(<ScheduleEditor policy={policy} onChange={onChange} />);

    expect(screen.getByText(/One-Time Use Guest PIN/i)).toBeDefined();
    const threeUsesBtn = screen.getByRole('button', { name: '3' });
    fireEvent.click(threeUsesBtn);

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({
      remainingUses: 3,
    }));
  });
});
