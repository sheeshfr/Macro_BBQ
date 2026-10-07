#!/usr/bin/env bash
# ============================================================================
# Macro BBQ - Linux Launcher & First-Time Setup
# ============================================================================
# Double-clickable all-in-one launcher for Linux users.
# On first run: checks prerequisites, configures udev rules for /dev/uinput,
#               registers desktop icon and application launcher, builds if
#               needed, and launches Macro BBQ.
# On subsequent runs: verifies setup and instantly starts Macro BBQ!
# ============================================================================

set -e

# Resolve directory location
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UDEV_RULE_SRC="$SCRIPT_DIR/Assets/99-macrobbq.rules"
UDEV_DEST="/etc/udev/rules.d/99-macrobbq.rules"
ICON_SRC="$SCRIPT_DIR/Assets/icon.png"
if [ ! -f "$ICON_SRC" ]; then
    ICON_SRC="$SCRIPT_DIR/logo.png"
fi

# Check if application binary exists
is_binary_ready() {
    if [ -x "$SCRIPT_DIR/MacroBBQ" ] || [ -x "$SCRIPT_DIR/publish/MacroBBQ" ]; then
        return 0
    elif [ -f "$SCRIPT_DIR/MacroBBQ.dll" ] || [ -f "$SCRIPT_DIR/publish/MacroBBQ.dll" ] || [ -f "$SCRIPT_DIR/bin/Release/net8.0/MacroBBQ.dll" ] || [ -f "$SCRIPT_DIR/bin/Debug/net8.0/MacroBBQ.dll" ]; then
        return 0
    fi
    return 1
}

run_app() {
    cd "$SCRIPT_DIR"
    if [ -x "$SCRIPT_DIR/MacroBBQ" ]; then
        exec "$SCRIPT_DIR/MacroBBQ" "$@"
    elif [ -x "$SCRIPT_DIR/publish/MacroBBQ" ]; then
        exec "$SCRIPT_DIR/publish/MacroBBQ" "$@"
    elif [ -f "$SCRIPT_DIR/MacroBBQ.dll" ] && command -v dotnet &>/dev/null; then
        exec dotnet "$SCRIPT_DIR/MacroBBQ.dll" "$@"
    elif [ -f "$SCRIPT_DIR/publish/MacroBBQ.dll" ] && command -v dotnet &>/dev/null; then
        exec dotnet "$SCRIPT_DIR/publish/MacroBBQ.dll" "$@"
    elif [ -f "$SCRIPT_DIR/bin/Release/net8.0/MacroBBQ.dll" ] && command -v dotnet &>/dev/null; then
        exec dotnet "$SCRIPT_DIR/bin/Release/net8.0/MacroBBQ.dll" "$@"
    elif [ -f "$SCRIPT_DIR/bin/Debug/net8.0/MacroBBQ.dll" ] && command -v dotnet &>/dev/null; then
        exec dotnet "$SCRIPT_DIR/bin/Debug/net8.0/MacroBBQ.dll" "$@"
    else
        echo "[!] Error: No executable found to run."
        exit 1
    fi
}

# Quick check if system has completed initial setup
is_setup_done() {
    # 1. Executable or compiled project must exist
    if ! is_binary_ready; then
        return 1
    fi

    # 2. /dev/uinput must be accessible and writable
    # If /dev/uinput is already writable (e.g. logind uaccess or existing group), setup is satisfied!
    if [ -w /dev/uinput ]; then
        return 0
    fi

    # 3. Otherwise udev rules must be installed in system directory
    if [ ! -f "$UDEV_DEST" ]; then
        return 1
    fi

    if [ ! -c /dev/uinput ] || [ ! -w /dev/uinput ]; then
        return 1
    fi

    return 0
}

# ----------------------------------------------------------------------------
# FAST PATH: Setup already verified -> launch immediately without terminal or prompts
# ----------------------------------------------------------------------------
if is_setup_done; then
    run_app "$@"
fi

# ----------------------------------------------------------------------------
# SETUP PATH: First-time setup or missing prerequisites
# ----------------------------------------------------------------------------

# If launched from GUI without an interactive terminal, spawn one so user can view
# setup progress and authenticate for udev rules
if [ ! -t 0 ] && [ "$1" != "--terminal-spawned" ]; then
    for term in x-terminal-emulator gnome-terminal konsole xfce4-terminal mate-terminal lxterminal alacritty kitty foot xterm; do
        if command -v "$term" &>/dev/null; then
            case "$term" in
                gnome-terminal|mate-terminal|xfce4-terminal|lxterminal)
                    exec "$term" --title="Macro BBQ Setup" -- bash "$0" --terminal-spawned "$@"
                    ;;
                konsole)
                    exec "$term" -p tabtitle="Macro BBQ Setup" -e bash "$0" --terminal-spawned "$@"
                    ;;
                *)
                    exec "$term" -e bash "$0" --terminal-spawned "$@"
                    ;;
            esac
        fi
    done
fi

echo "========================================================================"
echo "                   Macro BBQ - Linux Setup & Launcher                   "
echo "========================================================================"
echo " Preparing your system for high-speed game macro and auto-clicking."
echo " This setup runs automatically on first launch."
echo "========================================================================"
echo ""

