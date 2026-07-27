using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Ui
{
    internal sealed class ComparisonLoadResult
    {
        public ComparisonLoadResult(MetadataComparisonResult result, bool includedUnpublished)
        {
            Result = result;
            IncludedUnpublished = includedUnpublished;
        }

        public MetadataComparisonResult Result { get; }

        public bool IncludedUnpublished { get; }
    }
}

