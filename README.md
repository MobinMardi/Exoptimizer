<div align="center">
  <a href="\assets\icon-new.ico" target="_blank">
    <img width="180" src="\assets\icon-new.ico" alt="Exoptimizer">
  </a>
</div>

<h1 align="center">Exoptimizer</h1>

<div align="center">

  [![Version](https://img.shields.io/badge/version-3.0.1-blue.svg)](https://github.com/MobinMardi/Exoptimizer/releases)
  [![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)
  [![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey.svg)]()
  [![.NET](https://img.shields.io/badge/.NET-6.0-purple.svg)]()

</div>

**Exoptimizer** is a comprehensive Windows gaming optimization tool designed to maximize system performance for gaming. With an intuitive modern interface, real-time monitoring, advanced optimization features, and Extreme Optimization mode for maximum FPS boost, it provides everything needed to enhance your gaming experience.

## ✨ Features

### 🔧 System Optimization
- **Intelligent Service Management**: Automatic optimization detection and smart service control
- **Network Settings Optimization**: Reduced latency and improved connection stability
- **Registry Tweaks**: Enhanced gaming performance through system-level optimizations
- **Power Management**: Performance or battery optimization modes
- **Windows Defender Exclusions**: Gaming directory protection from real-time scanning
- **Extreme Optimization**: Maximum FPS boost with aggressive system tweaks
- **Quick Boost**: Fast performance enhancement for immediate gaming sessions

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
- **Automatic Restore Points**: System backup before applying optimizations, with reliability fixes so creation doesn't silently fail
- **One-click System Restore**: Quick recovery access for system issues
- **Comprehensive Rollback**: "Undo Optimizations" restores every changed service to its exact original state, not a hardcoded guess
- **Repair Critical Services**: One-click fix for machines upgraded from an older Exoptimizer version
- **Safe Optimization Detection**: Prevents duplicate optimization applications
- **Extreme Optimization Detection**: Smart detection of already applied extreme tweaks

### ⚙️ Advanced Settings
- **Dark Mode**: Onyx Black theme with Discord Blurple accents
- **System Tray Integration**: Minimize-to-tray functionality with background monitoring
- **Settings Persistence**: Configuration saved across application restarts
- **Customizable Interface**: Appearance and behavior customization options
- **Automatic Update Checker**: Checks GitHub for a newer release on startup (with a manual "Check for Updates" button too) and notifies you without any intrusive popups
- **In-App Changelog Viewer**: Browse release notes for the current and previous versions directly inside the app

## 📋 System Requirements

- **Operating System**: Windows 10 version 2004 (build 19041) or later / Windows 11
- **Architecture**: 64-bit processor and operating system
- **Privileges**: Administrator privileges required for full functionality
- **Runtime**: .NET 6.0 Runtime (included in installer)
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
- **Quick Boost**: Fast performance enhancement for immediate gaming
- **Extreme Optimization**: Maximum FPS boost with aggressive tweaks (advanced users)
- **Longer Battery**: Power-saving optimizations for laptop gaming

### ⚠️ Important Safety Notes
- Always run Exoptimizer as Administrator for complete functionality
- Create a system restore point before applying optimizations
- Restart your computer after applying system-level optimizations
- Use "Undo Optimizations" if you experience any system issues
- **Extreme Optimization** is recommended only for experienced users
- Keep Windows and drivers updated for optimal compatibility

## 🔧 Building from Source

```bash
git clone https://github.com/MobinMardi/Exoptimizer.git
cd Exoptimizer
dotnet restore
dotnet build --configuration Release
```

### Development Requirements
- Visual Studio 2022 or Visual Studio Code
- .NET 6.0 SDK
- Windows 10/11 development environment
- Administrator privileges for testing

## 🔍 Troubleshooting

### Common Issues
- **Optimizations don't apply**: Ensure running as Administrator
- **System becomes unstable**: Use "Undo Optimizations" or System Restore
- **VALORANT priority fails**: Restart VALORANT and try again
- **Monitoring shows errors**: Check Windows services and permissions
- **Settings don't save**: Verify write permissions in application directory

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
- Built with .NET 6.0 and Windows Forms for optimal performance

---

⭐ **Star this repository if you find it helpful!**

**Version**: 3.0.1 | **Developer**: mDev (Mobin Mardi) | **Copyright**: © 2026 - Licensed under MIT License
