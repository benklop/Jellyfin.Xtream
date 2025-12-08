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
- **XtreamVodProvider** (`Providers/XtreamVodProvider.cs`): Metadata provider with TMDB integration for Movies
- **XtreamSeriesProvider** (`Providers/XtreamSeriesProvider.cs`): Metadata provider with TMDB integration for TV Series

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
- `Credentials` list (must be `List<T>` not `IList<T>` for XML serialization) supports multiple login accounts for load balancing
- `NameFilters` list (must be `List<T>` not `IList<T>` for XML serialization) contains ordered regex patterns with scope controls
- **XML Serialization**: Use `List<T>` with `#pragma warning disable CA1002` instead of `IList<T>` to avoid runtime errors

## Key Patterns

### JSON Deserialization Quirks
Custom converters in `Client/` handle malformed Xtream API responses:
- **StringBoolConverter**: Converts string "0"/"1" to boolean
- **SingularToListConverter<T>**: Wraps single objects into lists (API inconsistency)
- **OnlyObjectConverter**, **Base64Converter**: Handle edge cases in API responses
- **LenientDateTimeConverter**: Handles malformed dates like "2024-01-18 (USA)" by extracting YYYY-MM-DD pattern
- **Nullable Error Handling**: `XtreamClient.NullableEventHandler()` silently ignores errors for nullable properties

### Tag Parsing and Name Filtering
`StreamService.ParseName(name, scope)` cleans and parses stream names:
- **Name Filters**: Applied first via `NameFilterService.ApplyFilters(name, filters, scope)` using configured regex patterns
  - Filters processed in order defined by `Order` property
  - Supports capture groups ($1, $2, etc.) for selective preservation
  - Only enabled filters are applied
  - 1-second timeout per regex to prevent runaway patterns
  - **Scope-based filtering**: Each filter has 6 boolean properties controlling where it applies:
    - `ApplyToLiveTvCategories`, `ApplyToLiveTvItems`
    - `ApplyToVodCategories`, `ApplyToVodItems`
    - `ApplyToSeriesCategories`, `ApplyToSeriesItems`
  - All `ParseName()` calls must specify `FilterScope` enum value
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
- Filters stored in `PluginConfiguration.NameFilters` as `List<NameFilter>` (XML serialization requirement)
- Each filter has:
  - `Pattern` (regex), `Replacement` (with $1, $2 groups), `Description`, `IsEnabled`, `Order`
  - Scope controls: `ApplyToLiveTvCategories`, `ApplyToLiveTvItems`, `ApplyToVodCategories`, `ApplyToVodItems`, `ApplyToSeriesCategories`, `ApplyToSeriesItems`
- Applied before tag extraction in `StreamService.ParseName(name, scope)`
- **UI Features** (`XtreamNameFilters.html/js`):
  - Add/edit/reorder filters with drag-like up/down buttons
  - Checkboxes to select which content types each filter applies to
  - **Live Preview**: Real-time preview showing before/after results on sample data
    - Updates 500ms after typing stops (debounced)
    - API endpoint: `POST /Xtream/TestFilter` returns samples from all content types
    - Shows only items that changed, color-coded in blue
  - Regex validation before save
- **FilterScope Enum**: `LiveTvCategory`, `LiveTvItem`, `VodCategory`, `VodItem`, `SeriesCategory`, `SeriesItem`
- Example use case: Remove country prefixes like "UK - Channel Name" → "Channel Name" with pattern `^UK\s*-\s*(.*)` and replacement `$1`, applied only to Live TV items

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

### JavaScript UI Patterns
All configuration pages follow consistent patterns:
- Use `form.dataset.listenerAttached` to prevent duplicate event listeners (not `isInitialized` variable)
- Load data once per tab view using the attached flag
- Access plugin ID via `Xtream.pluginConfig.UniqueId` (never hardcode GUID)
- Tab indices: 0=Credentials, 1=Live TV, 2=TV overrides, 3=Name Filters, 4=VOD, 5=Series
- Use `Dashboard.processPluginConfigurationUpdateResult()` for save feedback

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

### Metadata Providers
Both VOD and Series support TMDB metadata override via Jellyfin's provider system:
- **XtreamVodProvider**: Searches TMDB for movies using parsed title and year
  - Uses `IsTmdbVodOverride` config property (default: true)
  - Sets TMDB provider ID, triggering Jellyfin's metadata refresh
  - Year extracted from `VodInfo.ReleaseDate` field
- **XtreamSeriesProvider**: Searches TMDB for TV series using parsed title and extracted year
  - Uses `IsTmdbSeriesOverride` config property (default: true)
  - Extracts year from series name using regex pattern `\((\d{4})\)` (handles formats like "Series Name (2024)" or "Series Name (2024) (US)")
  - Year extraction improves TMDB matching accuracy since Xtream API doesn't provide structured release date for series
  - Requires TMDB plugin installed and configured in Jellyfin
- Both providers use `providerManager.GetRemoteSearchResults()` with `SearchProviderName = "TheMovieDb"`
- When TMDB ID found, sets `options.ReplaceAllMetadata = true` to fetch full metadata

### Testing Configuration Changes
`DataVersion` property drives cache invalidation:
- Located in `Plugin.cs`: `Assembly.Version + Configuration.GetHashCode()`
- Channels return this via `DataVersion` property
- Changing config or plugin version automatically invalidates Jellyfin cache

### API Controllers
`XtreamController` provides configuration endpoints:
- `GET /Xtream/LiveCategories`, `/VodCategories`, `/SeriesCategories` - List categories
- `GET /Xtream/LiveCategories/{id}`, `/VodCategories/{id}`, `/SeriesCategories/{id}` - Get items in category
- `GET /Xtream/LiveTv` - Get all configured channels with overrides
- `POST /Xtream/TestFilter` - Test name filter against sample data (returns before/after for up to 5 items per type)
- All endpoints require elevation (admin access)
- Use `#pragma warning disable CA3012` for regex injection on authorized endpoints

### Bug Fixes Applied
- **Series LastModified**: Made nullable with `DateTime.UtcNow` fallback (some providers omit this field)
- **XML Serialization**: Changed `IList<T>` to `List<T>` in configuration classes with CA1002 suppressions
- **JavaScript**: Standardized on `form.dataset.listenerAttached` pattern, fixed duplicate plugin GUID references
- **Malformed Dates**: `LenientDateTimeConverter` handles dates with extra text like "2024-01-18 (USA)" on `VodInfo.ReleaseDate` and `EpisodeInfo.ReleaseDate`
- **Add Filter Defaults**: New filters now initialize with all 6 scope properties set to `true` to match backend defaults

## Important Conventions

- Always propagate `CancellationToken` (CA2016 enforces this)
- Use `IFormatProvider` with string conversions (CA1305 enforces this)
- Prefix private fields with underscore (SA1309 explicitly allowed)
- GPL-3.0 license header required on all source files
- Nullable reference types enabled - handle nullability explicitly
