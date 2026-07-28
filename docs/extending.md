# Extending the comparison engine

The project separates retrieval, normalized snapshots, comparison rules, output filtering, export formatting, and WinForms UI behavior.

## Main locations

| Concern | Location |
| --- | --- |
| Snapshot and issue models | `src/EnvironmentComparison/Domain/MetadataModels.cs` |
| Dataverse read-only retrieval and normalization | `src/EnvironmentComparison/Services/DataverseMetadataService.cs` |
| Matching, severity, and comparison rules | `src/EnvironmentComparison/Services/MetadataComparisonService.cs` |
| Shared preview/export table scope | `src/EnvironmentComparison/Services/ComparisonOutputFilterService.cs` |
| CSV, HTML, and JSON output | `src/EnvironmentComparison/Services/*ExportService.cs` |
| Connection workflow and WinForms UI | `src/EnvironmentComparison/Ui/EnvironmentComparisonControl.cs` |
| Synthetic and recording-service tests | `tests/EnvironmentComparison.Tests` |

## Add a comparison area

1. Add a flag to `ComparisonAreas`.
2. Add a normalized immutable model for the component.
3. Load it with read-only SDK requests in `DataverseMetadataService`.
4. Match components by stable solution identity. Add a fallback only when it is deterministic and explain every fallback in Details.
5. Add an important-property severity map and comparison method in `MetadataComparisonService`.
6. Add the UI checkbox, labels, progress messages, and selection-state handling.
7. Include the area in preview filtering and every relevant export.
8. Add tests for identical, changed, missing in A, missing in B, unselected, scoped, and large-definition behavior.
9. Update the user guide, comparison-area reference, safety notes, and troubleshooting guidance.

## Comparison design rules

- Prefer stable logical or component identity over environment-generated IDs.
- Keep fallback identity conservative; ambiguous candidates must remain unmatched.
- Normalize only known deployment noise. Do not suppress a value unless the reason is documented and tested.
- Keep full values in snapshots and exports while using compact previews for large definitions.
- Apply the same table scope semantics to preview, CSV, HTML, and raw JSON.
- Do not add managed state, solution IDs, metadata IDs, timestamps, or version stamps to the default comparison.

The deliberate report-ID exception remains important: a fallback-matched SSRS report emits its differing IDs because matching IDs can matter for managed-solution deployment validation.

## Safety rule

Any future write capability should be built as a separate tool. Environment Comparison's contract is read-only comparison, and its tests and documentation rely on that boundary.

## Validation

Run:

```powershell
.\scripts\Build.ps1
```

The build restores, tests, compiles, and packages without contacting Dataverse. Update or add tests before changing matching, normalization, severity, filtering, or export behavior.

Before opening a pull request, complete the validation and submission steps in [Contributing](../CONTRIBUTING.md). New comparison areas should start with an agreed [feature suggestion](feature-suggestions.md).
