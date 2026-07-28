# Troubleshooting

## Compare metadata is unavailable

Check that:

- both Environment A and Environment B are selected;
- at least one comparison area is checked;
- XrmToolBox has completed both connections.

If a connection was changed, run the comparison again. Existing rows are intentionally treated as stale.

## No comparison rows are shown

A completed comparison with no rows means no selected important properties differ within the active table scope.

Also check:

- the table logical-name regex is not excluding all table-associated rows;
- area, severity, difference, classification, and search filters are cleared;
- the selected areas contain the components you expected to inspect;
- the published/unpublished choice matches the customization state.

## The regex is invalid

The regex field accepts a .NET regular expression applied to the complete table logical name. Correct the validation message or clear the field.

Useful examples:

```regex
^contoso_
```

```regex
^(?:contoso_.*|account|contact)$
```

The control is disabled for report-only comparisons because reports do not have a table logical name.

## The comparison is taking a long time

Large environments can contain thousands of columns and many large form, view, or report definitions.

Try:

1. compare published metadata first;
2. select only the area required for the investigation;
3. run reports separately from forms and views;
4. keep XrmToolBox open and monitor the Activity panel;
5. retry after confirming both saved connections are healthy.

The table regex reduces preview and export volume but is deliberately post-retrieval, so it does not shorten metadata retrieval.

Unpublished retrieval normally takes longer. Form and view definitions are paged in groups of 250; RDL-bearing reports use smaller 25-report pages to avoid one oversized request.

## XrmToolBox reports a lost host connection

Reconnect the affected saved connection and retry a smaller published comparison first. Then add areas one at a time to identify the slow retrieval surface. Large unpublished form/view/report runs are the most likely to expose connection or host timeouts.

No partial result should be treated as complete. Wait for the status line to confirm the comparison finished.

## Many forms or views appear missing even though they exist

Check:

- Environment A and B are in the intended direction;
- the form/view is published when published metadata is selected;
- the connection user can read `systemform` or `savedquery`;
- raw JSON contains the component on both sides;
- the identities differ rather than only environment-specific publication IDs.

Forms are matched by `UniqueName` when available and otherwise by `formid`. `formidunique` is diagnostic only. System views are matched by their saved-query component identity. If raw JSON shows both components but the plugin still reports two presence rows, retain the JSON and result export for investigation.

## A view differs only by `grid/@object`

The plugin already ignores the root Layout XML `grid/@object` value during equality checks because object type codes can differ by environment. If a Layout XML row remains, another layout attribute, row, cell, order, or width differs. Inspect the complete exported XML.

## A form differs only by generated IDs or role IDs

Generated label IDs and IDs on empty placeholder cells are normalized away. Meaningful cell/control identities remain.

Form security roles are compared using stable role template or root-role identity where possible. Raw role GUIDs remain in diagnostic JSON. If a role cannot be resolved to a stable identity, its original GUID remains significant.

## Reports appear as two missing rows

Reports are matched by Report ID first. If IDs differ, fallback matching requires exactly one report on each side with the same name, filename, report type, and language.

Two missing rows can therefore mean:

- one of the fallback fields differs;
- a required fallback field is blank;
- duplicate candidates make the fallback ambiguous;
- the report exists only in an external SSRS catalog and is not registered in one Dataverse environment.

Details explains an ambiguous fallback. Raw JSON exposes the retrieved report IDs and settings.

## The CSV shows part 1 of N columns

This is expected when a complete value exceeds Excel's 32,767-character cell limit. Concatenate the numbered parts in order, or use HTML/raw JSON to inspect the complete value without splitting.

## The CSV is difficult to read in Excel

Use Excel's import workflow if regional delimiter or encoding detection is incorrect. Every CSV field is quoted, and formula-like values are neutralized. For long XML and wide comparisons, prefer the HTML report.

## Enhanced diff is unavailable in HTML

The local report needs internet access to load pinned jsdiff and Diff2Html assets from jsDelivr. Normal complete values, filtering, copy, and download continue to work without the CDN.

If the enhanced viewer opens but is hard to navigate:

- enable wrapping for long XML lines;
- use its contained horizontal and vertical scrollbars;
- switch between side-by-side and line-by-line layouts;
- toggle **Show normal values** to return to the base viewer.

## The HTML report is slow or large

Narrow the table regex before export, or run smaller comparison areas. The HTML report renders one page at a time, but its embedded full values still determine file size and memory usage.

## A result still looks incorrect

Retain these three files where possible:

1. filtered CSV showing the unexpected row;
2. HTML report for complete-value inspection;
3. raw metadata JSON showing what was retrieved from each environment.

Record the selected areas, published/unpublished state, connection direction, and active regex. These details distinguish retrieval, matching, normalization, and display issues without requiring environment changes.
