# OS Deployment Assistant

![OS Deployment Assistant](https://img.shields.io/badge/version-2.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-6.0-purple)
![WPF](https://img.shields.io/badge/UI-WPF-green)

> Automated Windows OS Deployment Tool with SCCM and ServiceNow Integration

## 📋 Overview

OS Deployment Assistant is a modern Windows application that automates the deployment of operating systems through SCCM (System Center Configuration Manager) and synchronizes with ServiceNow for asset tracking and ticketing.

## ✨ Features

- **🚀 Automated Deployment**: One-click OS deployment through SCCM
- **🔄 ServiceNow Integration**: Automatic RITM ticket creation and synchronization
- **📊 Live Monitoring**: Real-time tracking of deployment tickets with auto-close after 45 minutes
- **🔧 Remote Execution**: Execute post-installation scripts on local or remote machines
- **⚙️ Automation**: Firewall, timezone, keyboard, and registry configuration
- **📝 Asset Naming**: Automatic asset naming with customizable prefixes and suffixes
- **🎯 MAC Address Validation**: Multi-line MAC address input with auto-formatting
- **🖥️ Modern UI**: Clean, modern interface with glass-morphism design

## 🚀 Quick Start

### Prerequisites

- Windows 10/11
- .NET 6.0 Runtime or SDK
- Administrator privileges (for some features)

### Installation

1. **Download the latest release**
   - Go to [Releases](https://github.com/JasserBA/OSDeploymentAssistant/releases)
   - Download the `OSDeploymentAssistant.exe`

2. **Run the application**

   ```bash
   OSDeploymentAssistant.exe
   ```

3. **Or build from source**
   ```bash
   git clone https://github.com/YOUR_USERNAME/OSDeploymentAssistant.git
   cd OSDeploymentAssistant
   dotnet build
   dotnet run
   ```

### 🖥️ Usage

1. **Configure Deployment**

- Select Infrastructure Node (LTN/TN4)

- Choose Target Operating System

- Set Device Profile Template

- Add MAC Addresses (one per line)

- Preview Asset Name

2. **Create SCCM Asset**

- Click "Create SCCM Asset & Sync ServiceNow"

- RITM tickets will be generated automatically

- Tickets appear in Live Tracking tab

3. **Run Automation**

- Choose Local or Remote execution

- Select automation options:

- Firewall rules

- Timezone settings

- Keyboard layout

- Registry fixes

- SCCM policy triggers

- AD updates

4. **Monitor Progress**

- Track active tickets in Live Tracking tab

- Tickets auto-close after 45 minutes

- Real-time status updates
