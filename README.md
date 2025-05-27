# ClaudeCodeManager

Windows-based Claude Code management system that allows remote control of WSL terminals through a web interface.

## System Overview

ClaudeCodeManager consists of two main components:

1. **Console Application (IPC Server)**: Manages WSL startup for terminals 1-5, handles incoming WSL requests, and launches corresponding ttyd aliases (ttyd1-ttyd5) on ports 7681-7685
2. **ASP.NET Web Application (IPC Client)**: Provides web interface for monitoring and controlling WSL terminal states, with redirect functionality to active terminal ports

The system uses IPC (Inter-Process Communication) where the console application runs as a background service managing WSL instances, while the web application communicates with the console app to request WSL startup and monitor status.

## Requirements

- Windows 10/11 with WSL2 installed and configured
- .NET 9 Runtime
- ttyd1-ttyd5 aliases configured in WSL
- Administrator privileges may be required for Named Pipe access
- Tailscale for secure remote access

## Security & Hosting

This system leverages **Tailscale** for security and hosting, making setup remarkably simple:

- **Zero-config security**: Tailscale handles authentication, encryption, and network access
- **No port forwarding**: No need to configure firewalls or expose ports publicly
- **Simple remote access**: Access your terminals securely from anywhere on your tailnet
- **Built-in HTTPS**: Tailscale serve provides automatic HTTPS termination

## Setup Instructions

### 1. WSL Environment Setup

First, ensure WSL2 is installed and configure the ttyd aliases. Add these to your WSL ~/.bashrc or ~/.bash_aliases:

```bash
alias ttyd1='ttyd -p 7681 -t fontSize=20 --writable tmux new -A -s ttysession1'
alias ttyd2='ttyd -p 7682 -t fontSize=20 --writable tmux new -A -s ttysession2'
alias ttyd3='ttyd -p 7683 -t fontSize=20 --writable tmux new -A -s ttysession3'
alias ttyd4='ttyd -p 7684 -t fontSize=20 --writable tmux new -A -s ttysession4'
alias ttyd5='ttyd -p 7685 -t fontSize=20 --writable tmux new -A -s ttysession5'
```
If you want to change the font size, change here. fontSize=20 -> fontSize=16(Default is 14).

Verify the aliases work:
```bash
wsl -e bash -c 'command -v ttyd1'
```

### 2. Application Setup

1. Download or build the applications (see Build Instructions below)
2. Install and configure Tailscale on your Windows machine
3. Run the console application (server) first - may need Administrator privileges
4. Start the web application
5. Use the provided PowerShell scripts to expose services via Tailscale:
   ```powershell
   .\tailscale-start.ps1
   ```
6. Access the web interface securely from anywhere via your Tailscale machine URL

## Usage

### Web Interface

The web interface provides:
- Start buttons for inactive terminals (1-5)
- Redirect links for active terminals
- Real-time status monitoring of WSL terminal states

### Terminal Management

- Each terminal corresponds to ports 7681-7685
- 5-second delay after WSL startup before launching ttyd aliases
- System prevents duplicate WSL instances
- Terminals can be started individually through the web interface

## Tailscale Remote Access

For remote access via Tailscale, use the provided PowerShell scripts:

```powershell
# Start all services
.\tailscale-start.ps1

# Stop all services
.\tailscale-stop.ps1
```

Manual Tailscale commands:
```powershell
# Expose web app remotely
tailscale serve --bg --https=443 https+insecure://localhost:7073

# Expose each terminal port
tailscale serve --bg --https=7681 localhost:7681
tailscale serve --bg --https=7682 localhost:7682
tailscale serve --bg --https=7683 localhost:7683
tailscale serve --bg --https=7684 localhost:7684
tailscale serve --bg --https=7685 localhost:7685

# Check status
tailscale serve status

# Reset all services
tailscale serve reset
```

## Build Instructions

### Development
```bash
# Build solution
dotnet build

# Run server
dotnet run --project ClaudeCodeManager.Server

# Run web app
dotnet run --project ClaudeCodeManager.Web

# Build Tailwind CSS
cd ClaudeCodeManager.Web && npm run build-css
```

### Production (Windows deployment)
```bash
# Publish server
dotnet publish ClaudeCodeManager.Server -c Release -o publish/server

# Publish web app
dotnet publish ClaudeCodeManager.Web -c Release -o publish/web
```

Copy the published files to your Windows machine and run the .exe files.

## Troubleshooting

### WSL Startup Issues
- Ensure WSL2 is properly installed and configured
- Verify ttyd1-ttyd5 aliases exist in WSL
- Check server console output for detailed error messages
- Verify Named Pipe permissions allow communication between applications

