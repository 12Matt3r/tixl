# TiXL Security Scripts

This directory contains security scanning tools and scripts for the TiXL project.

## Available Scripts

### security-scan.ps1
Main security scanning script that performs comprehensive vulnerability analysis.

**Usage:**
```powershell
.\scripts\security-scan.ps1                    # Run all security scans
.\scripts\security-scan.ps1 -Verbose          # Verbose output
.\scripts\security-scan.ps1 -FailOnWarning    # Fail on warnings
.\scripts\security-scan.ps1 -OutputPath "report.json"  # Custom output path
```

**Checks Performed:**
- NuGet package vulnerability audit
- dotnet-retire scan for known .NET CVEs
- Dependency analysis (outdated/deprecated packages)
- Security configuration validation

### security-setup.ps1
Interactive setup script for configuring security tools in the development environment.

**Usage:**
```powershell
.\scripts\security-setup.ps1                  # Basic setup
.\scripts\security-setup.ps1 -Interactive     # Interactive mode
.\scripts\security-setup.ps1 -ProjectPath "." # Custom project path
```

**Setup Tasks:**
- Install global .NET security tools (dotnet-retire, etc.)
- Create necessary configuration directories
- Install pre-commit hooks (if pip is available)
- Generate security monitoring scripts
- Create local security documentation

## Quick Start

1. **Initial Setup:**
   ```powershell
   .\scripts\security-setup.ps1 -Interactive
   ```

2. **Run Security Scan:**
   ```powershell
   .\scripts\security-scan.ps1 -Verbose
   ```

3. **Review Results:**
   - Check generated `security-report.json`
   - Review GitHub Actions security scan results
   - Monitor security issues in GitHub

## Security Tools Installed

- **dotnet-retire**: Known .NET CVE scanner
- **dotnet-reportgenerator**: Test coverage reporting
- **pre-commit hooks**: Local security validation

## Output Files

- `security-report.json`: Detailed security scan results
- `security-metrics-summary.md`: Security metrics report
- Local Git hooks for security checks

## Support

For issues with security scripts:
1. Check the PowerShell output for error messages
2. Verify .NET SDK and Git are properly installed
3. Ensure internet connectivity for package downloads
4. Create GitHub issue with 'security' label

## Additional Resources

- Main documentation: `docs/security_scanning_setup.md`
- Security guidelines: `docs/SECURITY_GUIDELINES.md`
- Implementation summary: `docs/SECURITY_IMPLEMENTATION_SUMMARY.md`