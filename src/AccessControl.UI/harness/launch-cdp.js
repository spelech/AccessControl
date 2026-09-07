#!/usr/bin/env node
/**
 * Launches Chromium with remote debugging port 9222 enabled.
 * This allows Chrome DevTools MCP or browser devtools to connect via CDP at:
 *   http://127.0.0.1:9222
 * 
 * Usage:
 *   node harness/launch-cdp.js [--headed] [--port 9222] [--url http://localhost:8150]
 */

import { spawn } from 'child_process';
import http from 'http';

const args = process.argv.slice(2);
const isHeaded = args.includes('--headed');
const portIndex = args.indexOf('--port');
const cdpPort = portIndex !== -1 ? parseInt(args[portIndex + 1], 10) : 9222;
const urlIndex = args.indexOf('--url');
const targetUrl = urlIndex !== -1 ? args[urlIndex + 1] : (process.env.UI_URL || 'http://localhost:8150');

const chromeExecutable = process.env.PUPPETEER_EXECUTABLE_PATH || 
  '/home/steve/.cache/ms-playwright/chromium-1243/chrome-linux64/chrome';

console.log(`🚀 Launching Chromium CDP Server...`);
console.log(`   Executable: ${chromeExecutable}`);
console.log(`   CDP Port:   ${cdpPort}`);
console.log(`   Target URL: ${targetUrl}`);
console.log(`   Mode:       ${isHeaded ? 'Headed' : 'Headless'}`);

const chromeArgs = [
  isHeaded ? '--headless=false' : '--headless=new',
  `--remote-debugging-port=${cdpPort}`,
  '--no-sandbox',
  '--disable-gpu',
  '--disable-dev-shm-usage',
  '--no-first-run',
  '--no-default-browser-check',
  '--window-size=1280,800',
  targetUrl
];

const chromeProcess = spawn(chromeExecutable, chromeArgs, {
  detached: false,
  stdio: ['ignore', 'pipe', 'pipe']
});

chromeProcess.stderr.on('data', (data) => {
  const line = data.toString();
  if (line.includes('DevTools listening on')) {
    console.log(`\n✅ ${line.trim()}`);
    console.log(`\n🔗 Chrome DevTools MCP is ready to connect:`);
    console.log(`   npx chrome-devtools-mcp --browserUrl http://127.0.0.1:${cdpPort}`);
    console.log(`   Or inspect in browser: http://127.0.0.1:${cdpPort}/json/version\n`);
  }
});

chromeProcess.on('exit', (code) => {
  console.log(`Chromium process exited with code ${code}`);
  process.exit(code || 0);
});

process.on('SIGINT', () => {
  console.log('\nStopping Chromium...');
  chromeProcess.kill();
  process.exit(0);
});
process.on('SIGTERM', () => {
  chromeProcess.kill();
  process.exit(0);
});
