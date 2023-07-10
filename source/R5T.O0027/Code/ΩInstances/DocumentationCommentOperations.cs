using System;


namespace R5T.O0027
{
    public class DocumentationCommentOperations : IDocumentationCommentOperations
    {
        #region Infrastructure

        public static IDocumentationCommentOperations Instance { get; } = new DocumentationCommentOperations();


        private DocumentationCommentOperations()
        {
        }

        #endregion
    }
}
