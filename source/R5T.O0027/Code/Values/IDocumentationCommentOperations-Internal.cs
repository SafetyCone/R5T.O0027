using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

using R5T.N0000;

using R5T.L0030.Extensions;
using R5T.T0131;
using R5T.T0159;
using R5T.T0162;
using R5T.T0203;
using R5T.T0212.F000;


namespace R5T.O0027.Internal
{
    [ValuesMarker]
    public partial interface IDocumentationCommentOperations : IValuesMarker
    {
        /// <summary>
        /// Entry-point for recursion.
        /// </summary>
        public void Expand_CrefInheritdocElements(
            MemberDocumentation memberDocumentation,
            IDictionary<IIdentityName, MemberDocumentation> originalMemberDocumentationsByIdentityName,
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            ITextOutput textOutput)
        {
            var encounteredInheritdocElements = new HashSet<InheritdocReference>();

            this.Expand_CrefInheritdocElements_Recursive(
                memberDocumentation,
                originalMemberDocumentationsByIdentityName,
                processedDocumentationsByIdentityName,
                missingDocumentationReferences,
                encounteredInheritdocElements,
                textOutput);
        }

        /// <summary>
        /// While there are crefs within the member documentation, expand them, but do not store the intermediate expansions or the resulting expansion for the cref.
        /// Modify (don't return) the member documentation.
        /// </summary>
        public void Expand_CrefInheritdocElements_Recursive(
            MemberDocumentation memberDocumentation,
            IDictionary<IIdentityName, MemberDocumentation> originalMemberDocumentationsByIdentityName,
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            HashSet<InheritdocReference> encounteredInheritdocElements,
            ITextOutput textOutput)
        {
            this.Write_MemberElement(textOutput, memberDocumentation);

            // Make a copy of the initial state for later use.
            var originalMemberDocumentation = Instances.MemberDocumentationOperator.Clone_WithClonedMemberElement(memberDocumentation);

            var hasInheritdoc = this.Has_InheritdocElement_First(memberDocumentation);
            if (hasInheritdoc)
            {
                textOutput.WriteInformation($"<inheritdoc> element found.");
            }
            else
            {
                textOutput.WriteInformation($"No <inheritdoc> elements found.");

                // There are no inheritdoc elements to expand.
                return;
            }

            // While there are any inheritdoc elements, keep replacing them.
            while (hasInheritdoc)
            {
                var inheritdocElement = hasInheritdoc.Result;

                textOutput.Write_Information_NoFormatting($"Processing <inheritdoc>...\n\t{inheritdocElement}");

                Process_InheritdocElement(inheritdocElement);

                // Show work on the member element thus far.
                this.Write_MemberElement(textOutput, memberDocumentation);

                // Prepare the next while-loop iteration.
                hasInheritdoc = this.Has_InheritdocElement_First(memberDocumentation);
            }

            void Process_InheritdocElement(XElement inheritdocElement)
            {
                var hasCref = Instances.InheritdocElementOperator.Has_Cref(inheritdocElement);
                var hasPath = Instances.InheritdocElementOperator.Has_Path(inheritdocElement);

                textOutput.WriteInformation($"cref: {hasCref.Exists}\npath: {hasPath.Exists}");

                // Has the inheritdoc element already been encountered?
                var inheritdocReference = new InheritdocReference
                {
                    // Null results are ok.
                    Cref = hasCref.Result,
                    Path = hasPath.Result,
                };

                var crefAlreadyEncountered = encounteredInheritdocElements.Contains(inheritdocReference);
                if (crefAlreadyEncountered)
                {
                    textOutput.WriteInformation("Inheritdoc element already encountered in cycle.");

                    // Just remove the element.
                    inheritdocElement.Remove();

                    // Done with this inheritdoc element.
                    return;
                }
                else
                {
                    // Well we have now.
                    // Add the inheritdoc element to the set of encountered elements.
                    encounteredInheritdocElements.Add(inheritdocReference);
                }

                if (!hasCref)
                {
                    // The element refers to its base member implementation, handle it.
                    this.Handle_InheritdocWithoutCref(
                        memberDocumentation,
                        inheritdocElement,
                        missingDocumentationReferences,
                        textOutput);

                    // Done with this inheritdoc.
                    return;
                }

                // At this point, we know there is a cref result.
                var cref = hasCref.Result;

                textOutput.Write_Information_NoFormatting($"Processing cref...\n\t{cref}");

                // If self-referential.
                var isSelfReferential = cref.Equals(memberDocumentation.IdentityName);
                if (isSelfReferential)
                {
                    textOutput.WriteInformation("Cref is self-referential.");

                    if (hasPath)
                    {
                        // If self-referential and has a path value, then use the original member documentation (not the current state of the member documentation).
                        var memberDocumentationForCref = originalMemberDocumentation;

                        // Perform substitution.
                        this.Substitute_Inheritdoc(
                            memberDocumentation,
                            inheritdocElement,
                            cref,
                            hasPath,
                            memberDocumentationForCref,
                            missingDocumentationReferences,
                            textOutput);

                        // Done with this inheritdoc.
                        return;
                    }
                    else
                    {
                        // If self-referential and has no path value, then just remove the node.
                        // (This should be a warning in VS since it would cause an infinite regress.)
                        // Add the identity name to the missing names list to surface the issue.

                        var missingDocumentationReference = new MissingDocumentationReference
                        {
                            DocumentationTarget = memberDocumentation.DocumentationTarget,
                            ReferencingName = memberDocumentation.IdentityName,
                            // This is an error (that should be a warning in VS), so mark it as such.
                            MissingName = Instances.SpecialIdentityNames.Error_SelfReferential,
                            Note = Instances.Values.Inheritdoc_SelfReferentialWithNoPath,
                        };

                        missingDocumentationReferences.Add(missingDocumentationReference);

                        inheritdocElement.Remove();

                        textOutput.WriteInformation("Self-referential cref had no path. This should be an error (since it would cause an infinite loop). See missing documentation references output.");

                        // Done with this inheritdoc.
                        return;
                    }
                }

                // As of this point, the member is not self-referential.

                // Note: it is irrelevant if the name has already been processed.
                // Each documentation must be processed from the initial state to match the VS functionality.
                // (Also, this must be true for the results of cycles to be encountered-order independent!)

                var nameIsAvailable = originalMemberDocumentationsByIdentityName.ContainsKey(cref);
                if (nameIsAvailable)
                {
                    textOutput.WriteInformation("Cref member documentation found.");

                    // Here we will need to recursve upon the cref.
                    // Use the original state of the cref's member, not its (potentially) processed state.
                    var crefMemberDocumentation = originalMemberDocumentationsByIdentityName[cref];

                    // Use a copy of the original state of the cref's member.
                    var locallyProcessedCrefMemberDocumentation = Instances.MemberDocumentationOperator.Clone_WithClonedMemberElement(crefMemberDocumentation);

                    // Recurse.
                    this.Expand_CrefInheritdocElements_Recursive(
                        locallyProcessedCrefMemberDocumentation,
                        originalMemberDocumentationsByIdentityName,
                        processedDocumentationsByIdentityName,
                        missingDocumentationReferences,
                        encounteredInheritdocElements,
                        textOutput);

                    // Perform substitution.
                    this.Substitute_Inheritdoc(
                        memberDocumentation,
                        inheritdocElement,
                        cref,
                        hasPath,
                        locallyProcessedCrefMemberDocumentation,
                        missingDocumentationReferences,
                        textOutput);

                    // Done with this inheritdoc.
                    return;
                }
                else
                {
                    // Note the missing name.
                    var missingDocumentationReference = new MissingDocumentationReference
                    {
                        DocumentationTarget = memberDocumentation.DocumentationTarget,
                        ReferencingName = memberDocumentation.IdentityName,
                        // This is an error (that should be a warning in VS), so mark it as such.
                        MissingName = cref,
                        // No note needed, since this is the actual case of a missing documentation member.
                    };

                    missingDocumentationReferences.Add(missingDocumentationReference);

                    inheritdocElement.Remove();

                    textOutput.WriteInformation("Cref member documentation not found. See missing documentation references output.");

                    // Done with this inheritdoc.
                    return;
                }
            }
        }

