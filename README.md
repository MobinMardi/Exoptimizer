<div align="center">
  <a href="\assets\icon-new.ico" target="_blank">
    <img width="180" src="\assets\icon-new.ico" alt="Exoptimizer">
  </a>
</div>

<h1 align="center">Exoptimizer</h1>

<div align="center">

  [![Version](https://img.shields.io/badge/version-3.2.0-blue.svg)](https://github.com/MobinMardi/Exoptimizer/releases)
  [![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)
  [![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey.svg)]()
  [![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)]()

</div>

**Exoptimizer** is a comprehensive Windows gaming optimization tool designed to maximize system performance for gaming. With an intuitive modern interface, real-time monitoring, advanced optimization features, and Extreme Optimization mode for maximum FPS boost, it provides everything needed to enhance your gaming experience.

## ✨ Features

### 🔧 System Optimization
- **Intelligent Service Management**: Automatic optimization detection and smart service control
- **Network Settings Optimization**: Reduced latency and improved connection stability
- **Registry Tweaks**: Enhanced gaming performance through system-level optimizations
- **Power Management**: Performance or battery optimization modes
- **Optional Windows Defender Exclusions**: Off by default - excludes your existing Riot Games folders from scans, shows the exact folders first, and Undo removes only what Exoptimizer added
- **Extreme Optimization**: Maximum FPS boost with aggressive system tweaks
- **Quick Boost**: Switches to the High performance power plan and clears the DNS cache (Undo restores your plan)

### 🎯 VALORANT Integration
- **Process Priority Boosting**: High/RealTime priority options for VALORANT
- **Network QoS Policy**: Prioritized game traffic management
- **Riot Vanguard Management**: One-click service start and monitoring
- **Real-time Process Monitoring**: Live tracking of VALORANT and Riot Client status

### 📊 System Monitoring
- **Real-time Performance Tracking**: Live CPU, memory, and network usage monitoring
- **Network Latency Testing**: Multi-server ping testing for gaming connections
- **Performance Dashboard**: Color-coded alerts and system health indicators
- **Process Status Monitoring**: Gaming application tracking and management

### 🛠️ Performance Tools
- **Windows System Utilities**: Quick access to Task Manager, Resource Monitor, and system tools
- **Deep System Cleanup**: Comprehensive maintenance and optimization
- **Registry Editor Integration**: Direct access to system configuration
- **Services Management**: Advanced Windows service control

### 💾 System Safety
- **Protected Services**: System Restore, networking, and firewall services can never be disabled by any optimization mode - see [CHANGELOG](docs/CHANGELOG.md#version-300) for the full list
- **Verified Restore Points**: A restore point is created and then confirmed before optimizing; if it can't be created nothing changes unless you explicitly choose to continue
- **One-click System Restore**: Quick recovery access for system issues
- **Exact Rollback**: Every service, registry value, power and TCP setting is recorded before it is changed, and "Undo Optimizations" restores exactly that - not a hardcoded guess
- **Repair Critical Services**: One-click fix for machines upgraded from an older Exoptimizer version
- **Safe Optimization Detection**: Prevents duplicate optimization applications
- **Extreme Optimization Detection**: Smart detection of already applied extreme tweaks

### ⚙️ Advanced Settings
- **Light & Dark Themes**: A minimal, refined interface with a Discord-style blurple accent - pale and airy in light mode, deep ink in dark mode - applied to every window and dialog
- **Splash Screen**: A short branded start-up screen (app icon and name) in the active theme
- **System Tray Integration**: Minimize-to-tray functionality with background monitoring
- **Settings Persistence**: Configuration saved across application restarts
- **Customizable Interface**: Appearance and behavior customization options
- **Automatic Update Checker**: Checks GitHub for a newer release on startup (with a manual "Check for Updates" button too) and notifies you without any intrusive popups
- **In-App Changelog Viewer**: Browse release notes for the current and previous versions directly inside the app

## 📋 System Requirements

- **Operating System**: Windows 10 version 2004 (build 19041) or later / Windows 11
- **Architecture**: 64-bit processor and operating system
- **Privileges**: Administrator privileges required for full functionality
- **Runtime**: .NET 10 Runtime (bundled with the app - nothing separate to install)
- **Display**: 1280 x 800 or larger recommended (the window has a fixed size of 1180 x 720)
- **Memory**: Minimum 4GB RAM recommended
- **Storage**: 100MB free disk space for installation

## 🚀 Installation

1. Download the latest installer from [Releases](https://github.com/MobinMardi/Exoptimizer/releases)
2. Right-click the installer and select **"Run as administrator"**
3. Follow the installation wizard with recommended settings
4. Choose installation options (desktop icon, startup, restore point)
5. Launch Exoptimizer from Start Menu or Desktop shortcut
6. Configure your preferences in the Settings tab

## 📖 Usage Guide

### 🎮 Recommended Workflow
1. **Create System Restore Point** in "System Restore" tab
2. **Apply Regular Optimizations** in "System Optimization" tab
3. **Use Extreme Optimization** for maximum FPS boost (advanced users only)
4. **Enable VALORANT Priority** in "VALORANT Priority" tab when gaming
5. **Monitor System Performance** in "System Monitor" tab
6. **Use Performance Tools** for additional system management

### 🔧 Optimization Modes
- **Optimize System**: Standard gaming optimizations for most users
- **Quick Boost**: High performance power plan + DNS cache flush for immediate gaming
- **Extreme Optimization**: Maximum FPS boost with aggressive tweaks (advanced users)
- **Longer Battery**: Power-saving optimizations for laptop gaming

### ⚠️ Important Safety Notes
- Always run Exoptimizer as Administrator for complete functionality
- Exoptimizer creates **and verifies** a system restore point before optimizing, and will not continue without your explicit approval if it cannot
- Every change is recorded (original values) in `%ProgramData%\Exoptimizer\journal.json`, so "Undo Optimizations" restores exactly what was there
- Windows Defender changes (real-time protection, folder exclusions) are separate, opt-in toggles that are **off by default**
- Restart your computer after applying system-level optimizations
- Use "Undo Optimizations" if you experience any system issues
- **Extreme Optimization** is recommended only for experienced users
- Keep Windows and drivers updated for optimal compatibility

## 🔧 Building from Source

```powershell
git clone https://github.com/MobinMardi/Exoptimizer.git
cd Exoptimizer

# Build and run the tests
dotnet restore src/Exoptimizer.csproj
dotnet build src/Exoptimizer.csproj -c Release
dotnet test tests/Exoptimizer.Tests/Exoptimizer.Tests.csproj -c Release

# Publish the self-contained single-file app (used by the installer)
dotnet publish src/Exoptimizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Build the installer (needs Inno Setup 6): output goes to installer/output/
iscc installer/exoptimizer_installer.iss
```

### Development Requirements
- Visual Studio 2022 or Visual Studio Code
- .NET 10 SDK
- Inno Setup 6 (only to build the installer)
- Windows 10/11 development environment
- Administrator privileges for testing

## 🔐 Verifying Your Download

Each release publishes the installer and the app together with a `SHA256SUMS.txt` file and a GitHub build-provenance attestation, so you can check that a file was built from this repository by the project's release workflow:

```powershell
# Compare the hash with SHA256SUMS.txt
Get-FileHash .\Exoptimizer-v3.2.0.exe -Algorithm SHA256

# Verify the build attestation (needs the GitHub CLI)
gh attestation verify .\Exoptimizer-v3.2.0.exe -R MobinMardi/Exoptimizer
```

## 🔍 Troubleshooting

### Common Issues
- **Optimizations don't apply**: Ensure running as Administrator
- **System becomes unstable**: Use "Undo Optimizations" or System Restore
- **VALORANT priority fails**: Restart VALORANT and try again
- **Monitoring shows errors**: Check Windows services and permissions
- **Settings don't save**: Verify write permissions in application directory
- **Undo says "nothing to undo"**: changes made by versions before 3.2.0 were not recorded in detail - use a System Restore point, or "Repair Critical Services"

### Support Resources
- Check Windows Event Viewer for detailed error logs
- Try "Reset to Defaults" in Settings before contacting support
- Create multiple restore points for different optimization configurations

## 💡 Pro Tips

- **Enable dark mode** for better gaming ambiance and reduced eye strain
- **Use system tray mode** to keep monitoring active without desktop clutter
- **Run Deep Cleanup weekly** for optimal system performance
- **Monitor network latency** during gaming sessions for connection quality
- **Use Extreme Optimization only when gaming** for maximum FPS
- **Regular optimization is sufficient** for most users
- **Extreme mode is recommended** for competitive VALORANT players

## 🔒 Security & Privacy

- **No Data Collection**: No telemetry or user data collection
- **Local Storage**: All settings stored locally with no cloud synchronization
- **Transparent Operations**: All optimizations are clearly documented and reversible
- **Minimal Footprint**: Efficient resource usage with minimal system impact
- **Open Source**: Full transparency with source code availability

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for detailed version history and update notes.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### How to Contribute
1. Fork the repository
2. Create a feature branch (\`git checkout -b feature/AmazingFeature\`)
3. Commit your changes (\`git commit -m 'Add some AmazingFeature'\`)
4. Push to the branch (\`git push origin feature/AmazingFeature\`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 👨‍💻 Author

**mDev (Mobin Mardi)**
- GitHub: [@MobinMardi](https://github.com/MobinMardi)
- Website: [mobinmardi.github.io](https://mobinmardi.github.io/)

## 🙏 Acknowledgments

- Thanks to the gaming community for feedback and suggestions
- Special thanks to VALORANT players for testing and optimization insights
- Built with .NET 10 and Windows Forms for optimal performance

---

⭐ **Star this repository if you find it helpful!**

**Version**: 3.2.0 | **Developer**: mDev (Mobin Mardi) | **Copyright**: © 2026 - Licensed under MIT License
