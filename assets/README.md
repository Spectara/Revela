# Revela Assets

This folder contains shared assets used across the Revela project.

## Logo / Icon

**Files:**
- `Spectara_100.png` (100×100 px) - NuGet package icon
- `Spectara_200.png` (200×200 px) - High-res version
- `Spectara_Original_BW.svg` - Original SVG (black & white)

**Usage:**
- NuGet package icon for all packages (CLI, plugins, themes)
- README.md header image
- Project branding

**References in .csproj:**
```xml
<PropertyGroup>
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>

<ItemGroup>
  <None Include="..\..\assets\Spectara_100.png" Pack="true" PackagePath="icon.png" />
</ItemGroup>
```

**Copyright:**
Logo copyright © 2025 Spectara. All rights reserved.