        public void Handle_InheritdocWithoutCref(
            MemberDocumentation memberDocumentation,
            XElement inheritdocElement,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            ITextOutput textOutput)
        {
            // The element refers to its base member implementation, so we will need to examine base types to find matching method signature.
            // For now, just remove the inheritdoc element.
            // Then add the identity name of the member to the missing names list to surface the issue. (Even though this might be confusing.)

            var missingDocumentationReference = new MissingDocumentationReference
            {
                DocumentationTarget = memberDocumentation.DocumentationTarget,
                ReferencingName = memberDocumentation.IdentityName,
                // There is no referenced name (yet... this will need to be deterined by searching base classes).
                MissingName = Instances.SpecialIdentityNames.BaseMember,
                Note = Instances.Values.Inheritdoc_BaseMemberNotYetSupported,
            };

            missingDocumentationReferences.Add(missingDocumentationReference);

            inheritdocElement.Remove();

            textOutput.WriteInformation("No cref attribute found for inheritdoc element: searching base implementations not implemented. See missing documentation references output.");
        }

        public void Substitute_Inheritdoc(
            MemberDocumentation memberDocumentation,
            XElement inheritdocElement,
            IIdentityName cref,
            WasFound<IXPathText> hasPath,
            MemberDocumentation memberDocumentationForCref,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            ITextOutput textOutput)
        {
            if (hasPath)
            {
                textOutput.WriteInformation("Replacing from path...");

                var xPathValue = "." + hasPath.Result.Value;

                if(hasPath.Result.Value.Contains("descendant"))
                {
                    xPathValue = "./" + hasPath.Result.Value;
                }

                // Assume path could select multiple elements.
                var elements = memberDocumentationForCref.MemberElement.Value.XPathSelectElements(
                    // Interprety XPath as from current node.
                    xPathValue)
                    .ToArray();

                // Don't replace with the selected elements themselves, but with their children.
                var nonCyclicChildNoes = elements
                    // Select all child nodes (not just elements).
                    .SelectMany(x => x.Nodes())
                    // Detect cycles.
                    .Where(childNode =>
                    {
                        if (childNode is XElement childElement)
                        {
                            var childElementIsOrContainsInheritdocElement = childElement.IsOrContains_Descendant(inheritdocElement);
                            if (childElementIsOrContainsInheritdocElement)
                            {
                                // Note the cycle name.
                                var missingDocumentationReference = new MissingDocumentationReference
                                {
                                    DocumentationTarget = memberDocumentation.DocumentationTarget,
                                    ReferencingName = memberDocumentation.IdentityName,
                                    // This is an error (that should be a warning in VS), so mark it as such.
                                    MissingName = cref,
                                    // Add a note about the situation.
                                    Note = Instances.Values.Inheritdoc_InfiniteSubstitutioDetected,
                                };

                                missingDocumentationReferences.Add(missingDocumentationReference);

                                // Don't remove the inheritdoc element, as it will be replaced.

                                textOutput.WriteInformation("Cref member would be cyclic.");

                                return false;
                            }
                        }

                        return true;
                    })
                .Now();

                inheritdocElement.ReplaceWith(
                    nonCyclicChildNoes);
            }
            else
            {
                textOutput.WriteInformation("Replacing from elements...");

                var replacementNodes = Instances.XElementOperator.Get_Nodes_ExceptLeadingAndTrailingWhitespaceNodes(
                    memberDocumentationForCref.MemberElement.Value)
                .Now();

                inheritdocElement.ReplaceWith(replacementNodes);
            }
        }

