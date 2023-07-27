using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using R5T.T0131;
using R5T.T0159;
using R5T.T0162;
using R5T.T0172;
using R5T.T0212.F000;


namespace R5T.O0027
{
    [ValuesMarker]
    public partial interface IDocumentationCommentOperations : IValuesMarker
    {
        private static Internal.IDocumentationCommentOperations Internal => O0027.Internal.DocumentationCommentOperations.Instance;


        /// <summary>
        /// Chooses <see cref="Get_DocumentationComments_Recursive(IProjectFilePath, ITextOutput, IList{MissingDocumentationReference})"/> as the default.
        /// <para><inheritdoc cref="Get_DocumentationComments_Recursive(IProjectFilePath, ITextOutput, IList{MissingDocumentationReference})" path="/summary"/></para>
        /// </summary>
        public Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments(
            IProjectFilePath projectFilePath,
            ITextOutput textOutput,
            IList<MissingDocumentationReference> missingDocumentationReferences)
        {
            return this.Get_DocumentationComments_Recursive(
                projectFilePath,
                textOutput,
                missingDocumentationReferences);
        }

        /// <summary>
        /// For a project, get all recursive project references, and expand all inheritdoc elements.
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive(
            IProjectFilePath projectFilePath,
            ITextOutput textOutput,
            IList<MissingDocumentationReference> missingDocumentationReferences)
        {
            var rawMemberDocumentationByIdentityName = await this.Get_DocumentationComments_Recursive_Raw(projectFilePath);

            var processedMemberDocumentationsByIdentityName = this.Expand_InheritdocElements(
                rawMemberDocumentationByIdentityName,
                textOutput,
                out var localMissingDocumentationReferences);

            missingDocumentationReferences.AddRange(localMissingDocumentationReferences);

            return processedMemberDocumentationsByIdentityName;
        }

        /// <summary>
        /// For a project, get all recursive project references, the get all raw member documentation comments by identity name.
        /// This data is "raw" in the sense that no inheritdoc element substitution is performed.
        /// <para>
        /// Note: all output <see cref="MemberDocumentation"/>s will share the same <see cref="ProjectDocumentationTarget"/>, that of the input project file path.
        /// </para>
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive_Raw(IProjectFilePath projectFilePath)
        {
            var recursiveProjectFilePaths = await Instances.ProjectFileOperations.Get_RecursiveProjectReferences(
                projectFilePath);

            var allDocumentationFilePaths = Instances.ProjectFilePathOperations.Get_DocumentationXmlFilePaths(
                recursiveProjectFilePaths.Append(projectFilePath));

            var memberDocumentationByIdentityName = Instances.MemberDocumentationOperator.Get_InitialMemberDocumentationsByIdentityName();

            var projectDocumentationTarget = new ProjectDocumentationTarget
            {
                ProjectFilePath = projectFilePath,
            };

            foreach (var documentationFilePath in allDocumentationFilePaths)
            {
                await Instances.DocumentationFileOperator.Add_MemberDocumentationsByIdentityName(
                    documentationFilePath,
                    memberDocumentationByIdentityName,
                    projectDocumentationTarget);
            }

            return memberDocumentationByIdentityName;
        }


        /// <summary>
        /// For many projects, get all recursive project references, the get all raw member documentation comments by identity name.
        /// This data is "raw" in the sense that no inheritdoc element substitution is performed.
        /// <para>
        /// Note: output <see cref="MemberDocumentation"/>s will use the <see cref="ProjectDocumentationTarget"/> of their associated input project file path.
        /// </para>
        /// </summary>
        public Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive_Raw(
            IList<IProjectFilePath> missingDocumentationFileProjectFilePaths,
            ITextOutput textOutput,
            IEnumerable<IProjectFilePath> projectFilePaths)
        {
            return this.Get_DocumentationComments_Recursive_Raw(
                missingDocumentationFileProjectFilePaths,
                textOutput,
                Instances.ProjectFilePathOperations.Get_DocumentationXmlFilePathsByProjectFilePath,
                projectFilePaths);
        }

