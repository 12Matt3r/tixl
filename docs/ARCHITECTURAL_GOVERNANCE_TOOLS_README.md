# TiXL Architectural Governance Tools

This directory contains tools and scripts for enforcing architectural boundaries in the TiXL codebase.

## Overview

The architectural governance system ensures that TiXL maintains its clean, modular architecture by:

- Preventing forbidden dependencies between modules
- Validating architectural patterns through static analysis
- Providing automated enforcement through build processes
- Offering tools for developers to check compliance

## Tools and Scripts

### 1. Architectural Validator (`Tools/ArchitecturalValidator/`)

A comprehensive tool that validates architectural boundaries by:

- Checking project file references
- Analyzing source code using patterns
- Validating namespace usage
- Generating compliance reports

**Usage:**
```bash
# Build the validator
dotnet build Tools/ArchitecturalValidator/TiXL.ArchitecturalValidator.csproj

# Run validation
dotnet run --project Tools/ArchitecturalValidator/TiXL.ArchitecturalValidator.csproj -- /path/to/TiXL.sln
```

### 2. Pre-commit Hook (`.githooks/pre-commit`)

Git hook that validates architectural constraints before each commit:

**Features:**
- Runs architectural validation automatically
- Checks for forbidden dependency patterns
- Validates code quality indicators
- Prevents commits with violations

**Installation:**
```bash
# The hook is already installed if you're reading this
# To reinstall manually:
chmod +x .githooks/pre-commit
cp .githooks/pre-commit .git/hooks/pre-commit
```

### 3. Validation Script (`scripts/validate-architecture.sh`)

Comprehensive validation script with multiple commands:

**Usage:**
```bash
# Make executable
chmod +x scripts/validate-architecture.sh

# Run architectural validation
./scripts/validate-architecture.sh validate

# Set up Git hooks
./scripts/validate-architecture.sh setup-hooks

# Build validator tool
./scripts/validate-architecture.sh build-validator

# Check dependency violations
./scripts/validate-architecture.sh check-deps

# Generate compliance report
./scripts/validate-architecture.sh generate-report

# Show help
./scripts/validate-architecture.sh help
```

## Configuration Files

### 1. Architectural Rules (`docs/ARCHITECTURAL_CONSTRAINTS.ruleset`)

Microsoft FxCop ruleset that enforces architectural constraints:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="TiXL Architectural Boundaries Enforcement">
  <!-- Enforces various architectural patterns -->
</RuleSet>
```

### 2. Build Configuration (`Directory.Build.props`)

MSBuild configuration that enforces constraints during compilation:

- Module-specific forbidden references
- Architectural validation targets
- Code analysis rule sets

### 3. Architectural Documentation (`docs/ARCHITECTURAL_GOVERNANCE.md`)

Complete architectural governance documentation including:

- Module boundaries and responsibilities
- Dependency rules and restrictions
- Cross-module communication patterns
- Code review checklists

## Module Architecture

### Core Module
- **Allowed Dependencies**: System, Microsoft
- **Forbidden Dependencies**: Operators, Gui, Editor, Gfx
- **Responsibilities**: Engine foundations, data types, rendering

### Operators Module  
- **Allowed Dependencies**: Core, System, Microsoft
- **Forbidden Dependencies**: Gui, Editor, Gfx
- **Responsibilities**: Plugin system, operators, dataflow

### Gfx Module
- **Allowed Dependencies**: Core, System, Microsoft, SharpDX
- **Forbidden Dependencies**: Operators, Gui, Editor
- **Responsibilities**: Graphics pipeline, DirectX 12

### Gui Module
- **Allowed Dependencies**: Core, Operators, System, Microsoft, ImGui.NET
- **Forbidden Dependencies**: Editor, Gfx
- **Responsibilities**: User interface, immediate-mode UI

### Editor Module
- **Allowed Dependencies**: All modules
- **Forbidden Dependencies**: None
- **Responsibilities**: Application orchestration, integration

## Common Violations and Fixes

### 1. Forbidden Project References

**Violation:**
```xml
<!-- TiXL.Core.csproj -->
<ProjectReference Include="..\Operators\TiXL.Operators.csproj" />
```

**Fix:**
- Remove the reference
- Use interface-based communication instead
- Move shared interfaces to appropriate module

### 2. Forbidden Using Statements

**Violation:**
```csharp
// In TiXL.Core module
using TiXL.Operators; // ❌ Forbidden
```

**Fix:**
- Remove the using statement
- Use abstractions defined in Core
- Implement communication through interfaces

### 3. Direct Instantiation

**Violation:**
```csharp
// In operators module
var renderer = new DirectXRenderer(); // ❌ Direct dependency on Gfx
```

**Fix:**
```csharp
// Use dependency injection
var renderer = serviceProvider.GetService<IRenderingEngine>();
```

### 4. Namespace Mismatches

**Violation:**
```csharp
// File in src/Core/ but namespace is TiXL.Operators
namespace TiXL.Operators
{
    // ❌ Namespace doesn't match directory structure
}
```

**Fix:**
```csharp
// Match namespace to directory structure
namespace TiXL.Core.Rendering
{
    // ✅ Correct namespace
}
```

## Continuous Integration

The architectural validation is integrated into the CI/CD pipeline:

### Azure DevOps Pipeline

```yaml
steps:
- script: |
    dotnet build --configuration Release --verbosity minimal
    dotnet run --project Tools/ArchitecturalValidator -- $(Agent.BuildDirectory)/s/TiXL.sln
  displayName: 'Architectural Validation'
