# Security Guidelines

Security is a top priority for TiXL. This guide covers security best practices, tools, and procedures for maintaining a secure codebase.

## Table of Contents

1. [Security Overview](#security-overview)
2. [Development Security](#development-security)
3. [Dependency Management](#dependency-management)
4. [Code Security Practices](#code-security-practices)
5. [Security Tools and Scanning](#security-tools-and-scanning)
6. [Incident Response](#incident-response)
7. [Security Documentation](#security-documentation)

## Security Overview

TiXL maintains comprehensive security through:

- **Automated security scanning** in CI/CD pipelines
- **Regular dependency vulnerability assessments**
- **Secure coding practices** and reviews
- **Security incident response procedures**
- **Continuous security monitoring**

## Development Security

### Secure Coding Practices

#### Input Validation
```csharp
// ❌ Bad: No input validation
public void ProcessFile(string filePath)
{
    var data = File.ReadAllBytes(filePath);
    Process(data);
}

// ✅ Good: Proper input validation
public void ProcessFile(string filePath)
{
    // Validate input
    if (string.IsNullOrEmpty(filePath))
        throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
    
    // Validate path
    var fullPath = Path.GetFullPath(filePath);
    if (!fullPath.StartsWith(GetSafeBaseDirectory()))
        throw new SecurityException("Access denied: Path traversal detected");
    
    // Validate file exists and is safe
    if (!File.Exists(fullPath))
        throw new FileNotFoundException("File not found", fullPath);
    
    var fileInfo = new FileInfo(fullPath);
    if (fileInfo.Length > MaxFileSize)
        throw new SecurityException("File too large");
    
    var data = File.ReadAllBytes(fullPath);
    Process(data);
}
```

#### Memory Safety
```csharp
// ❌ Avoid unsafe code blocks
unsafe
{
    var ptr = (int*)Marshal.AllocHGlobal(sizeof(int) * length).ToPointer();
    // Direct memory manipulation - high risk
}

// ✅ Use safe alternatives
var buffer = new int[length]; // Safe managed array
```

#### File I/O Security
```csharp
// ❌ Bad: Path traversal vulnerability
public Texture LoadTexture(string relativePath)
{
    var fullPath = Path.Combine(BasePath, relativePath);
    return new Texture(fullPath);
}

// ✅ Good: Secure path handling
public Texture LoadTexture(string relativePath)
{
    // Sanitize input
    var sanitizedPath = SanitizePath(relativePath);
    
    // Validate against allowed paths
    var fullPath = Path.GetFullPath(Path.Combine(BasePath, sanitizedPath));
    if (!IsPathSafe(fullPath))
        throw new SecurityException("Invalid or unsafe path");
    
    return new Texture(fullPath);
}

private string SanitizePath(string path)
{
    // Remove path traversal attempts
    path = path.Replace("..", "");
    path = path.Replace("/", "");
    path = path.Replace("\\", "");
    
    // Remove dangerous characters
    path = new string(path.Where(c => 
        char.IsLetterOrDigit(c) || 
        c == '.' || c == '_' || c == '-'
    ).ToArray());
    
    return path;
}
```

### Operator Security

Operators must validate inputs and handle security concerns:

```csharp
[Operator("LoadShader")]
public class ShaderLoadOperator : Instance
{
    [InputSlot("ShaderPath")]
    public ISlot<string> ShaderPathInput { get; }
    
    protected override void Evaluate(EvaluationContext context)
    {
        var shaderPath = ShaderPathInput.GetValue<string>();
        
        // Security: Validate shader path
        if (!IsValidShaderPath(shaderPath))
        {
            context.Logger.LogWarning($"Rejected unsafe shader path: {shaderPath}");
            return;
        }
        
        // Security: Validate file size
        var fileInfo = new FileInfo(shaderPath);
        if (fileInfo.Length > MaxShaderSize)
        {
            context.Logger.LogWarning($"Shader file too large: {fileInfo.Length} bytes");
            return;
        }
        
        // Load shader safely
        var shader = LoadShaderSecurely(shaderPath);
        OutputSlot.SetValue(shader);
    }
    
    private bool IsValidShaderPath(string path)
    {
        // Check for path traversal
        if (path.Contains("..") || path.Contains("~"))
            return false;
        
        // Check file extension
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".hlsl" || extension == ".cso";
    }
}
```

## Dependency Management

### NuGet Security

1. **Audit Dependencies**
   ```bash
   # Run dependency audit
   dotnet list package --vulnerable
   
   # Check for outdated packages
   dotnet list package --outdated
   ```

2. **Secure Package Sources**
   - Use official NuGet.org as primary source
   - Configure trusted package sources
   - Verify package integrity

3. **Vulnerability Scanning**
   - Automatic scanning in CI/CD
   - GitHub Dependabot alerts
   - Manual security reviews

### Package Security Guidelines

- **Review all new dependencies** before adding
- **Verify publisher authenticity**
- **Check for known vulnerabilities**
- **Monitor for security updates**
- **Remove unused dependencies**

## Code Security Practices

### Cryptography

```csharp
// ❌ Never implement custom crypto
public string MyEncryption(string data) // Unsafe!
{
    // Custom encryption implementation - high risk
}

// ✅ Use approved cryptographic libraries
using System.Security.Cryptography;

public string SecureEncryption(string data)
{
    using var aes = Aes.Create();
    aes.Key = GetSecureKey();
    aes.GenerateIV();
    
    using var encryptor = aes.CreateEncryptor();
    using var msEncrypt = new MemoryStream();
    using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
    using var swEncrypt = new StreamWriter(csEncrypt);
    swEncrypt.Write(data);
    
    return Convert.ToBase64String(msEncrypt.ToArray());
}
```

### Credential Management

```csharp
// ❌ Never hardcode secrets
public class MyClass
{
    private const string API_KEY = "sk-1234567890abcdef"; // Unsafe!
}

// ✅ Use secure configuration
public class MyClass
{
    private readonly string _apiKey;
    
    public MyClass(IConfiguration configuration)
    {
        _apiKey = configuration["ExternalAPI:Key"];
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("API key not configured");
    }
}
```

## Security Tools and Scanning

### Security Scanning Tools

| Tool | Purpose | When to Use |
|------|---------|-------------|
| **GitHub Security** | Automated vulnerability scanning | Always (CI/CD) |
| **dotnet-outdated** | Check for outdated packages | Weekly |
| **Microsoft.CodeAnalysis.NetAnalyzers** | Static security analysis | During development |
| **Security.txt scanner** | Validate security.txt files | Before releases |

### Running Security Scans

```bash
# Local security scan
./scripts/security-scan.ps1

# Check for vulnerabilities
dotnet list package --vulnerable --include-transitive

# Security audit of specific package
dotnet package add <package-name> --source <source> --audit

# Run code analysis
dotnet build /p:EnforceCodeStyleInBuild=true
```

### CI/CD Security Gates

Security gates prevent deploying vulnerable code:

1. **Dependency Check** - All dependencies must be secure
2. **Code Analysis** - No high/critical security warnings
3. **Container Scanning** - Docker images must be secure
4. **Secret Scanning** - No exposed credentials

## Incident Response

### Security Incident Process

1. **Immediate Response**
   - Create security issue with 'security' label
   - Notify security team immediately
   - Document affected systems

2. **Assessment**
   - Evaluate impact and severity
   - Identify root cause
   - Determine affected users/systems

3. **Remediation**
   - Fix vulnerabilities within SLA
   - Apply security patches
   - Update dependencies if needed

4. **Verification**
   - Confirm fix with security scans
   - Test thoroughly in staging
   - Validate no regressions

5. **Communication**
   - Notify users if user-facing
   - Update security documentation
   - Post-mortem analysis

### Contact Information

- **Security Team**: security@tixl.app
- **Maintainers**: @core-team on Discord
- **Emergency**: Create urgent GitHub issue

## Security Documentation

### Required Security Documentation

- **Security Architecture** - System security design
- **Dependency Inventory** - All dependencies with versions
- **Security Test Results** - Automated scan reports
- **Incident Response Plan** - How to handle security incidents
- **Security Contact Information** - Who to contact for security issues

### Security-Specific Wikis

- [Security Implementation Summary](Security/Security-Implementation)
- [NuGet Security Guidelines](Security/NuGet-Security)
- [File I/O Safety Guide](Security/File-IO-Safety)

## Best Practices Summary

### Do's ✅
- Validate all external inputs
- Use secure coding patterns
- Keep dependencies updated
- Run security scans regularly
- Follow principle of least privilege
- Use cryptographic libraries, not custom implementations

### Don'ts ❌
- Never hardcode secrets or credentials
- Don't ignore security warnings
- Never use unsafe code blocks unnecessarily
- Don't skip dependency security reviews
- Don't implement custom cryptography
- Don't delay security fixes

## Getting Help

**Resources:**
- [OWASP Secure Coding Practices](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/)
- [Microsoft Security Documentation](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [GitHub Security Advisories](https://github.com/advisories)

**Support:**
- Security team: security@tixl.app
- Discord: #security channel
- GitHub: Security advisories section

---

**Remember**: Security is everyone's responsibility. When in doubt, ask the security team before implementing potentially risky code.
