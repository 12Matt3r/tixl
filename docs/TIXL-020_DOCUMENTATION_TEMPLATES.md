# TiXL Documentation Templates

## Template Library for TiXL Code Documentation

This document provides standardized templates for documenting TiXL APIs across Core, Operators, and Editor modules.

---

## 1. Class/Interface Documentation Template

```csharp
/// <summary>
/// [Brief description of the class/interface purpose and responsibility]
/// </summary>
/// <remarks>
/// [Detailed description including:]<br/>
/// - Usage context and scenarios<br/>
/// - Threading model (if relevant)<br/>
/// - Performance characteristics<br/>
/// - Integration patterns<br/>
/// - Related classes/interfaces<br/>
/// </remarks>
/// <example>
/// [C# code example showing typical usage]
/// <code>
/// // Example showing constructor usage and primary method calls
/// var instance = new TiXLClass();
/// instance.DoSomething();
/// </code>
/// </example>
/// <see cref="RelatedClass"/> for related functionality.
/// <version added="1.0">Initial implementation</version>
public class TiXLClass
{
    // Class implementation
}
```

---

## 2. Method Documentation Template

```csharp
/// <summary>
/// [Brief description of what the method does]
/// </summary>
/// <param name="parameterName">[Parameter description including type, constraints, and meaning]</param>
/// <param name="options">[Optional parameters with default values]</param>
/// <returns>[Description of return value including type and meaning]</returns>
/// <exception cref="ArgumentNullException">[When parameter is null]</exception>
/// <exception cref="InvalidOperationException">[When method cannot be called in current state]</exception>
/// <remarks>
/// [Additional method details including:]<br/>
/// - Performance characteristics<br/>
/// - Thread safety<br/>
/// - Usage patterns<br/>
/// - Side effects<br/>
/// - Preconditions/postconditions<br/>
/// </remarks>
/// <example>
/// [C# code example]
/// <code>
/// // Example with parameters and return value handling
/// var result = instance.MethodName(param1, options);
/// if (result != null)
/// {
///     // Process result
/// }
/// </code>
/// </example>
/// <see cref="RelatedMethod"/> for related functionality.
public ReturnType MethodName(ParameterType parameterName, MethodOptions options = null)
{
    // Method implementation
}
```

---

## 3. Property Documentation Template

```csharp
/// <summary>
/// [Brief description of the property purpose and what it represents]
/// </summary>
/// <value>[Description of the property value, type, and meaning]</value>
/// <exception cref="InvalidOperationException">[When property cannot be read in current state]</exception>
/// <exception cref="ArgumentException">[When setting invalid value]</exception>
/// <remarks>
/// [Additional property details including:]<br/>
/// - Default value<br/>
/// - Validation rules<br/>
/// - Side effects of setting<br/>
/// - Thread safety considerations<br/>
/// </remarks>
/// <example>
/// [Property usage example]
/// <code>
/// // Getting property value
/// var value = instance.PropertyName;
/// 
/// // Setting property value
/// instance.PropertyName = newValue;
/// </code>
/// </example>
public PropertyType PropertyName { get; set; }
```

---

## 4. Operator-Specific Documentation Template

```csharp
/// <summary>
/// [Operator description focusing on its visual/transformation purpose]
/// </summary>
/// <param name="InputName">[Description of input parameter and its data type]</param>
/// <param name="ControlName">[Description of control parameter and its effect]</param>
/// <returns>[Output description and data type]</returns>
/// <remarks>
/// [Operator-specific details including:]<br/>
/// - Visual effect description<br/>
/// - Performance impact<br/>
/// - Context variable dependencies<br/>
/// - Special usage patterns<br/>
/// - Input validation rules<br/>
/// - Output format specifications<br/>
/// </remarks>
/// <example>
/// [Operator usage in node graph context]
/// <code>
/// // Example showing operator in node graph
/// var operator = new TransformOperator();
/// operator.ConnectInput("Position", positionNode.Output);
/// operator.SetParameter("Scale", new Vector2(2.0f, 2.0f));
/// var result = operator.GetOutput();
/// </code>
/// </example>
/// <category>Lib.Category</category> for operator categorization.
/// <see cref="SpecialVariableName"/> for context variable usage.
/// <see cref="RelatedOperator"/> for similar operators.
/// <version added="1.0">Initial operator implementation</version>
public class OperatorName
{
    // Operator implementation
}
```

---

## 5. Event Documentation Template