        public void Expand_InheritdocElements(
            MemberDocumentation memberDocumentation,
            IDictionary<IIdentityName, MemberDocumentation> memberDocumentationsByIdentityName,
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            ITextOutput textOutput)
        {
            // The main issue with expansion is how to handle cyles, where A references B, but B references A.
            // This code chooses the strategy of not recursing, and instead substituting only the called result.
            // This will do duplicate work to expand identical inheritdoc elements.

            // Note: there is still an issue of when an inheritdoc element is self-referential.
            // Care will need to be taken when an element is self-referential to ensure there is no infinite regress.
            // The Visual Studio functionality seems to break cycles by keeping a stack of inheritdoc elements and somehow equality testing?

            textOutput.Write_Information_NoFormatting($"Processing member...:\n\t'{memberDocumentation.IdentityName}'");

            this.Write_MemberElement(textOutput, memberDocumentation);

            // Short-circuit if the member documentation has already been processed.
            if (this.Should_ShortCircuit(processedDocumentationsByIdentityName, memberDocumentation, textOutput))
            {
                return;
            }

            // Start by creating a clone of the member documentation so that we don't modify the input member documentation.
            var processedMemberDocumentation = Instances.MemberDocumentationOperator.Clone_WithClonedMemberElement(memberDocumentation);

            // Use an internal method to ensure we use the right member documentation instance.
            Internal(processedMemberDocumentation);

            void Internal(MemberDocumentation memberDocumentation)
            {
                var hasInheritdoc = this.Has_InheritdocElement_First(memberDocumentation);
                if (hasInheritdoc)
                {
                    textOutput.WriteInformation($"<inheritdoc> element found.");
                }
                else
                {
                    textOutput.WriteInformation($"No <inheritdoc> elements found.");
                }

                void Process_InheritdocElement(XElement inheritdocElement)
                {
                    var hasCref = Instances.InheritdocElementOperator.Has_Cref(inheritdocElement);
                    var hasPath = Instances.InheritdocElementOperator.Has_Path(inheritdocElement);

                    textOutput.WriteInformation($"cref: {hasCref.Exists}\npath: {hasPath.Exists}");

                    if (!hasCref)
                    {
                        // The element refers to its base member implementation.

                        // If the element refers to its base implementation, we will need to examine base types to find matching method signature.
                        // For now, just remove the inheritdoc element.
                        // Add the identity name of the member to the missing names list to surface the issue. (Even though this might be confusing.)

                        var missingDocumentationReference = new MissingDocumentationReference
                        {
                            DocumentationTarget = memberDocumentation.DocumentationTarget,
                            ReferencingName = memberDocumentation.IdentityName,
                            // There is no referenced name (yet... this will need to be deterined by searching base classes).
                            MissingName = Instances.SpecialIdentityNames.BaseMember,
                            Note = Instances.Values.Inheritdoc_BaseMemberNotYetSupported,
                        };

                        missingDocumentationReferences.Add(missingDocumentationReference);

                        inheritdocElement.Remove();

                        textOutput.WriteInformation("No cref attribute found for inheritdoc element: searching base implementations not implemented. See missing documentation references output.");

                        return;
                    }

                    // At this point, we know there is a cref result.
                    var cref = hasCref.Result;

                    textOutput.Write_Information_NoFormatting($"Processing cref...\n\t{cref}");

                    // If self-referential.
                    var isSelfReferential = cref.Equals(memberDocumentation.IdentityName);
                    if (isSelfReferential)
                    {
                        textOutput.WriteInformation("Cref is self-referential.");

                        if (!hasPath)
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
                                Note = Instances.Values.Inheritdoc_SelfReferentialWithNoPath,
                            };

                            missingDocumentationReferences.Add(missingDocumentationReference);

                            inheritdocElement.Remove();

                            textOutput.WriteInformation("Self-referential cref had no path. This should be an error (since it would cause an infinite loop). See missing documentation references output.");

                            return;
                        }
                        // Else, if self-referential and has a path, it will be handled below.
                    }

                    // Has the cref's name already been processed?
                    var crefNameAlreadyProcessed = processedDocumentationsByIdentityName.ContainsKey(cref);
                    if (crefNameAlreadyProcessed)
                    {
                        textOutput.WriteInformation("Cref name already processed.");
                    }
                    else
                    {
                        // If the cref's name has not be processed, we will need to check that it exists.
                        var nameIsAvailable = memberDocumentationsByIdentityName.ContainsKey(cref);
                        if (nameIsAvailable)
                        {
                            textOutput.WriteInformation("Cref member documentation found.");
                        }
                        else
                        {
                            // Note the missing name.
                            var missingDocumentationReference = new MissingDocumentationReference
                            {
                                DocumentationTarget = memberDocumentation.DocumentationTarget,
                                ReferencingName = memberDocumentation.IdentityName,
                                // This is an error (that should be a warning in VS), so mark it as such.
                                MissingName = cref,
                                // No note needed, since this is the actual case of a missing documentation member.
                            };

                            missingDocumentationReferences.Add(missingDocumentationReference);

                            inheritdocElement.Remove();

                            textOutput.WriteInformation("Cref member documentation not found. See missing documentation references output.");

                            return;
                        }
                    }

                    // Cref name is available, or we have continued.
                    // Use processed result, if available.
                    var memberDocumentationForCref = crefNameAlreadyProcessed
                        ? processedDocumentationsByIdentityName[cref]
                        : isSelfReferential
                            // If self-referential, use the member documentation itself!
                            ? memberDocumentation
                            : memberDocumentationsByIdentityName[cref]
                        ;

                    // Perform substitution.
                    if (hasPath)
                    {
                        textOutput.WriteInformation("Replacing from path...");

                        // Assume path could select multiple elements.
                        var elements = memberDocumentationForCref.MemberElement.Value.XPathSelectElements(
                            // Interprety XPath as from current node.
                            "." + hasPath.Result.Value)
                            .ToArray();

                        // Don't replace with the selected elements themselves, but with their children.
                        var childNodes = elements
                            .SelectMany(x => x.Nodes())
                            ;

                        // Detect cycles.
                        var nonCyclicChildNoes = childNodes
                            .Where(childNode =>
                            {
                                if (childNode is XElement childElement)
                                {
                                    var childElementIsOrContainsInheritdocElement = childElement.IsOrContains_Descendant(inheritdocElement);
                                    if (childElementIsOrContainsInheritdocElement)
                                    {
                                        // Note the cycle name.
                                        var missingDocumentationReference = new MissingDocumentationReference
                                        {
                                            DocumentationTarget = memberDocumentation.DocumentationTarget,
                                            ReferencingName = memberDocumentation.IdentityName,
                                            // This is an error (that should be a warning in VS), so mark it as such.
                                            MissingName = cref,
                                            // Add a note about the situation.
                                            Note = Instances.Values.Inheritdoc_InfiniteSubstitutioDetected,
                                        };

                                        missingDocumentationReferences.Add(missingDocumentationReference);

                                        // Don't remove the inheritdoc element, as it will be replaced.

                                        textOutput.WriteInformation("Cref member would be cyclic.");

                                        return false;
                                    }
                                }

                                return true;
                            })
                            .Now();

                        inheritdocElement.ReplaceWith(
                            nonCyclicChildNoes);
                    }
                    else
                    {
                        textOutput.WriteInformation("Replacing from elements...");

                        var replacementNodes = Instances.XElementOperator.Get_Nodes_ExceptLeadingAndTrailingWhitespaceNodes(
                            memberDocumentationForCref.MemberElement.Value)
                            .Now();

                        inheritdocElement.ReplaceWith(replacementNodes);
                    }
                }

