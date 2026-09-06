PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA busy_timeout = 5000;
PRAGMA foreign_keys = ON;

-- 1. Users & Groups
CREATE TABLE IF NOT EXISTS UserGroups (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,               -- e.g. 'Family', 'Cleaners', 'Contractors', 'Guests'
    Description TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Users (
    Id TEXT PRIMARY KEY,
    GroupId TEXT,
    Name TEXT NOT NULL,
    Role TEXT NOT NULL DEFAULT 'Member', -- 'Admin', 'Member', 'Guest', 'Service'
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY(GroupId) REFERENCES UserGroups(Id) ON DELETE SET NULL
);

-- 2. Polymorphic Credentials (PIN, RFID, Badge)
CREATE TABLE IF NOT EXISTS Credentials (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    Type TEXT NOT NULL DEFAULT 'PIN', -- 'PIN', 'RFID', 'NFC', 'Badge', 'DuressPIN'
    EncryptedValue TEXT NOT NULL,     -- Encrypted PIN (for hardware slot sync)
    HashedValue TEXT NOT NULL,        -- Salted hash (for stateless keypad verification)
    PinLength INTEGER NOT NULL,
    Label TEXT,                       -- e.g. 'Primary PIN', 'Blue Keyfob'
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- 3. Access Points (Logical Doors / Gates / Garage)
CREATE TABLE IF NOT EXISTS AccessPoints (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,               -- e.g. 'Front Door', 'Side Door', 'Workshop Gate'
    LockProviderType TEXT NOT NULL,   -- 'ZWaveJsMqtt', 'Zigbee2Mqtt', 'GenericMqtt', 'Virtual'
    LockConfigJson TEXT NOT NULL,     -- Provider-specific JSON (topics, node IDs, etc.)
    KeypadProviderType TEXT NOT NULL, -- 'BuiltIn', 'RingMqtt', 'Zigbee2Mqtt', 'Wiegand', 'None'
    KeypadConfigJson TEXT,            -- Provider-specific JSON (topics, mode, etc.)
    DoorSensorProviderType TEXT,      -- 'MqttContact', 'HomeAssistant', 'None'
    DoorSensorConfigJson TEXT,        -- Config (topic, payload_open, payload_closed, invert)
    AutoLockEnabled INTEGER NOT NULL DEFAULT 1,
    AutoLockDaySeconds INTEGER NOT NULL DEFAULT 300,
    AutoLockNightSeconds INTEGER NOT NULL DEFAULT 60,
    RetryOnFailure INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

-- 4. Access Policies & Schedules
CREATE TABLE IF NOT EXISTS AccessPolicies (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,               -- e.g. 'Always 24/7', 'Weekdays 9-5', 'Weekend Guest'
    ScheduleType TEXT NOT NULL,       -- 'Always', 'WeeklyRecurring', 'DateRange', 'OneTime'
    DaysOfWeek INTEGER NOT NULL DEFAULT 127, -- Bitmask: 1=Mon, 2=Tue, 4=Wed, 8=Thu, 16=Fri, 32=Sat, 64=Sun
    StartTime TEXT,                   -- 'HH:mm:ss'
    EndTime TEXT,                     -- 'HH:mm:ss'
    ValidFrom TEXT,                   -- ISO8601 UTC
    ValidUntil TEXT,                  -- ISO8601 UTC
    RemainingUses INTEGER,            -- Decremented on unlock for OneTime schedules
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    TimeZoneId TEXT
);

-- 5. Access Assignments (User or Group <-> AccessPoint <-> Policy)
CREATE TABLE IF NOT EXISTS AccessAssignments (
    Id TEXT PRIMARY KEY,
    AccessPointId TEXT NOT NULL,
    UserId TEXT,                      -- Specific user, OR...
    GroupId TEXT,                     -- Entire group
    PolicyId TEXT NOT NULL,
    FOREIGN KEY(AccessPointId) REFERENCES AccessPoints(Id) ON DELETE CASCADE,
    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY(GroupId) REFERENCES UserGroups(Id) ON DELETE CASCADE,
    FOREIGN KEY(PolicyId) REFERENCES AccessPolicies(Id) ON DELETE CASCADE
);

-- 6. Hardware Slots (For slotted locks like Schlage, Yale, Kwikset)
CREATE TABLE IF NOT EXISTS HardwareSlots (
    Id TEXT PRIMARY KEY,
    AccessPointId TEXT NOT NULL,
    SlotNumber INTEGER NOT NULL,
    UserId TEXT,
    CredentialId TEXT,
    SyncStatus TEXT NOT NULL,         -- 'Synced', 'Adding', 'Deleting', 'Error'
    LastSyncedAt TEXT,
    FOREIGN KEY(AccessPointId) REFERENCES AccessPoints(Id) ON DELETE CASCADE,
    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE SET NULL,
    FOREIGN KEY(CredentialId) REFERENCES Credentials(Id) ON DELETE SET NULL,
    UNIQUE(AccessPointId, SlotNumber)
);

-- 7. Audit Trail & Access Logs
CREATE TABLE IF NOT EXISTS AccessLogs (
    Id TEXT PRIMARY KEY,
    AccessPointId TEXT NOT NULL,
    UserId TEXT,
    UserName TEXT NOT NULL,
    CredentialType TEXT NOT NULL,     -- 'PIN', 'Badge', 'Manual', 'Auto'
    EventType TEXT NOT NULL,          -- 'Unlocked', 'Locked', 'Denied', 'Jammed', 'AutoLocked'
    Method TEXT NOT NULL,             -- 'RingKeypad', 'BuiltInKeypad', 'ZWaveKeypad', 'Manual', 'RF', 'AutoLock'
    Timestamp TEXT NOT NULL,
    Details TEXT,
    FOREIGN KEY(AccessPointId) REFERENCES AccessPoints(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_access_logs_point_time ON AccessLogs(AccessPointId, Timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_assignments_point ON AccessAssignments(AccessPointId);
