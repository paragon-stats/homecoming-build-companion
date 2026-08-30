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

### Repository-workflow settings (applied by `make bootstrap`)

These are set `--local` on each clone by `make bootstrap`, so no manual step is needed. They are
listed here because git will not let a repository push configuration to the machines that clone
it — the settings live in `LOCAL_GIT_SETTINGS` in `scripts/dev/bootstrap_venv.py`, and the
bootstrap is the earliest point at which a clone runs repository code.

```bash
# A first push of a new branch sets its upstream, instead of failing with
# "has no upstream branch" and asking for --set-upstream.
git config --local push.autoSetupRemote true
```

`--local` is deliberate: it configures this checkout without overriding whatever the developer
prefers in their other repositories. To add a repository-workflow git setting, extend
`LOCAL_GIT_SETTINGS` rather than documenting a command for each person to run by hand.

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
find . -type f \( -name "*.md" -o -name "*.yml" -o -name "*.yaml" -o -name "*.json" -o -name "*.sh" -o -name "*.py" -o -name "*.tf" \) -exec dos2unix {} \;

# Verify conversion
git status
```

### Pre-commit Hook (Recommended)

Create `.git/hooks/pre-commit`:

```bash
#!/bin/bash
# Pre-commit hook to prevent CRLF line endings

# Check for CRLF line endings in staged files
if git diff --cached --name-only | xargs grep -l $'\r' 2>/dev/null; then
    echo "Error: Files with CRLF line endings detected!"
    echo "All files must use Unix (LF) line endings."
    echo "Run 'dos2unix <filename>' to fix."
    exit 1
fi

exit 0
```

Make it executable:

```bash
chmod +x .git/hooks/pre-commit
```

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
          if find . -type f \( -name "*.md" -o -name "*.yml" -o -name "*.yaml" -o -name "*.json" -o -name "*.sh" -o -name "*.py" -o -name "*.tf" \) -exec grep -l $'\r' {} \; 2>/dev/null | grep .; then
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
- [ ] Pre-commit hook installed and working
- [ ] Files validated with `file` command

---

**Critical**: Line ending configuration is non-negotiable for infrastructure automation reliability and cross-platform compatibility. All violations must be fixed immediately.
