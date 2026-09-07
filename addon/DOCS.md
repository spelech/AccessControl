# Home Assistant Add-on: AccessControl

**Universal Access Control & Lock/Keypad Synchronization Engine**

AccessControl is an open-source, general-purpose access control platform built on the Frigate architecture pattern. It provides centralized management for physical door locks, keypad credentials, access schedules, and audit trails across Z-Wave, Zigbee, Ring, Matter, and ESPHome.

## Features

- **Centralized Credential Synchronization**: Synchronize PIN codes, RFID tags, and access slots across physical locks and keypads seamlessly.
- **Home Assistant Ingress**: Direct access to the rich React 19 web dashboard within the Home Assistant UI without opening external ports.
- **Home Assistant MQTT Discovery**: Automatically emits 3–4 clean entities per door (lock proxy, event entity, contact sensor, auto-lock switch), eliminating entity bloat.
- **Pluggable Hardware Providers**: First-class support for Z-Wave JS UI, Zigbee2MQTT, Ring Alarm keypads, and Matter.
- **High Performance**: Built with .NET 10 and embedded SQLite with Write-Ahead Logging (WAL) for microsecond policy evaluation.
- **Rich Notifications**: Dual notification engine dispatching instant Home Assistant mobile events and webhook notifications to Apprise.

---

## Installation

1. In Home Assistant, navigate to **Settings** > **Add-ons** > **Add-on Store**.
2. Click the three dots (top right) and select **Repositories**.
3. Add the AccessControl add-on repository URL.
4. Locate **AccessControl** in the store list and click **Install**.
5. Once installed, toggle **Show in sidebar** to enable one-click Ingress navigation.
6. Click **Start** to run the add-on.

---

## Configuration

AccessControl configuration is managed via the **Configuration** tab in the add-on panel or through the interactive Web UI.

### Add-on Options

```yaml
mqtt_host: "core-mosquitto"
mqtt_port: 1883
```

- **`mqtt_host`** *(string, required)*: Hostname or IP address of the MQTT broker. If using the official Home Assistant Mosquitto add-on, leave as `core-mosquitto`.
- **`mqtt_port`** *(integer, required)*: Port of the MQTT broker (default: `1883`).

---

## Web Dashboard & Ingress

Click **AccessControl** in your Home Assistant sidebar to open the web management interface.

From the dashboard you can:
1. **Manage Doors**: Define access points, link physical locks (e.g. Z-Wave JS UI topics), and pair external keypads (e.g. Ring Keypad v2).
2. **Manage Credentials**: Provision users, create PIN codes with automatic cryptographic hashing, and set expiration limits or usage counts.
3. **Configure Time Windows**: Define recurring weekly schedules (e.g. Cleaners on Tuesdays 9am-12pm) or temporary visitor access.
4. **Live Audit Feed**: Monitor real-time access events, successful unlocks, invalid PIN attempts, and auto-lock state changes.

---

## Support & Contributing

- **Documentation**: [AccessControl Architecture & Design](https://github.com/spelech/accesscontrol)
- **Issue Tracker**: File bug reports and feature requests on GitHub.
