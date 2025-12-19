# Distributed Locking Solution

A distributed locking solution consisting of a REST service and client library for managing resource locks across distributed systems.

## Projects

### DistributedLocking.Service (.NET 8)
REST API service that manages distributed locks.

**Endpoints:**
- `POST /api/lock/acquire` - Acquire a lock on a resource
- `POST /api/lock/heartbeat` - Send heartbeat to keep lock alive
- `POST /api/lock/release` - Release a lock
- `GET /api/lock/status/{resourceName}` - Check lock status

**Features:**
- Lock acquisition with unique token
- Automatic lock expiration after 45 seconds without heartbeat
- Thread-safe lock management
- Background cleanup of expired locks

### DistributedLocking.Client (.NET Standard 2.0)
Client library for interacting with the locking service.

**Features:**
- `IDistributedLock` interface for lock operations
- `DistributedLockFactory` for creating client instances
- `LockHandle` - IDisposable lock handle with automatic heartbeat (every 30 seconds)
- Proper disposable pattern implementation

## Usage

### Running the Service

#### As a Console Application
```bash
cd src/DistributedLocking.Service
dotnet run
```
Service runs on `http://localhost:5000` by default.

#### As a Windows NT Service

**1. Publish the application:**
```powershell
cd src/DistributedLocking.Service
dotnet publish -c Release -o C:\Services\DistributedLockingService
```

**2. Install the Windows Service (Run as Administrator):**
```powershell
sc.exe create "DistributedLockingService" binPath="C:\Services\DistributedLockingService\DistributedLocking.Service.exe" start=auto
```

**3. Start the service:**
```powershell
sc.exe start DistributedLockingService
```

**4. Configure the service URL (optional):**
Edit `appsettings.json` in the publish folder to configure URLs:
```json
{
  "Urls": "http://localhost:5000"
}
```

**Managing the Windows Service:**
```powershell
# Check status
sc.exe query DistributedLockingService

# Stop service
sc.exe stop DistributedLockingService

# Delete/uninstall service
sc.exe delete DistributedLockingService
```

### Using the Client Library
```csharp
using DistributedLocking.Client;

// Create a lock client
IDistributedLock lockClient = DistributedLockFactory.Create("http://localhost:5000");

// Acquire a lock (returns null if lock is busy)
using (IDisposable lockHandle = lockClient.AcquireLock("my-resource"))
{
    if (lockHandle != null)
    {
        // Lock acquired - do work here
        // Heartbeat is automatically sent every 30 seconds
    }
    else
    {
        // Lock is busy, held by another client
    }
}
// Lock is automatically released when disposed

// Check if a resource is locked
bool isLocked = lockClient.IsLocked("my-resource");
```

## Building

```bash
dotnet build DistributedLocking.sln
```

## API Request/Response Examples

### Acquire Lock
**Request:**
```json
POST /api/lock/acquire
{
    "resourceName": "my-resource",
    "clientId": "client-123"
}
```

**Response (Success):**
```json
{
    "success": true,
    "status": "Acquired",
    "lockToken": "abc123...",
    "message": "Lock acquired successfully"
}
```

**Response (Busy):**
```json
{
    "success": false,
    "status": "Busy",
    "message": "Resource 'my-resource' is currently locked by another client"
}
```

### Heartbeat
**Request:**
```json
POST /api/lock/heartbeat
{
    "resourceName": "my-resource",
    "lockToken": "abc123..."
}
```

### Release Lock
**Request:**
```json
POST /api/lock/release
{
    "resourceName": "my-resource",
    "lockToken": "abc123..."
}
```
