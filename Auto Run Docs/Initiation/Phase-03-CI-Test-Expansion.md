# Phase 03: CI/CD Test Expansion

The CI/CD test enhancement plan (`Plans/CICD-TEST-ENHANCEMENT-PLAN.md`) identifies two critical blockers at ~60% completion: no golden datasets exist under `test-data/`, and content validation tests are too sparse (20 tests, target 50+). This phase scaffolds the golden dataset directory structure and expands content validation test coverage without requiring a live VBR environment — the structure and test logic are written against the existing CSV/HTML schema knowledge already in the codebase. By the end of this phase, Phase 3 of the CI plan will be unblocked and content validation will have grown from ~20 to 40+ tests.

## Tasks

- [ ] Read the full CI/CD plan at `Plans/CICD-TEST-ENHANCEMENT-PLAN.md` and the existing content validation tests before writing anything:
  - Read `vHC/VhcXTests/ContentValidation/CsvContentValidationTests.cs` fully
  - Read `vHC/VhcXTests/ContentValidation/ReportContentValidationTests.cs` fully
  - Read `Plans/CICD-TEST-ENHANCEMENT-PLAN.md` sections on Phase 3 golden datasets and Phase 2 content validation gaps
  - Note every CSV schema and HTML section already validated, to avoid duplicate coverage

- [ ] Scaffold the golden dataset directory structure:
  - Create `test-data/vbr-v12/` and `test-data/vbr-v13/` directories with a `README.md` in each explaining the expected contents and how to populate them from a live VBR run
  - Add a `.gitkeep` in each so the directories are tracked but clearly empty pending real data
  - Create `test-data/README.md` with front matter (`type: reference, tags: [testing, golden-data]`) documenting the golden dataset strategy from the CI plan

- [ ] Expand CSV content validation tests in `CsvContentValidationTests.cs`:
  - Read `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs` and `CCsvParser.cs` to understand field names and types used in parsing
  - Read the PS collection scripts in `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/` to identify which CSV columns are written per data source
  - Add tests covering at minimum: VBR job CSV required columns, repository CSV required columns, managed server CSV required columns, and session CSV required columns
  - Follow the naming convention `[Method]_[Scenario]_[ExpectedBehavior]` used throughout the project
  - Target: bring CSV validation from ~9 tests to 20+ tests

- [ ] Expand HTML report content validation tests in `ReportContentValidationTests.cs`:
  - Read `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs` to identify sections rendered in the VBR report
  - Add tests for sections not yet covered: security summary section presence, malware detection section, cloud infrastructure section, and accounts/users section (added in the recent `feat: add VBR account/user collection` commit)
  - Add negative-case tests: verify sections do NOT render when the backing CSV is empty or missing
  - Target: bring HTML validation from ~11 tests to 25+ tests

- [ ] Build and run all tests to confirm the new tests compile and any skippable tests (those needing Windows or live data) are correctly marked:
  - Run `dotnet build vHC/HC.sln --configuration Debug`
  - Run cross-platform tests and confirm all new tests either pass or are correctly skipped with `[SkipOn...]` attributes consistent with existing patterns in the test project
  - Fix any failures before marking this phase complete
