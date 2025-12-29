# Installation Guide

This guide covers all installation options for Revela.

## Quick Install

**Fastest option for most users:**

```bash
# Download from GitHub Releases
# https://github.com/spectara/revela/releases/latest
```

Just download, extract, and double-click `revela.exe`. No installation required!

---

## Installation Options

### Option A: Native Executable (Recommended for Photographers)

**Requirements:** None - fully self-contained

This is the easiest option. Download the ZIP/TAR file for your platform, extract it, and run.

#### Download Links

| Platform | File | Architecture |
|----------|------|--------------|
| **Windows** | [revela-win-x64.zip](https://github.com/spectara/revela/releases/latest) | 64-bit Intel/AMD |
| **Linux** | [revela-linux-x64.tar.gz](https://github.com/spectara/revela/releases/latest) | 64-bit Intel/AMD |
| **Linux ARM** | [revela-linux-arm64.tar.gz](https://github.com/spectara/revela/releases/latest) | 64-bit ARM (Raspberry Pi 4+) |
| **macOS Intel** | [revela-osx-x64.tar.gz](https://github.com/spectara/revela/releases/latest) | Intel Macs |
| **macOS Apple Silicon** | [revela-osx-arm64.tar.gz](https://github.com/spectara/revela/releases/latest) | M1/M2/M3 Macs |

#### Windows Installation

1. Download `revela-win-x64.zip`
2. Right-click → **Extract All...**
3. Choose a location (e.g., `C:\Revela\`)
4. Double-click `revela.exe` to start

**Optional: Add to PATH** (for command line use)
```powershell
# Run in PowerShell as Administrator
$revelPath = "C:\Revela"
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";$revelPath", "User")
```

#### Linux Installation

```bash
# Download
wget https://github.com/spectara/revela/releases/latest/download/revela-linux-x64.tar.gz

# Extract to /opt
sudo tar -xzf revela-linux-x64.tar.gz -C /opt/

# Create symlink
sudo ln -s /opt/revela/revela /usr/local/bin/revela

# Verify
revela --version
```

#### macOS Installation

```bash
# Download (Apple Silicon example)
curl -LO https://github.com/spectara/revela/releases/latest/download/revela-osx-arm64.tar.gz

# Extract
tar -xzf revela-osx-arm64.tar.gz -C ~/Applications/

# Add to PATH (add to ~/.zshrc)
export PATH="$HOME/Applications/revela:$PATH"

# Verify
revela --version
```

**Note:** On first run, macOS may show a security warning. Go to **System Preferences → Security & Privacy** and click "Open Anyway".

#### Advantages

- ✅ No dependencies required
- ✅ Works on any system
- ✅ Ideal for CI/CD pipelines
- ✅ Just extract and run

#### Disadvantages

- ❌ Larger download (~70 MB)
- ❌ Manual updates required

---

### Option B: .NET Global Tool (For Developers)

**Requirements:** [.NET Runtime 10.0](https://dotnet.microsoft.com/download) or later

If you already have .NET installed, this is the most convenient option.

```bash
# Install from NuGet.org
dotnet tool install -g Spectara.Revela

# Verify installation
revela --version
```

#### Update

```bash
dotnet tool update -g Spectara.Revela
```

#### Uninstall

```bash
dotnet tool uninstall -g Spectara.Revela
```

#### Advantages

- ✅ Small download (~10 MB)
- ✅ Easy updates with one command
- ✅ Automatic PATH configuration
- ✅ Works on all platforms

#### Disadvantages

- ❌ Requires .NET Runtime

---

### Option C: From Source (For Contributors)

**Requirements:** [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later

```bash
# Clone repository
git clone https://github.com/spectara/revela.git
cd revela

# Restore and build
dotnet restore
dotnet build

# Run directly
dotnet run --project src/Cli

# Or install as global tool
dotnet pack src/Cli -c Release
dotnet tool install -g --add-source ./artifacts/packages Spectara.Revela
```

See [Development Guide](development.md) for more details.

---

## Verify Installation

After installation, verify that Revela works:

```bash
# Check version
revela --version

# Show help
revela --help

# Start interactive mode
revela
```

On first run, the **Setup Wizard** will guide you through installing themes and plugins.

---

## Shell Completion

Enable tab-completion for faster command entry.

### Prerequisites

```bash
# Install dotnet-suggest globally
dotnet tool install --global dotnet-suggest
```

### PowerShell

Add to your `$PROFILE`:

```powershell
# Get profile path
$PROFILE

# Edit profile (creates if doesn't exist)
notepad $PROFILE
```

Add this line:
```powershell
dotnet suggest shell register
```

### Bash

Add to `~/.bashrc`:

```bash
# dotnet suggest shell
eval "$(dotnet suggest register)"
```

### Zsh

Add to `~/.zshrc`:

```zsh
# dotnet suggest shell
eval "$(dotnet suggest register)"
```

### Usage

After restarting your shell:

```bash
revela gen<TAB>     # → revela generate
revela theme <TAB>  # → shows: list, install, extract
```

---

## Troubleshooting

### "revela: command not found"

**Cause:** Revela is not in your PATH.

**Solution:**
- Windows: Add the Revela folder to your PATH environment variable
- Linux/macOS: Create a symlink in `/usr/local/bin` or add to PATH

### "Permission denied" (Linux/macOS)

**Cause:** The executable doesn't have execute permissions.

**Solution:**
```bash
chmod +x /path/to/revela
```

### Windows Defender SmartScreen Warning

**Cause:** The executable is not signed (yet).

**Solution:** Click "More info" → "Run anyway"

### macOS "cannot be opened because the developer cannot be verified"

**Solution:**
1. Go to **System Preferences → Security & Privacy → General**
2. Click **"Open Anyway"** next to the Revela warning

Or via Terminal:
```bash
xattr -d com.apple.quarantine /path/to/revela
```

---

## Next Steps

- [Getting Started Guide](getting-started/en.md) - Create your first photo site
- [Plugin Management](plugin-management.md) - Extend Revela with plugins
- [Development Guide](development.md) - Contribute to Revela
