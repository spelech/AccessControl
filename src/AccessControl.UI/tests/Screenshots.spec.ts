import { test } from '@playwright/test';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

test.describe('Capture Documentation Screenshots', () => {
  test('Capture all AccessControl views', async ({ page }) => {
    // Mock API routes with realistic homelab access control data
    await page.route('**/api/doors', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'door_front',
              name: 'Front Entry (Schlage Z-Wave Deadbolt)',
              lockProviderType: 'ZWaveJsMqtt',
              lockConfigJson: JSON.stringify({ nodeId: 'node_12', commandTopic: 'zwave/node_12/door_lock' }),
              keypadProviderType: 'BuiltInLockKeypad',
              keypadConfigJson: JSON.stringify({ nodeId: 'node_12' }),
              doorSensorProviderType: 'MqttContactSensor',
              doorSensorConfigJson: JSON.stringify({ topic: 'zwave/node_14/sensor_binary' }),
              autoLockEnabled: true,
              autoLockDaySeconds: 180,
              autoLockNightSeconds: 60,
              retryOnFailure: true,
              lockState: 'Locked',
              contactState: 'Closed',
              autoLockStatus: 'Idle',
              createdAt: '2026-09-01T00:00:00Z',
              updatedAt: '2026-09-06T00:00:00Z'
            },
            {
              id: 'door_back',
              name: 'Back Patio (August 3rd Gen + Ring Keypad v2)',
              lockProviderType: 'ZWaveJsMqtt',
              lockConfigJson: JSON.stringify({ nodeId: 'node_25', commandTopic: 'zwave/node_25/door_lock' }),
              keypadProviderType: 'RingMqttKeypad',
              keypadConfigJson: JSON.stringify({ locationId: 'loc_patio' }),
              doorSensorProviderType: 'MqttContactSensor',
              doorSensorConfigJson: JSON.stringify({ topic: 'homeassistant/binary_sensor/patio_door/state' }),
              autoLockEnabled: true,
              autoLockDaySeconds: 300,
              autoLockNightSeconds: 90,
              retryOnFailure: true,
              lockState: 'Unlocked',
              contactState: 'Closed',
              autoLockStatus: 'CountingDown',
              autoLockRemainingSeconds: 84,
              createdAt: '2026-09-02T00:00:00Z',
              updatedAt: '2026-09-06T00:00:00Z'
            }
          ])
        });
      } else {
        await route.continue();
      }
    });

    await page.route('**/api/users', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'usr_admin',
            name: 'Steven Pelech',
            role: 'Admin',
            isActive: true,
            credentials: [{ id: 'cred_1', type: 'PIN', pinLength: 4, label: 'Master Code' }],
            policies: [{ id: 'pol_1', name: '24/7 Unlimited Access', scheduleType: 'Always', isEnabled: true }]
          },
          {
            id: 'usr_member',
            name: 'Family Member',
            role: 'Member',
            isActive: true,
            credentials: [{ id: 'cred_2', type: 'PIN', pinLength: 6, label: 'Personal PIN' }],
            policies: [{ id: 'pol_2', name: 'Daily Access', scheduleType: 'WeeklyRecurring', isEnabled: true, daysOfWeek: 127 }]
          },
          {
            id: 'usr_cleaner',
            name: 'Cleaning Service',
            role: 'Service',
            isActive: true,
            credentials: [{ id: 'cred_3', type: 'PIN', pinLength: 4, label: 'Service PIN' }],
            policies: [{ id: 'pol_3', name: 'Tue & Thu 09:00 - 13:00', scheduleType: 'WeeklyRecurring', daysOfWeek: 10, startTime: '09:00', endTime: '13:00', isEnabled: true }]
          },
          {
            id: 'usr_guest',
            name: 'Weekend Guest (Airbnb)',
            role: 'Guest',
            isActive: true,
            credentials: [{ id: 'cred_4', type: 'PIN', pinLength: 4, label: 'Guest PIN' }],
            policies: [{ id: 'pol_4', name: 'Weekend Stay', scheduleType: 'DateRange', validFrom: '2026-09-05T15:00:00Z', validUntil: '2026-09-08T11:00:00Z', isEnabled: true }]
          }
        ])
      });
    });

    await page.route('**/api/logs*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'log_1',
            accessPointId: 'door_back',
            accessPointName: 'Back Patio (August 3rd Gen + Ring Keypad v2)',
            userName: 'Steven Pelech',
            eventType: 'Unlocked',
            method: 'RingKeypad',
            timestamp: new Date(Date.now() - 36000).toISOString(),
            details: 'Disarm command with verified PIN on Ring Keypad v2'
          },
          {
            id: 'log_2',
            accessPointId: 'door_front',
            accessPointName: 'Front Entry (Schlage Z-Wave Deadbolt)',
            userName: 'Cleaning Service',
            eventType: 'Unlocked',
            method: 'BuiltInKeypad',
            timestamp: new Date(Date.now() - 120000).toISOString(),
            details: 'Hardware slot 3 matched active recurring schedule'
          },
          {
            id: 'log_3',
            accessPointId: 'door_front',
            accessPointName: 'Front Entry (Schlage Z-Wave Deadbolt)',
            userName: null,
            eventType: 'AutoLocked',
            method: 'AutoLock',
            timestamp: new Date(Date.now() - 300000).toISOString(),
            details: 'Door closed auto-lock timer expired (180s)'
          },
          {
            id: 'log_4',
            accessPointId: 'door_back',
            accessPointName: 'Back Patio (August 3rd Gen + Ring Keypad v2)',
            userName: null,
            eventType: 'Denied',
            method: 'RingKeypad',
            timestamp: new Date(Date.now() - 600000).toISOString(),
            details: 'Invalid PIN entered on Ring Keypad v2'
          }
        ])
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const screenshotDir = path.resolve(__dirname, '../../../docs/screenshots');

    // 1. Doors View
    await page.screenshot({ path: path.join(screenshotDir, '01-dashboard-doors.png') });

    // 2. Door Setup Wizard Modal
    const addDoorBtn = page.getByRole('button', { name: /Add Door/i });
    if (await addDoorBtn.isVisible()) {
      await addDoorBtn.click();
      await page.waitForTimeout(400);
      await page.screenshot({ path: path.join(screenshotDir, '04-door-setup-wizard.png') });
      const cancelBtn = page.getByRole('button', { name: /Cancel/i });
      if (await cancelBtn.isVisible()) await cancelBtn.click();
      await page.waitForTimeout(200);
    }

    // 3. Users & Credentials View
    const usersTab = page.getByRole('button', { name: /Users & PINs/i });
    await usersTab.click();
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(screenshotDir, '02-users-and-schedules.png') });

    // 4. Activity Logs View
    const logsTab = page.getByRole('button', { name: /Activity Log/i });
    await logsTab.click();
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(screenshotDir, '03-activity-audit-log.png') });

    // 5. Mobile View (Pixel 7)
    await page.setViewportSize({ width: 412, height: 915 });
    const doorsTab = page.getByRole('button', { name: /Doors/i });
    await doorsTab.click();
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(screenshotDir, '05-mobile-responsive.png') });
  });
});
