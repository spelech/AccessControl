import React, { useEffect } from 'react';
import {
  Server,
  Radio,
  Shield,
  Wifi,
  Cpu,
  Bell,
  Save,
  RefreshCw,
  CheckCircle2,
  AlertCircle,
  Layers,
} from 'lucide-react';
import { getBasePath } from '../../api/apiClient';
import { useSettingsStore } from '../../stores/useSettingsStore';

export const SettingsView: React.FC = () => {
  const basePath = getBasePath();
  const {
    settings,
    transports,
    lastTestResult,
    isSaving,
    isTesting,
    error,
    saveSuccessMessage,
    fetchSettings,
    updateSettings,
    saveSettings,
    testConnection,
  } = useSettingsStore();

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  const zWaveTransportInfo = transports.find(
    (t) => t.transportId === 'zwave_ws' || t.transportId.toLowerCase().includes('zwave')
  );
  const mqttTransportInfo = transports.find(
    (t) => t.transportId === 'mqtt_broker' || t.transportId.toLowerCase().includes('mqtt')
  );

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    await saveSettings();
  };

  const handleTestConnection = async () => {
    await testConnection();
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem', maxWidth: '850px' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '1rem' }}>
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>System & Connectivity Settings</h2>
          <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
            Review integration endpoints, Home Assistant Ingress bindings, and Model Context Protocol (MCP) configuration.
          </p>
        </div>

        <button
          type="button"
          className="btn-primary"
          onClick={handleSave}
          disabled={isSaving}
          style={{ minHeight: '38px' }}
        >
          {isSaving ? (
            <>
              <RefreshCw size={16} className="animate-spin" /> Saving...
            </>
          ) : (
            <>
              <Save size={16} /> Save & Apply
            </>
          )}
        </button>
      </div>

      {/* Global Feedback Banners */}
      {saveSuccessMessage && (
        <div
          role="status"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
            padding: '0.75rem 1rem',
            borderRadius: 'var(--radius-md)',
            backgroundColor: 'var(--status-locked-subtle)',
            color: 'var(--status-locked)',
            fontSize: '0.85rem',
            fontWeight: 500,
          }}
        >
          <CheckCircle2 size={18} style={{ flexShrink: 0 }} />
          <span>{saveSuccessMessage}</span>
        </div>
      )}

      {error && (
        <div
          role="alert"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '0.5rem',
            padding: '0.75rem 1rem',
            borderRadius: 'var(--radius-md)',
            backgroundColor: 'var(--status-jammed-subtle)',
            color: 'var(--status-jammed)',
            fontSize: '0.85rem',
            fontWeight: 500,
          }}
        >
          <AlertCircle size={18} style={{ flexShrink: 0 }} />
          <span>{error}</span>
        </div>
      )}

      {/* Z-Wave Driver & Hardware Transport */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1.1rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Cpu size={20} style={{ color: 'var(--accent-primary)' }} />
            <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Z-Wave Transport & Driver</h3>
          </div>

          {zWaveTransportInfo && (
            <span className={`badge ${zWaveTransportInfo.isConnected ? 'badge-locked' : 'badge-jammed'}`}>
              {zWaveTransportInfo.isConnected ? 'Connected' : zWaveTransportInfo.status}
            </span>
          )}
        </div>

        <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          Configure how AccessControl communicates with physical Z-Wave locks, keypads, and scene controllers.
        </p>

        {/* Segmented Radio Toggle */}
        <div>
          <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 600, marginBottom: '0.4rem' }}>
            Transport Protocol Mode
          </label>
          <div
            style={{
              display: 'inline-flex',
              padding: '0.25rem',
              backgroundColor: 'var(--surface-elevated)',
              borderRadius: 'var(--radius-md)',
              border: '1px solid var(--border-color)',
              gap: '0.25rem',
            }}
            role="radiogroup"
            aria-label="Z-Wave Transport Mode"
          >
            <button
              type="button"
              role="radio"
              aria-checked={settings.zWaveTransportType === 'WebSocket'}
              className={settings.zWaveTransportType === 'WebSocket' ? 'btn-primary' : 'btn-outline'}
              onClick={() => updateSettings({ zWaveTransportType: 'WebSocket' })}
              style={{
                fontSize: '0.85rem',
                padding: '0.4rem 0.9rem',
                border: 'none',
                minHeight: '34px',
              }}
            >
              WebSocket (Direct)
            </button>
            <button
              type="button"
              role="radio"
              aria-checked={settings.zWaveTransportType === 'Mqtt'}
              className={settings.zWaveTransportType === 'Mqtt' ? 'btn-primary' : 'btn-outline'}
              onClick={() => updateSettings({ zWaveTransportType: 'Mqtt' })}
              style={{
                fontSize: '0.85rem',
                padding: '0.4rem 0.9rem',
                border: 'none',
                minHeight: '34px',
              }}
            >
              MQTT (Mosquitto)
            </button>
          </div>
        </div>

        {/* WebSocket Configuration */}
        {settings.zWaveTransportType === 'WebSocket' ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <div>
              <label htmlFor="zwave-ws-url" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
                Z-Wave JS WebSocket Server URL
              </label>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <input
                  id="zwave-ws-url"
                  type="text"
                  value={settings.zWaveWebSocketUrl}
                  onChange={(e) => updateSettings({ zWaveWebSocketUrl: e.target.value })}
                  placeholder="ws://10.0.0.10:8106"
                  style={{ flex: 1 }}
                />
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={handleTestConnection}
                  disabled={isTesting}
                  style={{ whiteSpace: 'nowrap', minHeight: '40px' }}
                >
                  {isTesting ? (
                    <>
                      <RefreshCw size={15} className="animate-spin" /> Testing...
                    </>
                  ) : (
                    'Test Connection'
                  )}
                </button>
              </div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block', marginTop: '0.25rem' }}>
                Direct low-latency WebSocket connection to Z-Wave JS Server (e.g. ws://10.0.0.10:8106).
              </span>
            </div>

            {/* Test Connection Results Card */}
            {lastTestResult && (
              <div
                style={{
                  border: `1px solid ${lastTestResult.success ? 'var(--status-locked)' : 'var(--status-jammed)'}`,
                  backgroundColor: lastTestResult.success ? 'var(--status-locked-subtle)' : 'var(--status-jammed-subtle)',
                  borderRadius: 'var(--radius-md)',
                  padding: '0.85rem 1rem',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '0.6rem',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    {lastTestResult.success ? (
                      <CheckCircle2 size={18} style={{ color: 'var(--status-locked)' }} />
                    ) : (
                      <AlertCircle size={18} style={{ color: 'var(--status-jammed)' }} />
                    )}
                    <span style={{ fontWeight: 600, fontSize: '0.85rem' }}>
                      {lastTestResult.success ? 'WebSocket Connected Successfully' : 'Connection Failed'}
                    </span>
                  </div>

                  {lastTestResult.success && lastTestResult.latencyMs !== undefined && (
                    <span style={{ fontSize: '0.75rem', fontWeight: 500, color: 'var(--status-locked)' }}>
                      Latency: {lastTestResult.latencyMs}ms
                    </span>
                  )}
                </div>

                {lastTestResult.success && (
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '1rem', fontSize: '0.8rem', color: 'var(--text-primary)' }}>
                    {lastTestResult.serverVersion && <span><strong>Server:</strong> v{lastTestResult.serverVersion}</span>}
                    {lastTestResult.driverVersion && <span><strong>Driver:</strong> v{lastTestResult.driverVersion}</span>}
                    {lastTestResult.nodeCount !== undefined && <span><strong>Nodes:</strong> {lastTestResult.nodeCount}</span>}
                  </div>
                )}

                {/* Detected Hardware Nodes Table */}
                {lastTestResult.success && lastTestResult.detectedNodes && lastTestResult.detectedNodes.length > 0 && (
                  <div style={{ marginTop: '0.35rem' }}>
                    <span style={{ fontSize: '0.75rem', fontWeight: 600, display: 'block', marginBottom: '0.35rem' }}>
                      Discovered Access Devices:
                    </span>
                    <div style={{ overflowX: 'auto', backgroundColor: 'var(--surface)', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-color)' }}>
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8rem', textAlign: 'left' }}>
                        <thead>
                          <tr style={{ borderBottom: '1px solid var(--border-color)', backgroundColor: 'var(--surface-elevated)', color: 'var(--text-secondary)' }}>
                            <th style={{ padding: '0.4rem 0.6rem' }}>Node</th>
                            <th style={{ padding: '0.4rem 0.6rem' }}>Device Name</th>
                            <th style={{ padding: '0.4rem 0.6rem' }}>Type</th>
                            <th style={{ padding: '0.4rem 0.6rem' }}>Hardware Model</th>
                          </tr>
                        </thead>
                        <tbody>
                          {lastTestResult.detectedNodes.map((node) => (
                            <tr key={node.nodeId} style={{ borderBottom: '1px solid var(--border-color)' }}>
                              <td style={{ padding: '0.4rem 0.6rem', fontWeight: 600 }}>Node {node.nodeId}</td>
                              <td style={{ padding: '0.4rem 0.6rem' }}>{node.name}</td>
                              <td style={{ padding: '0.4rem 0.6rem' }}>
                                <span className={`badge ${node.deviceType === 'lock' ? 'badge-locked' : 'badge-closed'}`}>
                                  {node.deviceType}
                                </span>
                              </td>
                              <td style={{ padding: '0.4rem 0.6rem', color: 'var(--text-secondary)' }}>{node.model}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {!lastTestResult.success && (
                  <span style={{ fontSize: '0.8rem', color: 'var(--status-jammed)' }}>
                    {lastTestResult.message}
                  </span>
                )}
              </div>
            )}
          </div>
        ) : (
          /* MQTT Prefix Configuration */
          <div>
            <label htmlFor="zwave-mqtt-prefix" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
              Z-Wave MQTT Topic Prefix
            </label>
            <input
              id="zwave-mqtt-prefix"
              type="text"
              value={settings.zWaveMqttPrefix}
              onChange={(e) => updateSettings({ zWaveMqttPrefix: e.target.value })}
              placeholder="zwave"
            />
            <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block', marginTop: '0.25rem' }}>
              MQTT topic prefix published by Z-Wave JS UI (e.g. &apos;zwave&apos;).
            </span>
          </div>
        )}
      </div>

      {/* Mosquitto MQTT Broker Configuration */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Wifi size={20} style={{ color: 'var(--accent-primary)' }} />
            <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Mosquitto MQTT Broker</h3>
          </div>

          {mqttTransportInfo && (
            <span className={`badge ${mqttTransportInfo.isConnected ? 'badge-locked' : 'badge-jammed'}`}>
              {mqttTransportInfo.isConnected ? 'Connected' : mqttTransportInfo.status}
            </span>
          )}
        </div>

        <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          Core pub/sub pipeline for Ring keypads, Zigbee contact sensors, and decoupled event dispatching.
        </p>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.75rem' }}>
          <div>
            <label htmlFor="mqtt-host" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
              Broker Host
            </label>
            <input
              id="mqtt-host"
              type="text"
              value={settings.mqttHost}
              onChange={(e) => updateSettings({ mqttHost: e.target.value })}
              placeholder="10.0.0.10"
            />
          </div>

          <div>
            <label htmlFor="mqtt-port" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
              Broker Port
            </label>
            <input
              id="mqtt-port"
              type="number"
              value={settings.mqttPort}
              onChange={(e) => updateSettings({ mqttPort: Number(e.target.value) || 8100 })}
              placeholder="8100"
            />
          </div>

          <div>
            <label htmlFor="mqtt-user" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
              Username (Optional)
            </label>
            <input
              id="mqtt-user"
              type="text"
              value={settings.mqttUsername}
              onChange={(e) => updateSettings({ mqttUsername: e.target.value })}
              placeholder="accesscontrol"
            />
          </div>

          <div>
            <label htmlFor="mqtt-password" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
              Password (Optional)
            </label>
            <input
              id="mqtt-password"
              type="password"
              value={settings.mqttPassword || ''}
              onChange={(e) => updateSettings({ mqttPassword: e.target.value })}
              placeholder="••••••••"
            />
          </div>
        </div>
      </div>

      {/* Apprise Notifications Card */}
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Bell size={20} style={{ color: 'var(--status-unlocked)' }} />
          <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Apprise Notification Dispatcher</h3>
        </div>

        <p style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
          Dispatches real-time alerts for manual unlocks, auto-lock failures, lock jams, and duress PIN triggers across 80+ notification services (Discord, Telegram, Pushover, Email).
        </p>

        <div>
          <label htmlFor="apprise-url" style={{ display: 'block', fontSize: '0.8rem', fontWeight: 500, marginBottom: '0.25rem' }}>
            Apprise Endpoint URL / Service Strings
          </label>
          <input
            id="apprise-url"
            type="text"
            value={settings.appriseUrl}
            onChange={(e) => updateSettings({ appriseUrl: e.target.value })}
            placeholder="e.g. discord://webhook_id/token, pfall://apikey"
          />
          <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block', marginTop: '0.25rem' }}>
            Multiple services can be comma-delimited or point to an Apprise microservice container.
          </span>
        </div>
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
          AccessControl provides native agent integration via SSE MCP endpoints at <code>/mcp/sse</code>. AI agents (like Claude or Gemini) can list access points, issue lock/unlock commands, query audit trails, and generate time-bounded guest PINs.
        </p>

        <div style={{ backgroundColor: 'var(--surface-elevated)', padding: '0.85rem', borderRadius: 'var(--radius-md)' }}>
          <span style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '0.35rem' }}>
            Registered Tools:
          </span>
          <ul style={{ paddingLeft: '1.25rem', fontSize: '0.8rem', color: 'var(--text-secondary)', display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
            <li><code>accesscontrol__list_doors</code> — List doors with real-time lock/contact telemetry</li>
            <li><code>accesscontrol__unlock_door</code> — Unlock door and start auto-lock timer</li>
            <li><code>accesscontrol__create_guest_pin</code> — Issue temporary 1-time or scheduled PIN</li>
            <li><code>accesscontrol__get_access_logs</code> — Query access control audit history</li>
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
          AccessControl never stores plain-text PINs on disk. All credential values are encrypted using AES-256-GCM (keyed from host machine secrets or DPAPI) and hashed via SHA-256 for rapid lookup.
        </p>
      </div>

      {/* Active Transports Summary */}
      {transports.length > 0 && (
        <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Layers size={20} style={{ color: 'var(--accent-primary)' }} />
            <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>Active Transport Subsystems</h3>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '0.75rem' }}>
            {transports.map((t) => (
              <div
                key={t.transportId}
                style={{
                  backgroundColor: 'var(--surface-elevated)',
                  padding: '0.75rem',
                  borderRadius: 'var(--radius-md)',
                  border: '1px solid var(--border-color)',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.25rem' }}>
                  <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>{t.displayName}</span>
                  <span className={`badge ${t.isConnected ? 'badge-locked' : 'badge-jammed'}`}>
                    {t.status}
                  </span>
                </div>
                {t.details && (
                  <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', display: 'block' }}>
                    {t.details}
                  </span>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
