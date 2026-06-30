# Android Connectivity & Keep-Alive Strategy

**Date:** 2026-06-30  
**Stream:** Android packaging / connectivity hardening (post-stream-1 integration pass)  
**Status:** Design — scaffold landed in `stream/android-packaging`; wiring deferred.

---

## 1. Context

SharpClient connects to MUSH servers over raw TCP (telnet protocol). On Android, the OS will
kill or throttle background sockets under Doze mode, battery saver, and OEM-specific power
managers. This document describes the full strategy for keeping those connections alive.

The scaffold (foreground service + `IConnectionKeepAlive` interface) is already in place. This
document covers the *wiring* pass: when to start/stop the service, socket-level options, telnet
NOP keepalive, reconnect on network change, and Play Store considerations.

---

## 2. Foreground Service Lifecycle

### 2.1 When to start

Start `ConnectionKeepAliveService` when the **first** `ITelnetConnection` transitions to
`ConnectionState.Connected`. This is raised by `TelnetConnection.StateChanged`.

Suggested integration point: a `SessionOrchestrator` (new, wired in DI) observes all active
`SessionViewModel` instances. When the connected-session count goes from 0 → 1, it calls
`IConnectionKeepAlive.Start("Connected to <host>")`. When it goes from 1 → 0 (last session
disconnects or is closed), it calls `IConnectionKeepAlive.Stop()`.

**Do not** tie start/stop to individual session objects — the service must survive the creation and
teardown of individual sessions.

### 2.2 Notification text

Pass `"Connected — <host>:<port>"` as the `statusText`. Update the notification when the user
switches the active session (best-effort; a sticky "N sessions active" label is also acceptable).
Update via a new `IConnectionKeepAlive.UpdateStatus(string)` method added in the wiring pass.

### 2.3 Service declaration

Already declared in `AndroidManifest.xml` with `foregroundServiceType="connectedDevice"`.

**Type rationale:** `connectedDevice` (API 29+, `ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE = 16`)
is the correct type for a service that maintains a persistent TCP connection to a **remote host
over the network**. The Android documentation explicitly lists "network" alongside Bluetooth/NFC/USB
as qualifying connection modes. Crucially, `connectedDevice` has **no time cap** on Android 15+,
whereas `dataSync` is capped at 6 h per 24 h period — a dealbreaker for all-day MUSH sessions.
`remoteMessaging` is semantically incorrect (it targets SMS/push-notification proxying, not a raw
data stream). `connectedDevice` is the right call.

### 2.4 Required permissions (already in manifest)

| Permission | Minimum API | Purpose |
|---|---|---|
| `FOREGROUND_SERVICE` | any | Required for any foreground service |
| `FOREGROUND_SERVICE_CONNECTED_DEVICE` | 34 | Required when `foregroundServiceType="connectedDevice"` |
| `POST_NOTIFICATIONS` | 33 | Required to post the mandatory persistent notification |

On Android 13 (API 33) `POST_NOTIFICATIONS` must be **runtime-requested** before calling
`startForeground`. Add a permission-request flow in `MainActivity.OnCreate` (or via
`Permissions.RequestAsync<Permissions.PostNotifications>()` in MAUI).

---

## 3. Socket-Level Keep-Alive (`SO_KEEPALIVE`)

### 3.1 Where to set it

In `TelnetConnection.ConnectAsync` (in `src/SharpClient.Core/Connection/TelnetConnection.cs`),
after the `TcpClient.ConnectAsync` call succeeds, set:

```csharp
_tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
// Optional (Linux kernel tuning — Android supports these via TCP_* socket options):
_tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);   // s before first probe
_tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10); // s between probes
_tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5); // probe count
```

`SO_KEEPALIVE` sends TCP-level ACK probes when the connection is idle. This catches dead
connections (e.g., NAT timeout, server crash) before the telnet layer would notice. Note that
`SO_KEEPALIVE` requires the OS to keep the socket open — the foreground service ensures Android
doesn't kill the process, but `SO_KEEPALIVE` ensures the socket itself stays alive at the
network layer.

**File path:** `src/SharpClient.Core/Connection/TelnetConnection.cs`  
**Do NOT edit** during stream-1 (owned by the runtime agent); add this in the post-stream-1 pass.

### 3.2 Interaction with Doze

Android's Doze mode restricts network access in maintenance windows. The foreground service
exempts the *process* from Doze process restrictions. However, the foreground service must be
running *before* Doze kicks in. `SO_KEEPALIVE` probes sent during Doze maintenance windows keep
the TCP state alive on the server side.

---

## 4. Telnet-Level Keepalive (Periodic IAC NOP)

### 4.1 Rationale

Some MUSH servers close idle connections after a configurable timeout (often 10–30 min). A pure
TCP keepalive is insufficient if the server's idle timer is shorter than the kernel's keepalive
interval. Sending a periodic telnet IAC NOP (0xFF 0xF1) keeps the session live at the
application layer.

### 4.2 Implementation

Add a `CancellationTokenSource`-driven loop in `TelnetConnection`, started after `ConnectAsync`
succeeds:

```csharp
// Inside TelnetConnection, after ConnectAsync:
_keepAliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(NopIntervalSeconds));
_keepAliveTask  = RunNopLoopAsync(_keepAliveCts.Token);

private async Task RunNopLoopAsync(CancellationToken ct)
{
    while (await _keepAliveTimer.WaitForNextTickAsync(ct))
    {
        // IAC NOP = 0xFF 0xF1
        await _stream.WriteAsync(new byte[] { 0xFF, 0xF1 }, ct);
    }
}
```

