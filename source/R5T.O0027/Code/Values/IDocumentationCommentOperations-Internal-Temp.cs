using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

using R5T.F0000;
using R5T.L0030.Extensions;
using R5T.T0131;
using R5T.T0159;
using R5T.T0162;
using R5T.T0203;
using R5T.T0212.F000;


namespace R5T.O0027.Internal
{
    public partial interface IDocumentationCommentOperations : IValuesMarker
    {
        public void Expand_InheritdocElements2(
            MemberDocumentation memberDocumentation,
            IDictionary<IIdentityName, MemberDocumentation> memberDocumentationsByIdentityName,
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            ITextOutput textOutput)
        {
            // The main issue with expansion is how to handle cyles, where A references B, but B references A.
            // This code chooses the strategy of recursing on references, but within the context of the current member documentation being evaluated.
            // The result of evaluating the reference will be used for the current member, but not the reference itself.
            // This will do duplicate work to expand identical inheritdoc elements, but will allow cycles to be evaluated.
            // A hashset (cheap stack) of inheritdoc element references will be maintained, and any inheritdoc crefs in the hashset will not be recursed upon.

            // Note: there is still an issue of when an inheritdoc element is self-referential.
            // Care will need to be taken when an element is self-referential to ensure there is no infinite regress.
            // The Visual Studio functionality seems to break cycles by keeping a stack of inheritdoc elements and somehow equality testing?

            textOutput.Write_Information_NoFormatting($"Processing member...:\n\t'{memberDocumentation.IdentityName}'");

            this.Write_MemberElement(textOutput, memberDocumentation);

            // Short-circuit if the member documentation has already been processed.
            if (this.Should_ShortCircuit(
                processedDocumentationsByIdentityName,
                memberDocumentation,
                textOutput))
            {
                return;
            }

            // Start by creating a clone of the member documentation so that we don't modify the input member documentation.
            var processedMemberDocumentation = Instances.MemberDocumentationOperator.Clone_WithClonedMemberElement(memberDocumentation);

            this.Expand_CrefInheritdocElements(
                processedMemberDocumentation,
                memberDocumentationsByIdentityName,
                processedDocumentationsByIdentityName,
                missingDocumentationReferences,
                textOutput);

            processedDocumentationsByIdentityName.Add(
                processedMemberDocumentation.IdentityName,
                processedMemberDocumentation);

            textOutput.Write_Information_NoFormatting($"Processed member '{memberDocumentation.IdentityName}'.");
        }
    }
}
