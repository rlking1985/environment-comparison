# Environment Comparison for XrmToolBox

Environment Comparison is a read-only XrmToolBox plugin for finding meaningful Dataverse definition differences between two environments.

It is designed for deployment checks where the important questions are whether tables, columns, forms, and system views are missing or materially different—not whether one environment contains managed components and the other contains unmanaged components.

## Comparison areas

Each area can be selected independently before a comparison:

- **Table metadata** — classification (`Standard`, `Intersect`, or `BPF`), schema/display names, ownership, primary columns, auditing, change tracking, activities, notes, queues, connections, document management, duplicate detection, quick create, SLA, and related table behavior. No table records are read.
- **Columns** — missing columns, possible renamed/recreated columns, display-name renames, data type, requirement level, auditing, field security, create/read/update behavior, search/form/grid availability, maximum length, ranges, precision, format, autonumber, lookup targets, choices, date/time behavior, file/image settings, and formulas.
- **Forms** — missing system forms, form type/state/presentation, and normalized form-definition differences.
- **System views** — missing system views, query type/default/quick-find behavior, and normalized FetchXML, layout, and column-set differences. Environment-specific root layout object type codes are ignored. Personal views are excluded.

The area flags and component models are intentionally separate so later versions can add relationships, keys, option sets, business rules, charts, dashboards, or other component types without changing the connection workflow.

## Safety and noise reduction

- All Dataverse operations are reads.
- Table and column metadata uses `RetrieveAllEntitiesRequest`.
- Forms use read-only queries against `systemform`.
- System views use read-only queries against `savedquery`.
- The plugin has no create, update, delete, associate, disassociate, import, export, publish, or solution-operation code.
- Managed/unmanaged status, solution layers, component version stamps, and metadata IDs are not compared.
- Published definitions are compared by default. Including unpublished metadata is explicit.
- Formula, form, and view XML is normalized before comparison so indentation, line-ending, and attribute-order differences do not create noise.
- View Layout XML comparison ignores only the root `grid/@object` code because Dataverse can assign different table object type codes in each environment.
- The result grid uses compact SHA-256 fingerprints for changed XML definitions; CSV export contains the complete normalized XML from both environments.
- CSV values are quoted and spreadsheet formulas are neutralized.
- Values longer than Excel's cell limit are split across numbered Environment A/B columns. Normal-sized exports keep the standard 12-column layout.

## Use

1. Open the plugin without a connection or from an existing XrmToolBox connection.
2. Choose **Environment A** and **Environment B**.
3. Select one or more areas: table metadata, columns, forms, or system views.
4. Leave **Include unpublished metadata** off for a deployed/published-state comparison.
5. Select **Compare metadata**.
6. Filter by area, severity, direction, or free text.
7. Select a row to inspect its preview values and explanation. XML definitions use compact fingerprints in the preview.
8. Export the currently filtered rows to CSV if required.

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

- `artifacts/EnvironmentComparison.XrmToolBox.1.0.0.4.nupkg`
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
