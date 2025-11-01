# TiXL Secure Build Process - Quick Start Guide

## 🚀 Get Started in 5 Minutes

This guide will help you quickly set up and use the TiXL secure build system.

## Prerequisites

1. **.NET 9.0 SDK** or later
2. **PowerShell 7.0** or later (Windows)
3. **Git** for version control

## Step 1: Verify Installation

Run the setup validation to ensure everything is configured correctly:

```powershell
# Validate your environment
.\scripts\tixl-secure-build-demo.ps1 -Action Setup
```

This will check:
- ✅ .NET SDK installation
- ✅ Required scripts presence
- ✅ Configuration files
- ✅ CI/CD integration

## Step 2: Run the Demo

Create a complete working example:

```powershell
# Run the full demo with certificate creation
.\scripts\tixl-secure-build-demo.ps1 -Action Demo -CreateTestCert

# Output will show:
# - Demo certificate creation
# - Sample project build
# - Code signing demonstration
# - Integrity verification
```

## Step 3: Use with Your Project

### Basic Usage

```powershell
# Build your project with reproducible settings
.\scripts\build-security.ps1 -Configuration Release

# Verify build integrity
.\scripts\verify-build-integrity.ps1
```

### With Code Signing

```powershell
# 1. Create or import a code signing certificate
.\scripts\manage-certificates.ps1 -Action Create -OutputPath "my-cert.pfx" -Password "secure-password"

# 2. Build with signing
.\scripts\build-security.ps1 -Configuration Release -SignArtifacts -CertificateThumbprint "YOUR_CERT_THUMBPRINT"

# 3. Verify everything
.\scripts\verify-build-security.ps1
```

## Step 4: CI/CD Integration

### GitHub Actions

Add to your `.github/workflows/secure-build.yml`:

```yaml
name: Secure Build
on: [push, pull_request]

jobs:
  secure-build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: .\scripts\build-security.ps1 -Configuration Release -SignArtifacts
      - run: .\scripts\verify-build-security.ps1
```

## Common Commands

### Certificate Management

```powershell
# Create certificate
.\scripts\manage-certificates.ps1 -Action Create -OutputPath "tixl-signing.pfx"

# Import certificate
.\scripts\manage-certificates.ps1 -Action Import -CertificatePath "tixl-signing.pfx" -Password "password"

# List certificates
.\scripts\manage-certificates.ps1 -Action List

# Verify certificate
.\scripts\manage-certificates.ps1 -Action Verify -CertificateThumbprint "ABC123..."
```

### Build Verification

```powershell
# Basic verification
.\scripts\verify-build-integrity.ps1

# Comprehensive security verification
.\scripts\verify-build-security.ps1 -OutputFormat "Markdown" -OutputPath "security-report.md"

# Verify against expected Git commit
.\scripts\verify-build-security.ps1 -ExpectedGitCommit "abc123"
```

### Troubleshooting

```powershell
# Enable debug logging
.\scripts\build-security.ps1 -Configuration Release -Debug -Verbose

# Check certificate issues
.\scripts\manage-certificates.ps1 -Action Info -CertificateThumbprint "YOUR_THUMBPRINT"

# Validate build path
.\scripts\verify-build-security.ps1 -BuildPath "artifacts\secure-build"
```

## File Locations

After running builds, you'll find:

- **Build Artifacts**: `artifacts/secure-build/`
- **Build Manifest**: `artifacts/secure-build/build-manifest.json`
- **Security Report**: `artifacts/secure-build/build-security-report.md`
- **Verification Results**: `build-integrity-report.md`

## Configuration Files

### Directory.Build.props

Already configured for reproducible builds:

```xml
<!-- SourceLink Integration -->
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<EmbedUntrackedSources>true</EmbedUntrackedSources>

<!-- Deterministic Builds -->
<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
<Deterministic>true</Deterministic>

<!-- Security -->
<NuGetAudit>true</NuGetAudit>
<EnableUnsafeBinaryFormatterSerialization>false</EnableUnsafeBinaryFormatterSerialization>
```

### NuGet.config

Package sources with security auditing:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="%userprofile%\.nuget\packages" />
  </config>
