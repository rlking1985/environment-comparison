# Environment Comparison for XrmToolBox

Environment Comparison is a read-only XrmToolBox plugin for finding meaningful Dataverse definition differences between two environments.

It is designed for deployment checks where the important questions are whether tables, columns, forms, system views, and organization SSRS reports are missing or materially different—not whether one environment contains managed components and the other contains unmanaged components.

## Comparison areas

Each area can be selected independently before a comparison:

- **Table metadata** — classification (`Standard`, `Intersect`, or `BPF`), schema/display names, ownership, primary columns, auditing, change tracking, activities, notes, queues, connections, document management, duplicate detection, quick create, SLA, and related table behavior. No table records are read.
- **Columns** — missing columns, possible renamed/recreated columns, display-name renames, data type, requirement level, auditing, field security, create/read/update behavior, search/form/grid availability, maximum length, ranges, precision, format, autonumber, lookup targets, choices, date/time behavior, file/image settings, and formulas.
- **Forms** — missing system forms, form type/state/presentation, and normalized form-definition differences.
- **System views** — missing system views, query type/default/quick-find behavior, and normalized FetchXML, layout, and column-set differences. Environment-specific root layout object type codes are ignored. Personal views are excluded.
- **SSRS reports** — missing organization Reporting Services reports, report settings, default filters, related tables, categories, visibility, and normalized RDL differences. Personal reports and reports stored only on an external SSRS server are excluded.

The area flags and component models are intentionally separate so later versions can add relationships, keys, option sets, business rules, charts, dashboards, or other component types without changing the connection workflow.

## Safety and noise reduction

- All Dataverse operations are reads.
- Table and column metadata uses `RetrieveAllEntitiesRequest`.
- Forms use read-only queries against `systemform`; draft definitions use the read-only `RetrieveUnpublishedMultiple` request only when explicitly selected.
- System views use read-only queries against `savedquery`, with the same explicit unpublished retrieval behavior.
- SSRS reports use read-only queries against `report`, `reportentity`, `reportcategory`, and `reportvisibility`; only organization reports with report type `Reporting Services Report` are loaded.
- The plugin has no create, update, delete, associate, disassociate, import, export, publish, or solution-operation code.
- Managed/unmanaged status, solution layers, and component version stamps are not compared. Metadata IDs are generally excluded, except that differing SSRS report IDs are reported when two reports require a fallback identity match.
- Published definitions are compared by default. Including unpublished metadata is explicit.
- Unpublished forms, views, and report publication records are retrieved in bounded pages. RDL-bearing report queries use smaller 25-report pages so large definitions do not hold a single Dataverse request open for the entire report set.
- Formula, form, view, report-filter, and RDL XML is normalized before comparison so indentation, line-ending, and attribute-order differences do not create noise.
- Form comparison ignores generated label resource IDs and IDs on empty placeholder cells; IDs on cells containing fields, controls, events, data, or meaningful labels remain significant.
- View Layout XML comparison ignores only the root `grid/@object` code because Dataverse can assign different table object type codes in each environment.
- The result grid uses compact SHA-256 fingerprints for changed XML definitions; CSV export contains the complete normalized XML from both environments.
- A table logical-name regular expression can scope table, column, form, and view rows after retrieval; for example, `^(ata_|mshied_)`. The same scope is applied to preview and every export. Report and other organization-level rows are retained, and the control is disabled for report-only comparisons.
- **Export filterable HTML** writes every comparison difference in the active table scope to a browser report with combined filters, sortable and drag-resizable columns, debounced/chunked searching, pagination, selectable page sizes, summary counts, and complete A/B values for changed rows. Presence-only missing rows are not inspectable. The report uses one primary results scroller and a viewport-filling detail viewer. Its pinned jsdiff 9.0.0 and Diff2Html 3.4.56 viewer loads automatically, renders wrapped diffs by default, has contained horizontal and vertical scrolling, and toggles back to normal values from the same button. If the CDN is unavailable, the base report and complete-value viewer continue to work. Report rows additionally expose complete normalized and raw RDL XML. Only one page is rendered at a time so large result sets do not create an equally large table DOM.
- A temporary background **Export raw metadata (JSON)** action exports both snapshots as structured JSON within the active table scope, including exact source form/view/report XML, identity fields, metadata mode, report publication relationships, and diagnostic form publication fields. With no table regex, the snapshots are unfiltered.
- Form security roles are compared by stable role template/root identity while the raw export retains original role GUIDs.
- SSRS reports are matched by report ID first. Remaining reports are fallback matched only when name, filename, report type, and language identify exactly one report on each side. A fallback match reports the differing IDs and records the match method in Details; ambiguous duplicates remain presence differences.
- CSV values are quoted and spreadsheet formulas are neutralized.
- Difference values longer than Excel's cell limit are split across numbered Environment A/B columns. The raw JSON export has no Excel cell-size limit.

## Use

1. Open the plugin without a connection or from an existing XrmToolBox connection.
2. Choose **Environment A** and **Environment B**.
3. Select one or more areas: table metadata, columns, forms, system views, or SSRS reports.
4. Leave **Include unpublished metadata** off for a deployed/published-state comparison.
5. Select **Compare metadata**.
6. Filter by area, severity, direction, or free text.
7. Select a row to inspect its preview values and explanation. XML definitions use compact fingerprints in the preview.
8. Optionally enter a table logical-name regex to limit table-associated preview and exports without changing metadata retrieval. Reports remain included.
9. Export the currently filtered rows to CSV, or export every difference in the active table scope to a standalone filterable HTML report without Excel cell limits. The enhanced diff viewer loads automatically when internet access is available.
10. Use **Export raw metadata (JSON)** when a complete diagnostic snapshot of the active table scope is required.

Environment A is treated as the source/reference side and Environment B as the target/comparison side when severity is calculated. Missing items in Environment B are therefore ranked more highly.

See the [user guide](docs/README.md) for interpretation details and the extension guide for adding future comparison areas.

## Build and test

Prerequisites:

- Windows
- .NET Framework 4.8 Developer Pack
- A current .NET SDK or Visual Studio with MSBuild

```powershell
.\scripts\Build.ps1
```

Or run the individual commands:

```powershell
dotnet restore EnvironmentComparison.sln
dotnet test EnvironmentComparison.sln -c Release
dotnet build src\EnvironmentComparison\EnvironmentComparison.csproj -c Release
```

The release build creates:

- `artifacts/EnvironmentComparison.XrmToolBox.1.0.0.8.nupkg`
- `src/EnvironmentComparison/bin/Release/net48/EnvironmentComparison.dll`

All tests use synthetic objects or a recording `IOrganizationService`. They do not authenticate to or contact Dataverse.

For local XrmToolBox development, close XrmToolBox and run:

```powershell
.\scripts\Build-And-Install.ps1 -XrmToolBoxPath C:\path\to\XrmToolBox
```

## Packaging

The NuGet package places the plugin DLL under `lib/net48/Plugins`, includes dedicated 80px and 32px images, keeps assembly/package versions aligned, and follows the XrmToolBox validation checklist used by the companion Team Security Role Mapper project.

## License

MIT. See [LICENSE](LICENSE).
