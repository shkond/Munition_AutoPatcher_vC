# Munition AutoPatcher vC - Product Overview

## Purpose
Munition AutoPatcher vC is a specialized WPF desktop application designed to automate ammunition mapping for Fallout 4 weapon modifications. The tool extracts weapon data from game plugins using Mutagen and generates patches to apply ammunition mappings, streamlining the modding workflow for Fallout 4 content creators.

## Core Value Proposition
- **Automated Weapon Analysis**: Eliminates manual inspection of weapon plugins by automatically extracting weapon data, damage values, firing rates, and default ammunition assignments
- **Dual Output Modes**: Supports both direct ESP patch generation (default) and RobCo Patcher INI configuration files
- **Conflict Resolution**: Uses Mutagen's WinningOverrides system to handle plugin conflicts and load order dependencies
- **Production Ready**: Generates ESL-flagged ESP patches that minimize load order impact while providing direct weapon record modifications

## Key Features

### Data Extraction & Analysis
- **Plugin Integration**: Automatically detects Fallout 4 installation and loads plugin data respecting load order
- **Weapon Data Mining**: Extracts comprehensive weapon information including names, damage, fire rates, and ammunition references
- **Ammunition Detection**: Identifies and catalogs available ammunition types from loaded plugins
- **Conflict Resolution**: Handles overlapping weapon modifications using Mutagen's conflict resolution system

### Mapping Management
- **Interactive Mapping Interface**: Provides UI for reviewing and editing weapon-to-ammunition mappings
- **Automatic Mapping Generation**: Implements intelligent mapping strategies based on weapon characteristics
- **Manual Override Support**: Allows users to manually adjust mappings before patch generation
- **Validation System**: Ensures mapping integrity and identifies potential conflicts

### Output Generation
- **ESP Patch Mode (Default)**: Creates `MunitionAutoPatcher_Patch.esp` with direct weapon record modifications
  - ESL-flagged for minimal load order impact
  - Automatic master reference inclusion
  - Direct WEAP record patching
- **INI Configuration Mode**: Generates timestamped RobCo Patcher configuration files
  - Compatible with existing RobCo Patcher workflows
  - Manual mapping flag support
  - Automatic directory creation

### User Experience
- **Real-time Logging**: Comprehensive logging system with UI display and file output
- **Progress Tracking**: Visual feedback during long-running operations
- **Configuration Management**: Persistent settings with JSON-based configuration
- **Error Handling**: Robust error handling with user-friendly feedback

## Target Users

### Primary Users
- **Fallout 4 Mod Authors**: Content creators developing weapon modifications who need automated ammunition mapping
- **Mod Pack Curators**: Users managing large collections of weapon mods requiring consistent ammunition assignments
- **Advanced Modders**: Experienced users working with complex plugin hierarchies and load order management

### Use Cases
- **New Weapon Integration**: Automatically assign appropriate ammunition to newly added weapons
- **Mod Compatibility**: Resolve ammunition conflicts between multiple weapon modification plugins
- **Batch Processing**: Process large numbers of weapon modifications efficiently
- **Quality Assurance**: Validate ammunition assignments across mod collections
- **Workflow Automation**: Integrate ammunition mapping into existing modding pipelines

## Technical Advantages
- **Mutagen Integration**: Leverages industry-standard Bethesda plugin manipulation library
- **MVVM Architecture**: Clean separation of concerns enabling maintainable and testable code
- **Dependency Injection**: Modern .NET practices with Microsoft.Extensions.DependencyInjection
- **Asynchronous Operations**: Non-blocking UI during intensive data processing operations
- **Extensible Design**: Service-oriented architecture supporting future enhancements