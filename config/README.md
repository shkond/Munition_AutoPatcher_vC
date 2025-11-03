# Configuration Directory

This directory contains application configuration files.

## Setup

1. Copy `config.sample.json` to `config.json`
2. Edit `config.json` with your specific paths and settings
3. The `config.json` file is gitignored to prevent committing local paths

## Configuration Options

### `gameDataPath`
- Path to your Fallout 4 Data directory
- Usually: `C:\Program Files (x86)\Steam\steamapps\common\Fallout 4\Data`
- Or: `C:\Games\Fallout 4\Data` for GOG/other installations

### `outputIniPath`
- Path where the generated INI file will be saved
- Example: `C:\Users\YourUsername\Documents\RobCoPatcher\MunitionPatcher.ini`

### `mappingStrategy`
- Strategy for mapping weapons to ammunition
- Options: `NameBased`, `TypeBased`, `Manual`
- Default: `NameBased`

### `excludedPlugins`
- Array of plugin names to exclude from processing
- Large mods like `Dank_ECO.esp` may cause false positives
- Add plugins that should not be analyzed

### `logLevel`
- Logging verbosity level
- Options: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`
- Default: `Information`

### `enableDiagnostics`
- Enable detailed diagnostic output for troubleshooting
- Boolean: `true` or `false`
- Default: `false`

## Notes

- Do not commit `config.json` - it contains environment-specific paths
- The application will create a default config if none exists
- Paths can use either forward slashes `/` or backslashes `\\` (escaped in JSON)
