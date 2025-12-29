# Application Icon

The file `revela.ico` is used as the application icon for the Windows executable.

## Creating the Icon

### Option 1: Online Converter (Easiest)

1. Go to [ConvertICO.com](https://convertico.com/) or [ICOConvert.com](https://icoconvert.com/)
2. Upload `revela_original.png`
3. Select sizes: 16x16, 32x32, 48x48, 256x256
4. Download and save as `revela.ico` in this folder

### Option 2: ImageMagick (Command Line)

```bash
# Install ImageMagick first
# Windows: winget install ImageMagick.ImageMagick
# macOS: brew install imagemagick
# Linux: sudo apt install imagemagick

# Convert PNG to multi-resolution ICO
magick revela_original.png -define icon:auto-resize=256,128,64,48,32,16 revela.ico
```

### Option 3: GIMP (Free)

1. Open `revela_original.png` in GIMP
2. Image → Scale Image → 256x256
3. File → Export As → `revela.ico`
4. Select sizes: 16, 32, 48, 256

### Option 4: Visual Studio

1. Right-click `revela_original.png` → Open With → Visual Studio
2. File → Save As → `revela.ico`

## Icon Requirements

- **Format:** Windows ICO (multi-resolution)
- **Sizes included:** 16x16, 32x32, 48x48, 256x256 (minimum)
- **Color depth:** 32-bit (with alpha transparency)
- **Location:** `assets/revela.ico`

## Usage

The icon is configured in `src/Cli/Cli.csproj`:

```xml
<ApplicationIcon>..\..\assets\revela.ico</ApplicationIcon>
```

After creating the icon, rebuild:

```bash
dotnet build
```

The `revela.exe` will show the icon in:
- Windows Explorer file listing
- Taskbar when running
- Alt+Tab window switcher
- Start menu (if pinned)
