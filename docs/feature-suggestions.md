# Suggesting a feature

Feature suggestions are welcome, especially when they describe a real environment-validation problem and the metadata needed to make a reliable decision.

## Before opening a request

1. Check the [roadmap](../ROADMAP.md).
2. Search existing GitHub issues and pull requests.
3. Confirm the request is suitable for a read-only comparison tool.
4. Remove customer names, environment URLs, email addresses, tenant IDs, credentials, secret values, and real metadata from examples.

Use the repository's **Feature request** issue template. One request should describe one comparison area or one cohesive improvement.

## What makes a useful suggestion

A strong request explains:

- the problem rather than only naming a component type;
- who performs the comparison and when;
- what Environment A and Environment B represent;
- how components can be matched across environments;
- which properties are important and which are deployment noise;
- how missing and changed items should be prioritized;
- what permissions or APIs may be required;
- expected scale and definition size;
- how the result should appear in preview and exports;
- examples using fictional metadata.

For example, “compare cloud flows” is a useful direction but not yet enough to implement safely. A complete suggestion would distinguish solution-aware flows, define stable flow identity, list meaningful trigger/action settings, explain handling of connection references and environment variables, identify required APIs/permissions, and state which generated deployment values must be ignored.

## Feature request template

Copy these headings when the GitHub issue template is unavailable:

```markdown
## Summary

## Problem and user scenario

## Proposed comparison or improvement

## Component identity and matching

## Important properties

## Expected missing/changed severity

## Values that should be ignored or normalized

## Retrieval API and permissions

## Expected scale and performance

## Preview, filter, and export expectations

## Read-only, security, and data-handling considerations

## Fictional examples or mock screenshots

## Alternatives or workarounds
```

## Guidance for new comparison areas

### Identity and matching

Describe the stable identity that survives deployment. A display name alone is rarely sufficient. If a fallback is required, explain how it avoids ambiguous matches and how the UI should disclose it.

### Important properties

List the settings that change behavior. If possible, group them by severity:

- **Critical** for storage, execution, required input, query behavior, or component availability;
- **High** for security, audit, identity, operational, or important configuration changes;
- **Medium** for meaningful non-blocking behavior;
- **Low** for labels or descriptions.

### Noise and normalization

Call out generated IDs, timestamps, version values, ordering, whitespace, managed state, or environment-specific references that could create false positives. Also provide a nearby meaningful change that must remain detectable.

### Retrieval and permissions

State whether the data is available through Dataverse metadata, a Dataverse system table, Power Automate APIs, or another read-only API. New authentication scopes or privileges must be visible to the user and cannot be assumed.

### Scale

Estimate the number and size of components in a large environment. Mention XML, JSON, binary, or other definitions that require paging, fingerprint previews, or on-demand inspection.

### Sensitive values

Identify credentials, secrets, connection details, personal data, or business records that must not be retrieved or exported. Secret environment-variable values and connection credentials are outside the intended comparison scope.

## Organization settings suggestions

Organization settings need particular care because many values are deliberately different across development, test, and production.

For each requested setting, explain whether it is:

- an expected portable configuration;
- an intentional environment-specific value;
- informational context only;
- volatile or generated and therefore unsuitable for comparison.

Group coherent settings rather than requesting the complete `organization` row. The project will prefer a reviewed allowlist over automatically comparing every available attribute.

## How requests are evaluated

Maintainers will consider:

- user value and deployment risk;
- stable identity and deterministic comparison;
- false-positive and false-negative risk;
- read-only API availability and permission impact;
- data sensitivity;
- performance in large environments;
- consistency with preview and export behavior;
- testability without live environment credentials;
- maintenance cost and Dataverse API stability.

A roadmap entry is not a committed delivery date. A request may be accepted for design, deferred pending API support, split into smaller issues, or declined when it conflicts with the read-only and metadata-focused scope.

## Ready to implement?

After the request has an agreed design, read [Contributing](../CONTRIBUTING.md) and [Extending the comparison engine](extending.md) before opening a pull request.
