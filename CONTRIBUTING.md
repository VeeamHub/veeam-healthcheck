# Contributing

We love your input! We want to make contributing to this project as easy and transparent as possible, whether it's:

* Reporting a bug
* Discussing the current state of the code
* Submitting a fix
* Proposing new features

All contributions to this repository must be signed as described on our [Developer Certificate of Origin](DCO.md). Your signature certifies that you wrote the contribution or have the right to pass it on as an open source contribution.

Please note we have a [Code of Conduct](#code-of-conduct). Please follow it in all your interactions with the project.

## Report Bugs/Feature Requests using the Github Issue Tracker

We use GitHub's Issue Tracker to track bugs/feature Requests. Report a bug or feature request by [opening a new issue](https://github.com/VeeamHub/veeam-healthcheck/issues/new/choose). It's that easy!

## Branching strategy

This repository uses a `dev` → `master` release flow:

- **`master`** — release branch. Protected. Updated only by maintainers via release PRs from `dev`. Do not open PRs directly against `master`.
- **`dev`** — integration branch. **All contributor PRs target `dev`.**
- **Feature branches** — branch off `dev`, open your PR back into `dev`.

To target `dev` from the command line:

```sh
gh pr create --base dev --title "..." --body "..."
```

If you accidentally opened a PR against `master`, you can change the base branch to `dev` in the GitHub UI ("Edit" next to the title → change base) — no need to close and reopen. Maintainers may also retarget your PR for you.

PRs to `master` will appear stuck (mergeability "blocked") because `master`'s required `build-and-test` check only runs on push events, not on PRs. Targeting `dev` avoids this entirely.

## Commit messages and versioning

Release versions are computed from commit messages, so the prefix you choose matters. Use [Conventional Commits](https://www.conventionalcommits.org/):

| Commit | Next version |
|---|---|
| `feat: ...` | minor bump (`3.0.x` → `3.1.0`) |
| `fix: ...`, `chore: ...`, `docs: ...`, anything else | patch bump (`3.1.0` → `3.1.1`) |
| `feat!: ...` (any type with `!`) or a `BREAKING CHANGE:` footer | major bump |

If your PR may be squash-merged, **make the PR title a Conventional Commit**: after a squash, the title is the only place the type is read. `Release-As:` and `BREAKING CHANGE:` footers are read from the commit body.

The `Commit Lint` check on your PR only warns about messages that don't follow the convention; it never blocks a merge. The full rules, and how to preview the version a commit would get, are in the [Versioning section of the workflows README](.github/workflows/README.md#versioning).

## Changelog

User-visible changes are recorded in [`ChangeLog.md`](ChangeLog.md), which follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). If your PR changes what a user sees or gets (new or changed report content, CLI arguments, behaviour, a fix for a reported problem, a security fix), add one line for it under `## [Unreleased]` in the matching group (`Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`). Write it for someone reading the report, not the diff, and link the issue (`[#N](https://github.com/VeeamHub/veeam-healthcheck/issues/N)`) when there is one. Refactors, tests, CI and docs-only changes do not need an entry.

When a release is cut, a maintainer renames `[Unreleased]` to `[x.y.z.r] - YYYY-MM-DD`, starts a fresh empty `[Unreleased]`, and updates the compare links at the bottom of the file. The docs site's Changelog page is copied from this file by CI, so edit `ChangeLog.md` only, never `docs/changelog.md`.

## License

By contributing, you agree that your contributions will be licensed under the projects original open source license.

## Code of Conduct

### Our Pledge

In the interest of fostering an open and welcoming environment, we as
contributors and maintainers pledge to making participation in our project and
our community a harassment-free experience for everyone, regardless of age, body
size, disability, ethnicity, gender identity and expression, level of experience,
nationality, personal appearance, race, religion, or sexual identity and
orientation.

### Our Standards

Examples of behavior that contributes to creating a positive environment
include:

* Using welcoming and inclusive language
* Being respectful of differing viewpoints and experiences
* Gracefully accepting constructive criticism
* Focusing on what is best for the community
* Showing empathy towards other community members

Examples of unacceptable behavior by participants include:

* The use of sexualized language or imagery and unwelcome sexual attention or
advances
* Trolling, insulting/derogatory comments, and personal or political attacks
* Public or private harassment
* Publishing others' private information, such as a physical or electronic
  address, without explicit permission
* Other conduct which could reasonably be considered inappropriate in a
  professional setting

### Our Responsibilities

Project maintainers are responsible for clarifying the standards of acceptable
behavior and are expected to take appropriate and fair corrective action in
response to any instances of unacceptable behavior.

Project maintainers have the right and responsibility to remove, edit, or
reject comments, commits, code, wiki edits, issues, and other contributions
that are not aligned to this Code of Conduct, or to ban temporarily or
permanently any contributor for other behaviors that they deem inappropriate,
threatening, offensive, or harmful.

### Scope

This Code of Conduct applies both within project spaces and in public spaces
when an individual is representing the project or its community. Examples of
representing a project or community include using an official project e-mail
address, posting via an official social media account, or acting as an appointed
representative at an online or offline event. Representation of a project may be
further defined and clarified by project maintainers.

### Enforcement

Instances of abusive, harassing, or otherwise unacceptable behavior may be
reported by contacting the project team at <veeamhub@veeam.com>. All
complaints will be reviewed and investigated and will result in a response that
is deemed necessary and appropriate to the circumstances. The project team is
obligated to maintain confidentiality with regard to the reporter of an incident.
Further details of specific enforcement policies may be posted separately.

Project maintainers who do not follow or enforce the Code of Conduct in good
faith may face temporary or permanent repercussions as determined by other
members of the project's leadership.

### Attribution

This Code of Conduct is adapted from the [Contributor Covenant][homepage], version 1.4,
available at [http://contributor-covenant.org/version/1/4][version]

[homepage]: http://contributor-covenant.org
[version]: http://contributor-covenant.org/version/1/4/