# 1. Check for .NET 8 Runtime or SDK
if ! command -v dotnet &>/dev/null; then
    if ! is_binary_ready; then
        echo "[!] Error: .NET 8 is required to build and run Macro BBQ."
        echo "    Please install the .NET 8 SDK or Runtime:"
        echo "    - Nobara / Fedora:    sudo dnf install dotnet-sdk-8.0"
        echo "    - Arch Linux:         sudo pacman -S dotnet-sdk-8.0"
        echo "    - Ubuntu / Debian:    sudo apt install dotnet-sdk-8.0"
        echo ""
        read -p "Press Enter to exit..."
        exit 1
    fi
fi

# 2. Build application if no binary exists yet
if ! is_binary_ready; then
    echo "[*] Building Macro BBQ Release binary via dotnet..."
    cd "$SCRIPT_DIR"
    dotnet publish AutoClicker.csproj -c Release -o "$SCRIPT_DIR/publish" --self-contained false
    if ! is_binary_ready; then
        echo "[!] Build failed or executable not produced."
        read -p "Press Enter to exit..."
        exit 1
    fi
    echo "[+] Build complete!"
fi

# 3. Install udev rules & setup /dev/uinput permissions if not already writable
if [ ! -w /dev/uinput ]; then
    echo ""
    echo "[*] Input Synthesis & Hotkey Permissions:"
    echo "    Macro BBQ requires non-root access to /dev/uinput"
    echo "    to synthesize ultra-low latency mouse clicks and keys inside games."
    echo "    Installing $UDEV_DEST..."
    echo ""
    if [ -f "$UDEV_RULE_SRC" ]; then
        sudo cp "$UDEV_RULE_SRC" "$UDEV_DEST"
    else
        echo 'KERNEL=="uinput", SUBSYSTEM=="misc", MODE="0660", GROUP="input", TAG+="uaccess", OPTIONS+="static_node=uinput"' | sudo tee "$UDEV_DEST" > /dev/null
        echo 'SUBSYSTEM=="input", MODE="0660", GROUP="input", TAG+="uaccess"' | sudo tee -a "$UDEV_DEST" > /dev/null
    fi
    sudo udevadm control --reload-rules || true
    sudo udevadm trigger || true
    sudo modprobe uinput 2>/dev/null || true
    sudo usermod -aG input "$USER" 2>/dev/null || true
    echo "[+] udev rules installed successfully."
fi

# 4. Configure Desktop Integration & Icons
echo "[*] Configuring desktop integration..."
REAL_HOME="$(getent passwd "$USER" 2>/dev/null | cut -d: -f6)"
[ -z "$REAL_HOME" ] && REAL_HOME="$HOME"

TARGET_HOMES=("$HOME")
if [ "$REAL_HOME" != "$HOME" ]; then
    TARGET_HOMES+=("$REAL_HOME")
fi

for TARGET_DIR in "${TARGET_HOMES[@]}"; do
    USER_ICON_DIR="$TARGET_DIR/.local/share/icons/hicolor/256x256/apps"
    mkdir -p "$USER_ICON_DIR" "$TARGET_DIR/.local/share/icons" 2>/dev/null || true
    if [ -f "$ICON_SRC" ]; then
        cp "$ICON_SRC" "$USER_ICON_DIR/macrobbq.png" 2>/dev/null || true
        cp "$ICON_SRC" "$TARGET_DIR/.local/share/icons/macrobbq.png" 2>/dev/null || true
    fi

    APP_DESKTOP_DIR="$TARGET_DIR/.local/share/applications"
    mkdir -p "$APP_DESKTOP_DIR" 2>/dev/null || true

    cat <<EOF > "$APP_DESKTOP_DIR/macrobbq.desktop"
[Desktop Entry]
Version=1.0
Type=Application
Name=Macro BBQ
GenericName=Game Macro & Auto Clicker
Comment=Ultra-fast gaming auto-clicker and input macro utility
Exec="$SCRIPT_DIR/MacroBBQ.sh"
Icon=macrobbq
Terminal=false
Categories=Game;Utility;
StartupNotify=true
StartupWMClass=MacroBBQ
X-GNOME-UsesNotifications=true
EOF

    chmod +x "$APP_DESKTOP_DIR/macrobbq.desktop" 2>/dev/null || true
    if command -v update-desktop-database &>/dev/null; then
        update-desktop-database "$APP_DESKTOP_DIR" 2>/dev/null || true
    fi
    if command -v gio &>/dev/null; then
        gio set "$APP_DESKTOP_DIR/macrobbq.desktop" metadata::trusted true 2>/dev/null || true
    fi
done

if command -v gio &>/dev/null; then
    gio set "$SCRIPT_DIR/MacroBBQ.desktop" metadata::trusted true 2>/dev/null || true
fi

echo "[+] Desktop integration registered!"
echo ""
echo "========================================================================"
echo " Setup complete! Starting Macro BBQ..."
echo " (Note: If this was your first time being added to the 'input' group,"
echo "  a re-login or reboot may be required if hotkeys aren't picked up)"
echo "========================================================================"
echo ""

sleep 1
run_app "$@"