**Suggested interval:** 120 seconds (2 min). Conservative enough to avoid server-side flooding,
aggressive enough to beat most idle-connection timeouts.

**Cancellation:** cancel `_keepAliveCts` in `DisconnectAsync` and `DisposeAsync`.

### 4.3 Integration with TNC

`TelnetNegotiationCore` processes inbound telnet bytes; it does not suppress outbound raw bytes.
Sending IAC NOP directly to the underlying `NetworkStream` bypasses TNC cleanly. Verify that TNC
does not interpret the local stream in a way that would intercept the NOP before it's written.

---

## 5. Reconnect on Network Change

### 5.1 Android `ConnectivityManager` callbacks

Register a `ConnectivityManager.NetworkCallback` in `ConnectionKeepAliveService.OnStartCommand`:

```csharp
var cm = (ConnectivityManager)GetSystemService(ConnectivityService)!;
var req = new NetworkRequest.Builder()
    .AddCapability(NetCapability.Internet)
    .Build();
cm.RegisterNetworkCallback(req, _networkCallback);
```

In `OnAvailable` (network came back), trigger reconnect. In `OnLost`, mark session as
disconnected (surface to UI) and stop attempting to write.

### 5.2 Reconnect logic

`TelnetConnection` already exposes `StateChanged`. On `ConnectionState.Disconnected` detected in
the foreground service (via the network callback or a ping failure), call:
`_telnetConnection.ConnectAsync(host, port, ct)` with an exponential back-off (1s, 2s, 4s, 8s,
cap 60s). Expose this via a new `ITelnetConnection.ReconnectAsync(CancellationToken)` or a
separate `SessionReconnector` service.

Store `(host, port)` in the foreground service (passed via intent extra at start time) for
reconnect use.

---

## 6. Battery Optimisation Exemption

### 6.1 `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`

Android allows apps to request exclusion from battery optimisation (the "Unrestricted" battery
mode in Settings → Apps → Battery). This prevents Doze from interfering even with the foreground
service's network access in extreme cases.

**Declaration:** `<uses-permission android:name="android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS" />`

**Play Store policy caveat (important):** Google Play's policy (`POLICY_BATTERY_OPTIMIZATION`) restricts
which app categories may request this exemption. Only apps that justify a "core function" requiring
it (navigation, VoIP, IoT) are permitted. A MUSH client may be borderline; **do not add this
permission by default**. Instead, provide a Settings screen with a deep-link to the OS battery
setting (`ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS`) so users can opt in voluntarily without
triggering a Play policy review.

### 6.2 `foregroundServiceType` Play Console declaration

When publishing to Play, declare the `connectedDevice` foreground service type in the **App content
→ Foreground Service Types** section of Play Console. Justification text should note: "SharpClient
connects to MUSH servers (text-based multiplayer games) over raw TCP. The foreground service
maintains the persistent TCP connection while the user is away from the screen. The connectedDevice
type is used because MUSH servers are remote hosts accessed over the network."

---

## 7. Manifest & Play-Readiness Findings (Task 2 Summary)

| Issue | Finding | Action taken |
|---|---|---|
| Target SDK | MAUI net10.0-android targets API 35 (Android 15) by default, meeting Play's August 2025 requirement | No change needed; documented |
| `android:label` | Missing — app appeared as "SharpClient.App" on-device | Set to `"SharpClient"` in manifest |
| `android:allowBackup` | Was `true` — risks credential leakage via Google Drive Auto Backup | Changed to `false` |
| `android:exported` on MainActivity | Must be explicit (Android 12+). MAUI's merger sets it from `[Activity(MainLauncher=true)]` | No action; MAUI handles it |
| `usesCleartextTraffic` | Not needed — Android's cleartext policy only applies to HTTP/HTTPS, not raw TCP | Not added; documented |
| `INTERNET` permission | Already present | No change |
| `ACCESS_NETWORK_STATE` | Already present | No change |
| Foreground service | Not declared | Added service + 3 permissions |
| `POST_NOTIFICATIONS` runtime request | Required on API 33+ | Noted; runtime prompt wiring deferred |
| Predictive back | Requires `android:enableOnBackInvokedCallback="true"` + `OnBackPressedCallback` in Activity | Deferred; MAUI 10 may add this automatically |
| Data Extraction Rules | Should define `android:dataExtractionRules` XML for cloud-backup opt-in | Deferred; allowBackup=false is sufficient for now |

---

## 8. Open Items / Deferred to Wiring Pass

- Wire `IConnectionKeepAlive` into DI via `MauiProgram.cs` (Android-conditional registration).
- `SessionOrchestrator` to watch session count and start/stop the service.
- Runtime `POST_NOTIFICATIONS` permission request flow (required on API 33+).
- `IConnectionKeepAlive.UpdateStatus(string)` for per-session notification updates.
- `SO_KEEPALIVE` socket options in `TelnetConnection.ConnectAsync` (owned by runtime agent).
- Telnet IAC NOP loop in `TelnetConnection` (owned by runtime agent).
- `ConnectivityManager.NetworkCallback` in the foreground service for reconnect-on-network-change.
- Reconnect back-off logic (`SessionReconnector` or extension to `TelnetConnection`).
- Decide on battery-optimisation exemption UX (Settings deep-link vs. no exemption).
- Predictive-back support (`OnBackPressedCallback` or MAUI equivalent).
- Update `android:name` in service declaration and `[Service(Name=...)]` when `ApplicationId` is finalised.
- Replace `Android.Resource.Drawable.IcDialogInfo` in the notification with a branded icon.
