using System;

using R5T.T0162;
using R5T.T0203;


namespace R5T.O0027
{
    public sealed class InheritdocReference : IEquatable<InheritdocReference>
    {
        public IIdentityName Cref { get; set; }
        public IXPathText Path { get; set; }


        public override bool Equals(object obj)
        {
            if(obj is InheritdocReference objAsInheritdocReference)
            {
                return this.Equals(objAsInheritdocReference);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var output = HashCode.Combine(
                this.Cref?.Value,
                this.Path?.Value);

            return output;
        }

        public bool Equals(InheritdocReference other)
        {
            var otherIsNull = other is null;
            if (otherIsNull)
            {
                // This instance can't be null (since we are calling a method on it), so if the other is null, then the two instance are not equal.
                return false;
            }

            var output = true
                && this.Cref.Value == other.Cref.Value
                && this.Path.Value == other.Path.Value
                ;

            return output;
        }
    }
}
