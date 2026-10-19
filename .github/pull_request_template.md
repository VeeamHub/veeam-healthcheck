# Pull Request Template

> [!IMPORTANT]
> **All pull requests must target the `dev` branch, not `master`.**
> `master` is the release branch; changes flow `dev` → `master` via release PRs only.
> If you opened this PR against `master` by mistake, change the base branch to `dev` in the dropdown above (no need to close and reopen).
> See [CONTRIBUTING.md → Branching strategy](../blob/dev/CONTRIBUTING.md#branching-strategy) for details.

> [!TIP]
> **Use a Conventional Commit PR title** (`feat: ...`, `fix: ...`, or `feat!: ...` for a breaking change). The release version is computed from commit messages, and if this PR is squash-merged the title is what gets read. See [CONTRIBUTING.md → Commit messages and versioning](../blob/dev/CONTRIBUTING.md#commit-messages-and-versioning).

By contributing, you agree that your contributions will be licensed under the projects original open source license.

## Summary

Brief description of the change and which issue is fixed.

Fixes # (issue)

---

## Changelog

> The sections below populate the GitHub release notes. Fill in what applies; remove sections that don't.

### ✨ Features
<!-- New features or enhancements — one bullet per line -->

### 🐛 Bug Fixes
<!-- Bug fixes — one bullet per line. Use "Fixes #N" to auto-close issues -->

### 🧪 Tests & CI
<!-- Test additions, CI/workflow improvements — one bullet per line -->

---

## Review Details

### Type of change

* [ ] Bug fix (non-breaking change which fixes an issue)
* [ ] New feature (non-breaking change which adds functionality)
* [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected; mark the PR title with `!`, e.g. `feat!:`)
* [ ] This change requires a documentation update

### How Has This Been Tested?

Please describe the tests that you ran to verify your changes. Provide instructions so we can reproduce. Please also list any relevant details for your test configuration.

### Checklist (check all applicable):

* [ ] My code follows the style guidelines of this project
* [ ] I have performed a self-review of my own code
* [ ] I have commented my code, particularly in _hard to understand_ areas
* [ ] I have made corresponding changes to the documentation
* [ ] My changes generate no new warnings
* [ ] I have added tests that prove my fix is effective or that my feature works
* [ ] New and existing unit tests pass locally with my changes
