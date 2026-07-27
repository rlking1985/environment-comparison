# Choosing comparison areas

The four checkboxes are independent. Selecting fewer areas reduces retrieval time and keeps the result focused.

## Table metadata

Use this to compare table-level behavior. It does not retrieve rows from the business table. A missing table in Environment B is Critical because every dependent component is also unavailable there.

Each result includes a **Table classification** value:

- **Standard** — a regular Dataverse table.
- **Intersect** — a many-to-many relationship joining table (`IsIntersect`).
- **BPF** — a table created for a business process flow (`IsBPFEntity`).

`BPF` takes precedence if the metadata unexpectedly reports both flags. Whether a standard table is enabled for use in business process flows remains a separate table setting.

Managed state, solution layer, metadata ID, and introduced version are deliberately excluded.

Every result also includes **Custom table** and **Custom component** context. Custom table comes from `EntityMetadata.IsCustomEntity`. Column rows use `AttributeMetadata.IsCustomAttribute` for Custom component. A table row uses the table value for both columns. Form and view component status is shown as `Unknown` because managed state does not reliably identify whether those components are custom.

## Columns

Use this for missing columns, renamed display labels, and important column settings. A required or primary column missing in Environment B is Critical; another missing column is High.

If an A-only and B-only column on the same table have the same display name, type, requirement level, and compatible length, the details show a **possible renamed/recreated column** hint. It is a review aid, not a claim that Dataverse renamed the logical name.

## Forms

This compares system forms associated with tables. Forms are matched by `UniqueName` when it is populated and otherwise by the System Form component ID (`formid`). The environment-specific `formidunique` value is not used. Formatting-only XML differences are ignored. A changed grid fingerprint means the normalized form definition differs; CSV export includes the complete normalized XML from both environments.

## System views

This compares `savedquery` definitions, including FetchXML, layout XML, and column-set XML. The root `grid/@object` value is ignored during Layout XML equality checks because table object type codes can differ between environments. Every other layout attribute, row, cell, order, and width remains significant. Personal views (`userquery`) are intentionally excluded because they are user-owned rather than deployed solution components. The grid uses fingerprints for changed XML, while CSV export includes the complete normalized definitions, including the original object codes when another genuine layout difference exists.

## Published versus unpublished

Leave **Include unpublished metadata** off when checking what is deployed and available to normal users. Turn it on when reviewing draft customizations that have not yet been published.

## Temporary raw metadata export

After a comparison, **Export raw metadata** writes an unfiltered diagnostic CSV containing every loaded property from Environment A and Environment B, even when the values are identical. Form rows include `formid`, `formidunique`, `UniqueName`, object type, the exact retrieved FormXML, and the normalized Form XML used by the comparison. View rows similarly include identifiers and exact retrieved FetchXML, LayoutXML, and column-set XML alongside normalized values. `formidunique` is diagnostic only and is never used as the form comparison key.

The export runs in the background, splits oversized values into Excel-safe numbered columns, and retrieves no table records. It is intentionally temporary debugging functionality.
