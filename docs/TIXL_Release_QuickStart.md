# TiXL Release & Versioning Quick Start Guide

## Overview

This guide helps TiXL developers quickly understand and implement the new release and versioning policy (TIXL-095). It provides essential information and step-by-step workflows for common release scenarios.

## 🎯 Key Concepts

### Version Format
```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```
- **MAJOR**: Breaking changes (e.g., 2.0.0)
- **MINOR**: New features (e.g., 1.1.0)  
- **PATCH**: Bug fixes (e.g., 1.0.1)

### Release Types
- **Major Release**: 12-18 months, breaking changes, requires migration
- **Minor Release**: 6-8 weeks, new features, backward compatible
- **Patch Release**: As needed, bug fixes, backward compatible
- **Emergency Release**: Critical fixes, 24-48 hours

---

## 🚀 Quick Start Workflows

### 1. Patch Release (Bug Fix)

**Scenario**: Fix a critical bug in production

```bash
# 1. Create hotfix branch from main
git checkout main
git pull upstream main
git checkout -b hotfix/critical-memory-leak

# 2. Fix the bug and add tests
# ... make your changes ...

# 3. Run quality gates
./scripts/release/prepare-release.sh v1.0.3 patch

# 4. Bump version
./scripts/release/bump-version.sh patch

# 5. Generate changelog
./scripts/release/generate-changelog.sh v1.0.2 v1.0.3

# 6. Commit and push
git add .
git commit -m "fix: resolve critical memory leak in texture disposal

Fixes #247
Addresses security concern CVE-2024-XXXX"
git push origin hotfix/critical-memory-leak

# 7. Create PR to main
# Use GitHub UI or:
gh pr create --base main --head hotfix/critical-memory-leak --title "hotfix: critical memory leak fix"

# 8. After merge, tag the release
git checkout main
git pull upstream main
git tag v1.0.3
git push origin v1.0.3
```

**Expected Timeline**: 3-5 days  
**Quality Requirements**: All tests must pass, security scan clean

### 2. Minor Release (New Features)

**Scenario**: Add new features and improvements

```bash
# 1. Ensure develop is ready
git checkout develop
git pull upstream develop

# 2. Create feature branch
git checkout -b feature/add-pbr-operator

# 3. Develop features (follow conventional commits)
# feat(operators): add PBR material operator
# perf(core): optimize texture upload performance
# docs(api): update PBR operator documentation

# 4. Test locally
dotnet test --configuration Release
./scripts/validate-architecture.sh validate

# 5. Merge to develop
git checkout develop
git merge feature/add-pbr-operator
git push origin develop

# 6. Prepare release (1-2 weeks before target)
./scripts/release/prepare-release.sh v1.2.0 minor

# 7. Create release branch
git checkout develop
git checkout -b release/v1.2.0

# 8. Final QA and version bump
./scripts/release/bump-version.sh minor
./scripts/release/generate-changelog.sh v1.1.0 v1.2.0

# 9. Create PR to main
git push origin release/v1.2.0
# Create PR: release/v1.2.0 → main

# 10. After merge and CI passes
git checkout main
git pull upstream main
git tag v1.2.0
git push origin v1.2.0
```

**Expected Timeline**: 6-8 weeks  
**Quality Requirements**: Enhanced testing, documentation, community beta testing

### 3. Major Release (Breaking Changes)

**Scenario**: Major platform upgrade with breaking changes

```bash
# 1. Planning phase (8 weeks before)
# - Create major release plan
# - Announce breaking changes to community
# - Design migration strategy

# 2. Create major release branch
git checkout main
git checkout -b release/v2.0.0

# 3. Implement breaking changes
# chore(breaking): migrate to async/await patterns
# feat(api): new typed configuration system
# refactor(core): modernize resource management

# 4. Create migration tools and documentation
# - Migration assistant
# - Adapter libraries
# - Detailed migration guide

# 5. Extended testing period (4 weeks)
./scripts/release/prepare-release.sh v2.0.0 major

# 6. Community beta testing
# - Release beta versions
# - Gather feedback
# - Iterate on issues

# 7. Final release preparation
./scripts/release/bump-version.sh major --target 2.0.0
./scripts/release/generate-changelog.sh v1.8.0 v2.0.0 --include-breaking

# 8. Major release PR
git push origin release/v2.0.0
# Create PR: release/v2.0.0 → main

# 9. After merge
git checkout main
git pull upstream main
git tag v2.0.0
git push origin v2.0.0
```

**Expected Timeline**: 12-18 weeks  
**Quality Requirements**: Comprehensive testing, migration tools, extensive documentation

---

## 📋 Daily Development Practices

### Conventional Commit Format
```bash
# Feature development
git commit -m "feat(operators): add PBR material operator with metal-rough workflow

Implements physically-based rendering material operator supporting:
- Base color, metallic, roughness inputs
- Normal mapping with tangent space support
- Compatible with existing ShaderOperator framework

Closes #142"

# Bug fixes
git commit -m "fix(core): resolve memory leak in render target disposal

Fixes potential memory corruption in GPU resource management.
Add proper disposal pattern for render targets.

Closes #201"

# Breaking changes
git commit -m "feat(api): migrate to async/await patterns

BREAKING CHANGE: All I/O operations now require async/await.
See migration guide for details.

Closes #150"
```

### Branch Naming Convention
- `feature/short-description` - New features
- `bugfix/issue-number-short-description` - Bug fixes  
- `hotfix/critical-issue` - Critical production fixes
- `release/version-number` - Release branches
- `docs/description` - Documentation changes
- `refactor/area-description` - Code refactoring

