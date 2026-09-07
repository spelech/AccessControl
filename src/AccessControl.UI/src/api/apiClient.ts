import type {
  AccessPoint,
  User,
  Credential,
  AccessPolicy,
  AccessLog,
  DiscoveredTopic,
  SystemSettings,
  SettingsResponse,
  TestConnectionRequest,
  TestConnectionResult,
  DiscoveredContactSensor,
  SnifferResult,
} from '../types';

declare global {
  interface Window {
    __ACCESSCONTROL_BASE_PATH__?: string;
  }
}

export function getBasePath(): string {
  if (typeof window !== 'undefined') {
    if (window.__ACCESSCONTROL_BASE_PATH__) {
      return window.__ACCESSCONTROL_BASE_PATH__.replace(/\/+$/, '');
    }
    const meta = document.querySelector('meta[name="base-path"]');
    if (meta) {
      const content = meta.getAttribute('content');
      if (content && content.trim().length > 0) {
        return content.trim().replace(/\/+$/, '');
      }
    }
  }
  return '';
}

export function buildUrl(endpoint: string): string {
  const base = getBasePath();
  const cleanEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
  return `${base}${cleanEndpoint}`;
}

async function request<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = buildUrl(endpoint);
  const headers = {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    ...(options?.headers || {}),
  };

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => '');
    throw new Error(`API Error [${response.status}] ${response.statusText}: ${errorText}`);
  }

  if (response.status === 204) {
    return {} as T;
  }

  return response.json();
}

export const apiClient = {
  // Access Points (Doors)
  doors: {
    list: async (): Promise<AccessPoint[]> => {
      return request<AccessPoint[]>('/api/doors');
    },
    get: async (id: string): Promise<AccessPoint> => {
      return request<AccessPoint>(`/api/doors/${id}`);
    },
    create: async (data: Partial<AccessPoint>): Promise<AccessPoint> => {
      return request<AccessPoint>('/api/doors', {
        method: 'POST',
        body: JSON.stringify(data),
      });
    },
    update: async (id: string, data: Partial<AccessPoint>): Promise<AccessPoint> => {
      return request<AccessPoint>(`/api/doors/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      });
    },
    delete: async (id: string): Promise<void> => {
      return request<void>(`/api/doors/${id}`, { method: 'DELETE' });
    },
    unlock: async (id: string): Promise<{ success: boolean; message?: string }> => {
      return request<{ success: boolean; message?: string }>(`/api/doors/${id}/unlock`, {
        method: 'POST',
      });
    },
    lock: async (id: string): Promise<{ success: boolean; message?: string }> => {
      return request<{ success: boolean; message?: string }>(`/api/doors/${id}/lock`, {
        method: 'POST',
      });
    },
  },

  // Users & Credentials
  users: {
    list: async (): Promise<User[]> => {
      return request<User[]>('/api/users');
    },
    get: async (id: string): Promise<User> => {
      return request<User>(`/api/users/${id}`);
    },
    create: async (data: Partial<User>): Promise<User> => {
      return request<User>('/api/users', {
        method: 'POST',
        body: JSON.stringify(data),
      });
    },
    update: async (id: string, data: Partial<User>): Promise<User> => {
      return request<User>(`/api/users/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      });
    },
    delete: async (id: string): Promise<void> => {
      return request<void>(`/api/users/${id}`, { method: 'DELETE' });
    },
    getCredentials: async (userId: string): Promise<Credential[]> => {
      return request<Credential[]>(`/api/users/${userId}/credentials`);
    },
    setPin: async (userId: string, pin: string, label?: string): Promise<Credential> => {
      return request<Credential>(`/api/users/${userId}/credentials/pin`, {
        method: 'POST',
        body: JSON.stringify({ pin, label: label || 'PIN' }),
      });
    },
    deleteCredential: async (userId: string, credentialId: string): Promise<void> => {
      return request<void>(`/api/users/${userId}/credentials/${credentialId}`, {
        method: 'DELETE',
      });
    },
    getPolicies: async (userId: string): Promise<AccessPolicy[]> => {
      return request<AccessPolicy[]>(`/api/users/${userId}/policies`);
    },
    savePolicy: async (userId: string, policy: Partial<AccessPolicy>): Promise<AccessPolicy> => {
      return request<AccessPolicy>(`/api/users/${userId}/policies`, {
        method: 'POST',
        body: JSON.stringify(policy),
      });
    },
  },

  // Logs & Audit Trail
  logs: {
    list: async (filter?: { doorId?: string; userId?: string; method?: string; limit?: number }): Promise<AccessLog[]> => {
      const params = new URLSearchParams();
      if (filter?.doorId) params.append('doorId', filter.doorId);
      if (filter?.userId) params.append('userId', filter.userId);
      if (filter?.method) params.append('method', filter.method);
      if (filter?.limit) params.append('limit', filter.limit.toString());

      const query = params.toString();
      return request<AccessLog[]>(`/api/logs${query ? `?${query}` : ''}`);
    },
  },

  // MQTT Topic Discovery
  discovery: {
    getTopics: async (): Promise<DiscoveredTopic[]> => {
      return request<DiscoveredTopic[]>('/api/discovery');
    },
    getSensors: async (): Promise<DiscoveredContactSensor[]> => {
      return request<DiscoveredContactSensor[]>('/api/discovery/sensors');
    },
    sniff: async (sinceIso: string): Promise<SnifferResult> => {
      return request<SnifferResult>(`/api/discovery/sniff?since=${encodeURIComponent(sinceIso)}`);
    },
  },

  // System Settings & Transports
  settings: {
    get: async (): Promise<SettingsResponse> => {
      return request<SettingsResponse>('/api/settings');
    },
    update: async (settingsData: Partial<SystemSettings>): Promise<{ success: boolean; message?: string }> => {
      return request<{ success: boolean; message?: string }>('/api/settings', {
        method: 'PUT',
        body: JSON.stringify(settingsData),
      });
    },
    testConnection: async (req: TestConnectionRequest): Promise<TestConnectionResult> => {
      return request<TestConnectionResult>('/api/settings/test-connection', {
        method: 'POST',
        body: JSON.stringify(req),
      });
    },
  },
};

