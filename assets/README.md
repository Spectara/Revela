# Revela Assets

This folder contains shared assets used across the Revela project.

## Logo / Icon

**Files:**
- `revela_100.png` (100×100 px) - NuGet package icon
- `revela_200.png` (200×200 px) - High-res version
- `revela_original_bw.svg` - Original SVG (black & white)

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
  <None Include="..\..\assets\revela_100.png" Pack="true" PackagePath="icon.png" />
</ItemGroup>
```

**Copyright:**
Logo copyright © 2025 Spectara. All rights reserved.
