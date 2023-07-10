using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using R5T.T0131;
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
        /// Chooses <see cref="Get_DocumentationComments_Recursive(IProjectFilePath, IList{MissingDocumentationReference})"/> as the default.
        /// <para><inheritdoc cref="Get_DocumentationComments_Recursive(IProjectFilePath, IList{MissingDocumentationReference})" path="/summary"/></para>
        /// </summary>
        public Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments(IProjectFilePath projectFilePath,
            IList<MissingDocumentationReference> missingDocumentationReferences)
        {
            return this.Get_DocumentationComments_Recursive(
                projectFilePath,
                missingDocumentationReferences);
        }

        /// <summary>
        /// For a project, get all recursive project references, and expand all inheritdoc elements.
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive(IProjectFilePath projectFilePath,
            IList<MissingDocumentationReference> missingDocumentationReferences)
        {
            var rawMemberDocumentationByIdentityName = await this.Get_DocumentationComments_Recursive_Raw(projectFilePath);

            var processedMemberDocumentationsByIdentityName = this.Replace_InheritDocElements(
                rawMemberDocumentationByIdentityName,
                out var localMissingDocumentationReferences);

            missingDocumentationReferences.AddRange(localMissingDocumentationReferences);

            return processedMemberDocumentationsByIdentityName;
        }

        /// <summary>
        /// For a project, get all recursive project references, the get all raw member documentation comments by identity name.
        /// This data is "raw" in the sense that no inheritdoc element substitution is performed.
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_Recursive_Raw(IProjectFilePath projectFilePath)
        {
            var recursiveProjectFilePaths = await Instances.ProjectFileOperations.Get_RecursiveProjectReferences(
                projectFilePath);

            var allDocumentationFilePaths = Instances.ProjectFilePathOperations.Get_DocumentationXmlFilePaths(
                recursiveProjectFilePaths.Append(projectFilePath));

            var memberDocumentationByIdentityName = Instances.MemberDocumentationOperator.Get_InitialMemberDocumentationByIdentityName();

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
        /// For a project expand all inheritdoc elements.
        /// <para>Note: if the project includes inheritdoc references to any other projects, the identity names of those inheritdoc references will be in the unrecognized names output.</para>
        /// To include inheritdoc referenes from all recursive project references, see <see cref="Get_DocumentationComments_Recursive(IProjectFilePath, IList{MissingDocumentationReference})"/>.
        /// </summary>
        public async Task<IDictionary<IIdentityName, MemberDocumentation>> Get_DocumentationComments_NonRecursive(IProjectFilePath projectFilePath,
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

            var processedMemberDocumentationsByIdentityName = this.Replace_InheritDocElements(
                rawMemberDocumentationByIdentityname,
                out var localMissingDocumentationReferences);

            missingDocumentationReferences.AddRange(localMissingDocumentationReferences);

            return processedMemberDocumentationsByIdentityName;
        }

        public IDictionary<IIdentityName, MemberDocumentation> Replace_InheritDocElements(
            IDictionary<IIdentityName, MemberDocumentation> memberDocumentationsByIdentityName,
            out MissingDocumentationReference[] missingDocumentationReferences)
        {
            var processedDocumentationsByIdentityName = new Dictionary<IIdentityName, MemberDocumentation>();

            var tempMissingDocumentationReferences = new List<MissingDocumentationReference>();

            foreach (var pair in memberDocumentationsByIdentityName)
            {
                Internal.Process_MemberDocumentation(
                    pair.Value,
                    memberDocumentationsByIdentityName,
                    processedDocumentationsByIdentityName,
                    tempMissingDocumentationReferences);
            }

            missingDocumentationReferences = tempMissingDocumentationReferences.ToArray();

            return processedDocumentationsByIdentityName;
        }
    }
}
