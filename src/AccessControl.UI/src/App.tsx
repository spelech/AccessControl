import React, { useState, useEffect } from 'react';
import { 
  ShieldCheck, 
  DoorClosed, 
  Users, 
  FileText, 
  Settings as SettingsIcon, 
  Moon, 
  Sun, 
  Plus, 
  RefreshCw,
  Sparkles
} from 'lucide-react';
import { useDoorStore } from './stores/useDoorStore';
import { useUserStore } from './stores/useUserStore';
import { useAuditStore } from './stores/useAuditStore';
import { DoorCard } from './components/doors/DoorCard';
import { DoorSetupWizard } from './components/doors/DoorSetupWizard';
import { UserManagement } from './components/users/UserManagement';
import { LiveEventFeed } from './components/logs/LiveEventFeed';
import { SettingsView } from './components/settings/SettingsView';
import type { AccessPoint } from './types';

export const App: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'doors' | 'users' | 'logs' | 'settings'>('doors');
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    if (typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches) {
      return 'dark';
    }
    return 'light';
  });

  const [isWizardOpen, setIsWizardOpen] = useState(false);
  const [editingDoor, setEditingDoor] = useState<AccessPoint | null>(null);

  const { doors, isLoading: isDoorsLoading, fetchDoors, createDoor, updateDoor, tickCountdowns } = useDoorStore();
  const { fetchUsers } = useUserStore();
  const { fetchLogs } = useAuditStore();

  // Apply theme to document
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  const toggleTheme = () => {
    setTheme((prev) => (prev === 'light' ? 'dark' : 'light'));
  };

  // Initial data loading
  useEffect(() => {
    fetchDoors();
    fetchUsers();
    fetchLogs();
  }, [fetchDoors, fetchUsers, fetchLogs]);

  // Tick auto-lock countdown timer every second
  useEffect(() => {
    const timer = setInterval(() => {
      tickCountdowns();
    }, 1000);
    return () => clearInterval(timer);
  }, [tickCountdowns]);

  const handleSaveDoor = async (doorData: Partial<AccessPoint>) => {
    if (editingDoor) {
      await updateDoor(editingDoor.id, doorData);
    } else {
      await createDoor(doorData);
    }
  };

  const handleOpenEdit = (door: AccessPoint) => {
    setEditingDoor(door);
    setIsWizardOpen(true);
  };

  const handleOpenAdd = () => {
    setEditingDoor(null);
    setIsWizardOpen(true);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      {/* Top Navigation Header */}
      <header
        style={{
          backgroundColor: 'var(--surface)',
          borderBottom: '1px solid var(--border-color)',
          position: 'sticky',
          top: 0,
          zIndex: 100,
          boxShadow: 'var(--shadow-sm)',
        }}
      >
        <div
          className="container"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '0.75rem',
            paddingTop: '0.75rem',
            paddingBottom: '0.75rem',
          }}
        >
          {/* Logo & Branding */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
            <div
              style={{
                backgroundColor: 'var(--accent-primary)',
                color: '#ffffff',
                padding: '0.45rem',
                borderRadius: 'var(--radius-md)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <ShieldCheck size={22} />
            </div>
            <div>
              <h1 style={{ fontSize: '1.2rem', fontWeight: 700, letterSpacing: '-0.02em', lineHeight: 1.1 }}>
                AccessControl
              </h1>
              <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>
                Universal Access Control Engine
              </span>
            </div>
          </div>

          {/* Navigation Tabs */}
          <nav
            style={{
              display: 'flex',
              gap: '0.25rem',
              backgroundColor: 'var(--surface-elevated)',
              padding: '0.25rem',
              borderRadius: 'var(--radius-md)',
              overflowX: 'auto',
              maxWidth: '100%',
            }}
          >
            <button
              type="button"
              className={activeTab === 'doors' ? 'btn-primary' : 'btn-outline'}
              onClick={() => setActiveTab('doors')}
              style={{ fontSize: '0.85rem', padding: '0.4rem 0.85rem', border: 'none', minHeight: '38px' }}
            >
              <DoorClosed size={16} /> Doors
            </button>
            <button
              type="button"
              className={activeTab === 'users' ? 'btn-primary' : 'btn-outline'}
              onClick={() => setActiveTab('users')}
              style={{ fontSize: '0.85rem', padding: '0.4rem 0.85rem', border: 'none', minHeight: '38px' }}
            >
              <Users size={16} /> Users & PINs
            </button>
            <button
              type="button"
              className={activeTab === 'logs' ? 'btn-primary' : 'btn-outline'}
              onClick={() => setActiveTab('logs')}
              style={{ fontSize: '0.85rem', padding: '0.4rem 0.85rem', border: 'none', minHeight: '38px' }}
            >
              <FileText size={16} /> Activity Log
            </button>
            <button
              type="button"
              className={activeTab === 'settings' ? 'btn-primary' : 'btn-outline'}
              onClick={() => setActiveTab('settings')}
              style={{ fontSize: '0.85rem', padding: '0.4rem 0.85rem', border: 'none', minHeight: '38px' }}
            >
              <SettingsIcon size={16} /> Settings
            </button>
          </nav>

          {/* Right Header Actions */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            {activeTab === 'doors' && (
              <button
                type="button"
                className="btn-primary"
                onClick={handleOpenAdd}
                style={{ fontSize: '0.85rem', minHeight: '38px' }}
              >
                <Plus size={16} /> Add Door
              </button>
            )}

            <button
              type="button"
              className="btn-outline"
              onClick={toggleTheme}
              title={`Switch to ${theme === 'light' ? 'Dark' : 'Light'} Mode`}
              style={{ padding: '0.5rem', minHeight: '38px', minWidth: '38px' }}
              aria-label="Toggle theme"
            >
              {theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}
            </button>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="container" style={{ flex: 1, paddingBottom: '3rem' }}>
        {activeTab === 'doors' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem' }}>
              <div>
                <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Access Points</h2>
                <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
                  Monitor lock state, contact sensors, and auto-lock countdowns.
                </p>
              </div>

              <button
                type="button"
                className="btn-secondary"
                onClick={() => fetchDoors()}
                disabled={isDoorsLoading}
                style={{ minHeight: '38px' }}
                aria-label="Refresh doors"
              >
                <RefreshCw size={16} className={isDoorsLoading ? 'animate-spin' : ''} />
              </button>
            </div>

            {doors.length === 0 ? (
              <div
                className="card"
                style={{
                  textAlign: 'center',
                  padding: '3rem 1.5rem',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  gap: '1rem',
                }}
              >
                <DoorClosed size={48} style={{ color: 'var(--text-muted)' }} />
                <div>
                  <h3 style={{ fontSize: '1.15rem', fontWeight: 600, marginBottom: '0.35rem' }}>
                    No Access Points Configured
                  </h3>
                  <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', maxWidth: '420px' }}>
                    Get started by using the 1-click Door Setup Wizard to connect your smart locks, keypads, and contact sensors.
                  </p>
                </div>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={handleOpenAdd}
                  style={{ minHeight: '44px' }}
                >
                  <Sparkles size={18} /> Launch Door Setup Wizard
                </button>
              </div>
            ) : (
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))',
                  gap: '1.25rem',
                }}
              >
                {doors.map((door) => (
                  <DoorCard key={door.id} door={door} onEdit={handleOpenEdit} />
                ))}
              </div>
            )}
          </div>
        )}

        {activeTab === 'users' && <UserManagement />}
        {activeTab === 'logs' && <LiveEventFeed />}
        {activeTab === 'settings' && <SettingsView />}
      </main>

      {/* Door Setup Wizard Modal */}
      <DoorSetupWizard
        isOpen={isWizardOpen}
        initialData={editingDoor}
        onClose={() => setIsWizardOpen(false)}
        onSave={handleSaveDoor}
      />

      {/* Minimal Footer */}
      <footer
        style={{
          borderTop: '1px solid var(--border-color)',
          backgroundColor: 'var(--surface)',
          padding: '0.85rem 1.25rem',
          fontSize: '0.75rem',
          color: 'var(--text-secondary)',
          textAlign: 'center',
        }}
      >
        AccessControl v1.5.0 • Modern Access Control Engine • Home Assistant Ingress Ready
      </footer>
    </div>
  );
};
export default App;