</configuration>
```

## Security Features

### What Gets Protected

- ✅ **All .dll and .exe files** - Cryptographically signed
- ✅ **NuGet packages** - Signed with certificate chains
- ✅ **Debug symbols (.pdb)** - Source link information embedded
- ✅ **Documentation** - Included in packages with provenance

### Verification Checks

- ✅ **Hash Verification** - SHA-256 checksums for all artifacts
- ✅ **Signature Validation** - Cryptographic signature verification
- ✅ **Source Provenance** - Source-to-binary traceability
- ✅ **Security Scanning** - Vulnerability detection

## Production Deployment

### 1. Obtain Production Certificate

```powershell
# Get a code signing certificate from a trusted CA
# Examples:
# - DigiCert Code Signing
# - Sectigo Code Signing  
# - Comodo Code Signing
```

### 2. Configure CI/CD Secrets

In GitHub, add these secrets:
- `CODE_SIGNING_PFX` - Base64 encoded certificate
- `CODE_SIGNING_PASSWORD` - Certificate password

### 3. Enable Signing in Pipeline

```yaml
- name: Run Secure Build
  run: .\scripts\build-security.ps1 -Configuration Release -SignArtifacts
  env:
    CERTIFICATE_THUMBPRINT: ${{ secrets.CERTIFICATE_THUMBPRINT }}
```

## Best Practices

### Development
- Use test certificates for development
- Run verification locally before committing
- Keep certificate passwords secure

### Production
- Use certificates from trusted CAs
- Monitor certificate expiration dates
- Implement certificate rotation procedures

### Security
- Never commit private keys to version control
- Use secure password storage (Azure Key Vault, HashiCorp Vault)
- Regularly rotate signing certificates
- Monitor for certificate compromise

## Troubleshooting

### Common Issues

**Build Not Reproducible**
```powershell
# Check environment variables
echo $env:DOTNET_BUILD_DATE
echo $env:SOURCE_LINK_DATE_TIME

# Verify fixed timestamps
Get-ChildItem -Recurse -File | Select-Object LastWriteTime | Sort-Object LastWriteTime
```

**Certificate Not Found**
```powershell
# List certificates in store
.\scripts\manage-certificates.ps1 -Action List

# Check certificate validity
.\scripts\manage-certificates.ps1 -Action Verify -CertificateThumbprint "YOUR_THUMBPRINT"
```

**Signing Failures**
```powershell
# Verify certificate has private key
.\scripts\manage-certificates.ps1 -Action Info -CertificateThumbprint "YOUR_THUMBPRINT"

# Check certificate permissions
certlm.msc  # Windows Certificate Manager
```

**Integrity Verification Fails**
```powershell
# Manually verify hash
Get-FileHash -Path "artifacts/secure-build/TiXL.Core.dll" -Algorithm SHA256

# Compare with manifest
$manifest = Get-Content "artifacts/secure-build/build-manifest.json" | ConvertFrom-Json
$manifest.Integrity | Where-Object { $_.FileName -eq "TiXL.Core.dll" }
```

## Getting Help

### Documentation
- **Complete Guide**: `docs/TIXL-059_Secure_Build_Process.md`
- **Implementation Summary**: `TIXL-059_Implementation_Summary.md`

### Support
- **Issues**: Create GitHub issue with 'build-security' label
- **Scripts**: Use `-?` or `-Help` parameter for built-in help
- **Debug Mode**: Add `-Debug -Verbose` for detailed logging

### Example Commands

```powershell
# Get help for build script
Get-Help .\scripts\build-security.ps1 -Full

# Get help for certificate management
Get-Help .\scripts\manage-certificates.ps1 -Examples

# Get help for verification
Get-Help .\scripts\verify-build-security.ps1 -Detailed
```

## Next Steps

1. **Complete the demo** to see the system in action
2. **Read the comprehensive documentation** for detailed configuration
3. **Set up production certificates** for real signing
4. **Integrate with your CI/CD pipeline** for automated security
5. **Monitor build security metrics** and maintain certificates

---

**Welcome to secure software development with TiXL!** 🛡️

For questions or issues, refer to the complete documentation or create a GitHub issue.