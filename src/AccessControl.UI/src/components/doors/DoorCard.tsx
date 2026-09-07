import React, { useState } from 'react';
import { 
  Lock, 
  Unlock, 
  AlertTriangle, 
  Clock, 
  DoorClosed, 
  DoorOpen, 
  Loader2, 
  Check, 
  Settings, 
  ShieldCheck,
  ShieldAlert
} from 'lucide-react';
import { useDoorStore } from '../../stores/useDoorStore';
import type { AccessPoint } from '../../types';

interface DoorCardProps {
  door: AccessPoint;
  onEdit?: (door: AccessPoint) => void;
}

export const DoorCard: React.FC<DoorCardProps> = ({ door, onEdit }) => {
  const { unlockDoor, lockDoor, isOperating, autoLockCountdowns } = useDoorStore();
  const [actionFeedback, setActionFeedback] = useState<'unlocked' | 'locked' | null>(null);

  const isCurrentOperating = isOperating[door.id] || false;
  const countdown = autoLockCountdowns[door.id] ?? door.remainingCountdownSeconds ?? 0;

  const handleUnlock = async () => {
    const success = await unlockDoor(door.id);
    if (success) {
      setActionFeedback('unlocked');
      setTimeout(() => setActionFeedback(null), 2500);
    }
  };

  const handleLock = async () => {
    const success = await lockDoor(door.id);
    if (success) {
      setActionFeedback('locked');
      setTimeout(() => setActionFeedback(null), 2500);
    }
  };

  const lockState = door.lockState || 'Locked';
  const contactState = door.contactState || 'Closed';

  const formatCountdown = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <div className="card door-card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
      {/* Header with Title and Edit */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h3 style={{ fontSize: '1.2rem', fontWeight: 600, color: 'var(--text-primary)' }}>
            {door.name}
          </h3>
          <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
            {door.lockProviderType} {door.keypadProviderType ? `• ${door.keypadProviderType}` : ''}
          </span>
        </div>
        {onEdit && (
          <button
            className="btn-outline"
            onClick={() => onEdit(door)}
            title="Configure Door"
            style={{ padding: '0.4rem', minHeight: '34px', borderRadius: 'var(--radius-md)' }}
            aria-label="Edit door configuration"
          >
            <Settings size={18} />
          </button>
        )}
      </div>

      {/* Badges Bar: Lock Status & Contact Sensor */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', alignItems: 'center' }}>
        {lockState === 'Locked' && (
          <span className="badge badge-locked">
            <ShieldCheck size={14} /> Locked
          </span>
        )}
        {lockState === 'Unlocked' && (
          <span className="badge badge-unlocked">
            <Unlock size={14} /> Unlocked
          </span>
        )}
        {lockState === 'Jammed' && (
          <span className="badge badge-jammed">
            <AlertTriangle size={14} /> Jammed
          </span>
        )}
        {lockState === 'Unknown' && (
          <span className="badge" style={{ backgroundColor: 'var(--surface-elevated)', color: 'var(--text-secondary)' }}>
            <ShieldAlert size={14} /> Unknown
          </span>
        )}

        {/* Contact Sensor */}
        {contactState === 'Closed' ? (
          <span className="badge badge-closed">
            <DoorClosed size={14} /> Door Closed
          </span>
        ) : (
          <span className="badge badge-open">
            <DoorOpen size={14} /> Door Open
          </span>
        )}

        {/* AutoLock Countdown Indicator */}
        {door.autoLockEnabled && lockState === 'Unlocked' && (
          <span 
            className="badge" 
            style={{ 
              backgroundColor: contactState === 'Open' ? 'var(--status-unlocked-subtle)' : 'var(--accent-subtle)',
              color: contactState === 'Open' ? 'var(--status-unlocked)' : 'var(--accent-primary)',
            }}
          >
            <Clock size={14} />
            {contactState === 'Open' 
              ? 'AutoLock Paused' 
              : countdown > 0 
                ? `AutoLock in ${formatCountdown(countdown)}` 
                : 'AutoLocking...'}
          </span>
        )}
      </div>

      {/* AutoLock Settings Summary */}
      {door.autoLockEnabled && (
        <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', display: 'flex', gap: '1rem' }}>
          <span>Day timer: {door.autoLockDaySeconds}s</span>
          <span>Night timer: {door.autoLockNightSeconds}s</span>
          {door.retryOnFailure && <span>Retries: Enabled</span>}
        </div>
      )}

      {/* Action Feedback Notification */}
      {actionFeedback && (
        <div 
          style={{ 
            fontSize: '0.85rem', 
            fontWeight: 500, 
            display: 'flex', 
            alignItems: 'center', 
            gap: '0.4rem',
            color: actionFeedback === 'locked' ? 'var(--status-locked)' : 'var(--status-unlocked)',
            backgroundColor: actionFeedback === 'locked' ? 'var(--status-locked-subtle)' : 'var(--status-unlocked-subtle)',
            padding: '0.4rem 0.75rem',
            borderRadius: 'var(--radius-md)'
          }}
        >
          <Check size={16} /> Successfully issued {actionFeedback} command!
        </div>
      )}

      {/* Primary Action Buttons */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginTop: 'auto' }}>
        <button
          type="button"
          className="btn-primary"
          onClick={handleUnlock}
          disabled={isCurrentOperating}
          style={{ minHeight: '44px' }}
        >
          {isCurrentOperating ? (
            <>
              <Loader2 className="animate-spin" size={18} /> Processing...
            </>
          ) : (
            <>
              <Unlock size={18} /> Unlock
            </>
          )}
        </button>

        <button
          type="button"
          className="btn-secondary"
          onClick={handleLock}
          disabled={isCurrentOperating}
          style={{ minHeight: '44px' }}
        >
          {isCurrentOperating ? (
            <>
              <Loader2 className="animate-spin" size={18} /> Processing...
            </>
          ) : (
            <>
              <Lock size={18} /> Lock
            </>
          )}
        </button>
      </div>
    </div>
  );
};
