# Migration Guide: ZIP to NuGet Plugins

**Last Updated:** December 17, 2025

## Overview

Revela has migrated from **ZIP-based plugins** to **NuGet packages** for better distribution and version management.

### What Changed?

| Aspect | Before (ZIP) | After (NuGet) |
|--------|--------------|---------------|
| **Distribution** | Manual ZIP files | NuGet packages (.nupkg) |
| **Installation** | `revela plugin install --from-zip` | `revela plugin install <name>` |
| **Sources** | Local files only | NuGet.org, GitHub Packages, custom feeds |
| **Versioning** | Manual filename (v1.0.0.zip) | Semantic versioning in package |
| **Dependencies** | Manual bundling | Automatic via .nuspec |
| **Updates** | Re-download ZIP | `revela plugin update <name>` |
| **Restore** | Manual tracking | `revela restore` from project.json |

---

## For Plugin Users

### Installing Plugins

**Old Way (ZIP):**
```bash
# Download ZIP manually
curl -L https://github.com/kirkone/Revela/releases/download/v1.0.0/Plugin.OneDrive.zip -o plugin.zip

# Install from ZIP
revela plugin install --from-zip plugin.zip
```

**New Way (NuGet):**
```bash
# Install directly by name (auto-downloads from NuGet.org)
revela plugin install OneDrive

# Or use full package ID
revela plugin install Spectara.Revela.Plugin.OneDrive

# Install specific version
revela plugin install OneDrive --version 1.2.0

# Install from local .nupkg file
revela plugin install ./path/to/Spectara.Revela.Plugin.OneDrive.1.0.0.nupkg

# Install from GitHub Packages
revela plugin install OneDrive --source github
```

### Managing Sources

Add custom NuGet sources (GitHub Packages, private feeds):

```bash
# Add GitHub Packages as source
revela plugin source add --name github --url https://nuget.pkg.github.com/kirkone/index.json

# Add private feed
revela plugin source add --name myfeed --url https://my-nuget-server.com/v3/index.json

# List all sources
revela plugin source list

# Remove source
revela plugin source remove github
```

### Project-Based Plugin Management

Plugins are now tracked in `project.json`:

```json
{
  "name": "My Photography Site",
  "theme": "Lumina",
  "plugins": {
    "Spectara.Revela.Plugin.OneDrive": "1.2.0",
    "Spectara.Revela.Plugin.Statistics": "1.0.0"
  }
}
```

**Restore plugins from project.json:**
```bash
# In project directory
revela restore

# Automatically installs missing plugins
# Runs in parallel (4 concurrent)
# Shows progress bar
```

### Updating Plugins

```bash
# Check installed plugins and versions
revela plugin list

# To update: uninstall and reinstall with new version
revela plugin uninstall OneDrive
revela plugin install OneDrive --version 2.0.0

# Note: `revela plugin update` command is planned for future release
```

---

## For Plugin Developers

### Creating NuGet Packages

**Old Way (ZIP):**
```bash
# Build plugin
dotnet publish -c Release -o ./publish

# Create ZIP manually
cd publish
zip -r ../Plugin.MyPlugin-v1.0.0.zip .
```

**New Way (NuGet):**
```bash
# Create .nuspec file or use .csproj properties
dotnet pack -c Release -o ./nupkgs

# Output: Spectara.Revela.Plugin.MyPlugin.1.0.0.nupkg
```

### Project Configuration (.csproj)

