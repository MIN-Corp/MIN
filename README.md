# MIN - Distributed Messenger

![Logo](https://raw.githubusercontent.com/MIN-Corp/MIN/main/Desktop/MIN.Desktop/Assets/Images/logoImage.png)

> **Secure distributed messenger with end-to-end encryption**

[![Stars](https://img.shields.io/github/stars/MIN-Corp/MIN?style=flat-square&logo=github)](https://github.com/MIN-Corp/MIN/stargazers)
[![Forks](https://img.shields.io/github/forks/MIN-Corp/MIN?style=flat-square&logo=github)](https://github.com/MIN-Corp/MIN/network/members)
[![License](https://img.shields.io/github/license/MIN-Corp/MIN?style=flat-square)](https://github.com/MIN-Corp/MIN/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-Latest-blue?style=flat-square&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)

[English](README.md) • [Русский](README.ru.md)

---

## Contents

- [Features](#-features)
- [Architecture](#-architecture)
- [Technology Stack](#-technology-stack)
- [Installation and Running](#-installation-and-running)
- [Project Structure](#-project-structure)
- [Security](#-security)
- [Connection Protocol](#-connection-protocol)
- [Multiplayer Sessions](#-multiplayer-sessions)
- [Port Tunnel Sessions](#-port-tunnel-sessions)
- [Screen Demo](#-screen-demo)
- [Port Forwarding](#-port-forwarding)
- [Direct Connection](#-direct-connection)
- [Interface](#-interface)
- [Authors](#-authors)

---

## Features

| Feature | Description |
|---------|-------------|
| **End-to-End Encryption** | All messages are encrypted using cryptography |
| **File Transfer** | Sending and receiving photos and files |
| **Local Discovery** | Automatic room discovery via UDP Broadcast |
| **Direct Connection** | Connect to a room directly by IP:Port |
| **Port Forwarding** | UPnP port forwarding for non-LAN connections |
| **Connection Protocol** | Secure handshake protocol before joining a room |
| **Text Messages** | Sending and receiving messages in real time |
| **Rooms** | Creating and joining chat rooms |
| **Multiplayer Sessions** | Download and run game sessions from other resources |
| **Port Tunnel Sessions** | Play unmodified multiplayer games via a generic encrypted TCP tunnel |
| **Screen Demo** | Share a monitor or an app window with room participants, with system sound |
| **Voice calls** | Start voice calls in room with all participants in it |
| **Desktop UI** | Intuitive interface using Avalonia |

---

## Interface

MIN provides a modern interface for:

| Function | Description |
|----------|-------------|
| **Rooms** | Creating and managing chat rooms |
| **Chat** | Real-time communication |
| **Files** | Transfer with progress display |
| **Voice calls** | Talk with your friends using room voice calls |
| **Screen demo** | Share your screen and watch other participants' streams |
| **Participants** | Online/offline statuses |

![Screen](https://raw.githubusercontent.com/MIN-Corp/MIN/main/Desktop/MIN.Desktop/Assets/Images/screen.png)

---

## Architecture

```mermaid
flowchart TB
    %%{init: {'flowchart': {'nodeSpacing': 12}}}%%
    classDef uiLayer fill:#dbeafe,stroke:#3b82f6,stroke-width:2px,color:#1e40af
    classDef infraLayer fill:#e0e7ff,stroke:#4f46e5,stroke-width:2px,color:#3730a3
    classDef coreLayer fill:#ede9fe,stroke:#7c3aed,stroke-width:2px,color:#5b21b6
    classDef component fill:#ffffff,stroke:#c7d2fe,stroke-width:1.5px,color:#312e81
    classDef highlight fill:#c7d2fe,stroke:#4f46e5,stroke-width:3px,color:#312e81
    
    subgraph UI["UI Layer (Avalonia)"]
        direction TB
        A["ChatPanel<br>Messages and rooms"]
        B["DiscoveryPanel<br>Room discovery"]
    end
    
    subgraph INF["Infrastructure Layer"]
        direction TB
        C["Chat Services<br>Messages and statuses"]
        D["Discovery Services<br>UDP Broadcast Discovery"]
        E["FileTransfer Services<br>Streaming transfer"]
        F["Session Services<br>Multiplayer sessions and tunnels"]
        SD["ScreenDemo Services<br>Screen sharing"]
        AU["Audio Services<br>Audio playback"]
        V["Voice Services<br>Voice calls and voice managment"]
    end
    
    subgraph CORE["Core Layer"]
        direction TB
        G["Cryptography<br>E2E encryption"]
        H["Messaging<br>Messaging system"]
        I["Events<br>Event Bus"]
        J["Handlers<br>Dispatcher"]
        K["Transport<br>TCP / UDP / Named Pipes"]
        L["Serialization<br>JSON"]
        M["Protocol<br>Connection handshake"]
        N["SubRooms<br>Session sub-rooms"]
        O["Services<br>Room hosting, routing"]
        P["Headers<br>Message headers"]
    end
    
    class A highlight;
    class B,C,D,E,F,V,SD,AU,G,H,I,J,K,L,M,N,O,P component
    class UI uiLayer;
    class INF infraLayer;
    class CORE coreLayer
    
    UI --> INF
    INF --> CORE
```

### Modules

#### Core
| Module | Purpose |
|--------|---------|
| `MIN.Core.Cryptography` | Cryptographic operations |
| `MIN.Core.Messaging` | Messaging system |
| `MIN.Core.Events` | Event Bus |
| `MIN.Core.Handlers` | Handlers with dispatcher |
| `MIN.Core.Transport` | TCP / UDP / Named Pipes transport |
| `MIN.Core.Protocol` | MIN connection protocol (handshake) |
| `MIN.Core.SubRooms` | Sub-room management for sessions |
| `MIN.Core.Identity` | Local user identity |
| `MIN.Core.Services` | Room hosting, connection, routing services |
| `MIN.Core.Headers` | Message headers |
| `MIN.Core.Serialization` | JSON serialization |
| `MIN.Core.Entities` | Data models |
| `MIN.Core.Stores` | Stores |
| `MIN.Core.Streaming` | Data streams |

#### Infrastructure (Business Logic)
| Module | Purpose |
|--------|---------|
| `MIN.Chat` | Chats and messages |
| `MIN.Discovery` | Room discovery via UDP Broadcast |
| `MIN.FileTransfer` | File transfer |
| `MIN.Sessions` | Multiplayer session management (process sessions and port tunnels) |
| `MIN.ScreenDemo` | Screen demo: capture, encoding, streaming and playback |
| `MIN.Audio` | Audio playback services |
| `MIN.Voice` | Voice calls |

#### Desktop (UI)
| Module | Purpose |
|--------|---------|
| `MIN.Desktop` | Avalonia desktop application |

#### Cross-Cutting
| Module | Purpose |
|--------|---------|
| `MIN.Common` | Common interfaces, MVC module system |
| `MIN.Helpers` | Logging, settings, versioning, updates |
| `MIN.DI` | Root dependency injection composition |

---

## Technology Stack

| Category | Technology | Description |
|----------|------------|-------------|
| Language | C# | Modern object-oriented language |
| Platform | .NET 8.0 | Cross-platform framework |
| UI | Avalonia | Cross-platform desktop interface |
| DI | Microsoft.Extensions.DependencyInjection | Dependency injection |
| Security | Microsoft.AspNetCore.DataProtection | Cross-Platform file protection |
| Transport | TCP / UDP | Network communication |
| UPnP | Open.Nat | Port forwarding for external connections |
| Voice calls | OpenAL | Cross-Platform voice recording and playback |
| Video encoding | OpenH264 | Screen demo video encoding and decoding |
| Screen capture | Windows Graphics Capture / WASAPI | Screen demo capture and system sound (Windows) |
| Noise reduction | Onnx | Reducing background noise |
| Modularity | MIN.Common.Mvc | Custom module/plugin system |
| Style | .editorconfig | Unified code style |

---

## Installation and Running

### Requirements

> [!TIP]
> Make sure you have .NET SDK 8.0 or higher installed.

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- Windows 10/11
- Local network (for room discovery)
- Virtual networks (Radmin, Hamachi) (if you want to connect outside local network without UPnP)

### Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/MIN-Corp/MIN.git
cd MIN

# 2. Restore dependencies
dotnet restore

# 3. Build the project
dotnet build

# 4. Run the application
dotnet run --project Desktop/MIN.Desktop/MIN.Desktop.csproj
```

---

## Project Structure

```mermaid
flowchart TB
    %%{init: {'flowchart': {'nodeSpacing': 20}}}%%
    classDef coreFolder fill:#ede9fe,stroke:#7c3aed,color:#5b21b6
    classDef infraFolder fill:#e0e7ff,stroke:#4f46e5,color:#3730a3
    classDef deskFolder fill:#dbeafe,stroke:#3b82f6,color:#1e40af
    classDef folder fill:#f5f3ff,stroke:#c7c2ea,color:#312e81
    classDef file fill:#fff,stroke:#c7d2fe,color:#4338ca
    
    subgraph MIN["MIN"]
        subgraph CORE["Core/"]
            G["Cryptography/"]
            H["Messaging/"]
            I["Events/"]
            J["Handlers/"]
            K["Transport/<br>TCP/ UDP/ NamedPipes"]
            L["Serialization/"]
            M["Protocol/"]
            N["SubRooms/"]
            O["Services/"]
            P["Headers/"]
            DI["DI/"]
        end
        subgraph INF["Infrastructure/"]
            Q["Chat/"]
            R["Discovery/"]
            S["FileTransfer/"]
            T["Sessions/"]
            U["Voice/"]
        end
        subgraph DESK["Desktop/"]
            V["MIN.Desktop/"]
            W["Views/"]
            X["Components/"]
            Y["Resources/"]
        end
        Z["Common/"]
        AA["Helpers/"]
        AB["DI/"]
    end
    
    class CORE coreFolder
    class INF infraFolder
    class DESK deskFolder
    class G,H,I,J,K,L,M,N,O,P,DI,Q,R,S,T,U,V,W,X,Y,Z,AA,AB file
```

> [!NOTE]
> Configuration files (`Directory.Build.props`, `Directory.Packages.props`) manage dependencies centrally.

---

## Security

> [!IMPORTANT]
> MIN uses **end-to-end encryption** — your messages are protected from interception.

### Encryption Scheme

```mermaid
flowchart LR
    classDef sender fill:#dbeafe,stroke:#3b82f6,stroke-width:3px,color:#1e40af
    classDef encrypted fill:#e0e7ff,stroke:#4f46e5,stroke-width:3px,color:#3730a3
    classDef recipient fill:#dbeafe,stroke:#3b82f6,stroke-width:3px,color:#1e40af
    
    A["Sender"] -->|"Encryption"| B["Encrypted Message"]
    B -->|"Network"| C["Recipient"]
    
    class A sender;
    class B encrypted;
    class C recipient
```

### Security Technologies

| Technology | Purpose |
|------------|---------|
| **Asymmetric (RSA)** | Key exchange between participants |
| **Symmetric (AES)** | Message content encryption |
| **DataProtection** | Secure key storage |

---

## Connection Protocol

> [!IMPORTANT]
> Before joining a room, each client must pass the MIN protocol handshake — this ensures only compatible MIN clients can connect.

```mermaid
sequenceDiagram
    participant Client
    participant Server
    
    Client->>Server: "MIN" (connection request)
    Server->>Server: Validate protocol
    Server->>Client: "MIN" + RoomInfo (JSON)
    Client->>Client: Validate RoomInfo
    Note over Client,Server: If validation fails → connection dropped
```

The protocol works as follows:
1. **Client** connects via TCP and sends the string `"MIN"`
2. **Server** receives the request, validates it matches the expected protocol
3. **Server** responds with `"MIN"` followed by serialized `RoomInfo` JSON containing room metadata
4. **Client** validates the response — if it doesn't start with `"MIN"` or the room info is invalid, the connection is terminated
5. Both sides use configurable timeouts to prevent hanging connections

This ensures:
- **Compatibility** — only valid MIN clients can join rooms
- **Security** — malformed or unexpected connections are rejected early
- **Metadata exchange** — clients receive room info immediately upon connection

---

## Multiplayer Sessions

MIN supports downloadable game sessions that run as separate processes alongside the messenger. These sessions enable multiplayer gaming within chat rooms.

There are two kinds of sessions:

- **Process sessions** — downloaded session packages with `presenter.json`, `server.exe` and `client.exe` (described below)
- **Port tunnel sessions** — a generic TCP tunnel for running unmodified multiplayer games as-is (see [Port Tunnel Sessions](#port-tunnel-sessions))

### How Sessions Work

```mermaid
flowchart LR
    A["Download Session"] --> B["Place in<br>Скаченные сессии/"]
    B --> C["MIN scans presenter.json"]
    C --> D["Start session from chat"]
    D --> E["Server process +<br>Client process"]
    E --> F["IPC via Named Pipes<br>or TCP Loopback"]
```

### How to Install a Session

1. Download the session archive from the provided link
2. Extract it into the `Скаченные сессии/` folder located next to `MIN.exe`:

```
MIN/
├── MIN.exe
├── Скаченные сессии/
│   └── MySession/
│       ├── presenter.json
│       ├── server.exe
│       ├── client.exe
│       └── thumbnail.png
```

3. Ensure the folder contains a valid `presenter.json` file at its root
4. Restart MIN or click "Rescan" — sessions will appear in the chat room

### presenter.json Format

```json
{
  "sessionId": "your_session_id",
  "name": "SessionName",
  "description": "SessionDescription",
  "version": "1.0.0",
  "serverExecutableFileName": "Name_of_server_exe_file.exe",
  "clientExecutableFileName": "Name_of_client_exe_file.exe",
  "maximumParticipants": 5,
  "thumbnailFileName": "thumbnail.png",
  "downloadLink": "https://github.com/..."
}
```

| Field | Description |
|-------|-------------|
| `sessionId` | Unique identifier for the session (must be unique among all sessions) |
| `name` | Name displayed in the UI for the session |
| `description` | Description displayed in the UI for the session |
| `version` | Version of the session (applies to both client and server) |
| `serverExecutableFileName` | Filename of the server executable |
| `clientExecutableFileName` | Filename of the client executable |
| `maximumParticipants` | Maximum number of participants (`null` = no limit) |
| `thumbnailFileName` | Thumbnail image filename (`null` = no thumbnail) |
| `downloadLink` | URL where users can download this session |

### Session Architecture

- **Server process** is started by the room host (the session communicates via IPC)
- **Client processes** connect through MIN's message routing
- Communication between MIN and session processes uses **IPC transports** (Named Pipes or TCP Loopback)
- Sessions operate within **SubRooms** — isolated sub-contexts inside a chat room
- Version compatibility is checked before joining a session

---

## Port Tunnel Sessions

Port tunnel sessions let you play **unmodified multiplayer games** through MIN. Instead of packaging a game as a downloadable session, the host specifies the port of an already running game server, and MIN tunnels every participant's game traffic to it over the encrypted MIN transport.

### How It Works

```mermaid
flowchart LR
    A["Game client<br>connects to 127.0.0.1:port"] --> B["MIN tunnel listener<br>on participant side"]
    B -->|"Encrypted<br>MIN transport"| C["MIN tunnel<br>on host side"]
    C --> D["Game server<br>host's port"]
```

1. The **host** creates a tunnel session and specifies the port the game server listens on (e.g., `7777`), then starts the game server as usual
2. **Participants** join the session from the chat — MIN on their side starts listening on a free local port
3. The game client connects to `127.0.0.1:<port>` instead of the real server address
4. All game traffic is relayed through the encrypted MIN transport to the host, which forwards it to the actual game server port

### Creating a Tunnel Session

1. Open the session panel in the chat and click the **"Own session"** card
2. Fill in the name, description, version, maximum number of players (1–256) and the **game server port** (1024–65535)
3. Optionally add a thumbnail image (up to 2 MB)
4. Start the session from the chat like any other session

> [!TIP]
> Tunnel sessions are a lightweight alternative to virtual networks (Radmin, Hamachi) for games that connect by IP and port: participants simply point the game client at `127.0.0.1` and the session port.

---

## Screen Demo

Participants can demonstrate their screen to the whole room — a screen sharing feature built into the voice call panel.

### How to Use

1. Join a voice call in the room and click the **monitor icon** button
2. Pick a source: a whole **monitor** or an **application window**
3. Optionally enable **system sound** sharing
4. Other participants see the stream in the chat and can open it in a separate window; your own stream is available in a preview window

### Technical Details

| Parameter | Value |
|-----------|-------|
| Capture | Windows Graphics Capture (monitors and windows) |
| Video codec | OpenH264 |
| Frame rate | up to 30 FPS |
| Resolution | up to 1080p height, aspect ratio preserved |
| Target bitrate | ~3000 kbps |
| Keyframes | every 2 seconds |
| Audio | WASAPI loopback, 48 kHz stereo, silence suppression |

> [!NOTE]
> Screen capture is currently implemented for Windows only (Windows Graphics Capture + WASAPI loopback). On other platforms MIN runs with capture stubs, so starting a demo there is unavailable.

---

## Port Forwarding

> [!TIP]
> Enable port forwarding when you want participants from outside your local network to connect to your room.

MIN supports UPnP (Universal Plug and Play) port forwarding for automatic router configuration:

- **Library**: [Open.Nat](https://github.com/lontivero/Open.Nat)
- **Protocol**: UPnP
- **When creating a room**, check the **"Port Forwarding"** checkbox to enable it
- The application will attempt to discover a UPnP-enabled router and create a port mapping automatically
- If UPnP is not available on the router, the operation will fail with an explanatory message
- The public IP is resolved automatically via [api.ipify.org](https://api.ipify.org)
- Port mappings are cleaned up when the room is closed

---

## Direct Connection

You can connect to a room directly by IP address and port, bypassing local discovery. This is useful for:

- Connecting to rooms outside your local network
- Testing and debugging
- Pre-configured connections

To use direct connection:

1. Click the **"Direct Connect"** button in the Discovery panel
2. Enter the IP address and port in the format `IP:Port` (e.g., `192.168.1.100:56784`)
3. Click **Connect** — the client will attempt the MIN protocol handshake automatically

---

## Authors

| Author | Role | Contribution |
|--------|------|--------------|
| [**CasCadeVR**](https://github.com/CasCadeVR) | Founder | Lead developer, architecture creator |
| [**Karo4a**](https://github.com/Karo4a) | Inspiration, Tester | Ideas and inspiration, smoke and unit testing |

---

> [!TIP]
> **Made with love by CasCade team**

*MIN — Distributed messenger*

---

[![Stars](https://img.shields.io/github/stars/MIN-Corp/MIN?style=social)](https://github.com/MIN-Corp/MIN)
[![Forks](https://img.shields.io/github/forks/MIN-Corp/MIN?style=social)](https://github.com/MIN-Corp/MIN)

[Source Code](https://github.com/MIN-Corp/MIN) • 
[Report an Issue](https://github.com/MIN-Corp/MIN/issues) • 
[MIT License](LICENSE)