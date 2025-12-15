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
```bash
cd src/DistributedLocking.Service
dotnet run
```
Service runs on `http://localhost:5000` by default.

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