Add NuGet package metadata to your plugin's `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    
    <!-- NuGet Package Metadata -->
    <PackageId>Spectara.Revela.Plugin.MyPlugin</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Description of your plugin</Description>
    <PackageTags>revela;plugin;photography</PackageTags>
    <PackageProjectUrl>https://github.com/yourname/revela-plugin-myplugin</PackageProjectUrl>
    <RepositoryUrl>https://github.com/yourname/revela-plugin-myplugin</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    
    <!-- Include only lib/net10.0/*.dll in package -->
    <IncludeBuildOutput>true</IncludeBuildOutput>
    <IncludeSymbols>false</IncludeSymbols>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Spectara.Revela.Core" Version="1.0.0" />
    <PackageReference Include="System.CommandLine" Version="2.0.0" />
  </ItemGroup>
</Project>
```

### GitHub Actions Workflow

Create `.github/workflows/release.yml` for automated releases:

```yaml
name: Release Plugin

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Extract version
        id: version
        run: echo "version=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT
      
      - name: Pack
        run: dotnet pack -c Release -o ./nupkgs -p:PackageVersion=${{ steps.version.outputs.version }}
      
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: ./nupkgs/*.nupkg
      
      - name: Publish to NuGet.org
        run: dotnet nuget push "./nupkgs/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }}
```

### Testing Locally

Test your plugin package before publishing:

```bash
# Pack plugin
dotnet pack -c Release -o ./nupkgs

# Install from local .nupkg file
revela plugin install ./nupkgs/Spectara.Revela.Plugin.MyPlugin.1.0.0.nupkg

# Or install from local directory
revela plugin install MyPlugin --source ./nupkgs
```

### Publishing to NuGet.org

1. **Create NuGet.org account:** https://www.nuget.org/

2. **Get API Key:**
   - Go to https://www.nuget.org/account/apikeys
   - Create new API key with "Push" permission
   - Copy the key (shown only once!)

3. **Publish package:**
```bash
dotnet nuget push ./nupkgs/Spectara.Revela.Plugin.MyPlugin.1.0.0.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY
```

4. **Verify on NuGet.org:**
   - Package appears within 5-10 minutes
   - Check: https://www.nuget.org/packages/Spectara.Revela.Plugin.MyPlugin

### Publishing to GitHub Packages

1. **Create Personal Access Token (PAT):**
   - Go to GitHub Settings → Developer Settings → Personal Access Tokens
   - Create token with `write:packages` permission

2. **Authenticate:**
```bash
dotnet nuget add source \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_PAT \
  --store-password-in-clear-text \
  https://nuget.pkg.github.com/YOUR_USERNAME/index.json
```

3. **Publish:**
```bash
dotnet nuget push ./nupkgs/*.nupkg \
  --source github \
  --api-key YOUR_PAT
```

### Namespace Convention

**IMPORTANT:** All plugins must use the `Spectara.Revela.Plugin.*` namespace:

```csharp
// ✅ CORRECT
namespace Spectara.Revela.Plugin.MyPlugin;

public sealed class MyPlugin : IPlugin
{
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "Spectara.Revela.Plugin.MyPlugin",  // Full name
        Version = "1.0.0"
    };
}

// ❌ INCORRECT (won't be discovered)
namespace MyCompany.Revela.Plugin;
namespace Revela.Plugin.MyPlugin;
```

---

## Breaking Changes

### Removed Features

1. **`--from-zip` option** - No longer supported
   - **Migration:** Use NuGet packages instead

2. **Manual ZIP downloads** - Not needed anymore
   - **Migration:** `revela plugin install <name>` auto-downloads

3. **Plugin ZIP structure** - Replaced by NuGet package structure
   - **Migration:** Use `dotnet pack` to create packages

### Changed Behavior

1. **Installation location:**
   - ZIP: Extracted to `./plugins/` only
   - NuGet: Can install locally (`./plugins/`) or globally (`%APPDATA%/Revela/plugins/`)

2. **Version management:**
   - ZIP: Version in filename, no tracking
   - NuGet: Version in package metadata, tracked in project.json

3. **Dependencies:**
   - ZIP: All dependencies bundled in ZIP
   - NuGet: Dependencies declared in .nuspec, resolved automatically

---

## Troubleshooting

### "Package not found" error

```bash
# Check spelling
revela plugin install OneDrive  # ✅ Correct
revela plugin install onedrive  # ✅ Also works (case-insensitive)

# Check if package exists on NuGet.org
# Visit: https://www.nuget.org/packages/Spectara.Revela.Plugin.OneDrive
```

### "Failed to install" error

```bash
# Check your NuGet sources
revela plugin source list

# Try with explicit source
revela plugin install OneDrive --source nuget.org

# Check logs
revela plugin install OneDrive --loglevel Debug
```

### GitHub Packages authentication

```bash
# Add GitHub Packages source with authentication
revela plugin source add \
  --name github \
  --url https://nuget.pkg.github.com/kirkone/index.json

# Note: You may need to configure authentication in ~/.nuget/NuGet.Config:
# <packageSourceCredentials>
#   <github>
#     <add key="Username" value="YOUR_GITHUB_USERNAME" />
#     <add key="ClearTextPassword" value="YOUR_PAT" />
#   </github>
# </packageSourceCredentials>
```

### Old ZIP plugins still installed

```bash
# Uninstall old plugin
revela plugin uninstall OldPluginName

# Reinstall from NuGet
revela plugin install OldPluginName
```

---

## Timeline

- **January 2025:** ZIP-based plugin system introduced
- **December 17, 2025:** Migrated to NuGet-based system
- **December 2025:** GitHub Packages auto-publishing implemented
- **Future:** Deprecate ZIP support entirely (already removed in current version)

---

## FAQ

### Can I still use ZIP files?

No, ZIP support has been removed. Use NuGet packages instead.

### How do I convert my ZIP plugin to NuGet?

See "For Plugin Developers" section above. Key steps:
1. Add NuGet metadata to .csproj
2. Use `dotnet pack` instead of manual ZIP
3. Publish to NuGet.org or GitHub Packages

### Are old ZIP plugins still available?

Only as GitHub Release assets for old versions. New versions are NuGet-only.

### Can I host my own NuGet feed?

Yes! Use `revela plugin source add` to configure custom feeds.

### Do I need to update my project.json manually?

No, `revela plugin install` automatically updates project.json.

### How do I share my project with plugins?

Just commit `project.json`. Others run `revela restore` to install all plugins automatically.

---

## Support

- **Documentation:** https://revela.website/docs
- **Issues:** https://github.com/kirkone/Revela/issues
- **Discussions:** https://github.com/kirkone/Revela/discussions
