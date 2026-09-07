export type LockStateType = 'Locked' | 'Unlocked' | 'Jammed' | 'Unknown';
export type DoorContactStateType = 'Closed' | 'Open';
export type AutoLockStatusType = 'Disabled' | 'Idle' | 'CountingDown' | 'PausedDoorOpen' | 'JammedRetry' | 'Locked';

export type UserRoleType = 'Admin' | 'Member' | 'Guest' | 'Service';
export type CredentialType = 'PIN' | 'RFID' | 'NFC' | 'Badge' | 'DuressPIN';
export type ScheduleType = 'Always' | 'WeeklyRecurring' | 'DateRange' | 'OneTime';

export type AccessEventType = 'Unlocked' | 'Locked' | 'Denied' | 'Jammed' | 'AutoLocked';
export type AccessMethod = 'RingKeypad' | 'BuiltInKeypad' | 'ZWaveKeypad' | 'Manual' | 'RF' | 'AutoLock';

export interface AccessPoint {
  id: string;
  name: string;
  lockProviderType: string;
  lockConfigJson: string;
  keypadProviderType?: string | null;
  keypadConfigJson?: string | null;
  doorSensorProviderType?: string | null;
  doorSensorConfigJson?: string | null;
  autoLockEnabled: boolean;
  autoLockDaySeconds: number;
  autoLockNightSeconds: number;
  retryOnFailure: boolean;
  createdAt?: string;
  updatedAt?: string;

  // Runtime / telemetry state
  lockState?: LockStateType;
  contactState?: DoorContactStateType;
  autoLockStatus?: AutoLockStatusType;
  remainingCountdownSeconds?: number;
}

export interface User {
  id: string;
  groupId?: string | null;
  name: string;
  role: UserRoleType;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface Credential {
  id: string;
  userId: string;
  type: CredentialType;
  pinLength: number;
  label?: string | null;
  createdAt?: string;
}

export interface AccessPolicy {
  id: string;
  name: string;
  scheduleType: ScheduleType;
  daysOfWeek: number; // bitmask 1=Mon, 2=Tue, 4=Wed, 8=Thu, 16=Fri, 32=Sat, 64=Sun (127=all)
  startTime?: string | null; // HH:mm:ss
  endTime?: string | null;   // HH:mm:ss
  validFrom?: string | null; // ISO datetime
  validUntil?: string | null;// ISO datetime
  remainingUses?: number | null;
  isEnabled: boolean;
}

export interface AccessLog {
  id: string;
  accessPointId: string;
  accessPointName?: string;
  userId?: string | null;
  userName?: string | null;
  credentialType?: CredentialType | null;
  eventType: AccessEventType;
  method: AccessMethod;
  timestamp: string;
  details?: string | null;
}

export interface DiscoveredTopic {
  topic: string;
  deviceType: 'lock' | 'keypad' | 'sensor';
  description: string;
  rawPayloadSample?: string;
}

export type ZWaveTransportType = 'WebSocket' | 'Mqtt';

export interface SystemSettings {
  zWaveTransportType: ZWaveTransportType;
  zWaveWebSocketUrl: string;
  zWaveMqttPrefix: string;
  mqttHost: string;
  mqttPort: number;
  mqttUsername: string;
  mqttPassword?: string;
  appriseUrl: string;
}

export interface TransportInfo {
  transportId: string;
  displayName: string;
  status: string;
  isConnected: boolean;
  details?: string | null;
}

export interface SettingsResponse {
  settings: SystemSettings;
  transports: TransportInfo[];
}

export interface DetectedZWaveNode {
  nodeId: number;
  name: string;
  deviceType: 'lock' | 'keypad' | 'sensor' | string;
  model: string;
}

export interface TestConnectionRequest {
  transportType: ZWaveTransportType;
  endpointUrl?: string;
}

export interface TestConnectionResult {
  success: boolean;
  latencyMs?: number;
  driverVersion?: string;
  serverVersion?: string;
  nodeCount?: number;
  detectedNodes?: DetectedZWaveNode[];
  message: string;
}

