# SonarAutoSwitch 🎧

> **Intelligent, Instant Audio Fallback & Bluetooth Auto-Switcher for SteelSeries Sonar**  
> Automatically routes all SteelSeries Sonar audio channels (Game, Chat, Media, Aux, Monitoring) to your wireless Bluetooth or priority headphones the second they connect, and cleanly restores your default headset when disconnected.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6.svg)](https://microsoft.com)
[![Zero Dependencies](https://img.shields.io/badge/Dependencies-Zero%20(Pure%20.NET)-brightgreen.svg)]()

---

## 🌟 The Problem with SteelSeries Sonar

SteelSeries Sonar has a built-in *"Backup Device Switch"* feature, but it has a major limitation:
- It only triggers when the currently active device is **physically unplugged or disconnected**.
- If you have a primary USB/wired headset (e.g., HyperX) always connected, and you turn on your wireless Bluetooth earbuds (e.g., TOZO, AirPods, Sony XM4/5), **Sonar will NOT automatically switch to your wireless earbuds**, because the wired headset is still connected.
- You are forced to open the SteelSeries GG app and manually re-select the device every single time.

## 🚀 The Solution: SonarAutoSwitch

**SonarAutoSwitch** solves this once and for all:
1. **Real-time Windows Audio Monitoring:** Instantly detects when your priority headphones connect to Windows via Bluetooth or USB.
2. **Dynamic Sonar API Control:** Communicates directly with SteelSeries Sonar's local HTTP engine to switch all output channels (`game`, `chat`, `media`, `aux`, `monitoring`) instantaneously.
3. **Clean Fallback:** When you power off or disconnect your wireless earbuds, audio seamlessly reverts back to your primary default headset.
4. **Protects Microphone Quality:** Keeps your wired studio/headset microphone active so Bluetooth voice profile (Hands-Free AG Audio) doesn't degrade your sound quality.
5. **System Tray GUI:** A sleek dark-mode configuration window to view detected devices, add keywords, reorder priorities, and toggle notifications.
6. **Zero External Dependencies:** Built with pure C# targeting standard Windows .NET Framework 4.8. No need to install Node, Python, or .NET SDK!

---

## 📸 Features

- ⚡ **Instant Switching:** Switches audio in under 1 second upon Bluetooth connection.
- 🎛️ **Priority Cascade:** Define multiple fallback levels (e.g. `TOZO` ➔ `AirPods` ➔ `HyperX`).
- 🔔 **Windows Toast Notifications:** Optional friendly popups when devices switch.
- 🔄 **Drift Protection:** Periodically audits Sonar channels; if Sonar or GG restarts, it re-aligns channels automatically.
- 🚀 **1-Click Portable Install:** Simple batch script that compiles and registers startup on any PC or laptop in seconds.

---

## 📥 Quick Installation (1-Click)

1. Clone or download this repository:
   ```cmd
   git clone https://github.com/2saleh1/SonarAutoSwitch.git
   cd SonarAutoSwitch
   ```
2. Double-click **`install.bat`** (or run it in Command Prompt).
3. That's it!
   - It compiles the standalone binary into `%LOCALAPPDATA%\SonarAutoSwitch\SonarAutoSwitch.exe`.
   - Creates a Windows Startup shortcut so it runs quietly in the background on every boot.
   - Look for the headphone icon in your **System Tray** (next to the clock).

---

## ⚙️ Configuration & GUI

- **Double-click** the tray icon (or right-click ➔ **Settings**):
  - **Priority List:** Add devices from the detected dropdown list or type keywords (e.g., `TOZO`, `Sony`, `AirPods`). Reorder with **Up / Down** buttons.
  - **Default Device:** Specify your fallback wired headset (e.g., `HyperX`).
  - **Notifications:** Enable/disable switch notifications.
  - **Auto-Start:** Toggle launch on Windows login.
- Settings are saved to:
  `%LOCALAPPDATA%\SonarAutoSwitch\config.json`
- Activity logs can be viewed anytime via the tray menu or at:
  `%LOCALAPPDATA%\SonarAutoSwitch\sonar_autoswitch.log`

---

## 📂 Project Structure

```
SonarAutoSwitch/
├── src/
│   ├── Program.cs         # Entry point, Tray Icon & audio monitoring loop
│   ├── SettingsForm.cs    # Modern dark-mode UI for priorities & options
│   ├── AudioHelper.cs     # Windows MMDevice / CoreAudio enumeration
│   ├── SonarClient.cs     # TCP port detection & SteelSeries Sonar REST client
│   └── Config.cs          # JSON configuration loader & serializer
├── build.bat              # Compiles source into bin\SonarAutoSwitch.exe
├── install.bat            # 1-Click build, deploy, & startup registration
├── uninstall.bat          # Clean uninstaller
├── LICENSE                # MIT License
└── README.md              # Documentation
```

---

## 🗑️ Uninstallation

Run `uninstall.bat` to terminate the process, remove the startup shortcut, and optionally delete settings and logs.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
