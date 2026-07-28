# Choosing comparison areas

The five checkboxes are independent. Selecting fewer areas reduces retrieval time and keeps the result focused.

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

This compares system forms associated with tables. Forms are matched by `UniqueName` when it is populated and otherwise by the System Form component ID (`formid`). The environment-specific `formidunique` value is not used as an identity. Formatting-only XML differences are ignored. Generated `labelid` values and IDs on empty placeholder cells are also ignored, while IDs on cells containing fields, controls, events, data, or meaningful labels remain significant. Form role assignments are compared separately: Microsoft roles use the stable `RoleTemplateId`, roles without templates use `ParentRootRoleId`, and unresolved roles retain their original GUID. Role order and environment-specific IDs for equivalent system roles do not produce Form XML differences. A changed grid fingerprint means the remaining normalized form definition differs; CSV export includes the complete normalized comparison XML from both environments.

## System views

This compares `savedquery` definitions, including FetchXML, layout XML, and column-set XML. The root `grid/@object` value is ignored during Layout XML equality checks because table object type codes can differ between environments. Every other layout attribute, row, cell, order, and width remains significant. Personal views (`userquery`) are intentionally excluded because they are user-owned rather than deployed solution components. The grid uses fingerprints for changed XML, while CSV export includes the complete normalized definitions, including the original object codes when another genuine layout difference exists.

## SSRS reports

This compares organization reports in the Dataverse `report` table where the report type is **Reporting Services Report**. Reports are matched by their report component ID first. Reports left unmatched are fallback matched only when report name, filename, report type, and language identify exactly one report in each environment. A fallback match is recorded in the Details value, including both report IDs, and the differing Report ID is emitted as a separate change. Ambiguous duplicate reports are never guessed and remain presence differences. The comparison covers presence, report ID, name, description, file name, status, language, MIME type, default filter, associated tables (`reportentity`), categories (`reportcategory`), visibility (`reportvisibility`), and the complete RDL stored in `bodytext`.

RDL and default-filter XML are normalized before comparison. Changed definitions use SHA-256 fingerprints in the grid and retain complete normalized XML in the CSV. The raw JSON also retains the exact retrieved XML and diagnostic report identifiers and component state. Personal reports are excluded. Reports that exist only in an external SSRS server catalog and are not registered in Dataverse cannot be discovered by the plugin.

## Published versus unpublished

Leave **Include unpublished metadata** off when checking what is deployed and available to normal users. Turn it on when reviewing draft customizations that have not yet been published. When selected, table metadata is retrieved as if published and forms, views, reports, and report publication records use Dataverse's read-only `RetrieveUnpublishedMultiple` request, so the option applies consistently to every selected comparison area. Full form and view definitions are retrieved in bounded 250-component pages. RDL-bearing report queries use 25-report pages to reduce oversized long-running responses; the activity panel reports each completed definition page.

## Temporary raw metadata export

After a comparison, **Export raw metadata (JSON)** writes hierarchical snapshots for Environment A and Environment B, even when values are identical. When a table logical-name regex is active, table, column, form, and view snapshots are limited to that scope; reports and other organization-level data are retained. Without a regex, the snapshots are unfiltered. JSON preserves the table/component structure, null-equivalent empty values, identifiers, XML, and line breaks without Excel column splitting. Forms include `formid`, `formidunique`, `UniqueName`, object type, component/publication state, solution and ancestor identifiers, original role GUIDs, resolved role identities, the exact retrieved FormXML, and the normalized Form XML used by the comparison. Views similarly include identifiers and exact retrieved FetchXML, LayoutXML, and column-set XML alongside normalized values. Reports include exact and normalized RDL/default-filter XML, report identifiers and settings, and the related tables, categories, and visibility records used by comparison. `formidunique` and raw role IDs are diagnostic only and are never used directly when a stable semantic identity is available.

The export streams to disk in the background, has no Excel cell-size limit, and retrieves no table records. It is intentionally temporary debugging functionality.
