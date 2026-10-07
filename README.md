<div align="center">
  <img src="logo.png" alt="Macro BBQ Logo" width="128" />
  <h1>Macro BBQ</h1>
</div>

<div align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-287cff?style=flat-square&color=9be1e6&labelColor=e4896e" alt="Platform: Windows 10 & 11">
  <img src="https://img.shields.io/badge/platform-Linux%20(Nobara%20%2F%20SteamOS%20%2F%20Arch%20%2F%20Ubuntu)-FCC624?style=flat-square&color=9be1e6&labelColor=e4896e" alt="Platform: Linux">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-blue?style=flat-square&color=9be1e6&labelColor=e4896e" alt="License"></a>

  <p>I got tired of searching for decent auto-clickers so I made one for myself. Now my pistol is a machine gun in Remnant 2! B)</p>
  <p>Vibe Coded by <a href="https://github.com/sheeshfr"><b>SheeshFr</b></a></p>

  <br />

  <img src="screenshot.png" alt="Macro BBQ" width="400" />
</div>

---

## How to Use

1. **Boot it up**
2. **Set your Hotkey** (Click the keycap button to bind any keyboard key or mouse button)
3. **Change the interval** (`50ms` = 20 CPS / clicks per sec)
4. **Choose Toggle or Hold**:
   - **Toggle**: An on/off switch.
   - **Hold**: Active while the button is held, off the instant it is released.
5. **Secondary Modifier Slot (⚡ Mod)**:
   - Assign an optional secondary button (e.g. Right Click for Aim Down Sights).
   - Choose **Hold** or **Spam**, configure execution order (Secondary 1st vs Primary 1st), and adjust initial delay.

---

## Running on Linux

Macro BBQ is fully native on Linux (supporting both **Wayland** and **X11** on Nobara, Fedora, Arch, Ubuntu, and Steam Deck desktop mode) using Linux kernel `/dev/uinput` for sub-millisecond click synthesis and `/dev/input/event*` for global hotkey detection inside Proton/Wine games.

### Double-Click Launch (Recommended)
1. Ensure executable permissions (only needed once):
   ```bash
   chmod +x MacroBBQ.desktop MacroBBQ.sh
   ```
2. Double-click **`MacroBBQ.desktop`** (or run `./MacroBBQ.sh` in your terminal).

* **On First Launch**: Macro BBQ automatically checks prerequisites, prompts for `sudo` to install udev rules for non-root `/dev/uinput` access, adds your user to the `input` group, registers the desktop icon in your application launcher, and starts the app.
* **On Subsequent Launches**: Verifies setup and launches instantly in the background with zero terminal popups!

---

## Download

Grab the latest ready-to-run **`Macro_BBQ_Release.zip`** from [**Releases**](https://github.com/sheeshfr/Macro_BBQ/releases/latest). Unzip and run!

---

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
