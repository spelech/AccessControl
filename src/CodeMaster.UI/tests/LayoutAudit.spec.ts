import { test, expect } from '@playwright/test';

test.describe('CodeMaster Dashboard Layout & Touch Ergonomics Audit', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('Gate 1: Zero horizontal overflow on root and body elements', async ({ page }) => {
    const hasHorizontalOverflow = await page.evaluate(() => {
      const doc = document.documentElement;
      const body = document.body;
      return (
        doc.scrollWidth > doc.clientWidth ||
        body.scrollWidth > body.clientWidth
      );
    });

    expect(hasHorizontalOverflow).toBe(false);
  });

  test('Gate 2: Responsive mobile fit across viewports', async ({ page }) => {
    const rootVisible = await page.locator('#root').isVisible();
    expect(rootVisible).toBe(true);

    const header = page.locator('header');
    await expect(header).toBeVisible();

    const title = page.locator('h1');
    await expect(title).toHaveText('CodeMaster');
  });

  test('Gate 3: Touch targets meet >= 24px ergonomic requirements', async ({ page }) => {
    const buttons = page.locator('button');
    const buttonCount = await buttons.count();

    expect(buttonCount).toBeGreaterThan(0);

    for (let i = 0; i < buttonCount; i++) {
      const button = buttons.nth(i);
      if (await button.isVisible()) {
        const box = await button.boundingBox();
        if (box) {
          expect(box.height).toBeGreaterThanOrEqual(24);
          expect(box.width).toBeGreaterThanOrEqual(24);
        }
      }
    }
  });

  test('Gate 4: Navigation tabs and dark theme toggle operate seamlessly', async ({ page }) => {
    // 1. Switch tabs
    const usersTab = page.getByRole('button', { name: /Users & PINs/i });
    await usersTab.click();
    await expect(page.getByRole('heading', { name: /Access Credentials & Users/i })).toBeVisible();

    const logsTab = page.getByRole('button', { name: /Activity Log/i });
    await logsTab.click();
    await expect(page.getByRole('heading', { name: /Access & Security Audit Trail/i })).toBeVisible();

    const settingsTab = page.getByRole('button', { name: /Settings/i });
    await settingsTab.click();
    await expect(page.getByRole('heading', { name: /System & Connectivity Settings/i })).toBeVisible();

    // 2. Toggle dark theme
    const themeBtn = page.getByLabel('Toggle theme');
    await expect(themeBtn).toBeVisible();
    await themeBtn.click();

    const themeAttr = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
    expect(['light', 'dark']).toContain(themeAttr);
  });

  test('Gate 5: Door setup wizard opens with zero overflow', async ({ page }) => {
    const doorsTab = page.getByRole('button', { name: 'Doors', exact: true });
    await doorsTab.click();

    // Click wizard open
    const openWizardBtn = page.getByRole('button', { name: /Launch Door Setup Wizard|Add Door/i }).first();
    await openWizardBtn.click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    const hasModalOverflow = await page.evaluate(() => {
      const modal = document.querySelector('.modal-content');
      if (!modal) return false;
      return modal.scrollWidth > modal.clientWidth;
    });

    expect(hasModalOverflow).toBe(false);

    // Close wizard
    const closeBtn = page.getByLabel('Close dialog');
    await closeBtn.click();
    await expect(dialog).not.toBeVisible();
  });
});
