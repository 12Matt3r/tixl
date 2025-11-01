# TiXL Secure Build Process - Implementation Guide

## Overview

The TiXL secure build process implements reproducible builds with code signing to provide end-to-end trust from source code to final artifacts. This system ensures build integrity, verifies provenance, and provides verifiable signatures for all released components.

## 🎯 Key Features

### 🔗 Source Link Integration
- **Build Provenance**: Automatically embeds source repository information in compiled assemblies
- **Debug Symbol Enhancement**: PDB files include source repository links
- **Source Code Attribution**: Every binary can be traced back to specific source code

### 🔨 Reproducible Builds
- **Deterministic Compilation**: Identical source code produces identical binaries
- **Fixed Timestamps**: Build environment variables ensure consistent timestamps
- **Environment Isolation**: Build dependencies and configurations are captured
- **Cross-Platform Consistency**: Builds can be reproduced on any supported platform

### 🔐 Code Signing
- **Automated Signing**: All artifacts are signed using industry-standard certificates
- **Timestamp Authority**: Timestamps ensure signatures remain valid after certificate expiration
- **Chain of Trust**: Signed artifacts provide cryptographic proof of authenticity
- **NuGet Integration**: Both assemblies and NuGet packages are signed

### 🛡️ Build Integrity Verification
- **Hash Verification**: SHA-256 checksums for all artifacts
- **Signature Validation**: Cryptographic signature verification
- **Provenance Tracking**: Source-to-binary traceability verification
- **Build Manifest**: Comprehensive build metadata and verification results

### 📦 Secure Artifact Management
- **Artifact Isolation**: Secure storage of all build outputs
- **Integrity Tracking**: Complete audit trail of all artifacts
- **Retention Policies**: Automated cleanup and archival
- **Distribution Security**: Secure artifact distribution channels

## 🚀 Quick Start

### Prerequisites

1. **.NET 9.0 SDK** or later
2. **PowerShell 7.0** or later (for Windows)
3. **Code Signing Certificate** (for signing operations)

### Local Development Build

```powershell
# Run secure build locally (without signing)
.\scripts\build-security.ps1 -Configuration Release

# Run secure build with code signing
.\scripts\build-security.ps1 -Configuration Release -SignArtifacts

# Verify build integrity
.\scripts\verify-build-integrity.ps1
```

### CI/CD Integration

The secure build process is automatically triggered in CI/CD pipelines:

```yaml
# Example GitHub Actions trigger
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
```

## 📋 Detailed Configuration

### Directory.Build.props Settings

The main configuration file `Directory.Build.props` contains all reproducible build settings:

```xml
<!-- SourceLink Integration -->
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<EmbedUntrackedSources>true</EmbedUntrackedSources>
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>

<!-- Deterministic Build Configuration -->
<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
<Deterministic>true</Deterministic>
<DeterministicSourcePaths>true</DeterministicSourcePaths>

<!-- Fixed Build Environment -->
<SourceLinkDateTime>2024-01-01T00:00:00+00:00</SourceLinkDateTime>
<BuildDate Condition="'$(BuildDate)' == ''">2024-01-01T00:00:00Z</BuildDate>
<SourceRevisionId Condition="'$(SourceRevisionId)' == ''">$(BuildDate)</SourceRevisionId>
```

### Environment Variables for Reproducible Builds

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_BUILD_DATE = "2024-01-01T00:00:00Z"
$env:SOURCE_LINK_DATE_TIME = "2024-01-01T00:00:00+00:00"
```

### Certificate Management

#### Creating a Code Signing Certificate

```powershell
# Create self-signed certificate for development
.\scripts\manage-certificates.ps1 -Action Create -OutputPath "certificates\tixl-signing.pfx" -Password "secure-password" -ValidityDays 365

# Import certificate to store
.\scripts\manage-certificates.ps1 -Action Import -CertificatePath "certificates\tixl-signing.pfx" -Password "secure-password"

# List certificates in store
.\scripts\manage-certificates.ps1 -Action List

# Verify certificate validity
.\scripts\manage-certificates.ps1 -Action Verify -CertificateThumbprint "YOUR_CERT_THUMBPRINT"
```

#### Using Code Signing Certificates

```powershell
# Build with code signing
.\scripts\build-security.ps1 -Configuration Release -SignArtifacts -CertificateThumbprint "YOUR_CERT_THUMBPRINT"

