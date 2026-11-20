# Exoptimizer Changelog

## Version 3.2.1
### 🌐 Updates now point to the official website
- **Download from the website** - when a new version is found, "Check for Updates" now offers to open the official Exoptimizer website (https://mobinmardi.github.io/exoptimizer/) instead of the GitHub releases page.
- **Update check unchanged** - the check that runs quietly at start-up and when you press "Check for Updates" still reads the latest release information directly from GitHub, so new versions are detected exactly as before; only the download link changed.
- **Clearer wording** - the update prompt now says it opens the website to download the update.
- The installer's "updates" link also points to the website's Exoptimizer page.

### 🗓️ Maintenance
- Version bumped to 3.2.1 across the app, installer, documentation and project files.

## Version 3.2.0
### 🛡️ Safety & Reliability Release
Every finding from the v3.1.0 review is addressed here. The goal: Exoptimizer never changes something it can't put back, never claims success it hasn't verified, and never changes your security settings without you explicitly asking.

### 🔙 Exact Undo (recovery journal)
- **Original values are recorded first** - before any service, registry value, power setting or TCP setting is changed, its original state (including "didn't exist") is written to a durable recovery journal in `%ProgramData%\Exoptimizer`. If a change can't be recorded, it is **not made**.
- **Undo restores exactly what was there** instead of guessing Windows defaults. It restores service start types (including delayed-start) and whether each service was running, registry values, the previous power plan and every power setting that was changed, TCP settings, and removes only the Defender exclusions Exoptimizer added.
- **Safer journal** - written atomically with a backup copy, locked to SYSTEM and Administrators, and validated entry by entry when read (invalid names, protected services and unrecognised registry values are ignored). A damaged file is set aside instead of silently lost. State saved by older versions is imported automatically.
- **Removed risky "undo" actions** - Undo no longer runs a global Winsock/TCP reset, and no longer forces the High performance plan or fixed registry defaults.
- **Honest scope** - if there is nothing recorded (for example changes made by versions before 3.2.0), Undo says so instead of pretending.

### 🔐 Windows Defender is opt-in
- **Two separate, off-by-default toggles**: "Exclude Riot Games folders from Defender scans" and "Turn off Defender real-time protection". Neither happens silently any more (previously scan exclusions were added on every run).
- **Full disclosure before anything changes** - the confirmation lists the exact folders and the real-time protection choice, and turning on real-time protection off needs its own warning prompt.
- **Verified, not assumed** - after the change, Exoptimizer asks Windows whether real-time protection is actually off. If Tamper Protection or a policy blocked it, the change is rolled back and reported as failed.
- **No duplicate actions** - Extreme mode no longer touches Defender on its own; both modes follow your two toggles. The old Extreme status check (which looked at a value Extreme never wrote) is gone.
- **Safer exclusions** - only folders that exist are added, resolved from the real Windows folders rather than a hard-coded user name, and never an exclusion that was already there.

### 🧯 Restore points you can trust
- **Created, then confirmed** - Exoptimizer asks Windows for the restore point and then checks that it really exists.
- **If it fails, nothing changes** - you must explicitly choose to continue without one (the default answer is No).
- **No permanent system changes** - the "one restore point per 24 hours" limit is lifted only while the request runs and then put back, and System Protection is only turned on if you agree.
- **Installer fixed** - the installer's restore point used an invalid type and always failed while reporting success. It now creates the restore point, verifies it, and tells you the truth.

### ⚙️ Reliable operations
- **One operation at a time** - Optimize, Extreme, Battery, Quick Boost, Undo, Repair, Cleanup and restore points are gated, so conflicting actions can't overlap (even across two windows). Navigation is disabled while one runs, and closing the app or exiting from the tray is blocked until it finishes.
- **No more UI-pumping** - work runs in the background instead of `Application.DoEvents()`, so the window stays responsive and can't be re-entered.
- **Real results** - every step reports what happened item by item (applied, skipped, failed) with exit codes. Success is shown only when everything succeeded; otherwise you see exactly what failed and it is **not** marked as optimized.
- **Safe command running** - external tools are run with timeouts, drained output (no hangs on large output) and checked exit codes. PowerShell scripts are passed encoded, so paths and names can't break out of a command line.
- **Progress is honest** - the bar reaches 100% only after the last step finishes.

### 🧰 Service handling
- Services are only changed after their original start type and running state were read and saved; unreadable or unsaved services are left alone.
- Services that you or Windows already disabled are no longer switched on, and services Windows itself protects (such as Update Medic) are reported as skipped instead of failing.
- Restoring a service verifies the result and keeps unsuccessful entries so you can retry.

### 🧹 Deep Cleanup
- **Preview and choose** - see how much each category holds and choose what to clean. Logs and crash dumps are no longer deleted by default.
- **Safer deletion** - links and junctions are never followed, files in use are skipped, and paths are resolved from Windows instead of hard-coded.
- **Windows Update is restored** - the service is paused only if it was running and is always started again afterwards, even if cleanup fails.
- **Real summary** - how much space was freed and how many files were skipped.

### 🩺 Repair Critical Services
- **Preview first** - shows exactly which services and Defender policy values would change, and starts only automatic services.
- **Backs up before changing** - current values are saved first; if that fails, nothing is changed.
- **Warns on managed PCs** - domain-joined or device-managed computers get an extra warning because those settings may be intentional.
- **Verified** - each change is checked and failures are listed.

### 📊 Honest status and features
- **Optimized status comes from what Exoptimizer actually did** (recorded runs), not a guess from system state. A partly failed run shows "Retry Optimization" instead of being stuck or falsely complete.
- **Quick Boost is real** - it now switches to the High performance plan and clears the DNS cache (and Undo restores your plan). Before, it only showed a success message.
- **VALORANT priority fixed** - the network QoS policy now uses the supported Windows QoS commands and reports failures. Previously the command could silently fail while the app said it worked.
- **Network tuning only when needed** - TCP auto-tuning and RSS are changed only if they differ from healthy defaults, and the previous values are recorded.
- **Startup entries are reversible** - Extreme mode disables common launchers using the same switch as Task Manager's Startup tab (current user only) instead of an unreliable legacy command.
- **Vanguard start reports the real outcome.**

### 🧱 Installer
- **Your recovery data is kept on uninstall** (asking you at the end) so Undo still works after a reinstall; settings are removed as before.
- **No duplicate "Installed apps" entry** - the extra hand-written uninstall registration is gone (and removed on upgrade).
- **Running copies are detected** - Exoptimizer now holds the single-instance lock the installer was already checking for, so a running copy is closed cleanly before updating.

### 🏗️ Build, release & supply chain
- **Upgraded to .NET 10 (LTS)** - .NET 6 is long out of support; .NET 8 reached end of support on 10 November 2026.
- **Reproducible release workflow** - a tagged release is built on GitHub Actions, checks that the packaged app version matches the tag, produces `SHA256SUMS.txt`, and creates build-provenance attestations for the installer and the app. Code signing is supported when a certificate secret is configured.
- **Build checks on every change** - the project builds and runs automated tests on Windows for each push and pull request. New tests cover service-name validation, journal validation and atomic saving (including failure injection), output parsing, command running (large output, timeouts) and the operation gate.
- **Fixed the README build instructions.**

### 🎨 Layout polish
- **Roomier window** - the window is a little taller (one size on every page), so the last card on System Optimization no longer sits against the bottom edge. It now has the same breathing room below it as the page title has above.
- **Defender options fully readable** - the descriptions under "Exclude Riot Games folders from Defender scans" and "Turn off Defender real-time protection" now have room for two lines, so nothing is cut off, and both rows share the same even spacing.
- **Sidebar header** - a little more space between the app name and its version number.

### 🗓️ Maintenance
- Version bumped to 3.2.0 across the app, installer, documentation and project files.

## Version 3.1.0
### 🎨 Complete UI/UX Overhaul
A ground-up redesign of the interface - minimal, refined and neat - with **no change to what any button does**.

- **New look** - a calm, spacious layout with rounded cards, Segoe UI typography and a Discord-style **blurple** accent. The light theme is pale and airy; the dark theme is deep ink with softly lit text.
- **Sidebar navigation** - the top tab bar is now a left sidebar with the app logo, a clear selected state, the update badge on Settings, and an indicator showing whether Exoptimizer is running as administrator.
- **Splash screen** - Exoptimizer now opens with a short splash showing the app icon and name, in the active light or dark theme. It is skipped when the app is set to start minimized in the system tray.
- **Redesigned pages** - System Optimization, VALORANT Priority, Performance Tools, System Monitor, System Restore and Settings were all re-laid out. Performance Tools now shows each tool with a short description, and the VALORANT page explains exactly what enabling priority changes.
- **Modern controls** - flat rounded buttons, animated toggle switches (replacing checkboxes), live gauge tiles on the System Monitor and status indicators with colored dots.
- **Themed dialogs** - confirmations, warnings, the progress window and the changelog viewer now follow the light/dark theme instead of showing bright system dialogs. The window title bar follows the theme too.
- **Smoother System Monitor** - CPU, memory and network readings are now gathered in the background, so the window no longer stutters while the monitor page is open.

### 🛠️ Polish & Fixes
- **Page headers and cards** - page titles and every card or setting title now have comfortable, consistent spacing from their descriptions on all pages, so the lines no longer crowd or overlap. The System Monitor tiles and Performance Tools tiles got the same treatment.
- **Performance Tools** - the Deep System Cleanup description now shows in full across both of its lines.
- **System Restore** - the "What does Exoptimizer never touch?" button is wide enough to show its whole label.

### 🗓️ Maintenance
- Version bumped to 3.1.0 across the app, installer and documentation.

## Version 3.0.1
### 🗓️ Maintenance
- Updated copyright and license year references from 2025 to 2026 (app UI, installer, README, LICENSE).

### ✨ New Features
- **Automatic update checker** - Exoptimizer now checks GitHub for a newer release shortly after starting, and shows a small badge on the Settings tab if one is found. A "Check for Updates" button in Settings also lets you check manually at any time, with clear feedback either way (up to date, update available, or check failed).
- **In-app changelog viewer** - a "View Changelog" button in Settings opens this changelog inside the app itself, with one tab per version, newest first.

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
