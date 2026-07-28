# Contributing

Thank you for helping improve Environment Comparison. Contributions are welcome for comparison rules, normalization, exports, performance, accessibility, tests, and documentation.

## Before starting

For a significant feature or new comparison area:

1. review the [roadmap](ROADMAP.md);
2. search existing GitHub issues and pull requests;
3. open a feature suggestion before investing in a large implementation;
4. confirm the design can remain read-only and can be tested without a live Dataverse environment.

Small bug fixes, tests, and documentation corrections can go directly to a pull request when the required behavior is clear.

## Development prerequisites

- Windows
- Git
- .NET Framework 4.8 Developer Pack
- A current .NET SDK or Visual Studio with MSBuild
- XrmToolBox only when manual UI validation is required

Clone your fork and start from the latest `dev` branch:

```powershell
git clone https://github.com/<your-account>/environment-comparison.git
cd environment-comparison
git remote add upstream https://github.com/rlking1985/environment-comparison.git
git fetch upstream
git switch -c feature/short-description upstream/dev
```

Use a focused branch name such as `feature/cloud-flows`, `fix/form-matching`, or `docs/export-guide`.

## Repository structure

| Path | Purpose |
| --- | --- |
| `src/EnvironmentComparison/Domain` | Immutable metadata and comparison models |
| `src/EnvironmentComparison/Services` | Retrieval, normalization, comparison, filtering, and exports |
| `src/EnvironmentComparison/Ui` | XrmToolBox connection workflow and WinForms UI |
| `tests/EnvironmentComparison.Tests` | Synthetic and recording-service tests |
| `docs` | User, safety, troubleshooting, and extension documentation |
| `scripts` | Build, package, and local installation helpers |

## Development rules

### Preserve the read-only contract

Environment calls must remain reads. Do not add create, update, delete, associate, disassociate, import, publish, solution deployment, or environment-repair behavior.

New retrieval code must:

- request only the fields needed for comparison or diagnostics;
- use bounded paging for large component definitions;
- avoid business table records unless the project explicitly changes scope after design review;
- never retrieve or export credentials, connection secrets, or secret environment-variable values;
- report missing permissions clearly rather than attempting to bypass them.

### Match components conservatively

- Prefer stable logical or solution-component identity.
- Use fallback matching only when it identifies exactly one component on each side.
- Include the fallback method and both identities in Details.
- Leave ambiguous candidates unmatched.

### Normalize only proven noise

Normalization must remove only formatting or environment-generated values that are known not to represent behavior. Every new normalization rule needs tests showing:

- the false-positive case is suppressed;
- a nearby meaningful change is still reported;
- complete raw values remain available for diagnostics where appropriate.

### Keep large comparisons responsive

- Do not render complete large XML/JSON values in the main grid.
- Keep retrieval paged and cancellation-aware where supported.
- Reuse shared output filtering so preview and exports agree.
- Add tests for large definitions, escaping, paging, and Excel/browser constraints when relevant.

### Keep changes focused

- Do not combine unrelated refactoring with a functional change.
- Follow the existing naming and formatting style.
- Update user documentation when behavior, filters, matching, severity, output, or permissions change.
- Use fictional names and sanitized metadata in tests, examples, and screenshots.
- Do not increment the package or assembly version in a feature pull request unless the maintainer asks for it.

## Tests and build

Run the complete repository script from the repository root:

```powershell
.\scripts\Build.ps1
```

If local PowerShell policy blocks scripts, use a process-only bypass:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

The script restores packages, runs synthetic tests, builds the .NET Framework 4.8 plugin, and creates the NuGet package. It does not contact Dataverse.

For a quicker test-only cycle:

```powershell
dotnet restore EnvironmentComparison.sln
dotnet test EnvironmentComparison.sln -c Release --no-restore
```

Add or update tests for every changed comparison, matching, normalization, filter, export, or UI-state rule. Automated tests must not require credentials or a live environment.

## Manual validation

Manual XrmToolBox testing is useful for connection workflow, layout, performance, and real SDK behavior. Use only environments you are authorized to access.

Do not include real environment names, URLs, email addresses, tenant IDs, report contents, schema names, or customer metadata in a pull request. Replace screenshots and sample exports with fictional mock data before submission.

Record:

- selected comparison areas;
- published or unpublished mode;
- whether the run completed;
- filter/export scenarios checked;
- any intentionally untested area.

## Commit guidance

- Write concise, imperative commit messages.
- Keep commits reviewable and logically grouped.
- Do not commit `bin`, `obj`, packages, local XrmToolBox files, generated exports, credentials, or real metadata captures.
- Rebase or merge the current upstream `dev` branch before requesting final review if the branch is out of date.

## Submit a pull request

Push your branch to your fork and open a pull request with `dev` as the base branch:

```powershell
git push -u origin feature/short-description
```

The pull request should include:

- a short, action-oriented title;
- the problem and resulting behavior;
- the comparison areas and exports affected;
- identity, matching, normalization, severity, or permission decisions;
- tests added and the build result;
- sanitized screenshots for visible UI changes;
- documentation changes;
- known limitations or follow-up work;
- a linked feature request or bug when one exists.

Complete the repository pull request template. Keep the pull request scoped so reviewers can validate the behavioral change and its false-positive risk.

## Review expectations

Reviewers will prioritize:

- correctness of identity and matching;
- false-positive and false-negative risk;
- preservation of meaningful metadata differences;
- read-only and secret-safe behavior;
- bounded retrieval and large-data performance;
- consistent plugin/CSV/HTML/JSON output;
- automated test coverage;
- user-facing documentation.

Changes may be requested even when the code works against one environment if the identity or normalization rule is not safe across deployments.

## Licensing

By submitting a contribution, you agree that it may be distributed under the repository's [MIT License](LICENSE).