        /// <summary>
        /// For many projects, get all recursive project references, the get all raw member documentation comments by identity name.
        /// This data is "raw" in the sense that no inheritdoc element substitution is performed.
        /// <para>
        /// Note: output <see cref="MemberDocumentation"/>s will use the <see cref="ProjectDocumentationTarget"/> of their associated input project file path.
        /// </para>
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive_Raw(
            IList<IProjectFilePath> missingDocumentationFileProjectFilePaths,
            ITextOutput textOutput,
            Func<IEnumerable<IProjectFilePath>, IDictionary<IProjectFilePath, IDocumentationXmlFilePath>> getDocumentationXmlFilePathsByProjectFilePath,
            IEnumerable<IProjectFilePath> projectFilePaths)
        {
            var recursiveProjectFilePaths = await Instances.ProjectFileOperations.Get_RecursiveProjectReferences_Inclusive(
                textOutput,
                projectFilePaths);

            var documentationFilePathsByProjectFilePath = getDocumentationXmlFilePathsByProjectFilePath(recursiveProjectFilePaths);

            var memberDocumentationByIdentityName = Instances.MemberDocumentationOperator.Get_InitialMemberDocumentationsByIdentityName();

            foreach (var pair in documentationFilePathsByProjectFilePath)
            {
                if (!Instances.FileSystemOperator.FileExists(pair.Value.Value))
                {
                    missingDocumentationFileProjectFilePaths.Add(pair.Key);

                    continue;
                }

                var projectDocumentationTarget = new ProjectDocumentationTarget
                {
                    ProjectFilePath = pair.Key,
                };

                //// For debug.
                ////if(@"C:\Code\DEV\Git\GitHub\SafetyCone\R5T.S0082\source\R5T.S0082\bin\publish\R5T.S0082.xml" == pair.Value.Value)
                //if(@"C:\Code\DEV\Git\GitHub\SafetyCone\R5T.S0082\source\R5T.S0082\R5T.S0082.csproj" == pair.Key.Value)
                //{
                //    Console.WriteLine("here!");
                //}

                await Instances.DocumentationFileOperator.Add_MemberDocumentationsByIdentityName(
                    pair.Value,
                    memberDocumentationByIdentityName,
                    projectDocumentationTarget);
            }

            return memberDocumentationByIdentityName;
        }

        /// <inheritdoc cref="Get_DocumentationComments_Recursive_Raw(IList{IProjectFilePath}, ITextOutput, IEnumerable{IProjectFilePath})"/>
        public Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive_Raw(
            IList<IProjectFilePath> missingDocumentationFileProjectFilePaths,
            ITextOutput textOutput,
            params IProjectFilePath[] projectFilePaths)
        {
            return this.Get_DocumentationComments_Recursive_Raw(
                missingDocumentationFileProjectFilePaths,
                textOutput,
                projectFilePaths.AsEnumerable());
        }

        /// <inheritdoc cref="Get_DocumentationComments_Recursive_Raw(IList{IProjectFilePath}, ITextOutput, IEnumerable{IProjectFilePath})"/>
        public Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive_Raw(
            IList<IProjectFilePath> missingDocumentationFileProjectFilePaths,
            ITextOutput textOutput,
            Func<IEnumerable<IProjectFilePath>, IDictionary<IProjectFilePath, IDocumentationXmlFilePath>> getDocumentationXmlFilePathsByProjectFilePath,
            params IProjectFilePath[] projectFilePaths)
        {
            return this.Get_DocumentationComments_Recursive_Raw(
                missingDocumentationFileProjectFilePaths,
                textOutput,
                getDocumentationXmlFilePathsByProjectFilePath,
                projectFilePaths.AsEnumerable());
        }

        /// <inheritdoc cref="Get_DocumentationComments_Recursive(IProjectFilePath, ITextOutput, IList{MissingDocumentationReference})"/>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive(
            IList<MissingDocumentationReference> missingDocumentationReferences,
            IList<IProjectFilePath> missingDocumentationFileProjectFilePaths,
            ITextOutput textOutput,
            IEnumerable<IProjectFilePath> projectFilePaths)
        {
            var rawMemberDocumentationByIdentityName = await this.Get_DocumentationComments_Recursive_Raw(
                missingDocumentationFileProjectFilePaths,
                textOutput,
                projectFilePaths);

            var processedMemberDocumentationsByIdentityName = this.Expand_InheritdocElements(
                rawMemberDocumentationByIdentityName,
                textOutput,
                out var localMissingDocumentationReferences);

            missingDocumentationReferences.AddRange(localMissingDocumentationReferences);

            return processedMemberDocumentationsByIdentityName;
        }

