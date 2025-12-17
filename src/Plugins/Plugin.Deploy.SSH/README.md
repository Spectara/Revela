# Spectara.Revela.Plugin.Deploy.SSH

[![NuGet](https://img.shields.io/nuget/v/Spectara.Revela.Plugin.Deploy.SSH.svg)](https://www.nuget.org/packages/Spectara.Revela.Plugin.Deploy.SSH)

Deploy your Revela site via SSH/SFTP to any server.

> ⚠️ **Coming Soon** - This plugin is planned but not yet implemented.

## Installation

```bash
revela plugin install Deploy.SSH
```

Or with full package name:
```bash
revela plugin install Spectara.Revela.Plugin.Deploy.SSH
```

## Configuration

Create a configuration file at `plugins/Spectara.Revela.Plugin.Deploy.SSH.json`:

```json
{
  "Spectara.Revela.Plugin.Deploy.SSH": {
    "Host": "your-server.com",
    "Port": 22,
    "Username": "deploy",
    "PrivateKeyPath": "~/.ssh/id_rsa",
    "RemotePath": "/var/www/photos"
  }
}
```

### Configuration Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `Host` | Yes | - | SSH server hostname |
| `Port` | No | 22 | SSH port |
| `Username` | Yes | - | SSH username |
| `Password` | No | - | SSH password (not recommended) |
| `PrivateKeyPath` | No | - | Path to private key file |
| `RemotePath` | Yes | - | Remote directory path |

## Usage

```bash
# Deploy generated site
revela deploy ssh

# Deploy with dry-run (show what would be uploaded)
revela deploy ssh --dry-run

# Deploy specific output directory
revela deploy ssh --output ./output
```

## Features (Planned)

- ✅ SFTP file upload
- ✅ Incremental sync (only changed files)
- ✅ Private key authentication
- ✅ Progress reporting
- ✅ Dry-run mode

## Requirements

- Revela CLI v1.0.0 or later
- SSH server with SFTP support

## License

MIT - See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)