```

### GitHub Actions

```yaml
- name: Validate Architecture
  run: |
    dotnet build Tools/ArchitecturalValidator/TiXL.ArchitecturalValidator.csproj
    dotnet run --project Tools/ArchitecturalValidator -- ${{ github.workspace }}/TiXL.sln
```

## Developer Workflow

### 1. Setup Development Environment

```bash
# Clone repository
git clone https://github.com/tixl3d/tixl.git
cd tixl

# Set up architectural validation
./scripts/validate-architecture.sh setup-hooks
./scripts/validate-architecture.sh build-validator

# Verify setup
./scripts/validate-architecture.sh validate
```

### 2. Development Process

1. **Before coding**: Run `./scripts/validate-architecture.sh validate`
2. **During development**: Commit hooks will catch violations
3. **Before committing**: All checks run automatically
4. **CI/CD**: Additional validation in build pipeline

### 3. Code Review Process

Reviewers should check:

- [ ] No forbidden dependencies added
- [ ] Module boundaries respected
- [ ] Proper use of interfaces and abstractions
- [ ] Namespace structure maintained
- [ ] Architecture documentation updated if needed

## Troubleshooting

### Common Issues

1. **Validator tool not found**
   ```bash
   ./scripts/validate-architecture.sh build-validator
   ```

2. **Git hooks not working**
   ```bash
   chmod +x .githooks/pre-commit
   ./scripts/validate-architecture.sh setup-hooks
   ```

3. **False positives in validation**
   - Check for comment patterns that match violation patterns
   - Verify namespace declarations match directory structure
   - Ensure proper use of `#region` directives

### Getting Help

1. Check `docs/ARCHITECTURAL_GOVERNANCE.md` for detailed rules
2. Run `./scripts/validate-architecture.sh validate -v` for detailed output
3. Use `./scripts/validate-architecture.sh generate-report` for compliance report
4. Review architecture diagrams and module documentation

## Contributing

When adding new tools or modifying existing ones:

1. **Update documentation** in `docs/ARCHITECTURAL_GOVERNANCE.md`
2. **Add tests** for new validation rules
3. **Update configuration files** as needed
4. **Test thoroughly** with the validation script
5. **Update this README** with changes

## Best Practices

1. **Keep dependencies minimal** - Only reference what's absolutely necessary
2. **Use interfaces** - Define contracts in appropriate modules
3. **Follow patterns** - Stick to established communication patterns
4. **Document changes** - Update architectural documentation when needed
5. **Test thoroughly** - Run validation before submitting changes

## Performance Considerations

The architectural validation tools are designed to be fast:

- Project file analysis: ~1-2 seconds
- Source code scanning: ~5-10 seconds for full codebase
- Complete validation: ~30-60 seconds

Use `./scripts/validate-architecture.sh validate` for quick checks during development.

## Maintenance

Regular maintenance tasks:

- **Weekly**: Check validation reports and trends
- **Monthly**: Review and update architectural rules
- **Quarterly**: Assess overall architectural health
- **Annually**: Major architectural review and updates

---

For more information, see [docs/ARCHITECTURAL_GOVERNANCE.md](../docs/ARCHITECTURAL_GOVERNANCE.md)