### Development Environment
- Can be developed and tested in WSL/Linux environment
- Windows-specific features will show warnings but won't crash
- For full functionality testing, deploy to actual Windows environment

## Bonus: Mac User Alternative

**注：Mac環境でのテストは行っていないため、あくまでおまけ情報です**  
**Note: This is bonus information only - not tested on Mac environment**

Mac users can achieve similar terminal sharing functionality using the same techniques:

### Manual Terminal Setup (Mac/Linux)

Set up ttyd aliases in your ~/.bashrc or ~/.zshrc:

```bash
# Mac/Linux ttyd aliases
alias ttyd1='ttyd -p 7681 -t fontSize=20 --writable tmux new -A -s ttysession1'
alias ttyd2='ttyd -p 7682 -t fontSize=20 --writable tmux new -A -s ttysession2'
alias ttyd3='ttyd -p 7683 -t fontSize=20 --writable tmux new -A -s ttysession3'
alias ttyd4='ttyd -p 7684 -t fontSize=20 --writable tmux new -A -s ttysession4'
alias ttyd5='ttyd -p 7685 -t fontSize=20 --writable tmux new -A -s ttysession5'
```

### Automated Startup Script

Create a startup script for easy deployment:

```bash
#!/bin/bash
# start-terminals.sh

echo "Starting ttyd terminals..."

# Start ttyd sessions in background
ttyd1 &
ttyd2 &
ttyd3 &
ttyd4 &
ttyd5 &

echo "All terminals started on ports 7681-7685"
echo "Access via http://localhost:7681-7685"

# Optional: Setup Tailscale serve
if command -v tailscale &> /dev/null; then
    echo "Setting up Tailscale serve..."
    tailscale serve --bg --https=7681 localhost:7681
    tailscale serve --bg --https=7682 localhost:7682
    tailscale serve --bg --https=7683 localhost:7683
    tailscale serve --bg --https=7684 localhost:7684
    tailscale serve --bg --https=7685 localhost:7685
    echo "Tailscale serve configured for all ports"
fi
```

### Alternative Remote Access Options

For users without Tailscale or using Chrome Remote Desktop:

**Option 1: Local Network Access**
```bash
# Bind to all interfaces (use with caution on trusted networks)
alias ttyd1='ttyd -p 7681 -i 0.0.0.0 -t fontSize=20 --writable tmux new -A -s ttysession1'
```

**Option 2: Chrome Remote Desktop Integration**
```bash
#!/bin/bash
# chrome-remote-setup.sh

# Start terminals locally
./start-terminals.sh

# Open Chrome with terminal tabs
if command -v google-chrome &> /dev/null; then
    google-chrome \
        "http://localhost:7681" \
        "http://localhost:7682" \
        "http://localhost:7683" \
        "http://localhost:7684" \
        "http://localhost:7685" &
elif command -v open &> /dev/null; then
    # macOS Safari
    open "http://localhost:7681"
    open "http://localhost:7682"
    open "http://localhost:7683"
    open "http://localhost:7684"
    open "http://localhost:7685"
fi

echo "Terminals ready for Chrome Remote Desktop access"
```

**Option 3: Complete Automation Script**
```bash
#!/bin/bash
# full-setup.sh

# Install ttyd if not present
if ! command -v ttyd &> /dev/null; then
    echo "Installing ttyd..."
    # macOS
    if command -v brew &> /dev/null; then
        brew install ttyd
    # Linux
    elif command -v apt &> /dev/null; then
        sudo apt update && sudo apt install ttyd
    fi
fi

# Install tmux if not present
if ! command -v tmux &> /dev/null; then
    echo "Installing tmux..."
    if command -v brew &> /dev/null; then
        brew install tmux
    elif command -v apt &> /dev/null; then
        sudo apt update && sudo apt install tmux
    fi
fi

# Start terminals
./start-terminals.sh

echo "Setup complete! Access terminals via:"
echo "- Local: http://localhost:7681-7685"
echo "- Tailscale: https://$(hostname).your-tailnet.ts.net:7681-7685"
echo "- Chrome Remote Desktop: Connect and use local URLs"
```

### Usage Tips for Mac Users

1. **Pre-setup for Remote Work**: Run the automation script before leaving the office
2. **Tailscale Integration**: Install Tailscale for secure remote access without VPN complexity
3. **Chrome Remote Desktop**: Ideal for accessing from non-Tailscale devices
4. **Session Persistence**: tmux ensures your work persists across disconnections