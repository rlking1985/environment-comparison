# Extending the comparison engine

The code separates retrieval, normalized snapshots, comparison rules, UI filtering, and CSV output.

To add a comparison area:

1. Add a flag to `ComparisonAreas`.
2. Add a normalized immutable model for that component.
3. Load it with read-only SDK requests in `DataverseMetadataService`.
4. Match components by a stable solution identity, with an explicit fallback only when necessary.
5. Add an important-property severity map and comparison method in `MetadataComparisonService`.
6. Add the UI checkbox and display label.
7. Add synthetic tests for missing, changed, identical, and unselected behavior.
8. Update the safety documentation if the retrieval surface changes.

Do not add managed state, solution IDs, metadata IDs, timestamps, or version stamps to the default comparison. Those values are usually deployment noise rather than functional differences.

Any future write capability should be built as a separate tool. This plugin's contract is read-only comparison.
