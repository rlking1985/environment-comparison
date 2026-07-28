# Environment Comparison user guide

Environment Comparison answers a focused question: which important Dataverse definitions are missing or different between Environment A and Environment B?

## Quick start

1. Select both saved XrmToolBox connections.
2. Check the areas you want to compare.
3. Compare published metadata unless draft customizations are intentionally in scope.
4. Review Critical and High differences first.
5. Filter to **Missing in Environment B** when validating a target deployment.
6. Optionally apply a table logical-name regex such as `^(ata_|mshied_)` to the preview and exports. Reports are retained and the filter is disabled for report-only comparisons.
7. Export the filtered result to CSV, or export all differences in the active table scope to the scalable HTML report when large values, interactive filtering, or rendered diffs are needed.

## Guides

- [Choosing comparison areas](comparison-areas.md)
- [Interpreting and exporting results](interpreting-results.md)
- [Extending the comparison engine](extending.md)

## Important boundaries

The plugin compares table definitions, column metadata, system forms, system views, and organization SSRS reports registered in Dataverse. It does not compare table records, personal views or reports, reports stored only on an external SSRS server, form access assignments, app modules, security roles, solution layers, or managed-versus-unmanaged status.

Every environment operation is read-only. CSV export writes only to the local path selected by the operator.
