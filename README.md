# Jellyfin.Xtream (klopstack fork)

![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/klopstack/Jellyfin.Xtream/total)
![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/klopstack/Jellyfin.Xtream/latest/total)
![Dynamic YAML Badge](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fklopstack%2FJellyfin.Xtream%2Frefs%2Fheads%2Fmaster%2Fbuild.yaml&query=targetAbi&label=Jellyfin%20ABI)
![Dynamic YAML Badge](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fklopstack%2FJellyfin.Xtream%2Frefs%2Fheads%2Fmaster%2Fbuild.yaml&query=framework&label=.NET%20framework)

The Jellyfin.Xtream plugin integrates content from an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/) into [Jellyfin](https://jellyfin.org/).

This repository is an **independent fork** maintained by the [klopstack](https://github.com/klopstack) organization. Upstream lives at [Kevinjil/Jellyfin.Xtream](https://github.com/Kevinjil/Jellyfin.Xtream). Releases and the plugin catalog URL point at this fork so you can install builds with fork-specific changes (for example XMLTV EPG support) without waiting on upstream.

## Installation

### From the plugin catalog (recommended)

1. Open the admin dashboard and go to **Plugins** → **Repositories**.
2. Click **+** and add a repository:
   - **Name:** `Jellyfin.Xtream (klopstack)`
   - **URL:** `https://klopstack.github.io/Jellyfin.Xtream/repository.json`
3. Save, then open **Plugins** → **Catalog** → **Live TV** → **Jellyfin Xtream**.
4. Install the desired version and **restart Jellyfin**.

> **Note:** Enable [GitHub Pages](https://github.com/klopstack/Jellyfin.Xtream/settings/pages) on this repo (GitHub Actions source) before the catalog URL works. It is populated automatically when you publish a [GitHub Release](https://github.com/klopstack/Jellyfin.Xtream/releases).

### Manual install from a release

1. Download the `.zip` from [Releases](https://github.com/klopstack/Jellyfin.Xtream/releases).
2. Extract into your Jellyfin plugins folder, for example:
   - Linux: `/var/lib/jellyfin/plugins/Jellyfin.Xtream/`
   - Docker: `/config/plugins/Jellyfin.Xtream/`
3. Restart Jellyfin.

### Build from source

```bash
git clone https://github.com/klopstack/Jellyfin.Xtream.git
cd Jellyfin.Xtream
dotnet publish --configuration=Release Jellyfin.Xtream.sln
```

Copy everything under `Jellyfin.Xtream/bin/Release/net9.0/publish/` into the Jellyfin plugins directory above, then restart the server.

In VS Code/Cursor, use the **build-and-copy** task (`.vscode/tasks.json`) after setting `jellyfinLinuxDataDir` in `.vscode/settings.json` if your data path differs from `~/.local/share/jellyfin`.

## Requirements

- Jellyfin **10.11.x** (see `targetAbi` in [`build.yaml`](build.yaml))
- .NET **9.0** runtime on the Jellyfin host

## Configuration

The plugin requires connection information for an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/).
Set these on the **Credentials** tab in the plugin settings.

| Property | Description |
| -------- | ----------- |
| Base URL | API URL without a trailing slash, including `http` or `https` |
| Username | Xtream username |
| Password | Xtream password |

### Live TV

1. Open **Live TV**, select categories or channels, and save.
2. Open **TV Overrides** to adjust channel numbers, names, and icons.

### XMLTV EPG

1. Open **XMLTV**, enable **Use XMLTV**, and set the XMLTV URL (and optional disk cache path).
2. Save and refresh the Live TV guide.

### Video On-Demand

1. Open **Video On-Demand**, enable **Show this channel to users**, select content, and save.

### Series

1. Open **Series**, enable **Show this channel to users**, select content, and save.

### TV Catch-up

1. On **Live TV**, enable **Show the catch-up channel to users** and save.

## Development

```bash
dotnet restore Jellyfin.Xtream.sln
dotnet build -c Release
dotnet test
dotnet format Jellyfin.Xtream.sln --verify-no-changes
```

CI runs build, `dotnet format`, tests, and a line-coverage gate on pull requests and pushes to `master`.

### Releasing (maintainers)

1. Merge changes to `master` — the changelog workflow opens a version-bump PR updating `build.yaml` and `Directory.Build.props`.
2. Merge the bump PR, then publish the drafted GitHub release.
3. The **Publish Plugin** workflow uploads release assets and updates `repository.json` on GitHub Pages.

## Known problems

### Loss of confidentiality

Jellyfin exposes remote paths in the API and default UI. Xtream URLs often embed username and password, so anyone with library access may see credentials. Use caution on shared servers.

## Troubleshooting

Configure [Jellyfin networking](https://jellyfin.org/docs/general/networking/):

1. Dashboard → **Networking**
2. Set **Published server URIs**, e.g. `all=https://jellyfin.example.com`

## Upstream

To use the original maintainer’s catalog instead of this fork:

`https://kevinjil.github.io/Jellyfin.Xtream/repository.json`

Only add **one** Xtream repository in Jellyfin to avoid duplicate catalog entries (the plugin GUID is the same).