```csharp
/// <summary>
/// [Brief description of when the event is raised and what it represents]
/// </summary>
/// <param name="sender">[Description of the event sender]</param>
/// <param name="e">[Description of event arguments and their properties]</param>
/// <remarks>
/// [Event details including:]<br/>
/// - When the event is triggered<br/>
/// - Thread safety considerations<br/>
/// - Performance implications<br/>
/// - Common use cases for subscribers<br/>
/// </remarks>
/// <example>
/// [Event subscription example]
/// <code>
/// instance.EventName += (sender, e) =>
/// {
///     // Handle event
///     var data = e.EventData;
/// };
/// </code>
/// </example>
public event EventHandler<EventArgsType> EventName;
```

---

## 6. Exception Documentation Template

```csharp
/// <summary>
/// [Description of when this exception is thrown and why]
/// </summary>
/// <param name="message">[Exception message]</param>
/// <param name="innerException">[Optional inner exception]</param>
/// <remarks>
/// [Exception details including:]<br/>
/// - When this exception occurs<br/>
/// - How to prevent it<br/>
/// - Recovery strategies<br/>
/// - Related exceptions<br/>
/// </remarks>
/// <example>
/// [Exception handling example]
/// <code>
/// try
/// {
///     instance.RiskyOperation();
/// }
/// catch (ExceptionType ex)
/// {
///     // Handle the specific exception
///     Logger.LogError(ex, "Operation failed");
/// }
/// </code>
/// </example>
public class ExceptionType : Exception
{
    public ExceptionType(string message, Exception innerException = null) 
        : base(message, innerException)
    {
    }
}
```

---

## 7. Field Documentation Template

```csharp
/// <summary>
/// [Brief description of the field purpose and what it represents]
/// </summary>
/// <value>[Description of the field value and its meaning]</value>
/// <remarks>
/// [Field details including:]<br/>
/// - Default value<br/>
/// - Thread safety<br/>
/// - Serialization behavior<br/>
/// - Usage considerations<br/>
/// </remarks>
private FieldType _fieldName;
```

---

## 8. Namespace Documentation Template

```csharp
/// <summary>
/// [Brief description of the namespace purpose and scope]
/// </summary>
/// <remarks>
/// [Namespace details including:]<br/>
/// - Module integration<br/>
/// - Key types and relationships<br/>
/// - Usage patterns<br/>
/// - Performance characteristics<br/>
/// </remarks>
namespace TiXL.Module
{
    // Namespace contents
}
```

---

## Module-Specific Templates

### Core Module Extensions

```csharp
/// <summary>
/// [Core-specific description including threading model]
/// </summary>
/// <threading model="[Single-threaded/Multi-threaded/Reentrant]">
/// [Threading behavior description]
/// </threading model>
/// <performance considerations="[Performance impact description]">
/// <performance considerations>
public class CoreType
{
    // Core implementation
}
```

### Operators Module Extensions

```csharp
/// <summary>
/// [Operator-specific visual/functional description]
/// </summary>
/// <category>Lib.CategoryName</category> for operator grouping.
/// <context variables="[Special variable dependencies]">
/// <context variables>
/// <visual output="[Description of visual result]">
/// <visual output>
/// <real-time considerations="[Performance and real-time behavior]">
/// <real-time considerations>
public class OperatorType
{
    // Operator implementation
}
```

### Editor Module Extensions

```csharp
/// <summary>
/// [Editor-specific UI/UX description]
/// </summary>
/// <ui behavior="[User interaction patterns]">
/// <ui behavior>
/// <user workflow="[Common usage scenarios]">
/// <user workflow>
/// <integration points="[Core/Operator integration]">
/// <integration points>
public class EditorType
{
    // Editor implementation
}
```

---

## Quality Guidelines for Templates

### Documentation Completeness
- Use all relevant template sections
- Provide concrete examples
- Include cross-references to related APIs
- Specify version information for new features

### Code Example Standards
- Examples must be compilable C# code
- Include necessary using statements
- Show both success and error handling cases
- Demonstrate common usage patterns

### Parameter Documentation Standards
- Always document parameter types
- Include validation rules and constraints
- Specify default values for optional parameters
- Note nullability and null handling

### Cross-Reference Guidelines
- Link to related classes and methods
- Reference context variables for operators
- Include category information for operators
- Specify version information for APIs

---

## Template Usage Guidelines

### 1. Consistency
- Use the same template structure across similar APIs
- Maintain consistent terminology throughout documentation
- Follow established naming conventions for examples

### 2. Completeness
- Fill in all relevant sections of the template
- Don't leave placeholder text in final documentation
- Include examples that cover common use cases

### 3. Accuracy
- Ensure code examples compile and run correctly
- Verify parameter descriptions match actual implementation
- Keep documentation synchronized with code changes

### 4. Maintainability
- Use consistent formatting and structure
- Include version information for tracking changes
- Cross-reference related documentation sections

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-02  
**Usage**: Reference these templates when documenting any public TiXL API