# homecoming-build-companion - project shortcuts
# Usage: make help

# ---------------------------------------------------------------------------
# Bootstrap (cross-platform .NET tools + Husky hooks)
# ---------------------------------------------------------------------------

.PHONY: bootstrap
bootstrap: ## Restore .NET tools and install the Husky hooks (cross-platform)
	dotnet run scripts/dev/bootstrap.cs

# ---------------------------------------------------------------------------
# Lint
# ---------------------------------------------------------------------------

.PHONY: super-linter
super-linter: ## Run full Super-Linter in Docker (same image + config as CI)
	dotnet run scripts/dev/run-super-linter.cs

# Git runs the hooks committed in .husky/ only when core.hooksPath points there and the hook files exist here.
.PHONY: hooks-check
hooks-check: ## Fail with the fix unless git will run the Husky hooks in this checkout
	@dotnet run scripts/dev/bootstrap.cs -- --verify

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
