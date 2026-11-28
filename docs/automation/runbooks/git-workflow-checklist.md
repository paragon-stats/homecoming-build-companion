# Git Workflow Checklist: Issue → PR → Merge

## Overview

This runbook documents the step-by-step Git workflow for LLM-assisted development with human-in-the-loop gates and trunk-based development cleanup.

**Key principles**:

- TDD-first (Red-Green-Refactor)
- Human approval gates (commit, push, merge)
- Protected operations (USER ONLY: push, merge, remote branch deletion)
- Post-merge cleanup (LLM: switch to main, delete local branch)

## Phase 1: Issue Selection & Branch Setup

- [ ] Identify issue to work on (from GitHub Projects board)
- [ ] Create local branch: `git checkout -b <type>/<scope>/<description>`
  - See [docs/repository-standards/naming-standards.md](../../repository-standards/naming-standards.md#git-branch-naming) for branch naming conventions
  - Example: `docs/templates/issue-template-tdd`
- [ ] Make initial changes (if needed before publishing branch)
  - Fix any DRY violations discovered during setup
  - Update related documentation
- [ ] Stage and commit initial changes: `git add <files>` then `git commit`
  - Use conventional commit format
  - Required before publishing branch to remote
- [ ] **HUMAN ACTION**: User publishes branch to remote
  - Via GUI: Use "Publish Branch" button in VS Code/IDE
  - Via CLI: `git push -u origin <branch-name>`
- [ ] Verify branch published: `git status -sb` (should show `origin/<branch-name>`)

## Phase 2: TDD Implementation (Red-Green-Refactor)

### Red: Write Failing Test First

- [ ] Identify test type needed:
  - Unit test: Single function/module logic
  - Integration test: Component interaction
  - E2E test: Full workflow validation
- [ ] Create test file (if doesn't exist):
  - PowerShell: `tests/<component>/<feature>.Tests.ps1`
  - C#: `tests/<Project>.Tests/<Area>/<Feature>Tests.cs`
  - Ansible: `tests/ansible/<role>/molecule/`
- [ ] Write failing test with clear assertion
- [ ] Run test manually: `dotnet test --project tests/<Project>.Tests --filter-method "*<TestName>"` or `Invoke-Pester <test-file>`
- [ ] Verify failure with expected error message
- [ ] Document expected behavior in test name/docstring

### Green: Implement Minimal Code to Pass

- [ ] Write minimal code to pass test (no extra features)
- [ ] Run test manually: verify pass
- [ ] Verify only targeted test passes (no side effects)
- [ ] Document any assumptions or limitations

### Refactor: Improve Quality and Maintainability

- [ ] Refactor for clarity (readable variable names, clear logic flow)
- [ ] Refactor for maintainability (DRY, single responsibility)
- [ ] Refactor for performance (if needed, with benchmarks)
- [ ] Run test manually: verify still passes after refactor
- [ ] Check for code duplication across codebase

## Phase 3: Local Validation

- [ ] Run targeted test suite:
  - PowerShell: `Invoke-Pester -Path tests/<component>/`
  - C#: `dotnet test --project tests/<Project>.Tests`
  - Ansible: `molecule test`
- [ ] Run the commit hook's checks on the staged files: `dotnet husky run --group pre-commit`
- [ ] Check test coverage:
  - PowerShell: Review Pester CodeCoverage output
  - C#: `dotnet tool run dotnet-coverage collect "dotnet test" -f xml -o coverage.xml`
  - Target: 100% line coverage (the build job's gate)
- [ ] Check CI test coverage:
  - Review `.github/workflows/` for relevant test jobs
  - If no CI test exists for new code, add test case to CI workflow

## Phase 4: Commit & Pre-Commit Checks

- [ ] Check git status: `git status --porcelain`
  - **CRITICAL**: If any `D` (deletion) entries exist, STOP and verify with user
  - NEVER commit unintended deletions
- [ ] Stage changes: `git add <files>`
  - Stage only files related to current issue
  - Avoid `git add .` or `git add -A` without review
- [ ] **HUMAN IN THE LOOP**: Notify user for review before commit
  - Show staged files: `git diff --cached --stat`
  - Highlight any deletions, renames, or large diffs
  - Wait for explicit user approval
- [ ] **REQUIRE**: Write conventional commit message using standard format

  - **NEVER** use non-conventional commit formats
  - **ENFORCE** structure: `<type>(<scope>): <subject>` with optional body and footer
  - **One Way To Win**: See [Conventional Commits](https://www.conventionalcommits.org/) for format
  - **REQUIRE** `Closes #<issue-number>` in footer for issue-closing commits
  - **REQUIRE** `Related: #<issue-number>` in footer for related issues

- [ ] Commit: `git commit` (triggers pre-commit hooks)
- [ ] Monitor pre-commit output:
  - [ ] No deletions flagged (git status check)
  - [ ] All hooks pass (gitleaks, markdownlint, shellcheck, etc.)
  - [ ] No major changes flagged (file renames, large diffs)
- [ ] **HUMAN IN THE LOOP**: Notify user of any major changes flagged
  - File renames or moves
  - Large diffs (>100 lines in single file)
  - New dependencies added
  - Configuration changes
- [ ] Resolve any pre-commit failures:
  - [ ] Fix issues locally
  - [ ] Re-stage changes: `git add <files>`
  - [ ] Re-commit: `git commit --amend` (if fixing same commit) or new commit
  - [ ] Repeat until pre-commit passes

## Phase 5: Push & CI Validation

- [ ] **HUMAN IN THE LOOP**: Notify user change is ready to sync to remote
  - Summary: Files changed, tests passing, pre-commit clean
  - Recommendation: Ready to push
- [ ] **HUMAN ACTION**: User pushes to remote
  - Via GUI: Use "Sync Changes" or "Push" button in VS Code/IDE
  - Via CLI: `git push`
  - **LLM NEVER performs push operations**
- [ ] Monitor CI runs (GitHub Actions):
  - [ ] All jobs pass (tests, linters, security scans)
  - [ ] No errors or warnings above INFO level
  - [ ] Test coverage meets the 100% line-coverage gate
  - [ ] No new security vulnerabilities detected
- [ ] On CI failures:
  - [ ] Review CI logs for root cause
  - [ ] Make changes locally (return to Phase 2: TDD Implementation)
  - [ ] Re-commit locally (Phase 4: Commit & Pre-Commit Checks)
  - [ ] **HUMAN ACTION**: User re-pushes (return to Phase 5)
  - [ ] Repeat until CI passes
- [ ] **REQUIRE**: Create draft PR using `.github/pull_request_template.md` structure
  - **NEVER** create PR with custom body format
  - **ENFORCE** template sections: Summary, Deliverables, Success Criteria, Dependencies, Validation
  - **REQUIRE** `Closes #<issue-number>` in "Linked Issue" line
  - **REQUIRE** `Related: #<issue-number>` in Dependencies section for related issues
  - **One Way To Win**: Copy template structure exactly, fill in all sections
- [ ] **REQUIRE**: Return to issue and verify all deliverables complete
  - Check issue acceptance criteria against PR changes
  - If incomplete: return to Phase 2 (TDD Implementation) for missing work
  - If complete: proceed to Phase 6 (PR Review & Merge)
  - **One Way To Win**: Issue deliverables = PR deliverables (no drift)

## Phase 6: PR Review & Merge

- [ ] Update PR description with final changes (LLM can update via github-api)
  - Summary of changes
  - Link to issue: `Closes #<issue-number>`
  - Link to related issues/PRs
  - Validation results (tests passing, CI green)
- [ ] Request review (if applicable)
  - Tag reviewers if needed
  - Respond to review comments
- [ ] Address review feedback:
  - [ ] Make requested changes locally (return to Phase 2)
  - [ ] Re-commit and re-push (Phases 4-5)
  - [ ] Update PR with responses to comments
- [ ] **HUMAN ACTION**: User approves and merges PR
  - Via GitHub UI: Click "Merge pull request"
  - **LLM NEVER merges PRs**
- [ ] **HUMAN ACTION**: User deletes remote branch
  - Via GitHub UI: Click "Delete branch" after merge
  - Via CLI: `git push origin --delete <branch-name>`
  - **LLM NEVER deletes remote branches**

## Phase 7: Post-Merge Cleanup (LLM Actions)

- [ ] Switch to main branch: `git checkout main`
- [ ] Pull latest changes: `git pull origin main`
- [ ] Delete merged local branch: `git branch -d <branch-name>`
  - If branch not fully merged, use `-D` flag (with caution)
- [ ] Verify clean state: `git status`
  - Should show: "nothing to commit, working tree clean"
  - Should show: "Your branch is up to date with 'origin/main'"
- [ ] Notify user: "Post-merge cleanup complete. Local branch deleted, switched to main."

## Validation Gates

### LLM NEVER Performs (Protected Operations)

- `git push` (any variant) - **USER ONLY**
- `git push --force` - **USER ONLY**
- `git push -u origin <branch>` - **USER ONLY**
- PR merge (via GitHub UI or API) - **USER ONLY**
- Remote branch deletion (`git push origin --delete <branch>`) - **USER ONLY**

### LLM Can Perform (Allowed Operations)

- Local commits (`git commit`)
- Local branch operations (`git checkout`, `git branch`, `git branch -d`)
- PR creation/updates (via github-api tool)
- Post-merge cleanup (switch to main, pull, delete local branch)
- Git status checks (`git status`, `git diff`, `git log`)

### Human-in-the-Loop Gates (Approval Required)

- **Before commit**: Review staged changes, verify no unintended deletions
- **Before push**: Confirm changes ready to sync to remote
- **Before merge**: Approve PR, verify CI passing, review final changes

## Troubleshooting

### Pre-Commit Hooks Failing

**Problem**: Pre-commit hooks fail with linting/formatting errors

**Solution**:

1. Review pre-commit output for specific errors
2. Fix issues locally (run linters manually if needed)
3. Re-stage changes: `git add <files>`
4. Re-commit: `git commit --amend` or new commit
5. Repeat until hooks pass

### CI Failing After Push

**Problem**: CI jobs fail after push to remote

**Solution**:

1. Review CI logs in GitHub Actions
2. Identify failing job and error message
3. Reproduce failure locally (run same test/linter)
4. Fix issue locally (return to Phase 2: TDD)
5. Re-commit and notify user to re-push
6. Monitor CI until passing

### Unintended Deletions Detected

**Problem**: `git status --porcelain` shows `D` entries

**Solution**:

1. **STOP immediately** - do not commit
2. Review deleted files: `git status`
3. Verify deletions are intentional with user
4. If unintentional: `git restore <file>` to recover
5. If intentional: Document reason in commit message
6. Proceed with commit only after user approval

### Merge Conflicts

**Problem**: PR has merge conflicts with main branch

**Solution**:

1. Pull latest main: `git checkout main && git pull origin main`
2. Switch back to feature branch: `git checkout <branch-name>`
3. Merge main into feature branch: `git merge main`
4. Resolve conflicts manually (edit files, remove conflict markers)
5. Stage resolved files: `git add <files>`
6. Commit merge: `git commit`
7. Notify user to push resolved conflicts

## See Also

- [TDD Strategy and Tactics](../../testing/tdd-strategy-and-tactics.md)
- [Husky hook tasks](../../../.husky/task-runner.json)
- [CI/CD Workflows](../../../.github/workflows/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [Pull Request Template](../../../.github/pull_request_template.md)