# Sign specific artifacts
.\scripts\manage-certificates.ps1 -Action Export -CertificateThumbprint "YOUR_CERT_THUMBPRINT" -OutputPath "exported-cert.cer"
```

## 🔧 Advanced Configuration

### Custom Timestamp Servers

```powershell
# Use custom RFC3161 timestamp server
.\scripts\build-security.ps1 -Configuration Release -SignArtifacts -TimestampUrl "http://timestamp.your-provider.com"
```

Supported timestamp servers:
- `http://timestamp.digicert.com` (DigiCert)
- `http://timestamp.sectigo.com` (Sectigo)
- `http://time.certum.pl` (Certum)
- Custom RFC3161 compliant servers

### Build Integrity Verification

```powershell
# Verify build against expected Git commit
.\scripts\verify-build-integrity.ps1 -ExpectedGitCommit "abc123" -VerifySignatures -CheckProvenance

# Custom verification report path
.\scripts\verify-build-integrity.ps1 -OutputReportPath "custom-verification-report.md"
```

### CI/CD Pipeline Integration

#### GitHub Actions Workflow

```yaml
# Trigger secure build
- name: Run Secure Build
  uses: ./.github/workflows/secure-build.yml
  with:
    signing_enabled: true
    verify_integrity: true
```

#### Azure DevOps Integration

```yaml
# Azure DevOps pipeline task
- task: PowerShell@2
  inputs:
    targetType: 'filePath'
    filePath: 'scripts\build-security.ps1'
    arguments: '-Configuration Release -SignArtifacts -VerifyIntegrity'
  displayName: 'Secure Build Process'
```

## 📊 Build Manifest and Reports

### Build Manifest Structure

```json
{
  "BuildInfo": {
    "BuildId": "20241102-041757",
    "BuildTime": "2024-11-02T04:17:57Z",
    "Configuration": "Release",
    "GitCommit": "abc123",
    "GitBranch": "main",
    "BuildMachine": "build-server-01",
    "User": "build-agent",
    "DotNetVersion": "9.0.0",
    "OSPlatform": "Windows"
  },
  "ReproducibleBuild": {
    "SourceLinkEnabled": true,
    "EmbedSources": true,
    "DeterministicBuild": true,
    "FixedBuildTime": true,
    "FixedFileVersion": true
  },
  "Security": {
    "SigningEnabled": true,
    "IntegrityVerification": true,
    "TimestampServer": "http://timestamp.digicert.com",
    "CertificateStore": "Cert:\\CurrentUser\\My"
  },
  "Artifacts": [
    {
      "FileName": "TiXL.Core.dll",
      "Hash": "SHA256:abc123...",
      "IsSigned": true,
      "Size": 1048576
    }
  ],
  "Integrity": [
    {
      "FileName": "TiXL.Core.dll",
      "FilePath": "artifacts/TiXL.Core.dll",
      "Hash": "SHA256:abc123...",
      "HashAlgorithm": "SHA256",
      "Size": 1048576,
      "IsSigned": true,
      "VerificationTime": "2024-11-02T04:18:00Z"
    }
  ]
}
```

### Verification Report

The verification report provides a human-readable summary of all security checks:

```markdown
# TiXL Build Integrity Verification Report

**Verification Time:** 2024-11-02 04:18:00 UTC  
**Overall Status:** ✅ PASSED  

## Summary
- **Total Artifacts:** 15
- **Verified Artifacts:** 15
- **Failed Artifacts:** 0
- **Signed Artifacts:** 15
- **Unsigned Artifacts:** 0

## Security Features
✅ Code signing verification enabled
✅ Source provenance verification enabled
✅ Hash integrity verification successful
```

## 🔒 Security Considerations

### Certificate Security

1. **Private Key Protection**: Never commit private keys to version control
2. **Password Management**: Use secure password storage or Azure Key Vault
3. **Certificate Rotation**: Regularly rotate certificates before expiration
4. **Access Control**: Limit certificate access to authorized build agents

### Build Environment Security

1. **Isolated Build Agents**: Use dedicated, hardened build agents
2. **Environment Variables**: Securely manage build environment variables
3. **Network Security**: Restrict network access to necessary resources only
4. **Audit Logging**: Maintain comprehensive audit logs of all build activities

### Artifact Security

