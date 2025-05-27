# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Windows-based Claude Code management system that allows remote control of WSL terminals through a web interface. The system consists of two main components:

1. **Console Application (IPC Server)**: Manages WSL startup for terminals 1-5, handles incoming WSL requests, and launches corresponding ttyd aliases (ttyd1-ttyd5) on ports 7681-7685
2. **ASP.NET Web Application (IPC Client)**: Provides web interface for monitoring and controlling WSL terminal states, with redirect functionality to active terminal ports

## Technical Requirements

- **.NET 9**: All applications must use .NET 9
- **Tailwind CSS**: Use Tailwind for styling the web application
- **WSL Integration**: System manages WSL terminals through alias commands (ttyd1-ttyd5)
- **Port Management**: Each terminal uses sequential ports 7681-7685
- **IPC Communication**: Console app acts as server, web app as client
- **ACL/Permissions**: Pay attention to Windows access control and permissions

## Architecture

The system uses IPC (Inter-Process Communication) where:
- Console application runs as a background service managing WSL instances
- Web application communicates with console app to request WSL startup and monitor status
- 5-second delay after WSL startup before launching ttyd aliases
- Web interface provides start buttons for inactive terminals and redirect links for active ones

## Development Notes

- **Target platform**: Windows with WSL2 integration (Windows 10/11 required)
- Terminal numbering: 1-5 corresponding to ttyd1-ttyd5 aliases
- Port mapping: Terminal N uses port 768N (e.g., terminal 1 → port 7681)
- Prevent duplicate WSL instances - check if terminal is already running before starting
- Applications are compiled as Windows x64 self-contained executables

## Build Commands

### Development (Cross-platform)
- Build solution: `dotnet build`
- Run server: `dotnet run --project ClaudeCodeManager.Server`
- Run web app: `dotnet run --project ClaudeCodeManager.Web`
- Build Tailwind CSS: `cd ClaudeCodeManager.Web && npm run build-css`

### Production (Windows deployment)
- Publish server: `dotnet publish ClaudeCodeManager.Server -c Release -o publish/server`
- Publish web app: `dotnet publish ClaudeCodeManager.Web -c Release -o publish/web`
- Copy to Windows machine and run the .exe files

## Setup Requirements

### Windows Environment
1. Windows 10/11 with WSL2 installed and configured
2. .NET 9 Runtime (or use self-contained deployment)
3. Ensure ttyd1-ttyd5 aliases are configured in WSL
4. Run the console application (server) as Administrator if needed for Named Pipe access
5. Web application will be available at https://localhost:7073 (or configured port)

### Tailscale Remote Access Setup
1. Install and configure Tailscale on the Windows machine
2. Ensure Tailscale is running and connected to your tailnet
3. Use Tailscale serve commands to expose the application remotely

#### Tailscale Serve Commands

**PowerShellスクリプト（推奨）:**
```powershell
# すべてのサービス開始
.\tailscale-start.ps1

# すべてのサービス停止
.\tailscale-stop.ps1
```

**手動コマンド（PowerShell）:**
```powershell
# Webアプリをリモート公開 (https://localhost:7073 -> https://machine-name.tail-xxxxx.ts.net)
tailscale serve --bg --https=443 https+insecure://localhost:7073

# 各ttydポートをリモート公開
tailscale serve --bg --https=7681 localhost:7681
tailscale serve --bg --https=7682 localhost:7682  
tailscale serve --bg --https=7683 localhost:7683
tailscale serve --bg --https=7684 localhost:7684
tailscale serve --bg --https=7685 localhost:7685

# すべてのサービス確認
tailscale serve status

# サービス停止
tailscale serve reset
```

### Development Environment (WSL/Linux)
- Can be developed and tested in WSL/Linux environment
- Windows-specific features (WSL control) will show warnings but won't crash
- For full functionality testing, deploy to actual Windows environment

## Troubleshooting

### WSL Startup Issues
- Ensure WSL is properly installed and configured
- Verify ttyd1-ttyd5 aliases exist in WSL: `wsl -e bash -c 'command -v ttyd1'`
- Check server console output for detailed error messages
- Verify Named Pipe permissions allow communication between applications

### Common ttyd Alias Setup
Add to WSL ~/.bashrc or ~/.bash_aliases:
```bash
alias ttyd1='ttyd -p 7681 bash'
alias ttyd2='ttyd -p 7682 bash'
alias ttyd3='ttyd -p 7683 bash'
alias ttyd4='ttyd -p 7684 bash'
alias ttyd5='ttyd -p 7685 bash'
```