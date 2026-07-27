# Environment Comparison

Environment Comparison is a read-only XrmToolBox tool for comparing Dataverse table, column, form, and system-view metadata between two saved connections.

It reports missing components, table classification (`Standard`, `Intersect`, or `BPF`), display-name changes, schema and type mismatches, and important metadata settings such as requirement level, auditing, field security, create/read/update availability, maximum length, precision, formats, lookup targets, choices, autonumber formats, formulas, forms, and system views.

The tool deliberately excludes records, personal views, solution layers, and managed-versus-unmanaged status. It uses metadata retrieval and read-only queries against `systemform` and `savedquery`. There is no create, update, delete, associate, disassociate, import, export, publish, or other Dataverse write path.

Choose **Environment A** and **Environment B**, select the comparison areas, then select **Compare metadata**. Published metadata is compared by default; including unpublished metadata is an explicit option. Results and CSV exports identify whether the table and supported component types are custom. Formula, form, and view XML is normalized to ignore formatting-only differences. Forms are matched by UniqueName when available and otherwise by their System Form component ID. Environment-specific root grid object type codes are ignored for view Layout XML equality. The grid shows compact fingerprints and the differences CSV includes complete normalized definitions. A temporary **Export raw metadata** button writes every loaded property from both snapshots, including source form/view XML and identity fields, for diagnostics. Values longer than Excel's cell limit are preserved across numbered columns.
