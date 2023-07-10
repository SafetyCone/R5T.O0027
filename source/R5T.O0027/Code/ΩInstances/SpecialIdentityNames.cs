using System;


namespace R5T.O0027
{
    public class SpecialIdentityNames : ISpecialIdentityNames
    {
        #region Infrastructure

        public static ISpecialIdentityNames Instance { get; } = new SpecialIdentityNames();


        private SpecialIdentityNames()
        {
        }

        #endregion
    }
}
