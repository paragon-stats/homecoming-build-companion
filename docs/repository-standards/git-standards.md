# Git Configuration for KloehnWars Homelab

## Overview

This document provides mandatory Git configuration settings to ensure all files in the repository use Linux (LF) line endings regardless of the development platform.

## Critical Requirements

**MANDATORY**: All text files must use Linux (LF) line endings for:

- CI/CD pipeline compatibility
- Cross-platform development consistency
- Container and Unix environment compatibility
- Ansible and infrastructure automation reliability

## Repository Configuration

### .gitattributes (Already Configured)

The repository includes a `.gitattributes` file that enforces LF line endings for all text files. This configuration is applied on checkout and takes precedence over local Git settings.

### Required Git Configuration

Every developer must configure Git with these settings:

```bash
# Configure Git to use LF line endings globally
git config --global core.autocrlf false
git config --global core.eol lf

# Configure Git to convert CRLF to LF on commit
git config --global core.safecrlf warn

# Optional: Set default editor with proper line ending support
git config --global core.editor "code --wait"
```

### For Windows Developers (CRITICAL)

Windows developers must run these additional commands:

```bash
# Disable automatic CRLF conversion
git config --global core.autocrlf false

# Ensure Git uses LF for all operations
git config --global core.eol lf

# Verify configuration
git config --list | grep -E "(autocrlf|eol|safecrlf)"
```

### For Existing Repository Clones

If you have an existing clone with incorrect line endings:

```bash
# 1. Ensure you have the latest .gitattributes
git pull origin main

# 2. Remove all files from Git's index
git rm --cached -r .

# 3. Reset the index with correct line endings
git reset --hard

# 4. Verify line endings are now correct
git status
```

## AI Assistant Requirements

### Augment Assistant Guidance

When using Augment in VS Code, ensure generated content follows repository standards:

```markdown
File Format Validation Checklist:

- [ ] Unix/Linux line endings (LF only)
- [ ] UTF-8 encoding without BOM
- [ ] Single trailing newline
- [ ] No trailing whitespace
- [ ] Consistent indentation
```

### GitHub Copilot Configuration

Copilot must respect the repository's line ending configuration:

```markdown
**Generated Code Requirements:**

- All text files: Unix (LF) line endings only
- Cross-platform compatibility maintained
- No Windows (CRLF) line endings under any circumstances
- Validation before code generation completion
```

## Validation and Testing

### Check File Line Endings

```bash
# Check line endings of specific file
file filename.md

# Check multiple files
find . -name "*.md" -exec file {} \;

# Count CRLF files (should be zero)
find . -type f -exec grep -l $'\r' {} \; 2>/dev/null | wc -l
```

### Fix Existing Files

```bash
# Convert CRLF to LF for all text files
find . -type f \( -name "*.md" -o -name "*.yml" -o -name "*.yaml" -o -name "*.json" -o -name "*.sh" -o -name "*.cs" -o -name "*.tf" \) -exec dos2unix {} \;

# Verify conversion
git status
```

### Pre-commit Hook

The Husky `pre-commit` hook runs `scripts/dev/check-text-files.cs`, which refuses a staged file whose line endings differ from what `.editorconfig` sets.
Git runs the hooks in `.husky/` (`core.hooksPath`), so a hook written to `.git/hooks/` never runs; `make bootstrap` installs them.

## Editor Configuration

### VS Code Settings

Add to `.vscode/settings.json`:

```json
{
  "files.eol": "\n",
  "files.insertFinalNewline": true,
  "files.trimTrailingWhitespace": true,
  "files.trimFinalNewlines": true
}
```

### Other Editors

- **Vim**: `set fileformat=unix`
- **Emacs**: `(setq buffer-file-coding-system 'utf-8-unix)`
- **Sublime Text**: `"default_line_ending": "unix"`
- **Atom**: `"line-ending-selector": { "defaultLineEnding": "LF" }`

## CI/CD Integration

### GitHub Actions Validation

```yaml
name: Line Ending Validation
on: [push, pull_request]

jobs:
  line-endings:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Check for CRLF line endings
        run: |
          if find . -type f \( -name "*.md" -o -name "*.yml" -o -name "*.yaml" -o -name "*.json" -o -name "*.sh" -o -name "*.cs" -o -name "*.tf" \) -exec grep -l $'\r' {} \; 2>/dev/null | grep .; then
            echo "❌ Files with CRLF line endings found!"
            exit 1
          else
            echo "✅ All files use correct LF line endings"
          fi
```

## Troubleshooting

### Common Issues

**Problem**: Git shows all files as modified after configuration change
**Solution**:

```bash
git add . -u
git commit -m "Normalize line endings"
```

**Problem**: Files still have CRLF after configuration
**Solution**:

```bash
# Reset and reapply .gitattributes
git rm --cached -r .
git reset --hard
```

**Problem**: Docker containers fail due to script line endings
**Solution**: Ensure all shell scripts use LF line endings:

```bash
find . -name "*.sh" -exec dos2unix {} \;
```

## Quality Gates

### Repository Standards

- [ ] `.gitattributes` configured for all file types
- [ ] All developers have correct Git configuration
- [ ] Pre-commit hooks validate line endings
- [ ] CI/CD pipeline checks line endings
- [ ] AI assistants generate LF-only content

### Developer Checklist

Before committing:

- [ ] Git configured with `core.autocrlf=false`
- [ ] Git configured with `core.eol=lf`
- [ ] Editor configured for Unix line endings
- [ ] Git hooks installed and working (`make hooks-check`)
- [ ] Files validated with `file` command

---

**Critical**: Line ending configuration is non-negotiable for infrastructure automation reliability and cross-platform compatibility. All violations must be fixed immediately.
