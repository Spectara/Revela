# Plugin Management Guide

**Last Updated:** December 19, 2025

## Overview

Revela uses **NuGet packages** for plugin distribution and version management.

---

## Installing Plugins

```bash
# Install by short name (auto-downloads from NuGet.org)
revela plugin install OneDrive

# Or use full package ID
revela plugin install Spectara.Revela.Plugin.OneDrive

# Install specific version
revela plugin install OneDrive --version 1.2.0

# Install from local .nupkg file
revela plugin install ./path/to/Spectara.Revela.Plugin.OneDrive.1.0.0.nupkg

# Install from custom source (e.g., GitHub Packages)
revela plugin install OneDrive --source github
```

## Managing Plugins

```bash
# List installed plugins
revela plugin list

# Uninstall a plugin
revela plugin uninstall OneDrive
```

## Updating Plugins

```bash
# Check installed plugins and versions
revela plugin list

# To update: uninstall and reinstall with new version
revela plugin uninstall OneDrive
revela plugin install OneDrive --version 2.0.0

# Note: `revela plugin update` command is planned for future release
```

---

## Managing NuGet Sources

Add custom NuGet sources (GitHub Packages, private feeds):

```bash
# Add GitHub Packages as source
revela plugin source add --name github --url https://nuget.pkg.github.com/spectara/index.json

# Add private feed
revela plugin source add --name myfeed --url https://my-nuget-server.com/v3/index.json

# List all sources
revela plugin source list

# Remove source
revela plugin source remove github
```

---

## Project-Based Plugin Management

Plugins are tracked in `project.json`:

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

**Sharing projects:** Just commit `project.json`. Others run `revela restore` to install all plugins automatically.

---

## For Plugin Developers

### Creating NuGet Packages

```bash
# Create package using .csproj properties
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
    <PackageReference Include="System.CommandLine" Version="2.0.1" />
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

# Or install from local directory as source
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

**IMPORTANT:** Plugin discovery requires the correct namespace pattern:

```csharp
// ✅ CORRECT - Official plugins
namespace Spectara.Revela.Plugin.MyPlugin;

// ✅ CORRECT - Community plugins
namespace YourName.Revela.Plugin.MyPlugin;

// ❌ INCORRECT (won't be discovered)
namespace MyCompany.SomeOther.Plugin;
```

---

## Troubleshooting

### "Package not found" error

```bash
# Check spelling (case-insensitive)
revela plugin install OneDrive  # ✅ Correct

# Check if package exists on NuGet.org
# Visit: https://www.nuget.org/packages/Spectara.Revela.Plugin.OneDrive
```

### "Failed to install" error

```bash
# Check your NuGet sources
revela plugin source list

# Try with explicit source
revela plugin install OneDrive --source nuget.org
```

### GitHub Packages authentication

```bash
# Add GitHub Packages source with authentication
revela plugin source add \
  --name github \
  --url https://nuget.pkg.github.com/spectara/index.json

# Note: You may need to configure authentication in ~/.nuget/NuGet.Config:
# <packageSourceCredentials>
#   <github>
#     <add key="Username" value="YOUR_GITHUB_USERNAME" />
#     <add key="ClearTextPassword" value="YOUR_PAT" />
#   </github>
# </packageSourceCredentials>
```

---

## Support

- **Documentation:** https://revela.website/docs
- **Issues:** https://github.com/spectara/revela/issues
- **Discussions:** https://github.com/spectara/revela/discussions
