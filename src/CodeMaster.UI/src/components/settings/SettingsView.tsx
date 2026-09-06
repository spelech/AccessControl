import { Server, Radio, Shield } from 'lucide-react';
import { getBasePath } from '../../api/apiClient';

export const SettingsView: React.FC = () => {
  const basePath = getBasePath();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem', maxWidth: '800px' }}>
      <div>
        <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>System & Connectivity Settings</h2>
        <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          Review integration endpoints, Home Assistant Ingress bindings, and Model Context Protocol (MCP) configuration.
        </p>
      </div>

      {/* Integration Status Cards */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Server size={20} style={{ color: 'var(--accent-primary)' }} />
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Ingress & Routing Context</h3>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem' }}>
          <div style={{ backgroundColor: 'var(--surface-elevated)', padding: '0.75rem', borderRadius: 'var(--radius-md)' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block' }}>Base Path</span>
            <code style={{ fontSize: '0.85rem', fontWeight: 600 }}>{basePath || '/ (Direct root)'}</code>
          </div>

          <div style={{ backgroundColor: 'var(--surface-elevated)', padding: '0.75rem', borderRadius: 'var(--radius-md)' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block' }}>Active Host</span>
            <code style={{ fontSize: '0.85rem', fontWeight: 600 }}>
              {typeof window !== 'undefined' ? window.location.host : 'localhost:8150'}
            </code>
          </div>

          <div style={{ backgroundColor: 'var(--surface-elevated)', padding: '0.75rem', borderRadius: 'var(--radius-md)' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block' }}>Port Mapping</span>
            <code style={{ fontSize: '0.85rem', fontWeight: 600 }}>8150 (Pipeline 810x SmartHome)</code>
          </div>
        </div>
      </div>

      {/* Model Context Protocol (MCP) Server Integration */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Radio size={20} style={{ color: 'var(--status-locked)' }} />
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Model Context Protocol (MCP) Gateway</h3>
        </div>

        <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          CodeMaster provides native agent integration via SSE MCP endpoints at <code>/mcp/sse</code>. AI agents (like Claude or Gemini) can list access points, issue lock/unlock commands, query audit trails, and generate time-bounded guest PINs.
        </p>

        <div style={{ backgroundColor: 'var(--surface-elevated)', padding: '0.85rem', borderRadius: 'var(--radius-md)' }}>
          <span style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '0.35rem' }}>
            Registered Tools:
          </span>
          <ul style={{ paddingLeft: '1.25rem', fontSize: '0.8rem', color: 'var(--text-secondary)', display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
            <li><code>codemaster__list_doors</code> — List doors with real-time lock/contact telemetry</li>
            <li><code>codemaster__unlock_door</code> — Unlock door and start auto-lock timer</li>
            <li><code>codemaster__create_guest_pin</code> — Issue temporary 1-time or scheduled PIN</li>
            <li><code>codemaster__get_access_logs</code> — Query access control audit history</li>
          </ul>
        </div>
      </div>

      {/* Security Best Practices */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Shield size={20} style={{ color: 'var(--status-unlocked)' }} />
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Hardware Security Architecture</h3>
        </div>

        <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          CodeMaster never stores plain-text PINs on disk. All credential values are encrypted using AES-256-GCM (keyed from host machine secrets or DPAPI) and hashed via SHA-256 for rapid lookup.
        </p>
      </div>
    </div>
  );
};
