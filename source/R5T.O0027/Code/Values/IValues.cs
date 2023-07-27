using System;

using R5T.T0131;


namespace R5T.O0027
{
    [ValuesMarker]
    public partial interface IValues : IValuesMarker
    {
        public string Inheritdoc_BaseMemberNotYetSupported => "Base member <inheritdoc> substition not yet supported.";
        public string Inheritdoc_InfiniteSubstitutioDetected => "Infinite <inheritdoc> substitution detected.";
        public string Inheritdoc_SelfReferentialWithNoPath => "Self-referential but with no path.";
    }
}
