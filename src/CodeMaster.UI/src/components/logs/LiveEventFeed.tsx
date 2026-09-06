import React, { useEffect } from 'react';
import { 
  ShieldCheck, 
  ShieldAlert, 
  Unlock, 
  AlertTriangle, 
  RefreshCw, 
  Radio, 
  User, 
  Smartphone,
  Timer
} from 'lucide-react';
import { useAuditStore } from '../../stores/useAuditStore';
import { useDoorStore } from '../../stores/useDoorStore';
import type { AccessEventType, AccessMethod } from '../../types';

export const LiveEventFeed: React.FC = () => {
  const { logs, filter, isLoading, isLive, fetchLogs, setFilter, toggleLive } = useAuditStore();
  const { doors } = useDoorStore();

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const getEventBadge = (eventType: AccessEventType) => {
    switch (eventType) {
      case 'Unlocked':
        return (
          <span className="badge badge-unlocked">
            <Unlock size={12} /> Unlocked
          </span>
        );
      case 'Locked':
        return (
          <span className="badge badge-locked">
            <ShieldCheck size={12} /> Locked
          </span>
        );
      case 'AutoLocked':
        return (
          <span className="badge badge-closed">
            <Timer size={12} /> Auto-Locked
          </span>
        );
      case 'Denied':
        return (
          <span className="badge badge-jammed">
            <ShieldAlert size={12} /> Denied
          </span>
        );
      case 'Jammed':
        return (
          <span className="badge badge-jammed">
            <AlertTriangle size={12} /> Jammed
          </span>
        );
      default:
        return <span className="badge">{eventType}</span>;
    }
  };

  const getMethodIcon = (method: AccessMethod) => {
    switch (method) {
      case 'RingKeypad':
      case 'BuiltInKeypad':
      case 'ZWaveKeypad':
        return <Radio size={15} style={{ color: 'var(--accent-primary)' }} />;
      case 'AutoLock':
        return <Timer size={15} style={{ color: 'var(--status-unlocked)' }} />;
      case 'RF':
      case 'Manual':
      default:
        return <Smartphone size={15} style={{ color: 'var(--text-secondary)' }} />;
    }
  };

  const formatTimestamp = (isoString: string) => {
    try {
      const date = new Date(isoString);
      return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }) + 
        ' ' + date.toLocaleDateString([], { month: 'short', day: 'numeric' });
    } catch {
      return isoString;
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* Header & Controls */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Access & Security Audit Trail</h2>
            {isLive && (
              <span style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.35rem',
                fontSize: '0.75rem',
                color: 'var(--status-locked)',
                fontWeight: 600
              }}>
                <span style={{
                  width: '8px',
                  height: '8px',
                  borderRadius: '50%',
                  backgroundColor: 'var(--status-locked)',
                  display: 'inline-block'
                }} />
                LIVE
              </span>
            )}
          </div>
          <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
            Immutable event history across physical keypads, smart locks, and automation rules.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <button
            type="button"
            className="btn-outline"
            onClick={() => toggleLive()}
            style={{ fontSize: '0.85rem', minHeight: '38px' }}
          >
            {isLive ? 'Pause Feed' : 'Resume Live'}
          </button>
          <button
            type="button"
            className="btn-secondary"
            onClick={() => fetchLogs()}
            disabled={isLoading}
            style={{ minHeight: '38px' }}
            aria-label="Refresh logs"
          >
            <RefreshCw size={16} className={isLoading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="card" style={{ padding: '0.85rem 1rem' }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.75rem' }}>
          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.25rem' }}>
              Filter by Door
            </label>
            <select
              value={filter.doorId || ''}
              onChange={(e) => setFilter({ doorId: e.target.value || undefined })}
            >
              <option value="">All Access Points</option>
              {doors.map((door) => (
                <option key={door.id} value={door.id}>
                  {door.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.25rem' }}>
              Filter by Event
            </label>
            <select
              value={filter.eventType || ''}
              onChange={(e) => setFilter({ eventType: e.target.value || undefined })}
            >
              <option value="">All Events</option>
              <option value="Unlocked">Unlocked</option>
              <option value="Locked">Locked</option>
              <option value="AutoLocked">Auto-Locked</option>
              <option value="Denied">Denied</option>
              <option value="Jammed">Jammed</option>
            </select>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.25rem' }}>
              Filter by Method
            </label>
            <select
              value={filter.method || ''}
              onChange={(e) => setFilter({ method: e.target.value || undefined })}
            >
              <option value="">All Methods</option>
              <option value="RingKeypad">Ring Keypad</option>
              <option value="BuiltInKeypad">Built-in Keypad</option>
              <option value="ZWaveKeypad">Z-Wave Keypad</option>
              <option value="AutoLock">Auto-Lock Engine</option>
              <option value="Manual">Manual / App</option>
            </select>
          </div>
        </div>
      </div>

      {/* Events Feed Table / List */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <div style={{ overflowX: 'auto', width: '100%' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.9rem' }}>
            <thead>
              <tr style={{ backgroundColor: 'var(--surface-elevated)', borderBottom: '1px solid var(--border-color)', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                <th style={{ padding: '0.75rem 1rem' }}>Timestamp</th>
                <th style={{ padding: '0.75rem 1rem' }}>Access Point</th>
                <th style={{ padding: '0.75rem 1rem' }}>User / Subject</th>
                <th style={{ padding: '0.75rem 1rem' }}>Method</th>
                <th style={{ padding: '0.75rem 1rem' }}>Status</th>
                <th style={{ padding: '0.75rem 1rem' }}>Details</th>
              </tr>
            </thead>
            <tbody>
              {logs.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: '2.5rem 1rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
                    No access events recorded yet.
                  </td>
                </tr>
              ) : (
                logs.map((log) => {
                  const door = doors.find((d) => d.id === log.accessPointId);
                  const doorDisplayName = log.accessPointName || door?.name || log.accessPointId;

                  return (
                    <tr
                      key={log.id}
                      style={{ borderBottom: '1px solid var(--border-color)', transition: 'background-color 0.1s' }}
                    >
                      <td style={{ padding: '0.75rem 1rem', whiteSpace: 'nowrap', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                        {formatTimestamp(log.timestamp)}
                      </td>
                      <td style={{ padding: '0.75rem 1rem', fontWeight: 500 }}>
                        {doorDisplayName}
                      </td>
                      <td style={{ padding: '0.75rem 1rem' }}>
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem' }}>
                          <User size={14} style={{ color: 'var(--text-secondary)' }} />
                          {log.userName || (log.userId ? `User ${log.userId.slice(0, 6)}` : 'System / Direct')}
                        </span>
                      </td>
                      <td style={{ padding: '0.75rem 1rem' }}>
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.85rem' }}>
                          {getMethodIcon(log.method)}
                          {log.method}
                        </span>
                      </td>
                      <td style={{ padding: '0.75rem 1rem' }}>
                        {getEventBadge(log.eventType)}
                      </td>
                      <td style={{ padding: '0.75rem 1rem', fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
                        {log.details || '—'}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