1. **Storage Encryption**: Encrypt all stored artifacts
2. **Access Control**: Implement strict access controls for artifact storage
3. **Integrity Monitoring**: Regularly verify artifact integrity
4. **Secure Distribution**: Use secure channels for artifact distribution

## 🐛 Troubleshooting

### Common Issues

#### Build Not Reproducible

**Symptom**: Identical source code produces different binaries

**Solution**:
```powershell
# Check environment variables
$env:DOTNET_BUILD_DATE
$env:SOURCE_LINK_DATE_TIME

# Verify fixed timestamps
Get-ChildItem -Recurse -File | Select-Object LastWriteTime, CreationTime | Sort-Object LastWriteTime
```

#### Code Signing Failures

**Symptom**: Certificate not found or signing failed

**Solution**:
```powershell
# Verify certificate in store
.\scripts\manage-certificates.ps1 -Action List

# Check certificate validity
.\scripts\manage-certificates.ps1 -Action Verify -CertificateThumbprint "YOUR_THUMBPRINT"

# Re-import certificate if needed
.\scripts\manage-certificates.ps1 -Action Import -CertificatePath "path\to\cert.pfx" -Password "password"
```

#### Integrity Verification Failures

**Symptom**: Hash verification fails for artifacts

**Solution**:
```powershell
# Verify build manifest exists
Test-Path "artifacts/secure-build/build-manifest.json"

# Check artifact integrity manually
Get-FileHash -Path "artifacts/secure-build/TiXL.Core.dll" -Algorithm SHA256

# Compare with manifest hash
$manifest = Get-Content "artifacts/secure-build/build-manifest.json" | ConvertFrom-Json
$manifest.Integrity | Where-Object { $_.FileName -eq "TiXL.Core.dll" }
```

### Debug Mode

Enable debug logging for detailed troubleshooting:

```powershell
.\scripts\build-security.ps1 -Configuration Release -Debug -Verbose
```

## 📈 Monitoring and Alerting

### Build Health Metrics

Monitor these key metrics:

1. **Build Success Rate**: Percentage of successful builds
2. **Reproducibility Rate**: Percentage of builds that are reproducible
3. **Signing Success Rate**: Percentage of successfully signed artifacts
4. **Integrity Verification Rate**: Percentage of artifacts passing integrity checks
5. **Build Time**: Average build duration
6. **Security Scan Results**: Number and severity of security findings

### Automated Alerts

Configure alerts for:

- Build failures
- Signing failures
- Integrity verification failures
- Certificate expiration warnings
- Security scan failures
- Build environment issues

### Dashboard Integration

Integrate with monitoring dashboards:

```powershell
# Export metrics for monitoring systems
.\scripts\build-security.ps1 -Configuration Release -GenerateMetrics | Out-File -FilePath "build-metrics.json"
```

## 🔄 Maintenance and Updates

### Regular Maintenance Tasks

1. **Certificate Management**
   - Monitor certificate expiration dates
   - Rotate certificates before expiration
   - Update certificate store on build agents

2. **Tool Updates**
   - Keep .NET SDK updated
   - Update security scanning tools
   - Refresh security vulnerability databases

3. **Build Environment**
   - Update build agent images
   - Refresh dependencies
   - Update environment variables

### Security Updates

1. **Vulnerability Monitoring**
   - Monitor security advisories
   - Update vulnerable dependencies
   - Apply security patches promptly

2. **Certificate Updates**
   - Monitor certificate revocation lists
   - Update certificate validation rules
   - Refresh trusted certificate authorities

## 📚 Additional Resources

### Documentation Links

- [SourceLink Documentation](https://github.com/dotnet/sourcelink)
- [NuGet Package Signing](https://docs.microsoft.com/en-us/nuget/create-packages/sign-a-package)
- [Windows Code Signing](https://docs.microsoft.com/en-us/windows/win32/seccrypto/signtool)
- [Reproducible Builds](https://reproducible-builds.org/)

### Tool References

- [dotnet-sourcelink](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub)
- [signtool](https://docs.microsoft.com/en-us/windows/win32/seccrypto/signtool)
- [PowerShell Security](https://docs.microsoft.com/en-us/powershell/)

### Support and Contact

- **Build Issues**: Create issue with 'build-security' label
- **Certificate Problems**: Contact security team
- **Documentation**: Review this guide and contribute improvements

---

*This documentation is part of the TiXL Secure Build Process implementation (TIXL-059)*