# Jellyfin.Xtream Copilot Instructions

## Project Overview

A Jellyfin plugin that integrates Xtream-compatible IPTV APIs, providing Live TV, Video On-Demand, Series, and Catch-up functionality. Built on .NET 9.0 targeting Jellyfin ABI 10.11.0.

## Architecture

### Component Model
- **Plugin** (`Plugin.cs`): Singleton entry point, exposes `XtreamClient` and `StreamService` instances
- **ConnectionPool** (`Client/ConnectionPool.cs`): Manages multiple Xtream credentials with round-robin load balancing
- **LiveTvService**: Implements `ILiveTvService` for EPG and live channel integration
- **Channel Implementations**: `VodChannel`, `SeriesChannel`, `CatchupChannel` implement `IChannel` for media browsing
- **XtreamClient** (`Client/XtreamClient.cs`): HTTP client wrapper for Xtream API communication
- **StreamService** (`Service/StreamService.cs`): Core business logic for stream management and GUID generation
- **NameFilterService** (`Service/NameFilterService.cs`): Applies regex-based name filters to clean channel and group names
- **XtreamVodProvider** (`Providers/XtreamVodProvider.cs`): Metadata provider with TMDB integration

### Dependency Injection
Services are registered in `PluginServiceRegistrator.cs`:
- Singleton pattern for `IXtreamClient`, `ILiveTvService`, all `IChannel` implementations
- Access via `Plugin.Instance` for plugin configuration and shared services

### ID Generation System
All Jellyfin item IDs are generated via `StreamService.ToGuid(prefix, id1, id2, id3)`:
- Encodes 4 integers into a GUID for stable, predictable IDs
- Prefixes distinguish item types: `LiveTvPrefix`, `VodCategoryPrefix`, `SeriesPrefix`, `EpisodePrefix`, etc.
- Example: `ToGuid(LiveTvPrefix, channel.StreamId, 0, 0)` for live TV channels
- Use `FromGuid()` to decode GUIDs back to integers

### Configuration Management
`PluginConfiguration` uses `SerializableDictionary<int, HashSet<int>>` for category/item selection:
- `LiveTv`, `Vod`, `Series` dictionaries map category IDs to item ID sets
- Empty `HashSet` means "all items in category"
- `DataVersion` property (assembly version + config hash) triggers cache invalidation on updates
- `Credentials` list supports multiple login accounts for load balancing across connections
- `NameFilters` list contains ordered regex patterns for cleaning channel/group names

## Key Patterns

### JSON Deserialization Quirks
Custom converters in `Client/` handle malformed Xtream API responses:
- **StringBoolConverter**: Converts string "0"/"1" to boolean
- **SingularToListConverter<T>**: Wraps single objects into lists (API inconsistency)
- **OnlyObjectConverter**, **Base64Converter**: Handle edge cases in API responses
- **Nullable Error Handling**: `XtreamClient.NullableEventHandler()` silently ignores errors for nullable properties

### Tag Parsing and Name Filtering
`StreamService.ParseName()` cleans and parses stream names:
- **Name Filters**: Applied first via `NameFilterService.ApplyFilters()` using configured regex patterns
  - Filters processed in order defined by `Order` property
  - Supports capture groups ($1, $2, etc.) for selective preservation
  - Only enabled filters are applied
  - 1-second timeout per regex to prevent runaway patterns
- **Tag Extraction**: After filtering, extracts tags from cleaned names
  - Regex pattern: `[TAG]` or `|TAG|` format
  - Also handles Unicode Block Elements (\u2580-\u259F) as tag separators
  - Returns `ParsedName` with cleaned title and extracted tags array

### Credential Exposure Caveat
Xtream URLs include credentials in path. Plugin exposes these via Jellyfin API - document security implications in user-facing changes.

### Channel Override System
`PluginConfiguration.LiveTvOverrides` allows per-channel customization:
- Override channel number, name, logo via `ChannelOverrides` dictionary
- Applied in `StreamService.GetLiveStreamsWithOverrides()`

### Multi-Credential Load Balancing
`ConnectionPool` manages multiple Xtream API credentials:
- Round-robin distribution across enabled credentials
- Falls back to legacy single username/password for backward compatibility
- Each `Plugin.Instance.Creds` call returns next available credential
- Track usage statistics via `GetStatistics()` method

### Name Filter System
`NameFilterService` applies ordered regex patterns to clean channel and group names:
- Filters stored in `PluginConfiguration.NameFilters` as `IList<NameFilter>`
- Each filter has: `Pattern` (regex), `Replacement` (with $1, $2 groups), `Description`, `IsEnabled`, `Order`
- Applied before tag extraction in `StreamService.ParseName()`
- UI in `XtreamNameFilters.html` allows adding/editing/reordering filters
- Example use case: Remove country prefixes like "UK - Channel Name" → "Channel Name" with pattern `^UK\s*-\s*(.*)` and replacement `$1`

## Build & Development

### Build Process
- **Project**: `Jellyfin.Xtream/Jellyfin.Xtream.csproj`
- **Framework**: net9.0
- **Metadata**: `build.yaml` defines plugin version, ABI compatibility, artifacts
- **Command**: `dotnet build` from repository root

### Code Analysis
- StyleCop + MultithreadingAnalyzer + SerilogAnalyzer enabled
- `TreatWarningsAsErrors=true` - must fix all warnings
- Custom rules in `jellyfin.ruleset`: SA1309 disabled (allows `_fieldName`), CA1305/CA2016 enforced (format providers, CancellationToken propagation)
- Documentation generation enabled (`<GenerateDocumentationFile>`)

### Embedded Resources
Configuration web UI files in `Configuration/Web/` are embedded resources (`.html`, `.js`, `.css`). Changes require rebuild to take effect.

## Common Tasks

### Adding New Stream Type
1. Define prefix constant in `StreamService` (e.g., `NewTypePrefix = 0x5d774c40`)
2. Create GUID with `ToGuid(NewTypePrefix, id1, id2, id3)`
3. Implement channel class extending `IChannel`
4. Register in `PluginServiceRegistrator`
5. Update `PluginConfiguration` with selection dictionary

### Adding Scheduled Tasks
- Implement `IScheduledTask` interface with `ExecuteAsync`, `GetDefaultTriggers`, and metadata properties
- Register in `PluginServiceRegistrator` using `AddSingleton<IScheduledTask, YourTask>()`
- Example: `MetadataRefreshTask` pre-downloads metadata for all configured VOD/Series items
- Use `Plugin.Instance` to access configuration and services

### Modifying Xtream API Calls
- All API methods in `XtreamClient.cs` use `ConnectionInfo` from `Plugin.Instance.Creds`
- JSON settings with error handler: `_serializerSettings` with `NullableEventHandler`
- Add converter to model if API response is malformed

### Testing Configuration Changes
`DataVersion` property drives cache invalidation:
- Located in `Plugin.cs`: `Assembly.Version + Configuration.GetHashCode()`
- Channels return this via `DataVersion` property
- Changing config or plugin version automatically invalidates Jellyfin cache

## Important Conventions

- Always propagate `CancellationToken` (CA2016 enforces this)
- Use `IFormatProvider` with string conversions (CA1305 enforces this)
- Prefix private fields with underscore (SA1309 explicitly allowed)
- GPL-3.0 license header required on all source files
- Nullable reference types enabled - handle nullability explicitly
