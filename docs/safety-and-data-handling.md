# Safety and data handling

## Read-only environment operations

Environment Comparison reads definitions and selected system component records. It does not contain a Dataverse write path.

The plugin does not:

- create, update, or delete records;
- associate or disassociate records;
- import or export a Dataverse solution;
- publish customizations;
- change security roles or form access;
- add, remove, or modify metadata.

Selecting **Include unpublished metadata** uses a read-only unpublished retrieval request. It does not publish anything.

## What is read

Depending on the selected areas, the plugin reads:

- table and column metadata;
- system form definitions and form role assignments;
- system view definitions;
- organization SSRS report definitions and related registration records.

It does not read rows from the business tables being compared. It also excludes personal views and personal reports.

## Permissions

Use saved XrmToolBox connections belonging to an account that is authorized to inspect both environments. The account needs enough read access to retrieve metadata and the selected system component tables.

The plugin does not elevate privileges. Missing permissions can appear as retrieval errors or incomplete component sets and should be corrected in the connection account rather than worked around in the plugin.

## Local exports

CSV, HTML, and JSON files are written only to the local path selected by the operator. Creating an export does not call a Dataverse export action.

Although business records are excluded, exports can contain sensitive implementation details, including:

- table and column schema;
- validation and security-related settings;
- FetchXML and form/view structure;
- formulas and autonumber patterns;
- report filters, SQL or FetchXML commands embedded in RDL, and complete report layout definitions;
- environment and component identifiers.

Store, share, and dispose of exports under the same controls used for solution source and environment configuration.

## HTML report and CDN assets

The filterable HTML report contains the complete exported comparison values in one local file. Its base search, filtering, normal-value inspection, copy, and download features make no environment calls.

Enhanced diff loads pinned jsdiff and Diff2Html scripts from jsDelivr when the report is opened. That third-party script runs in the local report page and can access the values embedded in that page. Use the CSV or raw JSON format, or open the HTML without network access, where organizational policy does not permit third-party CDN code to process metadata.

## Diagnostic raw JSON

Raw JSON is intentionally more detailed than the difference list. It retains equal values, identifiers, publication diagnostics, retrieved XML, and normalized XML within the active scope. Treat it as developer troubleshooting material and remove it when it is no longer required.

## Documentation images

Repository screenshots are sanitized mock images created from the plugin layout. They use fictional Contoso connections, logical names, component names, values, URLs, and counts. Real connection screenshots are not included in the repository.