                // While there are any inheritdoc elements, keep replacing them.
                while (hasInheritdoc)
                {
                    var inheritdocElement = hasInheritdoc.Result;

                    textOutput.Write_Information_NoFormatting($"Processing <inheritdoc>...\n\t{inheritdocElement}");

                    Process_InheritdocElement(inheritdocElement);

                    // Show work on the member element thus far.
                    this.Write_MemberElement(textOutput, memberDocumentation);

                    // Prepare the next while-loop iteration.
                    hasInheritdoc = this.Has_InheritdocElement_First(memberDocumentation);
                }

                processedDocumentationsByIdentityName.Add(
                    memberDocumentation.IdentityName,
                    memberDocumentation);

                textOutput.Write_Information_NoFormatting($"Processed member '{memberDocumentation.IdentityName}'.");
            }
        }

        public void Write_MemberElement(
            ITextOutput textOutput,
            MemberDocumentation memberDocumentation)
        {
            textOutput.Write_Information_NoFormatting($"\nMember element:\n{Instances.MemberElementOperator.ToString(memberDocumentation.MemberElement)}\n");
        }

        public bool Should_ShortCircuit(
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            MemberDocumentation memberDocumentation,
            ITextOutput textOutput)
        {
            var shouldShortCircuit = processedDocumentationsByIdentityName.ContainsKey(memberDocumentation.IdentityName);
            if(shouldShortCircuit)
            {
                textOutput.WriteInformation($"Member already processed '{memberDocumentation.IdentityName}'.");
            }
            else
            {
                textOutput.WriteInformation($"Member requires processing; not already processed.");
            }

            return shouldShortCircuit;
        }

        public WasFound<XElement> Has_InheritdocElement_First(MemberDocumentation memberDocumentation)
        {
            var output = Instances.XElementOperator.Has_Descendant_First(
                memberDocumentation.MemberElement.Value,
                Instances.XmlDocumentationCommentElementNames.Inheritdoc);

            return output;
        }

        [Obsolete("See Expand_InheritdocElements()")]
        public void Process_MemberDocumentation(
            MemberDocumentation memberDocumentation,
            IDictionary<IIdentityName, MemberDocumentation> memberDocumentationsByIdentityName,
            IDictionary<IIdentityName, MemberDocumentation> processedDocumentationsByIdentityName,
            IList<MissingDocumentationReference> missingDocumentationReferences,
            ITextOutput textOutput)
        {
            // First draft.

            textOutput.WriteInformation($"Processing member...:\n\t'{memberDocumentation.IdentityName}'");

            this.Write_MemberElement(textOutput, memberDocumentation);

            // Short-circuit if the member documentation has already been processed.
            if (this.Should_ShortCircuit(processedDocumentationsByIdentityName, memberDocumentation, textOutput))
            {
                return;
            }

            // Else, process all inheritdoc elements.
            var inheritdocElements = Instances.XElementOperator.Get_Descendants(
                memberDocumentation.MemberElement.Value,
                Instances.XmlDocumentationCommentElementNames.Inheritdoc)
                .Now();

            if(inheritdocElements.Any())
            {
                textOutput.WriteInformation($"{inheritdocElements.Length}: <inheritdoc> element count.");
            }
            else
            {
                textOutput.WriteInformation($"No <inheritdoc> elements found.");
            }

            foreach (var inheritdocElement in inheritdocElements)
            {
                textOutput.WriteInformation($"Processing <inheritdoc>...\n\t{inheritdocElement}");

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

                    textOutput.WriteInformation("No cref attribute found for inheritdoc element: searching base implementations not implemented. See missing documentation references output.");

                    continue;
                }

                // At this point, we know there is a cref result.
                var cref = hasCref.Result;

                textOutput.WriteInformation($"Processing cref...\n\t{cref}");

                // If self-referential.
                var isSelfReferential = cref.Equals(memberDocumentation.IdentityName);
                if (isSelfReferential)
                {
                    textOutput.WriteInformation("Cref is self-referential.");

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

                        textOutput.WriteInformation("Self-referential cref had no path. This should be an error (since it would cause an infinite loop). See missing documentation references output.");

                        continue;
                    }

                    // If self-referential, and has a path, it will be handled below.
                }

                // Has the cref already been processed?
                var nameAlreadyProcessed = processedDocumentationsByIdentityName.ContainsKey(cref);
                if(nameAlreadyProcessed)
                {
                    textOutput.WriteInformation("Cref already processed.");
                }
                else
                {
                    // If the name has not already been processed, we will need to recursively process the name itself.
                    if (isSelfReferential)
                    {
                        // At this point, the cref is self-referential, but has a path.
                        // This case will be handled below, so nothing to do.
                    }
                    else
                    {
                        var nameIsAvailable = memberDocumentationsByIdentityName.ContainsKey(cref);
                        if (nameIsAvailable)
                        {
                            textOutput.WriteInformation("Cref member documentation found.");
                        }
                        else
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

                            textOutput.WriteInformation("Cref member documentation not found. See missing documentation references output.");

                            continue;
                        }

                        // Recurse.
                        var memberDocumentationForName = memberDocumentationsByIdentityName[cref];

                        textOutput.WriteInformation("Processing cref...");

                        this.Process_MemberDocumentation(
                            memberDocumentationForName,
                            memberDocumentationsByIdentityName,
                            processedDocumentationsByIdentityName,
                            missingDocumentationReferences,
                            textOutput);

                        textOutput.WriteInformation("Processed cref.");

                        // Now the name will be available.
                    }
                }

                // Name is available, or we have continued.
                var processedDocumentationForName = isSelfReferential
                    // Use the member documentation's reference since the processed documentation doesn't exist yet (both link to the same memory location, so ok).
                    ? memberDocumentationsByIdentityName[cref]
                    : processedDocumentationsByIdentityName[cref]
                    ;

                if (hasPath)
                {
                    textOutput.WriteInformation("Replacing from path...");

                    // Assume path could select multiple elements.
                    var elements = processedDocumentationForName.MemberElement.Value.XPathSelectElements(
                        // Interprety XPath as from current node.
                        "." + hasPath.Result.Value)
                        .ToArray();

                    // Don't replace with the selected elements themselves, but with their children.
                    var childNodes = elements
                        .SelectMany(x => x.Nodes())
                        ;
                    
                    inheritdocElement.ReplaceWith(
                        childNodes);
                }
                else
                {
                    textOutput.WriteInformation("Replacing from elements...");

                    var replacementNodes = Instances.XElementOperator.Get_Nodes_ExceptLeadingAndTrailingWhitespaceNodes(
                        processedDocumentationForName.MemberElement.Value)
                        .Now();

                    inheritdocElement.ReplaceWith(replacementNodes);
                }

                // Show work on the member element thus far:
                textOutput.WriteInformation($"\nMember element:\n{Instances.MemberElementOperator.ToString(memberDocumentation.MemberElement)}\n");
            }

            processedDocumentationsByIdentityName.Add(
                memberDocumentation.IdentityName,
                memberDocumentation);

            textOutput.WriteInformation($"Processed member '{memberDocumentation.IdentityName}'.");
        }
    }
}
