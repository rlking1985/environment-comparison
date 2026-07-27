# Interpreting and exporting results

## Direction

- **Missing in B** means the definition exists in Environment A but not Environment B.
- **Missing in A** means the definition exists only in Environment B.
- **Changed** means both sides were matched but an important property differs.

Environment A is normally the reference/source and Environment B the target being validated.

## Severity

- **Critical** — likely to change storage, behavior, query results, required input, or component availability.
- **High** — important operational, security, audit, format, or component-presence difference.
- **Medium** — meaningful behavior difference that is less likely to block deployment.
- **Low** — display text or description difference worth reviewing but unlikely to break behavior.

Severity is a prioritization aid. Review business context before deciding whether a difference is correct.

## Filters

Search checks table names, table classification, component names and keys, properties, both preview values, and the explanation. Area, severity, and direction filters can be combined. **Export filtered CSV** exports exactly the visible filtered set.

## XML definitions

Formula definitions, forms, and views are normalized before comparison, so indentation, line endings, and XML attribute order do not create false differences. The grid and detail preview show SHA-256 fingerprints to remain responsive. A different fingerprint means the normalized definition changed.

CSV export contains the complete normalized Environment A and Environment B XML values, not the fingerprints. This allows the definitions to be inspected or diffed outside the tool without making the grid hold and render large XML values.

## CSV safety

Every field is quoted. Values beginning with spreadsheet formula characters are prefixed with an apostrophe to prevent formula execution when the file is opened in Excel.

Excel limits a cell to 32,767 characters. When any Environment A or Environment B value exceeds that limit, the export creates numbered columns such as `Environment A (part 1 of 3)`. Each part stays within the Excel-safe limit, the issue remains on one row, and concatenating the numbered parts recreates the complete value. If no values are oversized, the normal 12-column layout is retained.
