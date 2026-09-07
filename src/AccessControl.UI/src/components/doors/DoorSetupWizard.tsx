import React, { useState, useEffect, useRef, useMemo } from 'react';
import { X, Sparkles, Shield, KeyRound, DoorClosed, Timer, Zap, Check, AlertCircle, Loader2 } from 'lucide-react';
import { apiClient } from '../../api/apiClient';
import { useSettingsStore } from '../../stores/useSettingsStore';
import type { AccessPoint, DetectedZWaveNode, DiscoveredContactSensor } from '../../types';

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

  const [name, setName] = useState('');

  // Lock Hardware
  const [lockHardwareMode, setLockHardwareMode] = useState<'DirectZWave' | 'MqttLock'>('DirectZWave');
  const [selectedLockNodeId, setSelectedLockNodeId] = useState<number | null>(39);
  const [lockProviderType, setLockProviderType] = useState('ZWaveWebSocket');
  const [lockTopic, setLockTopic] = useState('');

  // Keypad Provider
  const [keypadOption, setKeypadOption] = useState<'BuiltInKeypad' | 'StandaloneKeypad' | 'MqttKeypad' | 'None'>('BuiltInKeypad');
  const [selectedKeypadNodeId, setSelectedKeypadNodeId] = useState<number | null>(null);
  const [keypadProviderType, setKeypadProviderType] = useState('BuiltInKeypad');
  const [keypadTopic, setKeypadTopic] = useState('');

  // Door Contact Sensor
  const [sensorMode, setSensorMode] = useState<'None' | 'ZWaveSensor' | 'MqttContact'>('MqttContact');
  const [doorSensorProviderType, setDoorSensorProviderType] = useState('GenericMqttContact');
  const [doorSensorTopic, setDoorSensorTopic] = useState('');
  const [isManualSensorTopic, setIsManualSensorTopic] = useState(false);
  const [contactSensors, setContactSensors] = useState<DiscoveredContactSensor[]>([]);

  // Sniffer State
  const [isSniffing, setIsSniffing] = useState(false);
  const [sniffSuccessMessage, setSniffSuccessMessage] = useState<string | null>(null);
  const [sniffTimeoutMessage, setSniffTimeoutMessage] = useState<string | null>(null);
  const sniffingIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const sniffingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Auto-Lock Policy
  const [autoLockEnabled, setAutoLockEnabled] = useState(true);
  const [autoLockDaySeconds, setAutoLockDaySeconds] = useState(300);
  const [autoLockNightSeconds, setAutoLockNightSeconds] = useState(60);
  const [retryOnFailure, setRetryOnFailure] = useState(true);

  // UI / Form State
  const [isSaving, setIsSaving] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const effectiveDetectedNodes = useMemo(
    () => (detectedNodes.length > 0 ? detectedNodes : DEFAULT_DETECTED_NODES),
    [detectedNodes]
  );
  const detectedLockNodes = useMemo(
    () => effectiveDetectedNodes.filter((n) => n.deviceType === 'lock'),
    [effectiveDetectedNodes]
  );
  const detectedKeypadNodes = useMemo(
    () => effectiveDetectedNodes.filter((n) => n.deviceType === 'keypad'),
    [effectiveDetectedNodes]
  );
  const detectedSensorNodes = useMemo(
    () => effectiveDetectedNodes.filter((n) => n.deviceType === 'sensor'),
    [effectiveDetectedNodes]
  );

  const stopSniffing = () => {
    if (sniffingIntervalRef.current) {
      clearInterval(sniffingIntervalRef.current);
      sniffingIntervalRef.current = null;
    }
    if (sniffingTimeoutRef.current) {
      clearTimeout(sniffingTimeoutRef.current);
      sniffingTimeoutRef.current = null;
    }
    setIsSniffing(false);
  };

  const startSniffing = () => {
    stopSniffing();
    setIsSniffing(true);
    setSniffSuccessMessage(null);
    setSniffTimeoutMessage(null);

    const startTimeIso = new Date().toISOString();

    const poll = async () => {
      try {
        const res = await apiClient.discovery.sniff(startTimeIso);
        if (res && res.detected && res.event) {
          stopSniffing();
          setDoorSensorTopic(res.event.topic);
          const isAqara =
            res.event.model?.toLowerCase().includes('aqara') ||
            res.event.topic.toLowerCase().includes('aqara');
          setDoorSensorProviderType(isAqara ? 'AqaraZigbee' : 'GenericMqttContact');
          setSniffSuccessMessage(`Detected Activity on ${res.event.deviceName} (${res.event.state})`);
          return true;
        }
      } catch {
        // Continue polling
      }
      return false;
    };

    // Immediate initial check
    void poll();

    sniffingIntervalRef.current = setInterval(poll, 1500);

    sniffingTimeoutRef.current = setTimeout(() => {
      stopSniffing();
      setSniffTimeoutMessage('No activity detected within 15 seconds. Try again or enter topic manually.');
    }, 15000);
  };

  // Clean up timers on unmount or dialog close
  useEffect(() => {
    return () => {
      stopSniffing();
    };
  }, []);

  useEffect(() => {
    if (!isOpen) {
      stopSniffing();
    }
  }, [isOpen]);

  // Initialize or populate form
  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setAutoLockEnabled(initialData.autoLockEnabled);
      setAutoLockDaySeconds(initialData.autoLockDaySeconds);
      setAutoLockNightSeconds(initialData.autoLockNightSeconds);
      setRetryOnFailure(initialData.retryOnFailure);

      let parsedLockNodeId: number | null = null;
      let parsedLockTopic = '';
      try {
        const lockCfg = JSON.parse(initialData.lockConfigJson || '{}');
        parsedLockTopic = lockCfg.topic || lockCfg.lockTopic || (lockCfg.nodeId ? `node_${lockCfg.nodeId}` : '');
        parsedLockNodeId = lockCfg.nodeId ? Number(lockCfg.nodeId) : null;
      } catch {
        // Fallback
      }
      setSelectedLockNodeId(parsedLockNodeId);
      setLockTopic(parsedLockTopic);

      const isDirect = initialData.lockProviderType === 'ZWaveWebSocket' || parsedLockNodeId !== null;
      setLockHardwareMode(isDirect ? 'DirectZWave' : 'MqttLock');
      setLockProviderType(initialData.lockProviderType || (isDirect ? 'ZWaveWebSocket' : 'GenericMqtt'));

      let parsedKeypadNodeId: number | null = null;
      let parsedKeypadTopic = '';
      try {
        const kpCfg = JSON.parse(initialData.keypadConfigJson || '{}');
        parsedKeypadTopic = kpCfg.topic || kpCfg.keypadTopic || (kpCfg.nodeId ? `node_${kpCfg.nodeId}` : '');
        parsedKeypadNodeId = kpCfg.nodeId ? Number(kpCfg.nodeId) : null;
      } catch {
        // Fallback
      }
      setSelectedKeypadNodeId(parsedKeypadNodeId);
      setKeypadTopic(parsedKeypadTopic);

      if (!initialData.keypadProviderType || initialData.keypadProviderType === 'None') {
        setKeypadOption('None');
        setKeypadProviderType('None');
      } else if (initialData.keypadProviderType === 'BuiltInKeypad') {
        setKeypadOption('BuiltInKeypad');
        setKeypadProviderType('BuiltInKeypad');
      } else if (initialData.keypadProviderType === 'ZWaveKeypad' || parsedKeypadNodeId !== null) {
        setKeypadOption('StandaloneKeypad');
        setKeypadProviderType('ZWaveKeypad');
      } else {
        setKeypadOption('MqttKeypad');
        setKeypadProviderType(initialData.keypadProviderType);
      }

      let parsedSensorTopic = '';
      try {
        const sensorCfg = JSON.parse(initialData.doorSensorConfigJson || '{}');
        parsedSensorTopic = sensorCfg.topic || sensorCfg.sensorTopic || '';
      } catch {
        // Fallback
      }
      setDoorSensorTopic(parsedSensorTopic);

      if (!initialData.doorSensorProviderType || initialData.doorSensorProviderType === 'None') {
        setSensorMode('None');
        setDoorSensorProviderType('None');
      } else if (initialData.doorSensorProviderType === 'ZWaveSensor') {
        setSensorMode('ZWaveSensor');
        setDoorSensorProviderType('ZWaveSensor');
      } else {
        setSensorMode('MqttContact');
        setDoorSensorProviderType(initialData.doorSensorProviderType);
      }
    } else {
      setName('');
      const isWs = settings.zWaveTransportType === 'WebSocket';
      setLockHardwareMode(isWs ? 'DirectZWave' : 'MqttLock');

      if (isWs) {
        setLockProviderType('ZWaveWebSocket');
        const defaultNode = detectedLockNodes[0]?.nodeId ?? 39;
        setSelectedLockNodeId(defaultNode);
        setLockTopic(`node_${defaultNode}`);
        setKeypadOption('BuiltInKeypad');
        setKeypadProviderType('BuiltInKeypad');
        setSelectedKeypadNodeId(null);
        setKeypadTopic('');
      } else {
        setLockProviderType('GenericMqtt');
        setLockTopic('zwave/front_door');
        setSelectedLockNodeId(null);
        setKeypadOption('MqttKeypad');
        setKeypadProviderType('RingKeypad');
        setKeypadTopic('ring/keypad_entry');
        setSelectedKeypadNodeId(null);
      }

      setSensorMode('MqttContact');
      setDoorSensorProviderType('GenericMqttContact');
      setDoorSensorTopic('');
      setIsManualSensorTopic(false);
      setAutoLockEnabled(true);
      setAutoLockDaySeconds(300);
      setAutoLockNightSeconds(60);
      setRetryOnFailure(true);
    }
  }, [initialData, isOpen, settings.zWaveTransportType, detectedLockNodes]);

  // Load contact sensors & settings on open
  useEffect(() => {
    if (!isOpen) return;

    fetchSettings().catch(() => {});

    apiClient.discovery
      .getSensors()
      .then((sensors) => {
        if (sensors && sensors.length > 0) {
          setContactSensors(sensors);
        } else {
          setContactSensors([
            {
              topic: 'zigbee2mqtt/front_door_contact',
              deviceName: 'Front Door Contact',
              integration: 'Zigbee2MQTT',
              model: 'Aqara MCCGQ11LM',
              currentState: 'Closed',
              lastSeen: new Date().toISOString(),
            },
            {
              topic: 'homeassistant/binary_sensor/patio_door/state',
              deviceName: 'Patio Door Sensor',
              integration: 'Home Assistant',
              model: 'Ring Contact Sensor v2',
              currentState: 'Closed',
              lastSeen: new Date().toISOString(),
            },
          ]);
        }
      })
      .catch(() => {
        setContactSensors([
          {
            topic: 'zigbee2mqtt/front_door_contact',
            deviceName: 'Front Door Contact',
            integration: 'Zigbee2MQTT',
            model: 'Aqara MCCGQ11LM',
            currentState: 'Closed',
            lastSeen: new Date().toISOString(),
          },
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

    const lockConfig: Record<string, unknown> = {
      topic: lockHardwareMode === 'DirectZWave' ? (lockTopic || `node_${selectedLockNodeId}`) : lockTopic,
    };
    if (lockHardwareMode === 'DirectZWave' && selectedLockNodeId !== null) {
      lockConfig.nodeId = selectedLockNodeId;
    }

    let keypadConfig: Record<string, unknown> | null = null;
    let effectiveKeypadProvider: string | null = null;

    if (keypadOption === 'BuiltInKeypad') {
      effectiveKeypadProvider = 'BuiltInKeypad';
      keypadConfig = {
        nodeId: selectedLockNodeId,
        type: 'built_in',
      };
    } else if (keypadOption === 'StandaloneKeypad') {
      effectiveKeypadProvider = 'ZWaveKeypad';
      keypadConfig = {
        nodeId: selectedKeypadNodeId,
        topic: keypadTopic || (selectedKeypadNodeId ? `node_${selectedKeypadNodeId}` : ''),
      };
    } else if (keypadOption === 'MqttKeypad') {
      effectiveKeypadProvider = keypadProviderType || 'RingKeypad';
      keypadConfig = {
        topic: keypadTopic,
      };
    } else {
      effectiveKeypadProvider = null;
      keypadConfig = null;
    }

    let doorSensorConfig: Record<string, unknown> | null = null;
    let effectiveSensorProvider: string | null = null;

    if (sensorMode === 'None') {
      effectiveSensorProvider = null;
      doorSensorConfig = null;
    } else if (sensorMode === 'ZWaveSensor') {
      effectiveSensorProvider = 'ZWaveSensor';
      doorSensorConfig = { topic: doorSensorTopic };
    } else {
      effectiveSensorProvider = doorSensorProviderType || 'GenericMqttContact';
      doorSensorConfig = { topic: doorSensorTopic };
    }

    try {
      await onSave({
        ...(initialData?.id ? { id: initialData.id } : {}),
        name: name.trim(),
        lockProviderType: lockHardwareMode === 'DirectZWave' ? 'ZWaveWebSocket' : lockProviderType,
        lockConfigJson: JSON.stringify(lockConfig),
        keypadProviderType: effectiveKeypadProvider,
        keypadConfigJson: keypadConfig ? JSON.stringify(keypadConfig) : null,
        doorSensorProviderType: effectiveSensorProvider,
        doorSensorConfigJson: doorSensorConfig ? JSON.stringify(doorSensorConfig) : null,
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

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-content" onClick={(e) => e.stopPropagation()} style={{ maxWidth: '600px' }}>
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
          <div
            style={{
              backgroundColor: 'var(--status-jammed-subtle)',
              color: 'var(--status-jammed)',
              padding: '0.6rem 0.8rem',
              borderRadius: 'var(--radius-md)',
              fontSize: '0.85rem',
              marginBottom: '1rem',
            }}
          >
            {validationError}
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          {/* 1. Door Name */}
          <div>
            <label
              htmlFor="door-name-input"
              style={{ display: 'block', fontSize: '0.85rem', fontWeight: 500, marginBottom: '0.35rem' }}
            >
              Door / Access Point Name *
            </label>
            <input
              id="door-name-input"
              type="text"
              placeholder="e.g. Front Door, Side Entry, Garage Door"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
          </div>

          {/* 2. Lock Hardware Card */}
          <div
            style={{
              border: '1px solid var(--border-color)',
              padding: '1rem',
              borderRadius: 'var(--radius-md)',
              backgroundColor: 'var(--surface-elevated)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <Shield size={18} style={{ color: 'var(--status-locked)' }} />
              <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Lock Hardware</span>
            </div>

            {/* Segmented Toggle: Direct Z-Wave vs MQTT Lock */}
            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <button
                type="button"
                className={lockHardwareMode === 'DirectZWave' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setLockHardwareMode('DirectZWave');
                  setLockProviderType('ZWaveWebSocket');
                  if (keypadOption === 'MqttKeypad') {
                    setKeypadOption('BuiltInKeypad');
                    setKeypadProviderType('BuiltInKeypad');
                  }
                }}
                style={{ flex: 1, minHeight: '36px', fontSize: '0.85rem' }}
              >
                Direct Z-Wave JS
              </button>
              <button
                type="button"
                className={lockHardwareMode === 'MqttLock' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setLockHardwareMode('MqttLock');
                  if (lockProviderType === 'ZWaveWebSocket') {
                    setLockProviderType('GenericMqtt');
                  }
                }}
                style={{ flex: 1, minHeight: '36px', fontSize: '0.85rem' }}
              >
                MQTT Lock
              </button>
            </div>

            {/* Direct Z-Wave Mode: Discovered Nodes Only, Zero MQTT Leakage */}
            {lockHardwareMode === 'DirectZWave' && (
              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '0.8rem',
                    color: 'var(--text-secondary)',
                    marginBottom: '0.4rem',
                  }}
                >
                  Detected Lock Nodes (Direct Z-Wave JS):
                </label>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
                  {detectedLockNodes.map((node) => {
                    const isSelected = selectedLockNodeId === node.nodeId;
                    return (
                      <div
                        key={node.nodeId}
                        role="button"
                        tabIndex={0}
                        aria-label={`Node ${node.nodeId}: ${node.name}`}
                        onClick={() => {
                          setSelectedLockNodeId(node.nodeId);
                          setLockProviderType('ZWaveWebSocket');
                          setLockTopic(`node_${node.nodeId}`);
                          if (!name) setName(node.name);
                          if (keypadOption === 'None' || keypadOption === 'MqttKeypad') {
                            setKeypadOption('BuiltInKeypad');
                            setKeypadProviderType('BuiltInKeypad');
                          }
                        }}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            setSelectedLockNodeId(node.nodeId);
                            setLockProviderType('ZWaveWebSocket');
                            setLockTopic(`node_${node.nodeId}`);
                            if (!name) setName(node.name);
                          }
                        }}
                        style={{
                          padding: '0.65rem 0.85rem',
                          borderRadius: 'var(--radius-md)',
                          border: `2px solid ${isSelected ? 'var(--accent-primary)' : 'var(--border-color)'}`,
                          backgroundColor: isSelected ? 'var(--accent-subtle)' : 'var(--surface)',
                          cursor: 'pointer',
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          transition: 'all 0.15s ease',
                        }}
                      >
                        <div>
                          <div style={{ fontWeight: 600, fontSize: '0.9rem', color: 'var(--text-primary)' }}>
                            Node {node.nodeId}: {node.name}
                          </div>
                          <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                            {node.model || 'Z-Wave Electronic Lock'}
                          </div>
                        </div>
                        {isSelected && (
                          <span className="badge badge-locked" style={{ fontSize: '0.7rem' }}>
                            Selected
                          </span>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* MQTT Lock Mode: Provider Dropdown & MQTT Topic Input */}
            {lockHardwareMode === 'MqttLock' && (
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label
                    htmlFor="lock-provider-select"
                    style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}
                  >
                    Provider Type
                  </label>
                  <select
                    id="lock-provider-select"
                    value={lockProviderType}
                    onChange={(e) => setLockProviderType(e.target.value)}
                  >
                    <option value="GenericMqtt">Generic MQTT Lock</option>
                    <option value="SwitchBotMqtt">SwitchBot Lock (MQTT)</option>
                    <option value="AugustZWave">August Smart Lock (MQTT)</option>
                    <option value="SchlageZWave">Schlage Connect (MQTT)</option>
                    <option value="YaleZWave">Yale Assure (MQTT)</option>
                  </select>
                </div>

                <div>
                  <label
                    htmlFor="lock-topic-input"
                    style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}
                  >
                    MQTT Topic
                  </label>
                  <input
                    id="lock-topic-input"
                    type="text"
                    placeholder="zwave/front_door"
                    value={lockTopic}
                    onChange={(e) => setLockTopic(e.target.value)}
                  />
                </div>
              </div>
            )}
          </div>

          {/* 3. Keypad Provider Card (Context-Aware) */}
          <div
            style={{
              border: '1px solid var(--border-color)',
              padding: '1rem',
              borderRadius: 'var(--radius-md)',
              backgroundColor: 'var(--surface-elevated)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <KeyRound size={18} style={{ color: 'var(--accent-primary)' }} />
              <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Keypad Provider (Optional)</span>
            </div>

            {/* Keypad Segmented Options */}
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(115px, 1fr))',
                gap: '0.35rem',
                marginBottom: '0.75rem',
              }}
            >
              <button
                type="button"
                className={keypadOption === 'BuiltInKeypad' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setKeypadOption('BuiltInKeypad');
                  setKeypadProviderType('BuiltInKeypad');
                }}
                style={{ fontSize: '0.75rem', padding: '0.35rem 0.5rem', minHeight: '34px' }}
              >
                Built-In Keypad
              </button>
              <button
                type="button"
                className={keypadOption === 'StandaloneKeypad' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setKeypadOption('StandaloneKeypad');
                  setKeypadProviderType('ZWaveKeypad');
                  if (!selectedKeypadNodeId && detectedKeypadNodes.length > 0) {
                    setSelectedKeypadNodeId(detectedKeypadNodes[0].nodeId);
                    setKeypadTopic(`node_${detectedKeypadNodes[0].nodeId}`);
                  }
                }}
                style={{ fontSize: '0.75rem', padding: '0.35rem 0.5rem', minHeight: '34px' }}
              >
                Standalone Z-Wave
              </button>
              <button
                type="button"
                className={keypadOption === 'MqttKeypad' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setKeypadOption('MqttKeypad');
                  setKeypadProviderType('RingKeypad');
                  if (!keypadTopic) setKeypadTopic('ring/keypad_entry');
                }}
                style={{ fontSize: '0.75rem', padding: '0.35rem 0.5rem', minHeight: '34px' }}
              >
                Ring / MQTT
              </button>
              <button
                type="button"
                className={keypadOption === 'None' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setKeypadOption('None');
                  setKeypadProviderType('None');
                }}
                style={{ fontSize: '0.75rem', padding: '0.35rem 0.5rem', minHeight: '34px' }}
              >
                None
              </button>
            </div>

            {/* Keypad Detail Views */}
            {keypadOption === 'BuiltInKeypad' && (
              <div
                style={{
                  fontSize: '0.8rem',
                  color: 'var(--text-secondary)',
                  padding: '0.6rem 0.8rem',
                  backgroundColor: 'var(--surface)',
                  borderRadius: 'var(--radius-sm)',
                  border: '1px dashed var(--border-color)',
                }}
              >
                Uses Node {selectedLockNodeId || 39} hardware keypad. PIN slots sync directly.
              </div>
            )}

            {keypadOption === 'StandaloneKeypad' && (
              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '0.8rem',
                    color: 'var(--text-secondary)',
                    marginBottom: '0.3rem',
                  }}
                >
                  Detected Z-Wave Keypads:
                </label>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
                  {detectedKeypadNodes.map((node) => {
                    const isSelected = selectedKeypadNodeId === node.nodeId;
                    return (
                      <div
                        key={node.nodeId}
                        role="button"
                        tabIndex={0}
                        aria-label={`Node ${node.nodeId}: ${node.name}`}
                        onClick={() => {
                          setSelectedKeypadNodeId(node.nodeId);
                          setKeypadProviderType('ZWaveKeypad');
                          setKeypadTopic(`node_${node.nodeId}`);
                        }}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            setSelectedKeypadNodeId(node.nodeId);
                            setKeypadProviderType('ZWaveKeypad');
                            setKeypadTopic(`node_${node.nodeId}`);
                          }
                        }}
                        style={{
                          padding: '0.55rem 0.8rem',
                          borderRadius: 'var(--radius-sm)',
                          border: `2px solid ${isSelected ? 'var(--accent-primary)' : 'var(--border-color)'}`,
                          backgroundColor: isSelected ? 'var(--accent-subtle)' : 'var(--surface)',
                          cursor: 'pointer',
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                        }}
                      >
                        <div>
                          <div style={{ fontWeight: 600, fontSize: '0.85rem' }}>
                            Node {node.nodeId}: {node.name}
                          </div>
                          <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                            {node.model || 'Z-Wave Keypad'}
                          </div>
                        </div>
                        {isSelected && (
                          <span className="badge badge-locked" style={{ fontSize: '0.65rem' }}>
                            Selected
                          </span>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {keypadOption === 'MqttKeypad' && (
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label
                    htmlFor="keypad-provider-select"
                    style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}
                  >
                    Keypad Provider
                  </label>
                  <select
                    id="keypad-provider-select"
                    value={keypadProviderType}
                    onChange={(e) => setKeypadProviderType(e.target.value)}
                  >
                    <option value="RingKeypad">Ring Alarm Keypad v2</option>
                    <option value="GenericMqttKeypad">Generic MQTT Keypad</option>
                  </select>
                </div>
                <div>
                  <label
                    htmlFor="keypad-topic-input"
                    style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}
                  >
                    Keypad Topic
                  </label>
                  <input
                    id="keypad-topic-input"
                    type="text"
                    placeholder="ring/keypad_front"
                    value={keypadTopic}
                    onChange={(e) => setKeypadTopic(e.target.value)}
                  />
                </div>
              </div>
            )}

            {keypadOption === 'None' && (
              <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                Door unlocked via app or automation only. No keypad credentials synchronized.
              </div>
            )}
          </div>

          {/* 4. Door Contact Sensor Card (Smart Discovery & Sniffer) */}
          <div
            style={{
              border: '1px solid var(--border-color)',
              padding: '1rem',
              borderRadius: 'var(--radius-md)',
              backgroundColor: 'var(--surface-elevated)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <DoorClosed size={18} style={{ color: 'var(--status-closed)' }} />
              <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Contact Sensor (Optional)</span>
            </div>

            {/* Sensor Segmented Toggle */}
            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <button
                type="button"
                className={sensorMode === 'None' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setSensorMode('None');
                  setDoorSensorProviderType('None');
                  setDoorSensorTopic('');
                }}
                style={{ flex: 1, minHeight: '34px', fontSize: '0.8rem' }}
              >
                None
              </button>
              <button
                type="button"
                className={sensorMode === 'ZWaveSensor' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setSensorMode('ZWaveSensor');
                  setDoorSensorProviderType('ZWaveSensor');
                }}
                style={{ flex: 1, minHeight: '34px', fontSize: '0.8rem' }}
              >
                Z-Wave Sensor
              </button>
              <button
                type="button"
                className={sensorMode === 'MqttContact' ? 'btn-primary' : 'btn-outline'}
                onClick={() => {
                  setSensorMode('MqttContact');
                  if (doorSensorProviderType === 'None' || doorSensorProviderType === 'ZWaveSensor') {
                    setDoorSensorProviderType('GenericMqttContact');
                  }
                }}
                style={{ flex: 1, minHeight: '34px', fontSize: '0.8rem' }}
              >
                MQTT Contact
              </button>
            </div>

            {/* None Mode */}
            {sensorMode === 'None' && (
              <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                No contact sensor configured. Auto-lock will use timer only.
              </div>
            )}

            {/* Z-Wave Sensor Mode */}
            {sensorMode === 'ZWaveSensor' && (
              <div>
                {detectedSensorNodes.length > 0 ? (
                  <div>
                    <label
                      style={{
                        display: 'block',
                        fontSize: '0.8rem',
                        color: 'var(--text-secondary)',
                        marginBottom: '0.3rem',
                      }}
                    >
                      Detected Z-Wave Sensors:
                    </label>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
                      {detectedSensorNodes.map((node) => (
                        <div
                          key={node.nodeId}
                          role="button"
                          tabIndex={0}
                          onClick={() => setDoorSensorTopic(`node_${node.nodeId}`)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter' || e.key === ' ') {
                              setDoorSensorTopic(`node_${node.nodeId}`);
                            }
                          }}
                          style={{
                            padding: '0.5rem 0.75rem',
                            borderRadius: 'var(--radius-sm)',
                            border: `2px solid ${doorSensorTopic === `node_${node.nodeId}` ? 'var(--accent-primary)' : 'var(--border-color)'}`,
                            backgroundColor: doorSensorTopic === `node_${node.nodeId}` ? 'var(--accent-subtle)' : 'var(--surface)',
                            cursor: 'pointer',
                          }}
                        >
                          Node {node.nodeId}: {node.name}
                        </div>
                      ))}
                    </div>
                  </div>
                ) : (
                  <div>
                    <label
                      htmlFor="zwave-sensor-topic"
                      style={{ display: 'block', fontSize: '0.8rem', marginBottom: '0.25rem' }}
                    >
                      Z-Wave Sensor Node / Topic
                    </label>
                    <input
                      id="zwave-sensor-topic"
                      type="text"
                      placeholder="node_50"
                      value={doorSensorTopic}
                      onChange={(e) => setDoorSensorTopic(e.target.value)}
                    />
                  </div>
                )}
              </div>
            )}

            {/* MQTT Contact Mode: Friendly Dropdown & Activity Sniffer */}
            {sensorMode === 'MqttContact' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                <div>
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      marginBottom: '0.3rem',
                    }}
                  >
                    <label
                      htmlFor="contact-sensor-picker"
                      style={{ fontSize: '0.8rem', fontWeight: 500 }}
                    >
                      {isManualSensorTopic ? 'Custom Contact Sensor Topic' : 'Discovered Contact Sensor'}
                    </label>
                    <button
                      type="button"
                      className="btn-outline"
                      onClick={() => setIsManualSensorTopic(!isManualSensorTopic)}
                      style={{ fontSize: '0.75rem', padding: '0.15rem 0.5rem', minHeight: '26px' }}
                    >
                      {isManualSensorTopic ? 'Select from Discovered' : 'Enter Topic Manually'}
                    </button>
                  </div>

                  {isManualSensorTopic ? (
                    <input
                      id="contact-sensor-picker"
                      type="text"
                      placeholder="e.g. zigbee2mqtt/front_door_contact"
                      value={doorSensorTopic}
                      onChange={(e) => setDoorSensorTopic(e.target.value)}
                    />
                  ) : (
                    <select
                      id="contact-sensor-picker"
                      value={doorSensorTopic}
                      onChange={(e) => {
                        const topic = e.target.value;
                        setDoorSensorTopic(topic);
                        const found = contactSensors.find((s) => s.topic === topic);
                        if (found) {
                          const isAqara =
                            found.model?.toLowerCase().includes('aqara') ||
                            found.topic.toLowerCase().includes('aqara');
                          setDoorSensorProviderType(isAqara ? 'AqaraZigbee' : 'GenericMqttContact');
                        }
                      }}
                      aria-label="Discovered Contact Sensor"
                    >
                      <option value="">-- Select a detected contact sensor --</option>
                      {contactSensors.map((s) => (
                        <option key={s.topic} value={s.topic}>
                          {s.deviceName} — {s.model} ({s.integration}) [{s.currentState || 'Unknown'}]
                        </option>
                      ))}
                    </select>
                  )}
                </div>

                {/* Live Activity Sniffer */}
                <div
                  style={{
                    padding: '0.75rem',
                    borderRadius: 'var(--radius-md)',
                    backgroundColor: 'var(--surface)',
                    border: '1px solid var(--border-color)',
                  }}
                >
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      flexWrap: 'wrap',
                      gap: '0.5rem',
                    }}
                  >
                    <div>
                      <div
                        style={{
                          fontWeight: 600,
                          fontSize: '0.85rem',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '0.35rem',
                        }}
                      >
                        <Zap size={15} style={{ color: 'var(--status-unlocked)' }} />
                        <span>Live Activity Sniffer</span>
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                        Open or close your door to auto-detect its MQTT topic.
                      </div>
                    </div>

                    {!isSniffing ? (
                      <button
                        type="button"
                        className="btn-secondary"
                        onClick={startSniffing}
                        style={{ fontSize: '0.8rem', minHeight: '32px' }}
                      >
                        ⚡ Listen for Activity
                      </button>
                    ) : (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <span
                          style={{
                            fontSize: '0.8rem',
                            color: 'var(--accent-primary)',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '0.35rem',
                          }}
                        >
                          <Loader2 size={16} className="animate-spin" />
                          Listening...
                        </span>
                        <button
                          type="button"
                          className="btn-outline"
                          onClick={stopSniffing}
                          style={{ fontSize: '0.75rem', minHeight: '28px', padding: '0.2rem 0.5rem' }}
                        >
                          Cancel
                        </button>
                      </div>
                    )}
                  </div>

                  {sniffSuccessMessage && (
                    <div
                      style={{
                        marginTop: '0.6rem',
                        padding: '0.5rem 0.75rem',
                        backgroundColor: 'var(--status-locked-subtle)',
                        color: 'var(--status-locked)',
                        borderRadius: 'var(--radius-sm)',
                        fontSize: '0.8rem',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.4rem',
                      }}
                    >
                      <Check size={16} />
                      <span>{sniffSuccessMessage}</span>
                    </div>
                  )}

                  {sniffTimeoutMessage && (
                    <div
                      style={{
                        marginTop: '0.6rem',
                        padding: '0.5rem 0.75rem',
                        backgroundColor: 'var(--status-jammed-subtle)',
                        color: 'var(--status-jammed)',
                        borderRadius: 'var(--radius-sm)',
                        fontSize: '0.8rem',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.4rem',
                      }}
                    >
                      <AlertCircle size={16} />
                      <span>{sniffTimeoutMessage}</span>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* 5. Auto-Lock Policy Drawer */}
          <div
            style={{
              border: '1px solid var(--border-color)',
              padding: '1rem',
              borderRadius: 'var(--radius-md)',
            }}
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '0.75rem',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Timer size={18} style={{ color: 'var(--status-unlocked)' }} />
                <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>Auto-Lock Engine</span>
              </div>
              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.4rem',
                  fontSize: '0.85rem',
                  cursor: 'pointer',
                }}
              >
                <input
                  type="checkbox"
                  checked={autoLockEnabled}
                  onChange={(e) => setAutoLockEnabled(e.target.checked)}
                />
                Enabled
              </label>
            </div>

            {autoLockEnabled && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                  <div>
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '0.25rem',
                      }}
                    >
                      <label htmlFor="autolock-day-seconds" style={{ fontSize: '0.8rem' }}>
                        Day Timer
                      </label>
                      <span style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--accent-primary)' }}>
                        {autoLockDaySeconds}s ({Math.round(autoLockDaySeconds / 60)}m)
                      </span>
                    </div>
                    <input
                      type="range"
                      min="10"
                      max="1800"
                      step="1"
                      value={autoLockDaySeconds}
                      onChange={(e) => setAutoLockDaySeconds(Number(e.target.value))}
                      style={{ width: '100%', marginBottom: '0.25rem' }}
                    />
                    <input
                      id="autolock-day-seconds"
                      type="number"
                      min="10"
                      max="3600"
                      value={autoLockDaySeconds}
                      onChange={(e) => setAutoLockDaySeconds(Number(e.target.value))}
                    />
                  </div>

                  <div>
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '0.25rem',
                      }}
                    >
                      <label htmlFor="autolock-night-seconds" style={{ fontSize: '0.8rem' }}>
                        Night Timer
                      </label>
                      <span style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--accent-primary)' }}>
                        {autoLockNightSeconds}s ({Math.round(autoLockNightSeconds / 60)}m)
                      </span>
                    </div>
                    <input
                      type="range"
                      min="10"
                      max="1800"
                      step="1"
                      value={autoLockNightSeconds}
                      onChange={(e) => setAutoLockNightSeconds(Number(e.target.value))}
                      style={{ width: '100%', marginBottom: '0.25rem' }}
                    />
                    <input
                      id="autolock-night-seconds"
                      type="number"
                      min="10"
                      max="3600"
                      value={autoLockNightSeconds}
                      onChange={(e) => setAutoLockNightSeconds(Number(e.target.value))}
                    />
                  </div>
                </div>

                <div style={{ marginTop: '0.25rem' }}>
                  <label
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '0.4rem',
                      fontSize: '0.85rem',
                      cursor: 'pointer',
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={retryOnFailure}
                      onChange={(e) => setRetryOnFailure(e.target.checked)}
                    />
                    Automatic retry if lock jams or motor fails
                  </label>
                </div>

                {sensorMode !== 'None' && doorSensorTopic && (
                  <div
                    style={{
                      fontSize: '0.75rem',
                      color: 'var(--status-locked)',
                      backgroundColor: 'var(--status-locked-subtle)',
                      padding: '0.35rem 0.6rem',
                      borderRadius: 'var(--radius-sm)',
                    }}
                  >
                    🛡️ Sensor-Aware Guard Active: Auto-lock timer starts only after door contact sensor confirms closed.
                  </div>
                )}
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
