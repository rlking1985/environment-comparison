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

## Columns

Use this for missing columns, renamed display labels, and important column settings. A required or primary column missing in Environment B is Critical; another missing column is High.

If an A-only and B-only column on the same table have the same display name, type, requirement level, and compatible length, the details show a **possible renamed/recreated column** hint. It is a review aid, not a claim that Dataverse renamed the logical name.

## Forms

This compares system forms associated with tables. Formatting-only XML differences are ignored. A changed grid fingerprint means the normalized form definition differs; CSV export includes the complete normalized XML from both environments.

## System views

This compares `savedquery` definitions, including FetchXML, layout XML, and column-set XML. Personal views (`userquery`) are intentionally excluded because they are user-owned rather than deployed solution components. The grid uses fingerprints for changed XML, while CSV export includes the complete normalized definitions.

## Published versus unpublished

Leave **Include unpublished metadata** off when checking what is deployed and available to normal users. Turn it on when reviewing draft customizations that have not yet been published.
