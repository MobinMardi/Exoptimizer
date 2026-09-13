# Exoptimizer Changelog

## Version 3.0.0
### 🛡️ Critical Safety Fixes
This release fixes the root cause of the most common issues reported against 2.1.x: **restore points failing to create or apply, System Restore no longer working, and the network adapter/service failing to start after optimizing.** All of it traced back to the same mistake - several optimization presets were setting critical Windows services (System Restore's VSS/swprv, and the network stack's nsi/NlaSvc/iphlpsvc/RasMan) to "Disabled" instead of "Manual", which stops Windows itself from ever starting them again.

- **Protected services list** - System Restore, core networking, and firewall/security services can no longer be disabled by *any* optimization mode, at any setting. See Settings > System Restore > "What does Exoptimizer never touch?" for the full list.
- **"Manual" instead of "Disabled"** - every other service Exoptimizer adjusts is now set to start on demand rather than disabled outright, so Windows (or anything that depends on it) can still start it if it's genuinely needed.
- **Accurate Undo** - "Undo Optimizations" now restores every service Exoptimizer has ever changed back to its actual original start type, instead of a hardcoded list of 6 services that left most changes in place.
- **Reliable restore points** - restore point creation now also enables System Protection if it's off and clears Windows' 24-hour throttle on new restore points, both of which could silently cause "Create Restore Point" to do nothing.
- **New: Repair Critical Services** - a one-click tool (System Restore tab) for machines that already have services stuck disabled from an older Exoptimizer version.
- **Windows Defender handling simplified** - "Extreme Optimization" no longer disables the Defender driver services (WinDefend/WdBoot/WdFilter) directly; it uses only the standard, fully-reversible Group Policy registry setting, the same one IT admins use.
- **Network tuning cleaned up** - removed several TCP tweaks that were either removed from Windows years ago (silently doing nothing) or actively hurt throughput on modern hardware, and removed the blind per-adapter MTU override that could break VPN/PPPoE connections.

### 🎨 UI/UX Improvements
- Long-running operations (Optimize, Extreme Optimize, Undo, Repair) now show a live progress dialog instead of freezing the window with no feedback.
- Optimization and Extreme Optimization now share one confirmation flow and one results dialog instead of two overlapping dialogs.
- Added a "what's protected" banner on the System Optimization tab and a full protected-services list under System Restore.
- Completion dialogs now report whether a restore point actually succeeded, instead of assuming it did.
- Minor copy fixes (e.g. "Battery" typo) and clearer checkbox/warning wording.

## Version 2.1.3
### 📋 Icon Change
- **App Icon Updated** - New Exoptimizer App Icon


## Version 2.1.2
### 🔥 NEW FEATURE: Extreme Optimization Mode
- **Extreme Optimization Button** - New red-themed button for maximum FPS boost
- **Aggressive Service Disabling** - Disables 50+ unnecessary Windows services
- **Complete Visual Effects Removal** - Removes all Windows animations and effects
- **Advanced Registry Tweaks** - Extreme CPU scheduling and memory optimizations
- **Windows Defender Complete Disable** - Fully disables real-time protection for gaming
- **Background Apps Termination** - Stops all non-essential background processes
- **Network Gaming Optimization** - Advanced network settings for competitive gaming
- **Startup Program Management** - Disables resource-heavy startup applications

### ⚠️ Safety & Warning System
- **Automatic Restore Point Creation** - Creates restore point before extreme changes
- **Enhanced Warning Messages** - Clear warnings about extreme optimization risks
- **Improved Undo Functionality** - Better rollback for extreme optimizations
- **System Stability Monitoring** - Recommendations for safe usage

### 🎮 Gaming Performance Enhancements
- **VALORANT-Specific Optimizations** - Tailored tweaks for competitive VALORANT
- **FPS Boost Prioritization** - Focus on frame rate improvements
- **Input Lag Reduction** - Minimized system latency for competitive gaming
- **Memory Management Optimization** - Advanced RAM allocation for games

### 🛠️ Technical Improvements
- **Enhanced Error Handling** - Better error management for extreme operations
- **Improved Service Management** - More reliable service start/stop operations
- **Registry Safety Checks** - Additional validation for registry modifications
- **Performance Monitoring Integration** - Real-time feedback during optimizations

