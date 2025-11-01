# TiXL Security Guidelines

## Overview

This document provides security guidelines for developers working on the TiXL project.

## Security Scanning Workflow

### 1. Development Phase
- Run security scans locally before committing
- Use `scripts/security-scan.ps1` for quick security checks
- Follow secure coding practices from OWASP

### 2. Pull Request Process
- All PRs are automatically scanned for security issues
- Security gates prevent merging code with critical vulnerabilities
- Review security scan results in the PR

### 3. Dependency Management
- Use NuGet package management with vulnerability auditing
- Keep dependencies up to date with Dependabot PRs
- Review and test dependency updates before merging

### 4. Security Incident Response
1. **Immediate:** Create security issue with 'security' label
2. **Assessment:** Evaluate impact and severity
3. **Remediation:** Fix vulnerabilities within SLA
4. **Verification:** Confirm fix with security scans
5. **Documentation:** Update security documentation

## Secure Coding Practices

### Input Validation
- Validate all external inputs
- Use allowlists, not denylists
- Sanitize file paths and names

### Memory Safety
- Avoid unsafe code blocks
- Use safe deserialization methods
- Review P/Invoke usage

### Cryptography
- Use approved cryptographic algorithms
- Never implement custom crypto
- Store keys securely

## Tools Reference

| Tool | Purpose | When to Use |
|------|---------|-------------|
| NuGet Audit | Package vulnerabilities | Every build |
| dotnet-retire | Known .NET CVEs | Weekly |
| CodeQL | Static analysis | Every commit |
| OWASP DC | Dependency scan | Daily |

## Emergency Contacts

- **Security Team:** security@tixl-project.org
- **DevOps Lead:** devops@tixl-project.org
- **GitHub Security:** security-advisories@tixl-project.org

## Security Analysis & Testing

### Input Handling Security (TIXL-060)
- **Document**: [`TIXL-060_Input_Handling_Security_Analysis.md`](TIXL-060_Input_Handling_Security_Analysis.md)
- **Test Suite**: [`InputHandlingSecurityTests.cs`](../Tests/Security/InputHandlingSecurityTests.cs)
- **Test Runner**: [`run-security-tests.sh`](../scripts/run-security-tests.sh) | [`run-security-tests.ps1`](../scripts/run-security-tests.ps1)

### Key Security Findings
- ✅ **BinaryFormatter**: Completely eliminated
- ✅ **File I/O**: Secure with path validation and size limits
- ✅ **Serialization**: System.Text.Json with security settings
- ⚠️ **Network I/O**: Needs enhanced validation
- ⚠️ **Audio/MIDI**: Requires buffer validation improvements

### Running Security Tests
```bash
# Linux/macOS
./scripts/run-security-tests.sh

# Windows PowerShell
./scripts/run-security-tests.ps1

# Generate detailed report
./scripts/run-security-tests.ps1 -GenerateReport
```

## Additional Resources

- [OWASP .NET Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html)
- [Microsoft Security Documentation](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [GitHub Security Features](https://docs.github.com/en/code-security)
- [TiXL Input Handling Security Analysis](TIXL-060_Input_Handling_Security_Analysis.md)