using System;


namespace R5T.O0027
{
    public static class Instances
    {
        public static T0212.F000.IDocumentationFileOperator DocumentationFileOperator => T0212.F000.DocumentationFileOperator.Instance;
        public static T0211.F000.IInheritdocElementOperator InheritdocElementOperator => T0211.F000.InheritdocElementOperator.Instance;
        public static T0212.F000.IMemberDocumentationOperator MemberDocumentationOperator => T0212.F000.MemberDocumentationOperator.Instance;
        public static O0006.IProjectFileOperations ProjectFileOperations => O0006.ProjectFileOperations.Instance;
        public static O0026.O003.IProjectFilePathOperations ProjectFilePathOperations => O0026.O003.ProjectFilePathOperations.Instance;
        public static ISpecialIdentityNames SpecialIdentityNames => O0027.SpecialIdentityNames.Instance;
        public static T0211.Z000.IXmlDocumentationCommentElementNames XmlDocumentationCommentElementNames => T0211.Z000.XmlDocumentationCommentElementNames.Instance;
        public static L0030.IXElementOperator XElementOperator => L0030.XElementOperator.Instance;
    }
}