### 📋 User Interface Updates
- **Red Theme Integration** - Danger-themed styling for extreme optimization
- **Enhanced Warning Labels** - Clear visual indicators for risky operations
- **Improved Button Layout** - Better organization of optimization options
- **Status Message Updates** - More informative feedback during operations

---

## Version 2.0.2
### 🐛 Bug Fixes & Improvements
- Fixed application and system tray icon loading to properly use custom icon.ico
- Improved icon path resolution with fallback mechanisms
- Updated all version references throughout the application
- Enhanced icon loading reliability across all forms and system tray

---

## Version 2.0.1
### 🐛 Bug Fixes & Minor Improvements
- Renamed VALORANT priority button variables for better code clarity
- Internal code cleanup and improved variable naming conventions
- Simplified system tray implementation using main application icon

---

## Version 2.0.0
### 🎨 Major UI Overhaul
- Complete redesign with modern Windows styling
- Dark mode support with Onyx Black theme and Discord Blurple accents
- Responsive tabbed navigation interface
- Transparent card design for cleaner appearance
- Improved typography and color scheme with better accessibility

### ⚙️ New Features
- **Settings Persistence** - Preferences saved between sessions
- **System Tray Integration** - Minimize to tray with proper icon support
- **Real-time System Monitoring** - Live CPU, memory, and network statistics
- **Deep System Cleanup** - Advanced cleanup tool for temporary files and cache
- **Battery Optimization Mode** - Extended battery life settings for laptops
- **Riot Vanguard Integration** - One-click service start functionality
- **Enhanced Process Monitoring** - Real-time VALORANT and Riot Client tracking
- **Automatic Optimization Detection** - Smart detection of applied optimizations

### 🔧 Enhanced Functionality
- Better admin rights checking with improved error handling
- Expanded service optimization with comprehensive management
- Network latency testing to multiple gaming servers
- Performance tools integration for Windows system utilities
- Registry optimization improvements with safer, targeted tweaks
- Power management enhancements for performance and battery modes

### 🛠️ Technical Improvements
- Upgraded to .NET 6.0 for better performance
- Improved error handling throughout the application
- Better memory management and resource cleanup
- Enhanced security with proper privilege handling
- Optimized startup time and reduced resource usage
- Better compatibility with Windows 10 version 2004+ and Windows 11

### 🎮 Gaming Enhancements
- VALORANT priority management with QoS policy integration
- Enhanced Windows Defender exclusions for gaming directories
- Better process priority handling for gaming applications
- Improved system restore integration for safer optimization rollback

### 🐛 Bug Fixes
- Fixed message box dialog handling issues
- Resolved optimization state detection problems
- Improved service start/stop reliability
- Fixed memory leaks in monitoring components
- Better handling of missing system components
- Resolved UI scaling issues on different DPI settings

### 📦 Installation & Distribution
- New installer with Inno Setup for professional installation experience
- Automatic dependency handling with bundled .NET runtime
- System restore point creation during installation
- Proper uninstallation with cleanup of settings and temporary files

---

## Version 1.1.0 (Previous Release)
### Legacy Features
- Basic system optimization functionality
- VALORANT priority boosting
- Gaming Network Mode with firewall rules
- Simple Windows UI with basic styling
- Manual optimization controls
- Basic system restore integration
- Windows Defender management
- Registry tweaks for gaming performance

---

## Migration Guide (v1.1 → v2.0+)

### What's New:
- All settings are now persistent across restarts
- Dark mode available for better night gaming experience
- System tray support for background operation
- Real-time monitoring dashboard
- One-click deep system cleanup
- Better organized features in logical tabs

### Breaking Changes:
- Settings file format updated (automatic migration)
- Registry keys locations updated for better compatibility
- Network optimization commands updated for Windows 11

### Upgrade Steps:
1. Create a system restore point before upgrading
2. Export current settings if you have custom configurations
3. Run as Administrator for full functionality
4. Restart system after first-time setup for optimal performance

---

*For support and updates, visit: https://mobinmardi.github.io/*
