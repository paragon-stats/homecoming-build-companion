# homecoming-build-companion - project shortcuts
# Usage: make lint | make fix | make test | make cov

# ---------------------------------------------------------------------------
# Environment detection
# ---------------------------------------------------------------------------

# Auto-detect venv bin path and executable suffix (Linux/macOS vs Windows)
ifneq ($(wildcard .venv/bin/python),)
	VENV_BIN ?= .venv/bin
	EXE :=
else
	VENV_BIN ?= .venv/Scripts
	EXE := .exe
endif

PYTHON  := $(VENV_BIN)/python$(EXE)
RUFF    := $(VENV_BIN)/ruff$(EXE)
MYPY    := $(VENV_BIN)/mypy$(EXE)
PYTEST  := $(PYTHON) -m pytest

# Bare --cov defers to [tool.coverage.run] source in pyproject.toml (scripts + src).
COV_PACKAGE ?=

# ---------------------------------------------------------------------------
# Bootstrap (cross-platform venv + dev tooling)
# ---------------------------------------------------------------------------

.PHONY: bootstrap
bootstrap: ## Bootstrap repo venv and dev tooling (cross-platform)
ifeq ($(OS),Windows_NT)
	pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev/Bootstrap-Venv.ps1
else
	./scripts/dev/bootstrap_venv.sh
endif

# ---------------------------------------------------------------------------
# Lint / Format / Type
# ---------------------------------------------------------------------------

.PHONY: lint
lint: ## Run ruff check + format --check + mypy
	$(RUFF) check .
	$(RUFF) format --check .
	$(MYPY) scripts src

.PHONY: fix
fix: ## Auto-fix ruff lint issues and reformat
	$(RUFF) check --fix .
	$(RUFF) format .

.PHONY: super-linter
super-linter: ## Run full Super-Linter in Docker (same image + config as CI)
	$(PYTHON) -m scripts.linting.run_super_linter

# Git runs the hooks committed in .husky/ only when core.hooksPath points there.
.PHONY: hooks-check
hooks-check: ## Fail with the setup command when the Husky hooks are not installed
	@$(if $(filter .husky,$(shell git config core.hooksPath)),echo Husky hooks are installed.,$(error Husky hooks are not installed. Run once: dotnet tool restore && dotnet husky install))

# ---------------------------------------------------------------------------
# Test / Coverage
# ---------------------------------------------------------------------------

.PHONY: test
test: ## Run all tests (fast, no coverage)
	$(PYTEST)

.PHONY: cov
cov: ## Run tests with coverage gate (--cov-fail-under=100)
	$(PYTEST) --cov$(if $(COV_PACKAGE),=$(COV_PACKAGE)) --cov-report=term-missing

.PHONY: cov-html
cov-html: ## Run tests with HTML coverage report
	$(PYTEST) --cov$(if $(COV_PACKAGE),=$(COV_PACKAGE)) --cov-report=html
	@echo "Open htmlcov/index.html"

# ---------------------------------------------------------------------------
# Validator (Phase 6)
# ---------------------------------------------------------------------------

.PHONY: validate
validate: ## Validate a build JSON (validator not yet implemented). Usage: make validate BUILD=path/to/build.json
	@if [ -z "$(BUILD)" ]; then echo "Usage: make validate BUILD=path/to/build.json"; exit 1; fi
	@echo "error: E01: build validator not yet implemented (Phase 6); no scripts.build_validator module exists — track progress in docs/engine/mids-port-spec.md (CP6 legality)"; exit 1

# ---------------------------------------------------------------------------
# .NET solution (CohEngine.slnx)
# ---------------------------------------------------------------------------

.PHONY: dotnet-build
dotnet-build: ## Build the .NET solution (warnings are errors)
	dotnet build -warnaserror

.PHONY: dotnet-test
dotnet-test: ## Run the .NET tests
	dotnet test

.PHONY: dotnet-format
dotnet-format: ## Verify .NET formatting (dotnet format --verify-no-changes)
	dotnet format --verify-no-changes --severity error

.PHONY: dotnet-publish
dotnet-publish: ## Publish the Native AOT win-x64 CLI
	dotnet publish src/CohEngine.Cli -c Release -r win-x64

# ---------------------------------------------------------------------------
# Help
# ---------------------------------------------------------------------------

.PHONY: help
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  %-15s %s\n", $$1, $$2}'

.DEFAULT_GOAL := help
