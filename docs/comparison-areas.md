# Choosing comparison areas

The five comparison areas are independent. Select only what is needed for the current check to reduce retrieval time and keep the result focused.

## Table metadata

Use this area to compare table-level identity and behavior. It does not retrieve rows from the business table.

The plugin checks table presence and these properties:

| Group | Properties |
| --- | --- |
| Identity and labels | Table classification, schema name, display name, display collection name, description, entity set name |
| Storage and ownership | Ownership type, primary ID column, primary name column |
| Table type | Activity table, activity party table |
| Data behavior | Audit enabled, change tracking enabled, business process enabled |
| Features | Connections, document management, duplicate detection, mail merge, quick create, advanced find, queues, SLA, activities, and notes |

Each table receives a **Table classification**:

- **Standard** - a regular Dataverse table.
- **Intersect** - a many-to-many relationship joining table reported by `IsIntersect`.
- **BPF** - a table created for a business process flow reported by `IsBPFEntity`.

`BPF` takes precedence if metadata unexpectedly reports both flags. Whether a standard table is enabled for use in business process flows remains a separate table setting.

A table missing from Environment B is Critical because every dependent component is unavailable there.

Managed state, solution layer, metadata ID, introduced version, and other version stamps are deliberately excluded.

## Columns

Use this area to find missing columns, renamed labels, possible recreated columns, and important field-setting differences.

The plugin checks column presence and these properties:

| Group | Properties |
| --- | --- |
| Identity and labels | Schema name, display name, description |
| Type and role | Attribute type, attribute type name, requirement level, primary ID, primary name, logical column, source type, attribute of |
| Security and availability | Audit enabled, field security enabled, valid for create/read/update, advanced find, forms, and grids |
| Text and number behavior | Autonumber format, format, format name, maximum length, minimum value, maximum value, precision, precision source |
| Date/time | Date/time behavior, whether the behavior can change |
| Lookup and choice | Lookup targets, default value, choice set name, global choice, choice values |
| File and image | File maximum size, image maximum height/width, store full image |
| Calculated behavior | Formula definition |

A required or primary column missing from Environment B is Critical; another column missing from B is High. Columns found only in B are Medium by default.

If an A-only and B-only column on the same table have the same display name, type, requirement level, and compatible length, the details show a **possible renamed/recreated column** hint. It is a review aid, not a claim that Dataverse renamed a logical name.

## Forms

This area compares system forms associated with tables.

It checks:

- form presence;
- name and description;
- form type;
- activation state;
- presentation;
- security role assignments;
- normalized Form XML.

Forms are matched by `UniqueName` when populated and otherwise by the System Form component ID (`formid`). The environment-specific `formidunique` value is diagnostic only and is not used as identity.

Formatting-only XML differences are ignored. Generated `labelid` values and IDs on empty placeholder cells are also ignored. IDs on cells that contain fields, controls, events, data, or meaningful labels remain significant.

Role order and environment-specific IDs for equivalent system roles do not produce Form XML differences. Microsoft roles use stable `RoleTemplateId`; roles without templates use `ParentRootRoleId`; unresolved roles retain their original GUID. Changed form role assignments are reported separately from Form XML.

## System views

This area compares `savedquery` system-view definitions. Personal `userquery` views are intentionally excluded because they are user-owned rather than deployed solution components.

It checks:

- view presence and name;
- query type;
- default-view and quick-find flags;
- FetchXML;
- Layout XML;
- column-set XML;
- advanced group-by settings.

The root `grid/@object` value is ignored for Layout XML equality because Dataverse table object type codes can differ between environments. Every other layout attribute, row, cell, order, and width remains significant.

## SSRS reports

This area compares organization reports in the Dataverse `report` table whose type is **Reporting Services Report**.

It checks:

- report presence and Report ID;
- name, description, filename, status, language, and MIME type;
- default filter;
- associated tables from `reportentity`;
- categories from `reportcategory`;
- visibility from `reportvisibility`;
- complete RDL stored in `bodytext`.

Reports are matched by Report ID first. Reports left unmatched are fallback matched only when report name, filename, report type, and language identify exactly one report in each environment. A fallback match is recorded in Details, includes both IDs, and emits the differing Report ID as a separate change. Ambiguous duplicate reports are never guessed and remain presence differences.

RDL and default-filter XML are normalized before comparison. Personal reports are excluded. Reports that exist only in an external SSRS server catalog and are not registered in Dataverse cannot be discovered by the plugin.

## XML normalization and preview

Formula, form, view, default-filter, and RDL XML is normalized before equality checks. Indentation, line endings, and XML attribute order do not create differences.

Changed definitions show compact SHA-256 fingerprints in the plugin grid. Complete normalized values remain available in CSV and HTML inspection; raw JSON retains both retrieved and normalized definitions where supported.

## Published versus unpublished

Leave **Include unpublished metadata** off when checking what is deployed and available to normal users.

Turn it on when draft customizations must be reviewed. Forms, views, reports, and related unpublished records use Dataverse's read-only `RetrieveUnpublishedMultiple` request. Full form and view definitions are retrieved in bounded 250-component pages. RDL-bearing report queries use 25-report pages to reduce oversized long-running requests.

The option never publishes or modifies metadata.

## Current exclusions

The current release does not compare relationships, keys, standalone option sets, business rules, cloud flows, charts, dashboards, app modules, security role definitions, organization settings, or business table data.

Return to the [user guide](README.md) or continue to [Interpreting results](interpreting-results.md).
