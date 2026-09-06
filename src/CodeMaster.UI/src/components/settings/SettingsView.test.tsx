import { describe, it, expect, afterEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/react';
import { SettingsView } from './SettingsView';

describe('SettingsView component', () => {
  afterEach(() => {
    cleanup();
  });

  it('renders system settings, Ingress context, and MCP tools', () => {
    render(<SettingsView />);

    expect(screen.getByText(/System & Connectivity Settings/i)).toBeDefined();
    expect(screen.getByText(/Ingress & Routing Context/i)).toBeDefined();
    expect(screen.getByText(/Model Context Protocol \(MCP\) Gateway/i)).toBeDefined();
    expect(screen.getByText(/codemaster__list_doors/i)).toBeDefined();
    expect(screen.getByText(/codemaster__unlock_door/i)).toBeDefined();
    expect(screen.getByText(/Hardware Security Architecture/i)).toBeDefined();
  });
});
