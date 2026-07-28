# Environment Comparison

Environment Comparison is a read-only XrmToolBox tool for comparing important Dataverse definitions between two saved connections.

## Compare

- table metadata and classification (`Standard`, `Intersect`, or `BPF`);
- columns and important field settings;
- system forms and form security role assignments;
- system views, FetchXML, Layout XML, and column-set XML;
- organization SSRS report settings, registrations, and RDL.

The plugin reports missing components and materially changed properties. It normalizes XML and ignores managed-versus-unmanaged state, solution layers, version stamps, and known environment-generated noise.

## Use

1. Choose Environment A as the reference.
2. Choose Environment B as the target.
3. Select one or more comparison areas.
4. Compare published metadata unless draft customizations are intentionally required.
5. Filter the result and export CSV, filterable HTML, or diagnostic raw JSON.

Environment operations are reads only. The plugin does not retrieve business table records and has no create, update, delete, import, publish, or solution-operation path.

The table logical-name regex scopes table-associated preview and exports after retrieval. Table classification, area, severity, difference, and search filters can be combined. HTML export provides independent browser filters, resizable columns, complete-value inspection, and enhanced diffs for changed rows.

## Documentation

- [User guide](https://github.com/rlking1985/environment-comparison/tree/dev/docs)
- [Getting started](https://github.com/rlking1985/environment-comparison/blob/dev/docs/getting-started.md)
- [Comparison areas](https://github.com/rlking1985/environment-comparison/blob/dev/docs/comparison-areas.md)
- [Exporting results](https://github.com/rlking1985/environment-comparison/blob/dev/docs/exporting-results.md)
- [Troubleshooting](https://github.com/rlking1985/environment-comparison/blob/dev/docs/troubleshooting.md)
- [Safety and data handling](https://github.com/rlking1985/environment-comparison/blob/dev/docs/safety-and-data-handling.md)
- [Roadmap](https://github.com/rlking1985/environment-comparison/blob/dev/ROADMAP.md)
- [Suggesting a feature](https://github.com/rlking1985/environment-comparison/blob/dev/docs/feature-suggestions.md)
- [Contributing](https://github.com/rlking1985/environment-comparison/blob/dev/CONTRIBUTING.md)
