import { useState } from 'react';
import { UserPlus, Key, Trash2, X, Check, AlertCircle } from 'lucide-react';
import { useUserStore } from '../../stores/useUserStore';
import { ScheduleEditor } from './ScheduleEditor';
import type { User, UserRoleType, AccessPolicy } from '../../types';

export const UserManagement: React.FC = () => {
  const {
    users,
    credentials,
    createUser,
    deleteUser,
    setUserPin,
    deleteCredential,
    savePolicy,
  } = useUserStore();

  const [isAddUserOpen, setIsAddUserOpen] = useState(false);
  const [newUserName, setNewUserName] = useState('');
  const [newUserRole, setNewUserRole] = useState<UserRoleType>('Member');

  const [pinModalUser, setPinModalUser] = useState<User | null>(null);
  const [pinCode, setPinCode] = useState('');
  const [pinLabel, setPinLabel] = useState('Front Keypad PIN');
  const [pinModalError, setPinModalError] = useState<string | null>(null);
  const [policyData, setPolicyData] = useState<Partial<AccessPolicy>>({
    name: 'Standard Access',
    scheduleType: 'Always',
    daysOfWeek: 127,
    isEnabled: true,
  });

  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const openPinModal = (user: User) => {
    setPinModalUser(user);
    setPinModalError(null);
    setPinCode('');
  };

  const closePinModal = () => {
    setPinModalUser(null);
    setPinModalError(null);
    setPinCode('');
  };

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUserName.trim()) return;

    await createUser({
      name: newUserName.trim(),
      role: newUserRole,
      isActive: true,
    });

    setNewUserName('');
    setIsAddUserOpen(false);
    setStatusMessage('User created successfully');
    setTimeout(() => setStatusMessage(null), 2500);
  };

  const handleSavePinAndSchedule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!pinModalUser) return;

    const cleanPin = pinCode.trim();
    if (cleanPin.length < 4 || cleanPin.length > 8 || !/^\d+$/.test(cleanPin)) {
      setPinModalError('PIN must be between 4 and 8 numeric digits (0-9).');
      return;
    }

    try {
      await setUserPin(pinModalUser.id, cleanPin, pinLabel);
      await savePolicy(pinModalUser.id, policyData);

      setPinModalUser(null);
      setPinCode('');
      setPinModalError(null);
      setStatusMessage(`PIN saved and synced for ${pinModalUser.name}`);
      setTimeout(() => setStatusMessage(null), 3000);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save PIN';
      const match = msg.match(/"error"\s*:\s*"([^"]+)"/);
      setPinModalError(match ? match[1] : msg);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {/* Action Bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Access Credentials & Users</h2>
          <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
            Manage PIN codes, RFID credentials, and time-restricted access schedules.
          </p>
        </div>

        <button
          type="button"
          className="btn-primary"
          onClick={() => setIsAddUserOpen(true)}
          style={{ minHeight: '44px' }}
        >
          <UserPlus size={18} /> Add User / Guest
        </button>
      </div>

      {statusMessage && (
        <div style={{
          backgroundColor: 'var(--status-locked-subtle)',
          color: 'var(--status-locked)',
          padding: '0.6rem 0.9rem',
          borderRadius: 'var(--radius-md)',
          fontSize: '0.85rem',
          display: 'flex',
          alignItems: 'center',
          gap: '0.5rem',
        }}>
          <Check size={16} /> {statusMessage}
        </div>
      )}

      {/* Users Grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
        gap: '1rem',
      }}>
        {users.map((user) => {
          const userCreds = credentials[user.id] || [];
          const pinCred = userCreds.find((c) => c.type === 'PIN');

          return (
            <div key={user.id} className="card" style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <h3 style={{ fontSize: '1.1rem', fontWeight: 600 }}>{user.name}</h3>
                  <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    Role: <strong>{user.role}</strong>
                  </span>
                </div>
                <button
                  type="button"
                  className="btn-outline"
                  onClick={() => deleteUser(user.id)}
                  title="Delete User"
                  style={{ color: 'var(--status-jammed)', padding: '0.35rem', minHeight: '32px' }}
                  aria-label={`Delete ${user.name}`}
                >
                  <Trash2 size={16} />
                </button>
              </div>

              {/* Credential Status */}
              <div style={{
                backgroundColor: 'var(--surface-elevated)',
                padding: '0.65rem 0.85rem',
                borderRadius: 'var(--radius-md)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <Key size={16} style={{ color: pinCred ? 'var(--status-locked)' : 'var(--text-muted)' }} />
                  <span style={{ fontSize: '0.85rem' }}>
                    {pinCred ? `PIN Configured (${pinCred.pinLength} digits)` : 'No PIN Assigned'}
                  </span>
                </div>

                {pinCred && (
                  <button
                    type="button"
                    className="btn-outline"
                    onClick={() => deleteCredential(user.id, pinCred.id)}
                    style={{ fontSize: '0.7rem', padding: '0.2rem 0.4rem', minHeight: '26px' }}
                  >
                    Remove
                  </button>
                )}
              </div>

              {/* Action Buttons */}
              <div style={{ display: 'flex', gap: '0.5rem', marginTop: 'auto' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => openPinModal(user)}
                  style={{ flex: 1, minHeight: '40px', fontSize: '0.85rem' }}
                >
                  <Key size={16} /> {pinCred ? 'Change PIN / Schedule' : 'Assign PIN'}
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {/* Add User Modal */}
      {isAddUserOpen && (
        <div className="modal-backdrop" onClick={() => setIsAddUserOpen(false)} role="dialog" aria-modal="true">
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <h2 style={{ fontSize: '1.2rem', fontWeight: 600 }}>Create New User</h2>
              <button
                type="button"
                className="btn-outline"
                onClick={() => setIsAddUserOpen(false)}
                style={{ padding: '0.3rem', minHeight: '32px' }}
                aria-label="Close dialog"
              >
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleCreateUser} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '0.35rem' }}>Name</label>
                <input
                  type="text"
                  placeholder="e.g. John Doe, Dog Walker, Housekeeper"
                  value={newUserName}
                  onChange={(e) => setNewUserName(e.target.value)}
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '0.35rem' }}>Role</label>
                <select value={newUserRole} onChange={(e) => setNewUserRole(e.target.value as UserRoleType)}>
                  <option value="Member">Member (Family)</option>
                  <option value="Admin">Admin</option>
                  <option value="Guest">Guest</option>
                  <option value="Service">Service / Contractor</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
                <button type="button" className="btn-secondary" onClick={() => setIsAddUserOpen(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn-primary">
                  Create User
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* PIN & Schedule Modal */}
      {pinModalUser && (
        <div className="modal-backdrop" onClick={closePinModal} role="dialog" aria-modal="true">
          <div className="modal-content" onClick={(e) => e.stopPropagation()} style={{ maxWidth: '580px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
              <div>
                <h2 style={{ fontSize: '1.2rem', fontWeight: 600 }}>Set PIN & Schedule</h2>
                <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                  Assigning access code for {pinModalUser.name}
                </span>
              </div>
              <button
                type="button"
                className="btn-outline"
                onClick={closePinModal}
                style={{ padding: '0.3rem', minHeight: '32px' }}
                aria-label="Close dialog"
              >
                <X size={18} />
              </button>
            </div>

            {pinModalError && (
              <div
                style={{
                  backgroundColor: 'var(--status-jammed-subtle)',
                  color: 'var(--status-jammed)',
                  padding: '0.65rem 0.85rem',
                  borderRadius: 'var(--radius-md)',
                  fontSize: '0.85rem',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.5rem',
                  border: '1px solid var(--status-jammed)',
                  marginBottom: '1rem'
                }}
                role="alert"
              >
                <AlertCircle size={18} style={{ flexShrink: 0 }} />
                <span style={{ fontWeight: 500 }}>{pinModalError}</span>
              </div>
            )}

            <form onSubmit={handleSavePinAndSchedule} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '0.35rem' }}>
                    PIN Code (4-8 digits) *
                  </label>
                  <input
                    type="password"
                    pattern="[0-9]{4,8}"
                    maxLength={8}
                    placeholder="••••"
                    value={pinCode}
                    onChange={(e) => {
                      setPinCode(e.target.value.replace(/\D/g, ''));
                      if (pinModalError) setPinModalError(null);
                    }}
                    style={{
                      borderColor: pinModalError ? 'var(--status-jammed)' : undefined,
                    }}
                    required
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '0.35rem' }}>
                    Credential Label
                  </label>
                  <input
                    type="text"
                    value={pinLabel}
                    onChange={(e) => setPinLabel(e.target.value)}
                  />
                </div>
              </div>

              {/* Schedule Editor Integration */}
              <ScheduleEditor policy={policyData} onChange={setPolicyData} />

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
                <button type="button" className="btn-secondary" onClick={closePinModal}>
                  Cancel
                </button>
                <button type="submit" className="btn-primary">
                  Save & Sync Keypads
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
