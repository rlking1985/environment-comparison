# Interpreting and exporting results

## Direction

- **Missing in B** means the definition exists in Environment A but not Environment B.
- **Missing in A** means the definition exists only in Environment B.
- **Changed** means both sides were matched but an important property differs.

SSRS reports use their report ID as the primary match. If IDs differ, the tool can fallback match a unique report name, filename, report type, and language combination. Every resulting changed row explains that fallback in Details and includes both IDs. The ID difference is also listed explicitly. If that identity is duplicated, no fallback match is made.

Environment A is normally the reference/source and Environment B the target being validated.

## Severity

- **Critical** — likely to change storage, behavior, query results, required input, or component availability.
- **High** — important operational, security, audit, format, or component-presence difference.
- **Medium** — meaningful behavior difference that is less likely to block deployment.
- **Low** — display text or description difference worth reviewing but unlikely to break behavior.

Severity is a prioritization aid. Review business context before deciding whether a difference is correct.

## Filters

Search checks table names, table classification, component names and keys, properties, both preview values, and the explanation. Area, severity, and direction filters can be combined. The table logical-name regex is a post-retrieval scope for tables, columns, forms, and views; reports and other organization-level rows are always retained. It is disabled when only reports are selected. **Export filtered CSV** exports exactly the visible filtered set. **Export filterable HTML** exports every difference in the active table regex scope so that set can be filtered independently in a browser.

## XML definitions

Formula definitions, forms, and views are normalized before comparison, so indentation, line endings, and XML attribute order do not create false differences. The grid and detail preview show SHA-256 fingerprints to remain responsive. A different fingerprint means the normalized definition changed.

CSV export contains the complete normalized Environment A and Environment B XML values, not the fingerprints. This allows the definitions to be inspected or diffed outside the tool without making the grid hold and render large XML values.

For view Layout XML, the environment-specific root `grid/@object` value is ignored when deciding whether a difference exists. If another layout setting differs, the issue is retained and the CSV still contains the original complete XML from both environments.

## Filterable HTML export

The HTML export is a single file and makes no environment calls. Its base features work offline: search, exact severity/area/difference/table/property filters, sortable and drag-resizable columns, live summary counts, and page sizes of 50, 100, 250, or 500 rows. The page uses one primary results scroller instead of nested page and table scrolling. Search and filtering are debounced and processed in browser-sized chunks, and only the selected page is added to the table, which keeps the page responsive when the comparison contains many rows. The enhanced diff has its own contained horizontal and vertical scrolling, and results-table styles are isolated so they do not distort Diff2Html's internal layout.

The main table uses the same compact preview values as the plugin. **Inspect** is available only for changed rows, where both Environment A and Environment B provide values to compare; missing/presence-only rows show no inspection action. The viewer supports wrapping, copying, and downloading either complete value. For report rows, the value selector also exposes the report's complete normalized and raw RDL XML, even when the selected report issue is for another property. XML and all other metadata are inserted as text rather than executable HTML, and embedded report data is escaped so metadata cannot close or inject the report's data script block.

The pinned jsdiff 9.0.0 engine and Diff2Html 3.4.56 renderer load automatically from jsDelivr. Select **Show enhanced diff** to render the current values, then use the same button—now labelled **Show normal values**—to toggle back. The layout can be changed between side-by-side and line-by-line. XML structural tags are split onto separate diff lines even when the definition contains multiline SQL, and long diff lines wrap by default. **Disable wrapping** switches both normal and enhanced views to horizontal scrolling when exact line layout is preferred. If the CDN is unavailable, complete side-by-side values, copy, and download continue to work offline. Diff calculation is on demand and includes a maximum edit limit so exceptionally dissimilar values do not lock the report indefinitely.

Unlike Excel, the HTML viewer does not split values at 32,767 characters. Its practical limit is the browser's available memory and the size of the generated file; the paged table prevents large values from being rendered until they are inspected. Column widths can be changed by dragging a header boundary, restored with **Reset column widths**, or individually reset by double-clicking a boundary.

## CSV safety

Every field is quoted. Values beginning with spreadsheet formula characters are prefixed with an apostrophe to prevent formula execution when the file is opened in Excel.

Excel limits a cell to 32,767 characters. When any Environment A or Environment B value exceeds that limit, the export creates numbered columns such as `Environment A (part 1 of 3)`. Each part stays within the Excel-safe limit, the issue remains on one row, and concatenating the numbered parts recreates the complete value. If no values are oversized, the normal 12-column layout is retained.
