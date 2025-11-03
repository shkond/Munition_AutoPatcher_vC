# Security Policy

## Supported Versions

Currently, we support the following versions with security updates:

| Version | Supported          |
| ------- | ------------------ |
| main    | :white_check_mark: |
| develop | :white_check_mark: |

## Reporting a Vulnerability

We take the security of Munition AutoPatcher vC seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### Where to Report

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them by:
1. Opening a private security advisory on GitHub (preferred)
2. Creating a private issue through the repository's security tab
3. Contacting the repository maintainers directly

### What to Include

When reporting a vulnerability, please include:

- Type of vulnerability (e.g., code execution, path traversal, etc.)
- Full paths of source file(s) related to the vulnerability
- Location of the affected source code (tag/branch/commit or direct URL)
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days with detailed plan
- **Fix Timeline**: Depends on severity and complexity

### Security Update Process

1. Vulnerability is reported and confirmed
2. Fix is developed and tested privately
3. Fix is released in a security update
4. Public disclosure after users have had time to update

## Security Best Practices

When using Munition AutoPatcher vC:

### Configuration Security
- **Never commit** `config/config.json` with real paths to public repositories
- Use `config.sample.json` as a template
- Store configuration files outside version control when possible

### Plugin Security
- Only load plugins from trusted sources
- Be cautious with large or unknown mod files
- The application reads plugin data but does not execute code from plugins

### File System Access
- The application requires read access to Fallout 4 Data directory
- The application requires write access to output directory
- Ensure proper file permissions on these directories

### Dependencies
- Keep .NET runtime updated to latest version
- Monitor for Mutagen.Bethesda updates
- Review dependency security advisories regularly

## Known Security Considerations

### Plugin Parsing
- The application uses Mutagen to parse Fallout 4 plugins
- Malformed plugins could potentially cause crashes or unexpected behavior
- Always backup important data before processing unknown plugins

### Path Traversal
- User-specified paths are used for reading game data and writing output
- Ensure paths are validated and do not escape intended directories
- Do not run with elevated privileges unless necessary

### Data Processing
- Large mods may consume significant memory
- Process untrusted mods in isolated environments when possible

## Disclosure Policy

When a security vulnerability is fixed:

1. Security advisory is published on GitHub
2. Release notes include security fix details
3. Users are notified through normal release channels
4. CVE may be requested for significant vulnerabilities

## Comments on This Policy

If you have suggestions on how this process could be improved, please submit a pull request or open an issue.

## Attribution

This security policy is based on industry best practices and adapted for this project's needs.
