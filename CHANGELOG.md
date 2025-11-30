# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- GitHub Copilot Coding Agent configuration
- Issue templates (bug report, feature request, configuration issue)
- Pull request template with comprehensive checklist
- CONTRIBUTING.md with development guidelines
- CODE_OF_CONDUCT.md (Contributor Covenant v2.0)
- SECURITY.md with security policy
- Example GitHub Actions CI workflow
- Configuration sample files (config.sample.json)
- Configuration documentation (config/README.md)
- ADR-009: FormKeyNormalizer bug fix documentation

### Changed
- Enhanced .github/copilot-instructions.md with detailed guidelines
- Updated README.md with references to contribution guidelines
- Updated README.md project structure to reflect current codebase (Models: 9, Interfaces: 17, Implementations: 30)
- MutagenV51EnvironmentAdapter now supports InMemoryLinkCache for testing
- WeaponDataExtractor sets CandidateFormKey to COBJ record instead of created Weapon

### Fixed
- **FormKeyNormalizer double extension bug** - `new ModKey(fileName, modType)` incorrectly added extensions twice (e.g., "TestMod.esp.esp"). Fixed by using `ModKey.FromNameAndExtension()`.
- **WeaponDataExtractor CandidateFormKey** - Was incorrectly set to CreatedObject (Weapon) FormKey instead of COBJ FormKey, causing LinkCache resolution failures.
- **MutagenV51EnvironmentAdapter InMemoryLinkCache** - Added support for in-memory LinkCache to enable proper E2E testing.
- **TestEnvironmentBuilder mod duplication** - `WithPlugin()` now reuses existing mods instead of creating duplicates.
- E2E tests now pass with 193/193 tests successful

### Removed
- Obsolete archive files: DECISIONS_old*.md, plan*.md, PR_*.md, CONSTITUTION.md (superseded by .specify/memory/constitution.md)

## [0.1.0] - Previous Work

### Added
- WPF application with MVVM architecture
- Mutagen integration for plugin parsing
- Weapon data extraction from Fallout 4 plugins
- Ammunition mapping functionality
- RobCo Patcher INI file generation
- Configuration service with JSON persistence
- Comprehensive logging system
- Unit and integration tests

### Features
- Settings view for game data path configuration
- Mapper view for weapon-ammunition mapping
- Real-time log display
- Automatic load order detection
- WinningOverrides conflict resolution
- Support for large mod exclusion (e.g., Dank_ECO.esp)

---

## Version History Format

Each version should include sections as applicable:

### Added
- New features

### Changed
- Changes in existing functionality

### Deprecated
- Soon-to-be removed features

### Removed
- Removed features

### Fixed
- Bug fixes

### Security
- Security fixes or improvements

[Unreleased]: https://github.com/shkond/Munition_AutoPatcher_vC/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/shkond/Munition_AutoPatcher_vC/releases/tag/v0.1.0
