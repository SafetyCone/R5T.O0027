using System;

using R5T.T0131;
using R5T.T0162;
using R5T.T0162.Extensions;


namespace R5T.O0027
{
    [ValuesMarker]
    public partial interface ISpecialIdentityNames : IValuesMarker
    {
        public IIdentityName BaseMember => "<Base Member>".ToIdentityName();
        public IIdentityName Error_SelfReferential => "<Error-Self Referential>".ToIdentityName();
    }
}
