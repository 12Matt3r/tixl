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

## Additional Resources

- [OWASP .NET Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html)
- [Microsoft Security Documentation](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [GitHub Security Features](https://docs.github.com/en/code-security)