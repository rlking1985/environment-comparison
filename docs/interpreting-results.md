# Interpreting results

## Direction

- **Missing in B** means the definition exists in Environment A but not Environment B.
- **Missing in A** means the definition exists only in Environment B.
- **Changed** means both sides were matched but an important property differs.

Environment A is normally the reference or source. Environment B is the target being validated.

For SSRS reports, Report ID is the primary identity. If IDs differ, the plugin can fallback match a unique report name, filename, report type, and language combination. Details explains every fallback and includes both IDs. If that identity is duplicated, no fallback match is made.

## Severity

| Severity | Intended meaning |
| --- | --- |
| Critical | Likely to affect storage, required input, runtime behavior, queries, or component availability |
| High | Important operational, security, audit, format, identity, or component-presence difference |
| Medium | Meaningful behavior difference that is less likely to block a deployment |
| Low | Display text or description difference worth reviewing but unlikely to break behavior |

Severity is a prioritization aid, not an automatic deployment decision. Review the business purpose of each component before accepting or rejecting a difference.

## Result columns

| Column | Meaning |
| --- | --- |
| Severity | Suggested review priority |
| Area | Table, Column, Form, View, or Report |
| Difference | Missing in A, Missing in B, or Changed |
| Table | Display and logical name for table-associated rows |
| Table classification | Standard, Intersect, BPF, or blank for organization-level rows |
| Component A / Component B | Environment-specific component name. Tables and columns include their logical name; forms, views, and reports use their display name. The missing side is blank for presence differences. |
| Component A (ID) / Component B (ID) | Environment-specific component GUID where Dataverse supplies one |
| Property | The presence or metadata setting that differs |
| Environment A/B | Compact values or definition fingerprints |
| Details | Plain-language explanation and any fallback or rename hint |

## Filters

The plugin filters can be combined:

- **Search** checks table names, classification, component names, logical names, IDs, property, preview values, and Details.
- **Table logical name regex** scopes table, column, form, and view rows after retrieval.
- **Classification** is populated from the comparison. **No classification** selects reports and other organization-level rows.
- **Area** limits the component type.
- **Severity** limits review priority.
- **Difference** limits direction or changed rows.

The regex does not remove report or organization-level rows. It is disabled when only reports are selected.

Select **Clear filters** to return to the complete result within the active table regex scope.

![A selected fictional column difference](images/selected-difference.png)

## Definition fingerprints

Formula definitions, forms, views, report filters, and RDL are normalized before comparison. Indentation, line endings, and XML attribute order do not create false differences.

The plugin grid shows SHA-256 fingerprints for changed large definitions. A different fingerprint means the normalized content is different; it does not show where it differs.

Use one of these options to inspect complete values:

- CSV for complete normalized values split into Excel-safe cells when needed;
- HTML **Inspect** for side-by-side values and enhanced diffs;
- raw JSON for retrieved and normalized diagnostic snapshots.

For view Layout XML, only the environment-specific root `grid/@object` value is ignored. If another layout setting differs, the issue remains and exports retain the complete XML.

## Common review patterns

### A table or column is Missing in B

Confirm the component should be deployed to the target. A missing table also causes every selected column on that table to appear missing, so start with the table row before reviewing its dependent column rows.

### A column has a possible rename hint

The plugin found an A-only and B-only column with compatible metadata. Verify whether the B column was deliberately recreated or whether the source column is missing. Logical names remain important for forms, views, integrations, plugins, reports, and managed-solution identity.

### Many forms or views appear missing

Confirm the connection direction, published/unpublished choice, and permissions. Forms match by exact Form ID first, then by a unique combination of table, `UniqueName`, form type, and presentation. A `Form identity` issue means multiple candidates shared that fallback identity and the plugin deliberately did not guess. Views use their saved-query identity.

### A system view reports Layout XML

Root object type codes are already ignored. Remaining layout differences indicate another attribute, row, cell, order, or width changed.

### A report is fallback matched

Read Details. The plugin found one report on each side with the same name, filename, report type, and language but different IDs. The report is compared as one component, and the ID difference remains visible because matching IDs can matter for solution deployment.

## Managed and unmanaged components

Managed-versus-unmanaged status is intentionally not compared. The plugin reports metadata behavior, presence, and definitions, not solution layering. A difference that exists only because one component is managed and the other is unmanaged should therefore not appear unless an important property also differs.

Continue to [Exporting results](exporting-results.md) or [Troubleshooting](troubleshooting.md).
