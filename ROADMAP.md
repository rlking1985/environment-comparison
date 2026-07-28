# Roadmap

Environment Comparison is intended to grow from a metadata comparison tool into a broader, read-only Dataverse environment validation toolkit.

This roadmap describes direction rather than a release commitment. Priorities may change based on Dataverse API constraints, performance, permissions, community feedback, and contributor availability. Items do not have committed dates until they are assigned to a release.

## Available now

- Table metadata and table classification (`Standard`, `Intersect`, and `BPF` table types)
- Column metadata
- System forms and form security role assignments
- System views
- Organization SSRS reports registered in Dataverse
- Published and explicitly selected unpublished retrieval
- Search, table regex, table classification, area, severity, and difference filters
- Filtered CSV, filterable HTML, enhanced diff, and diagnostic raw JSON exports
- Normalization of known environment-generated XML noise

The current `BPF` table classification identifies a Dataverse table created for a business process flow. It does not yet compare the business process flow definition, stages, steps, or activation state.

## Planned comparison areas

### Process automation

- **Business process flows** - definition presence, process identity, stages, stage order, steps, participating tables, activation state, and supporting client data.
- **Classic workflows and actions** - process identity, category/type, scope, trigger and execution settings, activation state, owner-independent configuration, and normalized process definition.
- **Business rules** - rule identity, scope, activation state, conditions, actions, and normalized definition.
- **Cloud flows** - solution-aware flow presence, workflow identity, activation state, trigger/action definition, connection-reference usage, environment-variable dependencies, and normalized flow definition.

Cloud flow retrieval may require additional APIs and permissions beyond the existing Dataverse metadata surface. The design must preserve the plugin's read-only contract and clearly report when a connection cannot retrieve a selected area.

### Organization and application configuration

- **Organization settings** - selected locale, regional, date, time, number, currency, email, tracking, security, guest-access, feature, desktop-flow, clustering, and version-related settings that materially affect behavior.
- **SLA pause configuration** - pause states and related organization-level service settings.
- **Site map** - SiteMap XML and meaningful navigation differences.
- **Model-driven applications** - app identity, included components, navigation, and selected app settings.

Organization settings will use an explicit allowlist. Environment identity, timestamps, capacity, tenant-specific IDs, volatile counters, and secrets will not be compared by default.

### Dataverse solution components

- Relationships, including one-to-many, many-to-one, and many-to-many definitions
- Alternate keys
- Global choices and reusable option sets
- Environment variable definitions and current/default value presence, with secret values excluded
- Connection references, without credentials or connection secrets
- Web resources
- Charts and dashboards
- Custom APIs, custom actions, and related request/response parameters
- Plug-in assemblies, types, and registered steps using stable component metadata rather than deployment timestamps

### Security configuration

- Security role definitions and privileges
- Field security profiles
- Teams and role assignments where a stable, non-user-specific comparison is possible
- Hierarchy security and selected organization security settings

User membership and individual-user access are not expected to be part of the default metadata comparison because they are operational data rather than deployable component definitions.

## Planned usability and delivery improvements

- Saved comparison profiles for reusable area and filter selections
- Configurable ignore rules for approved environment-specific differences
- Baseline files for comparing an environment against a stored snapshot
- Side-by-side structured XML/JSON inspection inside the plugin
- Clearer grouping of a missing parent component and its dependent differences
- Export profiles and summary-only reports
- Command-line or headless comparison for build and release pipelines
- Machine-readable exit codes and policy thresholds for automated deployment gates
- Comparison history stored locally, with no environment writes
- Cancellation, resumable retrieval, and additional progress diagnostics for very large environments
- Accessibility and keyboard-navigation improvements

## Design investigations

These areas need discovery before they can be scheduled:

- Reliable cloud-flow retrieval across solution-aware and non-solution-aware flows
- Stable identity and normalization for workflow/process definitions across managed deployments
- Which organization settings are portable expectations rather than deliberate environment-specific configuration
- Secure handling of environment variables, connection references, and other components that can reference secrets
- Scalable comparison of large web resources, plug-in assemblies, reports, and process definitions
- Whether organization-level and table-level scopes need separate filter profiles

## Ongoing quality work

- Expand normalization only when a difference is proven to be environment-generated noise
- Add regression tests for every false-positive rule
- Keep matching conservative and expose every fallback identity in Details
- Maintain bounded/paged retrieval and responsive preview/export behavior
- Improve troubleshooting diagnostics without exposing credentials or business table records
- Keep documentation and sanitized mock screenshots aligned with the current UI

## Explicit non-goals

- Writing, synchronizing, deploying, publishing, or repairing either environment
- Comparing business table records by default
- Treating managed-versus-unmanaged state as a functional metadata difference
- Exporting credentials, secret environment-variable values, or connection secrets
- Guessing ambiguous component matches

Any future write capability should be a separate tool with a separate security model.

## Suggest or contribute a feature

- Read [Suggesting a feature](docs/feature-suggestions.md) before opening a request.
- Read [Contributing](CONTRIBUTING.md) before preparing a pull request.
- For a new comparison area, also follow [Extending the comparison engine](docs/extending.md).
