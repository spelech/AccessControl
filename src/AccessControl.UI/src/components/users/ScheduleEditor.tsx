import React from 'react';
import { Calendar, Clock, RotateCcw, Sparkles } from 'lucide-react';
import type { AccessPolicy, ScheduleType } from '../../types';

interface ScheduleEditorProps {
  policy: Partial<AccessPolicy>;
  onChange: (updated: Partial<AccessPolicy>) => void;
}

const DAYS = [
  { bit: 1, label: 'M', full: 'Monday' },
  { bit: 2, label: 'T', full: 'Tuesday' },
  { bit: 4, label: 'W', full: 'Wednesday' },
  { bit: 8, label: 'Th', full: 'Thursday' },
  { bit: 16, label: 'F', full: 'Friday' },
  { bit: 32, label: 'Sa', full: 'Saturday' },
  { bit: 64, label: 'Su', full: 'Sunday' },
];

export const ScheduleEditor: React.FC<ScheduleEditorProps> = ({ policy, onChange }) => {
  const scheduleType: ScheduleType = policy.scheduleType || 'Always';
  const daysOfWeek = policy.daysOfWeek ?? 127;
  const isOneTime = scheduleType === 'OneTime';

  const handleTypeChange = (type: ScheduleType) => {
    onChange({
      ...policy,
      scheduleType: type,
      remainingUses: type === 'OneTime' ? policy.remainingUses || 1 : null,
    });
  };

  const toggleDay = (bit: number) => {
    const nextDays = (daysOfWeek & bit) !== 0 ? daysOfWeek & ~bit : daysOfWeek | bit;
    onChange({ ...policy, daysOfWeek: nextDays });
  };

  const selectPreset = (preset: 'all' | 'weekdays' | 'weekends') => {
    let mask = 127;
    if (preset === 'weekdays') mask = 1 | 2 | 4 | 8 | 16; // 31
    if (preset === 'weekends') mask = 32 | 64;           // 96
    onChange({ ...policy, daysOfWeek: mask });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* Schedule Type Segmented Control */}
      <div>
        <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 600, marginBottom: '0.5rem' }}>
          Access Schedule Type
        </label>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))',
            gap: '0.4rem',
            backgroundColor: 'var(--surface-elevated)',
            padding: '0.3rem',
            borderRadius: 'var(--radius-md)',
          }}
        >
          {(['Always', 'WeeklyRecurring', 'DateRange', 'OneTime'] as ScheduleType[]).map((type) => {
            const isSelected = scheduleType === type;
            const labels: Record<ScheduleType, string> = {
              Always: 'Always 24/7',
              WeeklyRecurring: 'Recurring',
              DateRange: 'Date Range',
              OneTime: 'One-Time Pass',
            };

            return (
              <button
                key={type}
                type="button"
                className={isSelected ? 'btn-primary' : 'btn-outline'}
                onClick={() => handleTypeChange(type)}
                style={{
                  fontSize: '0.8rem',
                  padding: '0.4rem 0.6rem',
                  minHeight: '36px',
                  borderRadius: 'var(--radius-sm)',
                  border: isSelected ? 'none' : '1px solid transparent',
                }}
              >
                {labels[type]}
              </button>
            );
          })}
        </div>
      </div>

      {/* Weekly Recurring Days of the Week */}
      {scheduleType === 'WeeklyRecurring' && (
        <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.65rem' }}>
            <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>Active Days of the Week</span>
            <div style={{ display: 'flex', gap: '0.3rem' }}>
              <button
                type="button"
                className="btn-outline"
                onClick={() => selectPreset('all')}
                style={{ fontSize: '0.7rem', padding: '0.2rem 0.5rem', minHeight: '26px' }}
              >
                All
              </button>
              <button
                type="button"
                className="btn-outline"
                onClick={() => selectPreset('weekdays')}
                style={{ fontSize: '0.7rem', padding: '0.2rem 0.5rem', minHeight: '26px' }}
              >
                Weekdays
              </button>
              <button
                type="button"
                className="btn-outline"
                onClick={() => selectPreset('weekends')}
                style={{ fontSize: '0.7rem', padding: '0.2rem 0.5rem', minHeight: '26px' }}
              >
                Weekends
              </button>
            </div>
          </div>

          {/* Visual Day Pills */}
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            {DAYS.map((day) => {
              const active = (daysOfWeek & day.bit) !== 0;
              return (
                <button
                  key={day.bit}
                  type="button"
                  onClick={() => toggleDay(day.bit)}
                  aria-pressed={active}
                  title={day.full}
                  style={{
                    width: '40px',
                    height: '40px',
                    borderRadius: 'var(--radius-full)',
                    fontSize: '0.85rem',
                    fontWeight: 600,
                    padding: 0,
                    backgroundColor: active ? 'var(--accent-primary)' : 'var(--surface-elevated)',
                    color: active ? '#ffffff' : 'var(--text-secondary)',
                    border: active ? '1px solid var(--accent-primary)' : '1px solid var(--border-color)',
                  }}
                >
                  {day.label}
                </button>
              );
            })}
          </div>

          {/* Time Window Sliders / Inputs */}
          <div style={{ marginTop: '1rem', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
            <div>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.8rem', marginBottom: '0.3rem' }}>
                <Clock size={14} /> Start Time
              </label>
              <input
                type="time"
                value={policy.startTime || '08:00'}
                onChange={(e) => onChange({ ...policy, startTime: e.target.value })}
              />
            </div>

            <div>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.8rem', marginBottom: '0.3rem' }}>
                <Clock size={14} /> End Time
              </label>
              <input
                type="time"
                value={policy.endTime || '18:00'}
                onChange={(e) => onChange({ ...policy, endTime: e.target.value })}
              />
            </div>
          </div>
        </div>
      )}

      {/* Date Range Picker */}
      {scheduleType === 'DateRange' && (
        <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', marginBottom: '0.75rem' }}>
            <Calendar size={18} style={{ color: 'var(--accent-primary)' }} />
            <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>Temporary Access Window</span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.3rem' }}>Valid From</label>
              <input
                type="date"
                value={policy.validFrom ? policy.validFrom.split('T')[0] : ''}
                onChange={(e) => onChange({ ...policy, validFrom: `${e.target.value}T00:00:00Z` })}
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.3rem' }}>Valid Until</label>
              <input
                type="date"
                value={policy.validUntil ? policy.validUntil.split('T')[0] : ''}
                onChange={(e) => onChange({ ...policy, validUntil: `${e.target.value}T23:59:59Z` })}
              />
            </div>
          </div>
        </div>
      )}

      {/* One-Time Pass Toggle & Counter */}
      {isOneTime && (
        <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--surface-elevated)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
            <Sparkles size={18} style={{ color: 'var(--status-unlocked)' }} />
            <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>One-Time Use Guest PIN</span>
          </div>

          <p style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '0.75rem' }}>
            PIN code will immediately expire and be erased from hardware locks upon being entered.
          </p>

          <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
            <label style={{ fontSize: '0.85rem', fontWeight: 500 }}>Remaining Uses:</label>
            <div style={{ display: 'flex', gap: '0.4rem' }}>
              {[1, 2, 3, 5].map((uses) => (
                <button
                  key={uses}
                  type="button"
                  className={policy.remainingUses === uses ? 'btn-primary' : 'btn-outline'}
                  onClick={() => onChange({ ...policy, remainingUses: uses })}
                  style={{ minHeight: '34px', minWidth: '40px', padding: '0.2rem 0.6rem', fontSize: '0.85rem' }}
                >
                  {uses}
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Active Enable Switch */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.5rem 0' }}>
        <span style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          Policy Status: {policy.isEnabled !== false ? 'Active' : 'Disabled'}
        </span>
        <button
          type="button"
          className="btn-outline"
          onClick={() => onChange({ ...policy, isEnabled: policy.isEnabled === false })}
          style={{ fontSize: '0.8rem', padding: '0.3rem 0.75rem', minHeight: '32px' }}
        >
          <RotateCcw size={14} /> Toggle {policy.isEnabled !== false ? 'Off' : 'On'}
        </button>
      </div>
    </div>
  );
};
