using Nullable.Extended.AnalyzerFramework;

namespace Nullable.Extended.NullForgivingAnalyzer
{
    public class NullForgivingAnalysisResult : AnalysisResult
    {
        private readonly IReadOnlyList<NullForgivingDocumentResult> _variants;

        internal NullForgivingAnalysisResult(IReadOnlyList<NullForgivingDocumentResult> variants)
            : base(variants)
        {
            _variants = variants;
        }

        public string? Justification => _variants[0].Justification;

        public bool IsJustified => _variants[0].IsJustified;

        public NullForgivingContext Context => _variants[0].Context;

        /// <summary>
        /// Whether the operator is required: <c>true</c> if it is required in at least one variant, e.g. in one target framework,
        /// <c>null</c> if it could not be determined (yet) for at least one variant and none requires it, <c>false</c> if it is not required in any variant.
        /// </summary>
        public bool? IsRequired
        {
            get
            {
                var isUnknown = false;

                foreach (var variant in _variants)
                {
                    switch (variant.IsRequired)
                    {
                        case true:
                            return true;
                        case null:
                            isUnknown = true;
                            break;
                    }
                }

                return isUnknown ? null : false;
            }
        }

        internal override void NotifyVariantChanged()
        {
            base.NotifyVariantChanged();

            OnPropertyChanged(nameof(IsRequired));
        }
    }
}
