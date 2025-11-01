using Microsoft.CodeAnalysis.Text;

namespace TiXL.NamingConventionChecker
{
    /// <summary>
    /// Represents a naming convention violation found in the codebase
    /// </summary>
    public class Violation
    {
        public string RuleId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CurrentName { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public TextSpan Span { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
        public ViolationType ViolationType { get; set; }
    }

    /// <summary>
    /// Type of naming convention violation
    /// </summary>
    public enum ViolationType
    {
        ClassName,
        InterfaceName,
        MethodName,
        PropertyName,
        FieldName,
        EventName,
        EnumName,
        EnumMemberName,
        NamespaceName,
        ConstantsName
    }

    /// <summary>
    /// Severity levels for violations
    /// </summary>
    public enum ViolationSeverity
    {
        Error,
        Warning,
        Info
    }
}