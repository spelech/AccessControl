import React, { useState, useEffect } from 'react';
import { X, Sparkles, Shield, KeyRound, DoorClosed, Timer } from 'lucide-react';
import { apiClient } from '../../api/apiClient';
import { useSettingsStore } from '../../stores/useSettingsStore';
import type { AccessPoint, DiscoveredTopic, DetectedZWaveNode } from '../../types';

interface DoorSetupWizardProps {
  isOpen: boolean;
  initialData?: AccessPoint | null;
  onClose: () => void;
  onSave: (doorData: Partial<AccessPoint>) => Promise<void>;
}

const DEFAULT_DETECTED_NODES: DetectedZWaveNode[] = [
  { nodeId: 39, name: 'Front Door Lock', deviceType: 'lock', model: 'Allegion BE469ZP' },
  { nodeId: 40, name: 'Laundry Room Keypad', deviceType: 'keypad', model: 'Ring 4AK1SZ' },
];

export const DoorSetupWizard: React.FC<DoorSetupWizardProps> = ({
  isOpen,
  initialData,
  onClose,
  onSave,
}) => {
  const { settings, detectedNodes, fetchSettings } = useSettingsStore();
  const isWebSocketMode = settings.zWaveTransportType === 'WebSocket';

  const [name, setName] = useState('');
  const [lockProviderType, setLockProviderType] = useState('AugustZWave');
  const [lockTopic, setLockTopic] = useState('');
  const [selectedLockNodeId, setSelectedLockNodeId] = useState<number | null>(null);

  const [keypadProviderType, setKeypadProviderType] = useState('RingKeypad');
  const [keypadTopic, setKeypadTopic] = useState('');
  const [selectedKeypadNodeId, setSelectedKeypadNodeId] = useState<number | null>(null);

  const [doorSensorProviderType, setDoorSensorProviderType] = useState('AqaraZigbee');
  const [doorSensorTopic, setDoorSensorTopic] = useState('');
  const [autoLockEnabled, setAutoLockEnabled] = useState(true);
  const [autoLockDaySeconds, setAutoLockDaySeconds] = useState(300);
  const [autoLockNightSeconds, setAutoLockNightSeconds] = useState(60);
  const [retryOnFailure, setRetryOnFailure] = useState(true);

  const [discoveredTopics, setDiscoveredTopics] = useState<DiscoveredTopic[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setLockProviderType(initialData.lockProviderType || 'AugustZWave');
      setKeypadProviderType(initialData.keypadProviderType || 'None');
      setDoorSensorProviderType(initialData.doorSensorProviderType || 'None');
      setAutoLockEnabled(initialData.autoLockEnabled);
      setAutoLockDaySeconds(initialData.autoLockDaySeconds);
      setAutoLockNightSeconds(initialData.autoLockNightSeconds);
      setRetryOnFailure(initialData.retryOnFailure);

      try {
        const lockCfg = JSON.parse(initialData.lockConfigJson || '{}');
        setLockTopic(lockCfg.topic || lockCfg.lockTopic || (lockCfg.nodeId ? `node_${lockCfg.nodeId}` : ''));
        setSelectedLockNodeId(lockCfg.nodeId ? Number(lockCfg.nodeId) : null);
      } catch {
        setLockTopic('');
        setSelectedLockNodeId(null);
      }

      try {
        const kpCfg = JSON.parse(initialData.keypadConfigJson || '{}');
        setKeypadTopic(kpCfg.topic || kpCfg.keypadTopic || (kpCfg.nodeId ? `node_${kpCfg.nodeId}` : ''));
        setSelectedKeypadNodeId(kpCfg.nodeId ? Number(kpCfg.nodeId) : null);
      } catch {
        setKeypadTopic('');
        setSelectedKeypadNodeId(null);
      }

      try {
        const sensorCfg = JSON.parse(initialData.doorSensorConfigJson || '{}');
        setDoorSensorTopic(sensorCfg.topic || sensorCfg.sensorTopic || '');
      } catch {
        setDoorSensorTopic('');
      }
    } else {
      setName('');
      setLockProviderType('AugustZWave');
      setLockTopic('zwave/front_door_lock');
      setSelectedLockNodeId(null);
      setKeypadProviderType('RingKeypad');
      setKeypadTopic('ring/keypad_entry');
      setSelectedKeypadNodeId(null);
      setDoorSensorProviderType('AqaraZigbee');
      setDoorSensorTopic('zigbee2mqtt/front_door_contact');
      setAutoLockEnabled(true);
      setAutoLockDaySeconds(300);
      setAutoLockNightSeconds(60);
      setRetryOnFailure(true);
    }
  }, [initialData, isOpen]);

  useEffect(() => {
    if (!isOpen) return;

    fetchSettings().catch(() => {});

    apiClient.discovery
      .getTopics()
      .then((topics) => {
        if (topics && topics.length > 0) {
          setDiscoveredTopics(topics);
        } else {
          // Fallback auto-discovery suggestions
          setDiscoveredTopics([
            { topic: 'zwave/august_smartlock_pro', deviceType: 'lock', description: 'August Smart Lock Pro (Z-Wave)' },
            { topic: 'zwave/schlage_be469zp', deviceType: 'lock', description: 'Schlage Connect Z-Wave Deadbolt' },
            { topic: 'ring/keypad_v2', deviceType: 'keypad', description: 'Ring Alarm Keypad v2' },
            { topic: 'zigbee2mqtt/entry_contact', deviceType: 'sensor', description: 'Aqara Contact Sensor MCCGQ11LM' },
          ]);
        }
      })
      .catch(() => {
        // Fallback discovery mocks
        setDiscoveredTopics([
          { topic: 'zwave/august_smartlock_pro', deviceType: 'lock', description: 'August Smart Lock Pro (Z-Wave)' },
          { topic: 'zwave/schlage_be469zp', deviceType: 'lock', description: 'Schlage Connect Z-Wave Deadbolt' },
          { topic: 'ring/keypad_v2', deviceType: 'keypad', description: 'Ring Alarm Keypad v2' },
          { topic: 'zigbee2mqtt/entry_contact', deviceType: 'sensor', description: 'Aqara Contact Sensor' },
        ]);
      });
  }, [isOpen, fetchSettings]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setValidationError('Door name is required');
      return;
    }

    setValidationError(null);
    setIsSaving(true);

    const lockConfig: Record<string, unknown> = { topic: lockTopic };
    if (selectedLockNodeId !== null) {
      lockConfig.nodeId = selectedLockNodeId;
    }

    const keypadConfig: Record<string, unknown> = { topic: keypadTopic };
    if (selectedKeypadNodeId !== null) {
      keypadConfig.nodeId = selectedKeypadNodeId;
    }

    try {
      await onSave({
        ...(initialData?.id ? { id: initialData.id } : {}),
        name: name.trim(),
        lockProviderType,
        lockConfigJson: JSON.stringify(lockConfig),
        keypadProviderType: keypadProviderType === 'None' ? null : keypadProviderType,
        keypadConfigJson: keypadProviderType === 'None' ? null : JSON.stringify(keypadConfig),
        doorSensorProviderType: doorSensorProviderType === 'None' ? null : doorSensorProviderType,
        doorSensorConfigJson: doorSensorProviderType === 'None' ? null : JSON.stringify({ topic: doorSensorTopic }),
        autoLockEnabled,
        autoLockDaySeconds: Number(autoLockDaySeconds),
        autoLockNightSeconds: Number(autoLockNightSeconds),
        retryOnFailure,
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save access point';
      setValidationError(message);
    } finally {
      setIsSaving(false);
    }
  };

  const lockDiscovered = discoveredTopics.filter((t) => t.deviceType === 'lock');
  const keypadDiscovered = discoveredTopics.filter((t) => t.deviceType === 'keypad');
  const sensorDiscovered = discoveredTopics.filter((t) => t.deviceType === 'sensor');

  const effectiveDetectedNodes = detectedNodes.length > 0 ? detectedNodes : DEFAULT_DETECTED_NODES;
  const detectedLockNodes = effectiveDetectedNodes.filter((n) => n.deviceType === 'lock');
  const detectedKeypadNodes = effectiveDetectedNodes.filter((n) => n.deviceType === 'keypad');

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
            <Sparkles size={22} style={{ color: 'var(--accent-primary)' }} />
            <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>
              {initialData ? 'Configure Access Point' : '1-Click Door Setup Wizard'}
            </h2>
          </div>
          <button 
            type="button" 
            className="btn-outline" 
            onClick={onClose} 
            style={{ padding: '0.3rem', minHeight: '32px' }}
            aria-label="Close dialog"
          >
            <X size={18} />
          </button>
        </div>

        {validationError && (
          <div style={{ 
            backgroundColor: 'var(--status-jammed-subtle)', 
            color: 'var(--status-jammed)', 
            padding: '0.6rem 0.8rem', 
            borderRadius: 'var(--radius-md)', 
            fontSize: '0.85rem',
            marginBottom: '1rem' 
          }}>
            {validationError}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.1rem' }}>
          {/* Door Name */}
          <div>
            <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 500, marginBottom: '0.35rem' }}>
              Door / Access Point Name *
            </label>
            <input
              type="text"
              placeholder="e.g. Front Door, Side Entry, Garage Door"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </div>

          {/* Section: Lock Provider */}
          <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--surface-elevated)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <Shield size={18} style={{ color: 'var(--status-locked)' }} />
              <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Lock Provider</span>
            </div>

            {/* Detected Z-Wave Locks Quick-Pick */}
            {isWebSocketMode && detectedLockNodes.length > 0 && (
              <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.6rem' }}>
                <span style={{ fontWeight: 600, display: 'block', marginBottom: '0.3rem' }}>
                  Detected Z-Wave Locks (WebSocket):
                </span>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.35rem' }}>
                  {detectedLockNodes.map((node) => (
                    <button
                      key={node.nodeId}
                      type="button"
                      className={selectedLockNodeId === node.nodeId ? 'btn-primary' : 'btn-outline'}
                      onClick={() => {
                        setSelectedLockNodeId(node.nodeId);
                        setLockProviderType('ZWaveWebSocket');
                        setLockTopic(`node_${node.nodeId}`);
                        if (!name) setName(node.name);
                      }}
                      style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem', minHeight: '26px' }}
                    >
                      Node {node.nodeId}: {node.name} ({node.model})
                    </button>
                  ))}
                </div>
              </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>Provider Type</label>
                <select value={lockProviderType} onChange={(e) => setLockProviderType(e.target.value)}>
                  {isWebSocketMode && <option value="ZWaveWebSocket">Z-Wave JS WebSocket (Direct)</option>}
                  <option value="AugustZWave">August Smart Lock (Z-Wave)</option>
                  <option value="SchlageZWave">Schlage Connect (Z-Wave)</option>
                  <option value="YaleZWave">Yale Assure (Z-Wave)</option>
                  <option value="SwitchBotMqtt">SwitchBot Lock (MQTT)</option>
                  <option value="GenericMqtt">Generic MQTT Lock</option>
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>
                  {isWebSocketMode ? 'Z-Wave Node / Topic' : 'MQTT Topic'}
                </label>
                <input
                  type="text"
                  placeholder={isWebSocketMode ? 'node_39' : 'zwave/front_door'}
                  value={lockTopic}
                  onChange={(e) => setLockTopic(e.target.value)}
                />
              </div>
            </div>

            {lockDiscovered.length > 0 && !isWebSocketMode && (
              <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                <span>Auto-detected: </span>
                {lockDiscovered.map((item) => (
                  <button
                    key={item.topic}
                    type="button"
                    className="btn-outline"
                    onClick={() => setLockTopic(item.topic)}
                    style={{ fontSize: '0.7rem', padding: '0.15rem 0.45rem', minHeight: '22px', margin: '0.2rem' }}
                  >
                    {item.topic}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Section: Keypad Provider */}
          <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--surface-elevated)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <KeyRound size={18} style={{ color: 'var(--accent-primary)' }} />
              <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Keypad Provider (Optional)</span>
            </div>

            {/* Detected Z-Wave Keypads Quick-Pick */}
            {isWebSocketMode && detectedKeypadNodes.length > 0 && (
              <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginBottom: '0.6rem' }}>
                <span style={{ fontWeight: 600, display: 'block', marginBottom: '0.3rem' }}>
                  Detected Z-Wave Keypads (WebSocket):
                </span>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.35rem' }}>
                  {detectedKeypadNodes.map((node) => (
                    <button
                      key={node.nodeId}
                      type="button"
                      className={selectedKeypadNodeId === node.nodeId ? 'btn-primary' : 'btn-outline'}
                      onClick={() => {
                        setSelectedKeypadNodeId(node.nodeId);
                        setKeypadProviderType('ZWaveKeypad');
                        setKeypadTopic(`node_${node.nodeId}`);
                      }}
                      style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem', minHeight: '26px' }}
                    >
                      Node {node.nodeId}: {node.name} ({node.model})
                    </button>
                  ))}
                </div>
              </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>Keypad Type</label>
                <select value={keypadProviderType} onChange={(e) => setKeypadProviderType(e.target.value)}>
                  <option value="None">None (App Only)</option>
                  <option value="RingKeypad">Ring Alarm Keypad v2</option>
                  <option value="ZWaveKeypad">Z-Wave Central Scene Keypad</option>
                  <option value="BuiltInKeypad">Lock Built-in Keypad</option>
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>
                  {isWebSocketMode ? 'Keypad Node / Topic' : 'Keypad Topic'}
                </label>
                <input
                  type="text"
                  placeholder={isWebSocketMode ? 'node_40' : 'ring/keypad_front'}
                  value={keypadTopic}
                  disabled={keypadProviderType === 'None'}
                  onChange={(e) => setKeypadTopic(e.target.value)}
                />
              </div>
            </div>

            {keypadDiscovered.length > 0 && keypadProviderType !== 'None' && !isWebSocketMode && (
              <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                <span>Auto-detected: </span>
                {keypadDiscovered.map((item) => (
                  <button
                    key={item.topic}
                    type="button"
                    className="btn-outline"
                    onClick={() => setKeypadTopic(item.topic)}
                    style={{ fontSize: '0.7rem', padding: '0.15rem 0.45rem', minHeight: '22px', margin: '0.2rem' }}
                  >
                    {item.topic}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Section: Door Contact Sensor */}
          <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)', backgroundColor: 'var(--surface-elevated)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <DoorClosed size={18} style={{ color: 'var(--status-closed)' }} />
              <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Contact Sensor (Optional)</span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>Sensor Type</label>
                <select value={doorSensorProviderType} onChange={(e) => setDoorSensorProviderType(e.target.value)}>
                  <option value="None">None</option>
                  <option value="AqaraZigbee">Aqara Zigbee Contact Sensor</option>
                  <option value="RingContactSensor">Ring Contact Sensor</option>
                  <option value="GenericMqttContact">Generic MQTT Contact</option>
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>Sensor Topic</label>
                <input
                  type="text"
                  placeholder="zigbee2mqtt/door_contact"
                  value={doorSensorTopic}
                  disabled={doorSensorProviderType === 'None'}
                  onChange={(e) => setDoorSensorTopic(e.target.value)}
                />
              </div>
            </div>

            {sensorDiscovered.length > 0 && doorSensorProviderType !== 'None' && (
              <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                <span>Auto-detected: </span>
                {sensorDiscovered.map((item) => (
                  <button
                    key={item.topic}
                    type="button"
                    className="btn-outline"
                    onClick={() => setDoorSensorTopic(item.topic)}
                    style={{ fontSize: '0.7rem', padding: '0.15rem 0.45rem', minHeight: '22px', margin: '0.2rem' }}
                  >
                    {item.topic}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Section: Auto-Lock Policy */}
          <div style={{ border: '1px solid var(--border-color)', padding: '1rem', borderRadius: 'var(--radius-md)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Timer size={18} style={{ color: 'var(--status-unlocked)' }} />
                <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Auto-Lock Engine</span>
              </div>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.85rem', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={autoLockEnabled}
                  onChange={(e) => setAutoLockEnabled(e.target.checked)}
                />
                Enabled
              </label>
            </div>

            {autoLockEnabled && (
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>
                    Day Timer (seconds)
                  </label>
                  <input
                    type="number"
                    min="10"
                    max="3600"
                    value={autoLockDaySeconds}
                    onChange={(e) => setAutoLockDaySeconds(Number(e.target.value))}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}>
                    Night Timer (seconds)
                  </label>
                  <input
                    type="number"
                    min="10"
                    max="3600"
                    value={autoLockNightSeconds}
                    onChange={(e) => setAutoLockNightSeconds(Number(e.target.value))}
                  />
                </div>

                <div style={{ gridColumn: '1 / -1', marginTop: '0.25rem' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.85rem', cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={retryOnFailure}
                      onChange={(e) => setRetryOnFailure(e.target.checked)}
                    />
                    Automatic retry if lock jams or motor fails
                  </label>
                </div>
              </div>
            )}
          </div>

          {/* Action Buttons */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
            <button type="button" className="btn-secondary" onClick={onClose} disabled={isSaving}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={isSaving}>
              {isSaving ? 'Saving...' : initialData ? 'Update Access Point' : 'Create Access Point'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