        /// <inheritdoc cref="Get_DocumentationComments_Recursive(IProjectFilePath, ITextOutput, IList{MissingDocumentationReference})"/>
        public Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive(
            IList<MissingDocumentationReference> missingDocumentationReferences,
            IList<IProjectFilePath> missingDocumentationFileProjectFilePaths,
            ITextOutput textOutput,
            params IProjectFilePath[] projectFilePaths)
        {
            return this.Get_DocumentationComments_Recursive(
                missingDocumentationReferences,
                missingDocumentationFileProjectFilePaths,
                textOutput,
                projectFilePaths.AsEnumerable());
        }

        /// <summary>
        /// For a project expand all inheritdoc elements.
        /// <para>Note: if the project includes inheritdoc references to any other projects, the identity names of those inheritdoc references will be in the unrecognized names output.</para>
        /// To include inheritdoc referenes from all recursive project references, see <see cref="Get_DocumentationComments_Recursive(IProjectFilePath, ITextOutput, IList{MissingDocumentationReference})"/>.
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_NonRecursive(
            IProjectFilePath projectFilePath,
            ITextOutput textOutput,
            IList<MissingDocumentationReference> missingDocumentationReferences)
        {
            var documentationFilePath = Instances.ProjectFilePathOperations.Get_DocumentationXmlFilePath(projectFilePath);

            var projectDocumentationTarget = new ProjectDocumentationTarget
            {
                ProjectFilePath = projectFilePath,
            };

            var rawMemberDocumentationByIdentityname = await Instances.DocumentationFileOperator.Get_MemberDocumentationsByIdentityName(
                documentationFilePath,
                projectDocumentationTarget);

            var processedMemberDocumentationsByIdentityName = this.Expand_InheritdocElements(
                rawMemberDocumentationByIdentityname,
                textOutput,
                out var localMissingDocumentationReferences);

            missingDocumentationReferences.AddRange(localMissingDocumentationReferences);

            return processedMemberDocumentationsByIdentityName;
        }

        public IDictionary<IIdentityName, MemberDocumentation> Expand_InheritdocElements(
            IDictionary<IIdentityName, MemberDocumentation> memberDocumentationsByIdentityName,
            ITextOutput textOutput,
            out MissingDocumentationReference[] missingDocumentationReferences)
        {
            var processedDocumentationsByIdentityName = new Dictionary<IIdentityName, MemberDocumentation>();

            var tempMissingDocumentationReferences = new List<MissingDocumentationReference>();

            foreach (var pair in memberDocumentationsByIdentityName)
            {
                //Internal.Process_MemberDocumentation(
                //Internal.Expand_InheritdocElements(
                Internal.Expand_InheritdocElements2(
                    pair.Value,
                    memberDocumentationsByIdentityName,
                    processedDocumentationsByIdentityName,
                    tempMissingDocumentationReferences,
                    textOutput);
            }

            missingDocumentationReferences = tempMissingDocumentationReferences.ToArray();

            return processedDocumentationsByIdentityName;
        }

        public MemberDocumentation Expand_InheritdocElements(
            MemberDocumentation memberDocumentation,
            ITextOutput textOutput,
            out MissingDocumentationReference[] missingDocumentationReferences)
        {
            var memberDocumentationsByIdentityName = Instances.MemberDocumentationOperator.Get_MemberDocumentationsByIdentityName(
                memberDocumentation);

            var processed = this.Expand_InheritdocElements(
                memberDocumentationsByIdentityName,
                textOutput,
                out missingDocumentationReferences);

            var output = processed.First().Value;
            return output;
        }

        public MemberDocumentation[] Expand_InheritdocElements(
            ITextOutput textOutput,
            out MissingDocumentationReference[] missingDocumentationReferences,
            IEnumerable<MemberDocumentation> memberDocumentations)
        {
            var memberDocumentationsByIdentityName = Instances.MemberDocumentationOperator.Get_MemberDocumentationsByIdentityName(
                memberDocumentations);

            var processed = this.Expand_InheritdocElements(
                memberDocumentationsByIdentityName,
                textOutput,
                out missingDocumentationReferences);

            var output = processed.Values.Now();
            return output;
        }

        public MemberDocumentation[] Expand_InheritdocElements(
            ITextOutput textOutput,
            out MissingDocumentationReference[] missingDocumentationReferences,
            params MemberDocumentation[] memberDocumentations)
        {
            return this.Expand_InheritdocElements(
                textOutput,
                out missingDocumentationReferences,
                memberDocumentations.AsEnumerable());
        }
    }
}
