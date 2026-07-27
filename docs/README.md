# Environment Comparison user guide

Environment Comparison answers a focused question: which important Dataverse definitions are missing or different between Environment A and Environment B?

## Quick start

1. Select both saved XrmToolBox connections.
2. Check the areas you want to compare.
3. Compare published metadata unless draft customizations are intentionally in scope.
4. Review Critical and High differences first.
5. Filter to **Missing in Environment B** when validating a target deployment.
6. Export the filtered result when a review record is needed.

## Guides

- [Choosing comparison areas](comparison-areas.md)
- [Interpreting and exporting results](interpreting-results.md)
- [Extending the comparison engine](extending.md)

## Important boundaries

The plugin compares table definitions, column metadata, system forms, and system views. It does not compare table records, personal views, form access assignments, app modules, security roles, solution layers, or managed-versus-unmanaged status.

Every environment operation is read-only. CSV export writes only to the local path selected by the operator.

