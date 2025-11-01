# Contribution Guidelines

Thank you for your interest in contributing to TiXL! This guide will help you understand our development workflow, coding standards, and contribution process.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Environment Setup](#development-environment-setup)
- [Project Structure](#project-structure)
- [Code Style and Standards](#code-style-and-standards)
- [Commit Message Conventions](#commit-message-conventions)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Development Workflow](#development-workflow)
- [Code Review Process](#code-review-process)
- [Community Guidelines](#community-guidelines)

## Getting Started

### What is TiXL?

TiXL (Tooll 3) is an open-source platform for creating real-time motion graphics. It combines:
- Real-time rendering with DirectX 12
- Graph-based procedural content generation
- Linear keyframe animation
- Audio-reactive visual creation
- Plugin-based operator system

### Ways to Contribute

- 🐛 **Bug Reports**: Help us identify and fix issues
- 💡 **Feature Requests**: Suggest new operators or improvements
- 📝 **Documentation**: Improve guides, tutorials, and examples
- 🎨 **Operators**: Create new graphics, audio, or utility operators
- 🔧 **Core Development**: Work on engine improvements
- 🖥️ **UI/UX**: Enhance the user interface and experience
- 🧪 **Testing**: Write tests and improve code quality

## Development Environment Setup

### Prerequisites

- **Operating System**: Windows 10/11 (primary development platform)
- **.NET SDK**: .NET 9.0.0 or later
- **IDE**: Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- **GPU**: DirectX 11.3 compatible (GTX 970 or later recommended)
- **Git**: Latest version with Git LFS support

### Initial Setup

1. **Fork the Repository**
   ```bash
   git clone https://github.com/tixl3d/tixl.git
   cd tixl
   git remote add upstream https://github.com/tixl3d/tixl.git
   ```

2. **Build the Solution**
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Run Tests**
   ```bash
   dotnet test
   ```

4. **Launch Editor**
   ```bash
   cd Editor
   dotnet run
   ```

## Project Structure

TiXL follows a modular architecture:

```
├── Core/                    # Fundamental engine components
├── Operators/              # Plugin-based operator system
├── Editor/                 # User interface & environment
└── Resources/              # Application resources
```

## Code Style and Standards

### C# Coding Conventions

#### Naming Conventions

```csharp
// Classes, methods, properties: PascalCase
public class RenderTarget
{
    public string Name { get; set; }
    public int Width { get; }
    
    public void Initialize()
    {
        // Method implementation
    }
}

// Fields: camelCase with underscore prefix for private fields
private readonly RenderTarget _renderTarget;
private string _textureName;

// Constants: UPPER_CASE
public const int MAX_TEXTURES = 16;
public const string DEFAULT_SHADER_NAME = "Default";
```

#### XML Documentation Comments

```csharp
/// <summary>
/// Applies a shader material to render geometry with specific properties.
/// </summary>
/// <param name="geometry">The geometry to render</param>
/// <param name="material">The material containing shader and parameters</param>
/// <param name="camera">The camera view matrix for rendering</param>
/// <returns>True if rendering succeeded, false otherwise</returns>
public bool ApplyMaterial(Geometry geometry, Material material, Camera camera)
{
    // Implementation
}
```

## Commit Message Conventions

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat**: New feature or operator
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, naming)
- **refactor**: Code refactoring
- **test**: Adding or updating tests
- **chore**: Build process, tooling, dependencies
- **perf**: Performance improvements

### Examples

```bash
feat(operators): add PBR material operator with metal-rough workflow

Implements a physically-based rendering material operator that supports:
- Base color, metallic, roughness inputs
- Normal mapping with tangent space support  
- Emission and AO integration
- Compatible with existing ShaderOperator framework

Closes #142
```

## Pull Request Process

### Before Submitting

1. **Ensure your fork is up to date**
   ```bash
   git checkout main
   git pull upstream main
   git push origin main
   ```

2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Run all tests**
   ```bash
   dotnet test --configuration Release
   ```

4. **Verify code style**
   ```bash
   dotnet format --verify-no-changes
   ```

### PR Template

When submitting a pull request, include:

```markdown
## Description
Brief description of changes and motivation

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing performed
- [ ] Performance impact assessed

## Checklist
- [ ] My code follows the code style of this project
- [ ] I have performed a self-review of my own code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
```

## Testing Requirements

### Testing Strategy

TiXL uses multiple testing levels:

1. **Unit Tests**: Individual component testing
2. **Integration Tests**: Module interaction testing
3. **End-to-End Tests**: Full application testing
4. **Performance Tests**: Benchmarking and profiling
5. **Operator Tests**: Specific operator validation

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter Category=Unit

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run performance tests
dotnet test --filter Category=Performance
```

## Development Workflow

### Branch Strategy

We use a GitFlow-inspired workflow:
- **main**: Production-ready code
- **develop**: Integration branch for features
- **feature/**: New features or operators
- **bugfix/**: Bug fixes
- **hotfix/**: Critical production fixes

### Creating Features

1. **Start from develop branch**
   ```bash
   git checkout develop
   git pull upstream develop
   ```

2. **Create feature branch**
   ```bash
   git checkout -b feature/add-pbr-material-operator
   ```

3. **Develop and commit**
   ```bash
   # Make changes
   git add .
   git commit -m "feat(operators): add PBR material operator"
   ```

4. **Push and create PR**
   ```bash
   git push origin feature/add-pbr-material-operator
   ```

## Code Review Process

### Review Expectations

**For Authors:**
- Keep PRs focused and atomic
- Write clear commit messages
- Include tests for new functionality
- Update documentation
- Be responsive to feedback

**For Reviewers:**
- Review code thoroughly and constructively
- Focus on architecture, design, and quality
- Test changes when possible
- Provide actionable feedback

### Review Checklist

**Architecture and Design**
- [ ] Code follows SOLID principles
- [ ] Appropriate use of interfaces and abstractions
- [ ] Proper separation of concerns
- [ ] Consistent with existing patterns

**Functionality**
- [ ] Code implements the intended feature
- [ ] Error handling is appropriate
- [ ] Edge cases are handled
- [ ] No breaking changes (or properly marked)

**Performance**
- [ ] No unnecessary allocations
- [ ] Efficient algorithms and data structures
- [ ] GPU resources managed properly

**Testing**
- [ ] Adequate test coverage
- [ ] Tests are meaningful and reliable
- [ ] Integration tests validate interactions

## Community Guidelines

### Communication

- **Discord**: Primary community chat (https://discord.gg/YmSyQdeH3S)
- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: General discussions and Q&A
- **YouTube**: Tutorial videos and showcases

### Code of Conduct

**Be Respectful:**
- Treat all community members with respect
- Acknowledge diverse backgrounds and perspectives
- Focus on constructive feedback and collaboration

**Be Inclusive:**
- Welcome newcomers and help them get started
- Use inclusive language in all communications
- Respect different skill levels and learning styles

**Be Professional:**
- Keep discussions on-topic and constructive
- Report security issues privately to maintainers
- Respect the time and effort of contributors

### Getting Help

**Before asking for help:**
1. Check existing documentation and wiki
2. Search through existing issues
3. Review similar operator examples
4. Test with minimal reproduction cases

**When asking for help:**
- Provide clear context and goals
- Include system information and versions
- Share relevant code or project files
- Describe what you've already tried

## Additional Resources

### Learning Resources

- [C# Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [DirectX 12 Programming Guide](https://docs.microsoft.com/en-us/windows/win32/direct3d12/direct3d-12-programming-guide)
- [ImGui Documentation](https://github.com/ocornut/imgui/wiki)

### TiXL-Specific Resources

- [Official Website](https://tixl.app)
- [Video Tutorials](https://www.youtube.com/watch?v=eH2E02U6P5Q)
- [Operator Examples Repository](https://github.com/tixl3d/Operators)
- [TiXL Resources](https://github.com/tixl3d/Resources)

---

## Conclusion

Thank you for contributing to TiXL! These guidelines help maintain code quality, ensure consistent development practices, and create a welcoming environment for all contributors.

Remember: Contributing to open source is a collaborative effort. Be patient, be helpful, and focus on making TiXL better for everyone in the community.

**Happy coding!** 🎨✨
