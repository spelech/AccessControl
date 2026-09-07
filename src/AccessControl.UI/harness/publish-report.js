#!/usr/bin/env node
import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const outputDir = path.join(__dirname, 'output');
if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir, { recursive: true });
}

// Find all generated screenshots
const images = fs.readdirSync(outputDir)
  .filter(f => f.endsWith('.png'))
  .sort();

console.log(`📸 Found ${images.length} flow screenshots in ${outputDir}`);

// Build static HTML gallery
const htmlContent = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>AccessControl UI Development Harness - Flow Gallery</title>
  <style>
    :root {
      --bg: #0f172a;
      --card-bg: #1e293b;
      --text: #f8fafc;
      --text-muted: #94a3b8;
      --border: #334155;
      --accent: #3b82f6;
    }
    body {
      font-family: system-ui, -apple-system, sans-serif;
      background: var(--bg);
      color: var(--text);
      margin: 0;
      padding: 2rem;
    }
    header {
      max-width: 1200px;
      margin: 0 auto 2rem;
      border-bottom: 1px solid var(--border);
      padding-bottom: 1rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    h1 { margin: 0; font-size: 1.5rem; display: flex; align-items: center; gap: 0.5rem; }
    .badge {
      background: #10b981;
      color: #064e3b;
      font-weight: 600;
      padding: 0.25rem 0.6rem;
      border-radius: 9999px;
      font-size: 0.75rem;
    }
    .grid {
      max-width: 1200px;
      margin: 0 auto;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(500px, 1fr));
      gap: 1.5rem;
    }
    .card {
      background: var(--card-bg);
      border: 1px solid var(--border);
      border-radius: 8px;
      overflow: hidden;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }
    .card-header {
      padding: 0.75rem 1rem;
      font-weight: 600;
      border-bottom: 1px solid var(--border);
      background: rgba(0,0,0,0.2);
    }
    .card img {
      width: 100%;
      height: auto;
      display: block;
      border-bottom: 1px solid var(--border);
    }
    .timestamp { font-size: 0.8rem; color: var(--text-muted); }
  </style>
</head>
<body>
  <header>
    <div>
      <h1>🛡️ AccessControl UI Development Harness <span class="badge">Live Validated</span></h1>
      <p class="timestamp">Generated: ${new Date().toISOString()}</p>
    </div>
    <a href="http://10.0.0.10:8150" target="_blank" style="color: var(--accent); text-decoration: none; font-weight: 500;">
      Open Live UI ↗
    </a>
  </header>
  <div class="grid">
    ${images.map(img => `
      <div class="card">
        <div class="card-header">${img.replace(/^\d+-/, '').replace('.png', '').replace(/-/g, ' ').toUpperCase()}</div>
        <img src="./${img}" alt="${img}" />
      </div>
    `).join('')}
  </div>
</body>
</html>`;

fs.writeFileSync(path.join(outputDir, 'index.html'), htmlContent);
console.log('✅ Generated index.html in output directory.');

// Publish to Agent Preview Hub
try {
  console.log('🚀 Publishing flow gallery to Agent Preview Hub...');
  const res = execSync(`agent-preview publish accesscontrol-harness ${outputDir} --title "AccessControl UI Development Harness" --category "AccessControl"`, { encoding: 'utf-8' });
  console.log(res);
} catch (e) {
  console.error('Warning: agent-preview publish failed:', e.message);
}
