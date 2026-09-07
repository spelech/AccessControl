import { test, expect } from '@playwright/test';

test.describe('AccessControl UI Development & Flow Harness', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('Flow 1: View Navigation & Shell Consistency', async ({ page }) => {
    // 1. Verify header branding
    await expect(page.locator('h1')).toContainText('AccessControl');

    // 2. Navigate through all tabs
    // Users tab
    await page.click('button:has-text("Users & PINs")');
    await expect(page.locator('h2:has-text("Access Credentials & Users")')).toBeVisible();

    // Activity Log tab
    await page.click('button:has-text("Activity Log")');
    await expect(page.locator('h2:has-text("Access & Security Audit Trail")')).toBeVisible();

    // Settings tab
    await page.click('button:has-text("Settings")');
    await expect(page.locator('h3:has-text("Z-Wave Transport & Driver")')).toBeVisible();

    // Doors tab
    await page.click('button:has-text("Doors")');
    await expect(page.locator('button:has-text("Add Door")')).toBeVisible();

    await page.screenshot({ path: 'harness/output/01-navigation-shell.png', fullPage: true });
  });

  test('Flow 2: Settings & Live Transport Diagnostics', async ({ page }) => {
    // 1. Go to Settings tab
    await page.click('button:has-text("Settings")');
    await expect(page.locator('h3:has-text("Z-Wave Transport & Driver")')).toBeVisible();

    // 2. Verify Z-Wave JS Server Card is rendered
    await expect(page.locator('text=WebSocket (Direct)')).toBeVisible();

    // 3. Click "Test Connection" button on Z-Wave WebSocket
    const testButton = page.locator('button:has-text("Test Connection")');
    await expect(testButton).toBeVisible();
    await testButton.click();

    // 4. Await test connection response (reports Connected and node details)
    await page.waitForTimeout(2000);
    await expect(page.locator('text=Connected').first()).toBeVisible();

    await page.screenshot({ path: 'harness/output/02-settings-diagnostics.png', fullPage: true });
  });

  test('Flow 3: Door Setup Wizard & Hardware Detection', async ({ page }) => {
    // 1. Ensure on Doors view
    await page.click('button:has-text("Doors")');

    // 2. Open Add Door wizard
    await page.click('button:has-text("Add Door")');
    await expect(page.locator('h2:has-text("1-Click Door Setup Wizard")')).toBeVisible();

    // 3. Fill door name
    await page.fill('input[placeholder*="Front Door"]', 'Front Entry Test');

    // 4. Verify Direct Z-Wave JS toggle is selected
    const directZWaveToggle = page.locator('button:has-text("Direct Z-Wave JS")');
    await expect(directZWaveToggle).toBeVisible();
    if (!(await directZWaveToggle.getAttribute('class'))?.includes('btn-primary')) {
      await directZWaveToggle.click();
    }
    await expect(directZWaveToggle).toHaveClass(/btn-primary/);

    // 5. Select Node 39 card
    const node39Card = page.locator('[aria-label*="Node 39"], [role="button"]:has-text("Node 39"), button:has-text("Node 39")').first();
    await expect(node39Card).toBeVisible();
    await node39Card.click();

    // 6. Assert zero MQTT leak: no MQTT topic input exists in DOM
    await expect(page.locator('input[placeholder*="zwave/"]')).toHaveCount(0);

    // 7. Assert Built-In Lock Keypad is active
    const builtInKeypadBtn = page.locator('button:has-text("Built-In Keypad")');
    await expect(builtInKeypadBtn).toBeVisible();
    await expect(builtInKeypadBtn).toHaveClass(/btn-primary/);

    // 8. Switch Contact Sensor to MQTT Contact and verify select has friendly options
    const mqttContactBtn = page.locator('button:has-text("MQTT Contact")');
    await expect(mqttContactBtn).toBeVisible();
    await mqttContactBtn.click();

    const sensorSelect = page.locator('select#contact-sensor-picker, select[aria-label="Discovered Contact Sensor"]');
    await expect(sensorSelect).toBeVisible();
    await expect(sensorSelect.locator('option').filter({ hasText: /Aqara|Contact/ }).first()).toBeAttached();

    // 9. Verify Auto-Lock Engine is rendered
    await expect(page.locator('text=Auto-Lock Engine')).toBeVisible();

    // 10. Capture screenshot
    await page.screenshot({ path: 'harness/output/03-door-setup-wizard.png' });

    // 11. Close modal without saving
    await page.click('button[aria-label="Close dialog"]');
    await expect(page.locator('h2:has-text("1-Click Door Setup Wizard")')).not.toBeVisible();
  });

  test('Flow 4: User Management & Strict PIN Validation Flow', async ({ page }) => {
    // 1. Go to Users tab
    await page.click('button:has-text("Users & PINs")');
    await expect(page.locator('h2:has-text("Access Credentials & Users")')).toBeVisible();

    // 2. Check if Add User button exists
    const addUserBtn = page.locator('button:has-text("Add User / Guest")');
    await expect(addUserBtn).toBeVisible();
    await addUserBtn.click();

    // 3. Modal opens with title "Create New User"
    await expect(page.locator('h2:has-text("Create New User")')).toBeVisible();
    await page.fill('input[placeholder*="John Doe"]', 'Test Resident');

    // Submit user
    await page.click('button:has-text("Create User")');
    await expect(page.locator('h2:has-text("Create New User")')).not.toBeVisible();

    // 4. Verify the newly created user is rendered
    await expect(page.locator('text=Test Resident').first()).toBeVisible();

    // 5. Open Set PIN modal for the user
    const assignPinBtn = page.locator('button:has-text("Assign PIN"), button:has-text("Change PIN")').first();
    await assignPinBtn.click();

    // 6. Test short invalid PIN -> verify browser HTML5 validation marks input invalid
    await expect(page.locator('h2:has-text("Set PIN & Schedule")')).toBeVisible();
    const pinInput = page.locator('input[type="password"]');
    await pinInput.fill('12'); // Less than 4 digits

    // Input fails HTML5 pattern validation
    const isShortInvalid = await pinInput.evaluate((el: HTMLInputElement) => !el.checkValidity());
    expect(isShortInvalid).toBe(true);

    // 7. Fix PIN to valid 4 digits -> input validity passes
    await pinInput.fill('4821');
    const isValid = await pinInput.evaluate((el: HTMLInputElement) => el.checkValidity());
    expect(isValid).toBe(true);

    // 8. Test Schedule tabs
    await page.click('button:has-text("Recurring")');
    await expect(page.locator('text=Active Days of the Week')).toBeVisible();

    await page.click('button:has-text("Date Range")');
    await expect(page.locator('text=Temporary Access Window')).toBeVisible();
    await expect(page.locator('text=Valid From')).toBeVisible();

    await page.click('button:has-text("One-Time Pass")');
    await expect(page.locator('text=One-Time Use Guest PIN')).toBeVisible();

    await page.click('button:has-text("Always 24/7")');

    await page.screenshot({ path: 'harness/output/04-user-pin-validation.png' });

    // Close PIN modal
    await page.click('button:has-text("Cancel")');
  });

  test('Flow 5: Live Activity Audit Feed & SSE Heartbeat', async ({ page }) => {
    // 1. Navigate to Activity Log
    await page.click('button:has-text("Activity Log")');
    await expect(page.locator('h2:has-text("Access & Security Audit Trail")')).toBeVisible();

    // 2. Verify filter controls exist
    await expect(page.locator('text=Filter by Door')).toBeVisible();
    await expect(page.locator('text=Filter by Event')).toBeVisible();

    // 3. Verify Live feed toggle button
    const liveToggle = page.locator('button:has-text("Pause Feed"), button:has-text("Resume Live")').first();
    await expect(liveToggle).toBeVisible();

    await page.screenshot({ path: 'harness/output/05-live-event-feed.png', fullPage: true });
  });
});
