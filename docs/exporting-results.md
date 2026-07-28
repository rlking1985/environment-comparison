# Exporting results

The plugin provides three local export formats. Choose the smallest format that answers the current question.

| Format | Best for | Scope |
| --- | --- | --- |
| Filtered CSV | Sharing a focused issue list or opening results in Excel | Exactly the rows visible under all current plugin filters |
| Filterable HTML | Large comparisons, long XML, interactive filtering, and visual diffs | Every difference in the active table regex scope; browser filters are independent |
| Raw metadata JSON | Troubleshooting component retrieval or identity matching | Structured snapshots from both environments within the active table regex scope |

All three actions write to a local path chosen by the operator. They do not export a Dataverse solution and do not modify either environment.

## Filtered CSV

**Export filtered CSV** respects the current:

- free-text search;
- table logical-name regex;
- table classification;
- area;
- severity;
- difference filter.

Clear or change filters before exporting if the file needs a broader set.

Every field is quoted. Values beginning with spreadsheet formula characters are prefixed with an apostrophe to prevent formula execution when opened in Excel.

### Excel cell limits

Excel limits one cell to 32,767 characters. If an Environment A or Environment B value exceeds that limit, the plugin creates numbered columns such as:

- `Environment A (part 1 of 3)`;
- `Environment A (part 2 of 3)`;
- `Environment A (part 3 of 3)`.

The issue remains on one row. Joining the numbered parts in order recreates the complete value. Files without oversized values retain the normal Environment A and Environment B columns.

Use HTML or raw JSON when long definitions are the main focus.

## Filterable HTML

**Export filterable HTML** creates one standalone report containing every difference in the active table regex scope. It does not copy the plugin's other filters, because the browser report provides independent filters for:

- search;
- severity;
- area;
- difference;
- table;
- table classification;
- property.

The report header identifies Environment A and Environment B. It supports sortable and drag-resizable columns, resettable widths, summary counts, pagination, and page sizes of 50, 100, 250, or 500 rows. Only the current page is rendered, which keeps large reports responsive.

The base report works as a local file and makes no environment calls. It embeds the comparison values that existed when it was exported.

### Inspect and diff

**Inspect** is available for changed rows where both sides have values. Presence-only missing rows do not show an inspection action.

The inspector provides:

- complete Environment A and Environment B values;
- wrapping on or off;
- copy and download actions;
- side-by-side or line-by-line enhanced diff;
- a toggle back to normal values;
- contained horizontal and vertical scrolling for large definitions.

Report rows also expose complete normalized and raw RDL XML, even when the selected report issue concerns another property.

Enhanced diff loads pinned jsdiff and Diff2Html libraries from jsDelivr each time the local report is opened. If internet or CDN access is unavailable, filtering, complete normal values, copy, and download continue to work; only enhanced rendering is unavailable.

Unlike Excel, the HTML report does not split values at 32,767 characters. Its practical limits are the generated file size and the browser's available memory.

## Raw metadata JSON

**Export raw metadata (JSON)** is a diagnostic snapshot, not only a list of differences. It contains hierarchical Environment A and Environment B metadata, including values that compare equal.

When a table regex is active, table, column, form, and view snapshots are limited to that scope. Reports and other organization-level data are retained. With no regex, table-associated snapshots are unfiltered.

The JSON preserves structure, identifiers, null-equivalent empty values, line breaks, retrieved XML, and normalized XML without Excel splitting. Depending on the selected areas, it includes:

- tables and columns;
- form IDs, `UniqueName`, type and state, role identities, retrieved Form XML, and normalized Form XML;
- view IDs, retrieved FetchXML/Layout XML/column-set XML, and normalized values;
- report identifiers, settings, publication relationships, retrieved RDL/default-filter XML, and normalized definitions.

Use raw JSON when investigating an unexpected missing component, fallback match, publication state, or normalization result. It can contain extensive internal definitions and should be handled as environment metadata.

## Recommended choices

- Use **CSV** for a filtered action list.
- Use **HTML** for stakeholder review, large result sets, complete XML, and visual diffs.
- Use **raw JSON** for developer troubleshooting and evidence of what the plugin retrieved.

See [Safety and data handling](safety-and-data-handling.md) before sharing an export outside the delivery team.
