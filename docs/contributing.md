# Contributing

We welcome contributions — bug reports, fixes, new features, and documentation improvements.

## How to Contribute

All contributions to this repository must be signed as described in the [Developer Certificate of Origin](https://github.com/VeeamHub/veeam-healthcheck/blob/master/DCO.md). Your signature certifies that you wrote the contribution or have the right to pass it on as an open source contribution.

### Report a Bug or Request a Feature

[Open a new issue](https://github.com/VeeamHub/veeam-healthcheck/issues/new/choose) — it's that easy.

### Submit a Fix or Feature

1. Fork the repository
2. Create a feature branch from `dev`
3. Write your change with tests where applicable
4. Follow the [test naming convention](architecture/index.md#testing): `[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`
5. Open a pull request against `dev` (`master` is the release branch and only takes release PRs from `dev`)

## Development Setup

```bash
# Restore dependencies
dotnet restore vHC/HC.sln

# Build (debug)
dotnet build vHC/HC.sln --configuration Debug

# Run tests (Windows, macOS, Linux; Windows-only tests skip elsewhere)
dotnet test vHC/VhcXTests/VhcXTests.csproj
```

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/) — `feat:`, `fix:`, `chore:`, `ci:`, `test:`, `docs:`. Feature commits (`feat:`) appear in the auto-generated [Feature Timeline](timeline.md).

## License

By contributing, you agree that your contributions will be licensed under the project's [MIT License](https://github.com/VeeamHub/veeam-healthcheck/blob/master/LICENSE).
