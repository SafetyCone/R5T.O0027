using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.XPath;

using R5T.T0131;
using R5T.T0162;
using R5T.T0172;
using R5T.T0212.F000;


namespace R5T.O0027.Internal
{
    [ValuesMarker]
    public partial interface IDocumentationCommentOperations : IValuesMarker
    {
        public void Process_MemberDocumentation(
            MemberDocumentation memberDocumentation,
            IDictionary<IIdentityName, MemberDocumentation> memberDocumentationsByIdentityName,
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            IList<MissingDocumentationReference> missingDocumentationReferences)
        {
            //// For debugging.
            //if(memberDocumentation.IdentityName.Value == "M:R5T.F0000.Extensions.IDictionaryExtensions.Add_IfKeyNotFound``2(System.Collections.Generic.IDictionary{``0,``1},``0,``1)")
            //{
            //    Console.WriteLine("Here");
            //}

            // Short-circuit if the member documentation has already been processed.
            if (processedDocumentationsByIdentityName.ContainsKey(memberDocumentation.IdentityName))
            {
                return;
            }

            // Else, process all inheritdoc elements.
            var inheritdocElements = Instances.XElementOperator.Get_Children(
                memberDocumentation.MemberElement.Value,
                Instances.XmlDocumentationCommentElementNames.Inheritdoc)
                .Now();

            foreach (var inheritdocElement in inheritdocElements)
            {
                var hasCref = Instances.InheritdocElementOperator.Has_Cref(inheritdocElement);
                var hasPath = Instances.InheritdocElementOperator.Has_Path(inheritdocElement);

                if(!hasCref)
                {
                    // If the element refers to its base implementation, we will need to examine base types to find matching method signature.
                    // For now, just remove the inheritdoc element.
                    // Add the identity name of the member to the missing names list to surface the issue. (Even though this might be confusing.)

                    var missingDocumentationReference = new MissingDocumentationReference
                    {
                        DocumentationTarget = memberDocumentation.DocumentationTarget,
                        ReferencingName = memberDocumentation.IdentityName,
                        // There is no referenced name (yet... this will need to be deterined by searching base classes).
                        MissingName = Instances.SpecialIdentityNames.BaseMember,
                    };

                    missingDocumentationReferences.Add(missingDocumentationReference);

                    inheritdocElement.Remove();

                    continue;
                }

                var cref = Instances.InheritdocElementOperator.Get_Cref(inheritdocElement);

                // If self-referential.
                var isSelfReferential = cref.Equals(memberDocumentation.IdentityName);
                if (isSelfReferential)
                {
                    if(!hasPath)
                    {
                        // If self referential, and has no path value, then just remove the node.
                        // (This should be a warning in VS.)
                        // Add the identity name to the missing names list to surface the issue.

                        var missingDocumentationReference = new MissingDocumentationReference
                        {
                            DocumentationTarget = memberDocumentation.DocumentationTarget,
                            ReferencingName = memberDocumentation.IdentityName,
                            // This is an error (that should be a warning in VS), so mark it as such.
                            MissingName = Instances.SpecialIdentityNames.Error_SelfReferential,
                        };

                        missingDocumentationReferences.Add(missingDocumentationReference);

                        inheritdocElement.Remove();

                        continue;
                    }

                    // If self-referential, and has a path, it will be handled below.
                }

                var nameAlreadyProcessed = processedDocumentationsByIdentityName.ContainsKey(cref);
                if (!nameAlreadyProcessed && !isSelfReferential)
                {
                    var nameIsAvailable = memberDocumentationsByIdentityName.ContainsKey(cref);
                    if (!nameIsAvailable)
                    {
                        // Note the missing name.
                        var missingDocumentationReference = new MissingDocumentationReference
                        {
                            DocumentationTarget = memberDocumentation.DocumentationTarget,
                            ReferencingName = memberDocumentation.IdentityName,
                            // This is an error (that should be a warning in VS), so mark it as such.
                            MissingName = cref,
                        };

                        missingDocumentationReferences.Add(missingDocumentationReference);

                        continue;
                    }

                    // Recurse.
                    var memberDocumentationForName = memberDocumentationsByIdentityName[cref];

                    this.Process_MemberDocumentation(
                        memberDocumentationForName,
                        memberDocumentationsByIdentityName,
                        processedDocumentationsByIdentityName,
                        missingDocumentationReferences);

                    // Now the name will be available.
                }

                // Name is available, or we have continued.
                var processedDocumentationForName = isSelfReferential
                    // Use the member documentation's reference since the processed documentation doesn't exist yet (both link to the same memory location, so ok).
                    ? memberDocumentationsByIdentityName[cref]
                    : processedDocumentationsByIdentityName[cref]
                    ;

                if (hasPath)
                {
                    // Assume path could select multiple elements.
                    var elements = processedDocumentationForName.MemberElement.Value.XPathSelectElements(
                        // Interprety XPath as from current node.
                        "." + hasPath.Result.Value)
                        .ToArray();
                    
                    inheritdocElement.ReplaceWith(
                        elements);
                }
                else
                {
                    var replacementNodes = Instances.XElementOperator.Get_Nodes_ExceptLeadingAndTrailingWhitespaceNodes(
                        processedDocumentationForName.MemberElement.Value)
                        .Now();

                    inheritdocElement.ReplaceWith(replacementNodes);
                }
            }

            processedDocumentationsByIdentityName.Add(
                memberDocumentation.IdentityName,
                memberDocumentation);
        }
    }
}
