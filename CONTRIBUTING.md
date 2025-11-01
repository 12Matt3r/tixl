# Contributing to TiXL

Thank you for your interest in contributing to TiXL (Tooll 3)! This guide will help you get started with contributing to our real-time motion graphics platform quickly and efficiently.

## 🚀 Quick Start

### Prerequisites
- **OS**: Windows 10/11 (primary development platform)
- **.NET SDK**: .NET 9.0.0 or later
- **IDE**: Visual Studio 2022, VS Code, or JetBrains Rider
- **GPU**: DirectX 11.3 compatible (GTX 970+ recommended)
- **Git**: Latest version with Git LFS

### Initial Setup

```bash
# 1. Fork and clone the repository
git clone https://github.com/tixl3d/tixl.git
cd tixl

# 2. Add upstream remote
git remote add upstream https://github.com/tixl3d/tixl.git

# 3. Build the solution
dotnet restore
dotnet build --configuration Release

# 4. Run tests
dotnet test

# 5. Launch editor (optional)
cd Editor
dotnet run
```

## 📋 Contribution Workflow

### 1. Choose Your Contribution Type

- 🐛 **Bug Reports**: [Issues](https://github.com/tixl3d/tixl/issues)
- 💡 **Feature Requests**: [Issues](https://github.com/tixl3d/tixl/issues)
- 📝 **Documentation**: Guides, tutorials, and examples
- 🎨 **Operators**: New graphics, audio, or utility operators
- 🔧 **Core Development**: Engine improvements and performance
- 🖥️ **UI/UX**: Interface enhancements and user experience
- 🧪 **Testing**: Test coverage and quality improvements

### 2. Development Workflow

```bash
# 1. Sync your fork
git checkout develop
git pull upstream develop
git push origin develop

# 2. Create a feature branch
git checkout -b feature/your-feature-name

# 3. Make your changes
# Follow coding standards (see below)
# Write/update tests
# Update documentation

# 4. Commit with proper format
git commit -m "feat(operators): add PBR material operator"

# 5. Push and create PR
git push origin feature/your-feature-name
```

### 3. Branch Strategy (GitFlow-Inspired)

- **`main`**: Production-ready code
- **`develop`**: Integration branch for features
- **`feature/`**: New features or operators (e.g., `feature/add-pbr-operator`)
- **`bugfix/`**: Bug fixes (e.g., `bugfix/fix-render-leak`)
- **`hotfix/`**: Critical production fixes (e.g., `hotfix/security-patch`)

## 💻 Code Standards

### C# Coding Conventions

**Naming Conventions:**
```csharp
// Classes, methods, properties: PascalCase
public class RenderTarget
{
    public string Name { get; set; }
    public void Initialize() { }
}

// Fields: camelCase with underscore prefix
private readonly RenderTarget _renderTarget;
private string _textureName;

// Constants: UPPER_CASE
public const int MAX_TEXTURES = 16;
```

**File Organization:**
```csharp
// File: Core/Rendering/RenderTarget.cs
using System;
using TiXL.Core.IO;

namespace TiXL.Core.Rendering
{
    /// <summary>
    /// Manages render target resources for graphics operations.
    /// </summary>
    public class RenderTarget : IDisposable
    {
        // Fields, properties, constructor
        // Methods (public, then protected/private)
        // Private helper methods
    }
}
```

**XML Documentation:**
```csharp
/// <summary>
/// Applies a shader material to render geometry.
/// </summary>
/// <param name="geometry">The geometry to render</param>
/// <param name="material">The material containing shader parameters</param>
/// <returns>True if rendering succeeded</returns>
public bool ApplyMaterial(Geometry geometry, Material material)
{
    // Implementation
}
```

### Performance Guidelines

- Use `IDisposable` for GPU resources
- Implement object pools for frequently allocated objects
- Avoid blocking operations; use async/await
- Leverage compute shaders for real-time processing

### Architectural Boundaries

**Module Dependencies (CRITICAL):**
- **Core**: Engine foundations, data types, rendering infrastructure
- **Operators**: Plugin-based operator system  
- **Graphics**: DirectX 12 pipeline, shader management
- **GUI**: User interface components, window management
- **Editor**: Application orchestration, project management

**Key Rules:**
- Core cannot reference Operators, GUI, or Editor
- Operators cannot reference GUI or Editor
- Graphics cannot reference Operators or GUI
- Editor is the integration point (can reference all modules)

**Validate Architecture:**
```bash
# Run architectural validation
./scripts/validate-architecture.sh validate

# Set up Git hooks for automatic validation
./scripts/validate-architecture.sh setup-hooks
```

**Detailed Standards:** See [CONTRIBUTION_GUIDELINES.md](docs/CONTRIBUTION_GUIDELINES.md)

## 🧪 Testing Requirements

### Test Coverage

All contributions must include appropriate tests:

- **Unit Tests**: Individual component testing
- **Integration Tests**: Module interaction testing  
- **Performance Tests**: Benchmarking and profiling
- **Operator Tests**: Specific operator validation

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific category
dotnet test --filter Category=Unit

# Run performance tests
dotnet test --filter Category=Performance
```

### Test Quality

```csharp
[Test]
public void Constructor_ValidSize_CreatesRenderTarget()
{
    // Arrange
    var width = 1920;
    var height = 1080;
    
    // Act
    var renderTarget = new RenderTarget(width, height);
    
    // Assert
    Assert.That(renderTarget.Width, Is.EqualTo(width));
    Assert.That(renderTarget.Height, Is.EqualTo(height));
}
```

## 📝 Commit Message Conventions

**Format:**
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature or operator
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes
- `refactor`: Code refactoring
- `test`: Adding/updating tests
- `chore`: Build process, tooling, dependencies
- `perf`: Performance improvements

**Examples:**
```bash
feat(operators): add PBR material operator with metal-rough workflow

Implements physically-based rendering material operator supporting:
- Base color, metallic, roughness inputs
- Normal mapping with tangent space support
- Compatible with existing ShaderOperator framework

Closes #142

fix(core): resolve memory leak in render target disposal

Fixes #201
```

**Best Practices:**
- Use present tense ("Add feature" not "Added feature")
- Keep subject line under 50 characters
- Use body to explain what and why vs. how
- Reference issue numbers in footer

## 🔄 Pull Request Process

### Before Submitting

1. **Sync your fork**
   ```bash
   git checkout develop
   git pull upstream develop
   git push origin develop
   ```

2. **Run quality checks**
   ```bash
   dotnet test --configuration Release
   dotnet build --configuration Release
   dotnet format --verify-no-changes
   ./scripts/validate-architecture.sh validate
   ```

3. **Update documentation** if needed

### PR Template

```markdown
## Description
Brief description of changes and motivation

## Type of Change
- [ ] Bug fix (non-breaking change)
- [ ] New feature (non-breaking change)
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing performed
- [ ] Performance impact assessed

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Comments added for complex code
- [ ] Documentation updated
- [ ] No new warnings generated
- [ ] Tests prove fix/feature works

## Screenshots/Videos
If applicable, demonstrate the changes

## Related Issues
Closes #(issue number)
```

### Review Process

1. **Automated Checks**: All CI/CD checks must pass
2. **Code Review**: At least one core team member approval required
3. **Testing**: Changes tested on multiple configurations
4. **Documentation**: Updated for code changes

**Review Focus:**
- Architecture compliance
- Code quality and maintainability
- Performance implications
- Test coverage adequacy
- Documentation completeness

## 🐛 Issue Reporting

### Bug Reports

Use the bug report template with:
- Clear description of the bug
- Reproduction steps
- Expected vs actual behavior
- Environment information (OS, .NET version, GPU, etc.)
- Screenshots/videos if applicable
- Log files and error messages

### Feature Requests

Include:
- Clear description of the problem/use case
- Proposed solution
- Alternative solutions considered
- Implementation ideas (if any)
- Prioritization level

## 📚 Additional Resources

### Documentation
- [Detailed Contribution Guidelines](docs/CONTRIBUTION_GUIDELINES.md)
- [Architectural Governance](docs/ARCHITECTURAL_GOVERNANCE.md)
- [Security Guidelines](docs/SECURITY_GUIDELINES.md)
- [Code Coverage Documentation](docs/CODE_COVERAGE_README.md)

### Development Tools
- **Visual Studio 2022**: Primary IDE
- **RenderDoc**: Graphics debugging
- **dotMemory**: Memory profiling
- **BenchmarkDotNet**: Performance benchmarking

### Community
- **Discord**: [Primary Community Chat](https://discord.gg/YmSyQdeH3S)
- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: General discussions and Q&A
- **YouTube**: Tutorial videos and showcases

## 🎯 Getting Help

### Before Asking
1. Check existing documentation and wiki
2. Search through existing issues
3. Review similar operator examples
4. Test with minimal reproduction cases

### When Asking for Help
- Provide clear context and goals
- Include system information and versions
- Share relevant code or project files
- Describe what you've already tried

### Architecture Questions
Run validation tools for detailed output:
```bash
./scripts/validate-architecture.sh validate -v
```

## 🤝 Community Guidelines and Governance

### Essential Governance Documents

We have established comprehensive governance to ensure a safe, welcoming, and productive community:

- **[Code of Conduct](docs/CODE_OF_CONDUCT.md)** - Professional community standards and behavior guidelines
- **[Security Disclosure Process](docs/SECURITY_DISCLOSURE_PROCESS.md)** - Clear process for responsible security reporting
- **[Governance Structure](docs/GOVERNANCE_STRUCTURE.md)** - Project governance and decision-making framework
- **[Community Guidelines](docs/COMMUNITY_GUIDELINES.md)** - Comprehensive engagement standards and expectations
- **[Enforcement Procedures](docs/ENFORCEMENT_PROCEDURES.md)** - Clear procedures for addressing violations
- **[Legal Protection](docs/LEGAL_PROTECTION.md)** - Liability and legal considerations documentation
- **[Community Recognition](docs/COMMUNITY_RECOGNITION.md)** - Program for acknowledging contributor achievements

### Code of Conduct Highlights
- **Be Respectful**: Treat all community members with respect
- **Be Inclusive**: Welcome newcomers and different perspectives  
- **Be Professional**: Keep discussions constructive and on-topic
- **Be Safe**: Report security issues through proper channels

### Community Engagement
- Follow [Community Guidelines](docs/COMMUNITY_GUIDELINES.md) for communication standards
- Participate in [Community Recognition](docs/COMMUNITY_RECOGNITION.md) programs
- Respect [Enforcement Procedures](docs/ENFORCEMENT_PROCEDURES.md) for dispute resolution
- Contribute to positive community culture

### Security and Safety
**Report security issues privately to maintainers via email.**
Include detailed reproduction steps and allow time for fixes before public disclosure. See our [Security Disclosure Process](docs/SECURITY_DISCLOSURE_PROCESS.md) for complete guidelines.

---

## ✅ Quick Checklist

Before submitting your contribution:

- [ ] Fork is up to date with upstream
- [ ] Feature branch created from `develop`
- [ ] Code follows naming conventions
- [ ] Architecture boundaries respected
- [ ] Tests written and passing
- [ ] Commit messages follow conventional format
- [ ] Documentation updated (if applicable)
- [ ] Performance considered (no regressions)
- [ ] Security best practices followed
- [ ] PR template completed

## 🎉 Thank You!

Thank you for contributing to TiXL! Your involvement helps make real-time motion graphics more accessible and powerful for everyone. Whether you're fixing bugs, adding features, or improving documentation, every contribution matters.

**Happy coding!** 🎨✨

---

*Questions about contributing? Reach out through [Discord](https://discord.gg/YmSyQdeH3S) or [GitHub Discussions](https://github.com/tixl3d/tixl/discussions).*