### Quality Gates (Before Each PR)
```bash
# Run all quality checks
dotnet test --configuration Release
dotnet build --configuration Release
dotnet format --verify-no-changes
./scripts/validate-architecture.sh validate

# Security check
dotnet list package --vulnerable

# Performance check (for significant changes)
cd Benchmarks
dotnet run --configuration Release -- --filter "*YourChange*"
```

---

## 🛠️ Essential Scripts

### Version Management
```bash
# Auto-detect version bump
./scripts/release/bump-version.sh auto

# Manual version bump
./scripts/release/bump-version.sh minor
./scripts/release/bump-version.sh patch
./scripts/release/bump-version.sh major --target 2.0.0

# Verify versions
./scripts/release/verify-versions.sh v1.2.0
```

### Changelog Generation
```bash
# Generate changelog
./scripts/release/generate-changelog.sh v1.1.0 v1.2.0

# Generate with breaking changes
./scripts/release/generate-changelog.sh v1.8.0 v2.0.0 --include-breaking

# Exclude performance section
./scripts/release/generate-changelog.sh v1.1.0 v1.2.0 --no-performance
```

### Release Preparation
```bash
# Full release preparation
./scripts/release/prepare-release.sh v1.2.0 minor

# Skip tests (not recommended)
./scripts/release/prepare-release.sh v1.2.0 minor --skip-tests

# Verbose output
./scripts/release/prepare-release.sh v1.2.0 minor --verbose
```

---

## 🔍 Quality Gates Checklist

### Code Quality
- [ ] All tests pass (`dotnet test`)
- [ ] Code coverage ≥ 85%
- [ ] No new critical warnings
- [ ] Architecture validation passes
- [ ] Security scan shows no vulnerabilities

### Testing
- [ ] Unit tests: All scenarios
- [ ] Integration tests: Cross-module
- [ ] Performance tests: Benchmarks
- [ ] Manual testing: GUI workflows
- [ ] Beta testing: Community feedback

### Documentation
- [ ] API documentation updated
- [ ] Migration guide (if breaking)
- [ ] Changelog generated
- [ ] Examples updated
- [ ] Wiki updated

### Community Communication
- [ ] Release notes prepared
- [ ] Social media content ready
- [ ] Discord announcements scheduled
- [ ] Blog post drafted
- [ ] Video demonstrations prepared

---

## 🚨 Emergency Procedures

### Critical Security Patch
```bash
# Immediate hotfix process
git checkout main
git checkout -b hotfix/security-patch-CVE-2024-XXXX

# Apply minimal fix
# - Address security vulnerability
# - Add security tests
# - Update security documentation

# Expedited review and merge
./scripts/release/prepare-release.sh v1.0.3 patch --skip-tests
./scripts/release/bump-version.sh patch
./scripts/release/generate-changelog.sh v1.0.2 v1.0.3

# Emergency release (24-48 hours)
git push origin hotfix/security-patch-CVE-2024-XXXX
# Fast-track PR review and merge
git tag v1.0.3
git push origin v1.0.3
```

### Production Rollback
```bash
# If critical issue discovered post-release
# 1. Create rollback branch
git checkout v1.0.3^1  # Previous tag
git checkout -b rollback/v1.0.3

# 2. Cherry-pick fixes
git cherry-pick <safe-commits-from-v1.0.3>

# 3. Release rollback version
./scripts/release/bump-version.sh patch --target 1.0.4
git tag v1.0.4
git push origin v1.0.4
```

---

## 📊 Success Metrics

### Release Quality Targets
- **Test Coverage**: ≥ 85%
- **Performance Regression**: < 5%
- **Security Issues**: 0 (medium+ severity)
- **Lead Time**: < 7 days (minor releases)
- **Failure Rate**: < 2% (releases needing hotfix)

### Community Satisfaction
- **Adoption Rate**: 60% within 30 days (minor releases)
- **User Satisfaction**: > 4.0/5.0
- **Issue Volume**: < 5% increase
- **Documentation Quality**: < 2% bounce rate

---

## 🎓 Learning Resources

### Documentation
- [Full Release Policy](TIXL-095_Release_and_Versioning_Policy.md)
- [Migration Guide](TIXL_Migration_Guide_v1_to_v2.md)
- [Contributing Guidelines](../CONTRIBUTING.md)
- [Architectural Governance](ARCHITECTURAL_GOVERNANCE.md)

### Tools
- **Migration Assistant**: `dotnet tool install -g TiXL.MigrationAssistant`
- **Version Manager**: `./scripts/release/bump-version.sh --help`
- **Quality Gates**: `./scripts/release/prepare-release.sh --help`

### Community
- **Discord**: [#release-announcements](https://discord.gg/tixl-release)
- **GitHub Discussions**: [Release Planning](https://github.com/tixl/tixl/discussions)
- **Office Hours**: Weekly release planning sessions
- **Slack**: #release-management channel

---

## ✅ Quick Reference

### When to Increment
- **MAJOR**: Breaking API changes
- **MINOR**: New features, performance improvements
- **PATCH**: Bug fixes, security patches

### Branch Strategy
- **main**: Production releases only
- **develop**: Integration branch
- **feature/**: New development
- **hotfix/**: Critical fixes
- **release/**: Release preparation

### Quality Gates
1. Code Analysis & Build
2. Test Execution & Coverage
3. Security Scanning
4. Performance Validation
5. Documentation Review
6. Community Testing (minor/major)

### Communication Timeline
- **Major**: 8 weeks advance notice
- **Minor**: 2 weeks community testing
- **Patch**: Immediate after testing

---

*Need help? Contact the release management team at releases@tixl.io or join our [Discord release channel](https://discord.gg/tixl-release).*

**Last Updated**: November 2, 2024  
**Version**: 1.0  
**Policy**: [TIXL-095](TIXL-095_Release_and_Versioning_Policy.md)