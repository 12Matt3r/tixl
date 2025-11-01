# Security Disclosure Process

## Reporting Security Vulnerabilities

The TiXL community takes the security of our software seriously. We appreciate your efforts to responsibly disclose vulnerabilities to us.

## How to Report a Security Issue

### Contact Information

**Primary Contact**: security@tixl-project.org  
**Alternative Contact**: maintainers@tixl-project.org  
**GitHub Security Advisory**: [Report via GitHub](https://github.com/tixl3d/tixl/security/advisories/new)

### What to Include in Your Report

When reporting a security vulnerability, please include:

1. **Type of issue** (e.g., buffer overflow, SQL injection, cross-site scripting, etc.)
2. **Full paths of source file(s) related to the manifestation of the issue
3. **The location of the affected source code** (tag/branch/commit or direct URL)
4. **Any special configuration required to reproduce the issue**
5. **Step-by-step instructions to reproduce the issue**
6. **Proof-of-concept or exploit code** (if possible)
7. **Impact of the issue**, including how an attacker might exploit the issue

### Response Timeline

We are committed to responding to security reports within the following timelines:

- **Initial Response**: Within 48 hours of receiving your report
- **Triage Assessment**: Within 7 days
- **Fix Development**: Within 30 days for critical issues, 90 days for non-critical
- **Public Disclosure**: Coordinated disclosure after fix is available

### Severity Classification

We classify security issues using the following severity levels:

#### Critical (P0)
- Remote code execution
- Authentication bypass
- Privilege escalation to system level
- Data exfiltration of sensitive user information

#### High (P1)
- Local privilege escalation
- Data corruption or loss
- Authentication bypass (partial)
- Information disclosure

#### Medium (P2)
- Cross-site scripting (XSS)
- Cross-site request forgery (CSRF)
- File inclusion vulnerabilities
- Denial of service attacks

#### Low (P3)
- Information disclosure (non-sensitive)
- Configuration issues
- Weak cryptography (no immediate exploitation)

### Coordinated Disclosure

We believe in coordinated disclosure and will work with security researchers to:

1. Acknowledge receipt of vulnerability reports within 48 hours
2. Provide a timeline for fixes based on severity
3. Keep researchers informed of our progress
4. Credit researchers appropriately (unless they prefer anonymity)
5. Give researchers adequate time to verify fixes before public disclosure

### Safe Harbor

We consider security research conducted under this policy to be:

- Authorized in accordance with applicable laws
- Exempt from DMCA takedown notices
- Exempt from circumvention provisions of applicable laws
- Legitimate and conducted in good faith

### Security Updates and Notifications

We will notify users about security updates through:

- [GitHub Security Advisories](https://github.com/tixl3d/tixl/security/advisories)
- Release notes in GitHub releases
- Discord announcements (#announcements channel)
- Email notifications to security mailing list (when available)

### Bug Bounty Program

Currently, TiXL does not have a formal bug bounty program. However, we greatly appreciate security research and may provide:

- Public acknowledgment in security advisories
- Special contributor recognition
- Invitation to join security research group
- Swag or other non-monetary recognition (when available)

### Third-Party Dependencies

When reporting vulnerabilities, please note if the issue affects:

- Direct dependencies (listed in project files)
- Transitive dependencies
- Build tools or CI/CD infrastructure
- Documentation or website infrastructure

### Exclusions

The following are explicitly excluded from our security program:

- Vulnerabilities in dependencies that are not maintained by TiXL
- Issues in development or preview versions that are clearly marked as such
- Denial of service attacks that require excessive resources
- Issues that require physical access to a user's machine
- Social engineering attacks

## Security Best Practices for TiXL Users

### General Security Recommendations

1. **Keep TiXL Updated**: Always use the latest stable release
2. **Verify Downloads**: Download TiXL only from official sources
3. **Network Security**: Be cautious when using network operators (OSC, NDI, etc.)
4. **File Security**: Validate project files from untrusted sources
5. **Plugin Security**: Review third-party operators before installation

### Network Operator Security

When using network-based operators:

- Use secure networks whenever possible
- Implement proper firewall rules
- Monitor network traffic in production environments
- Use authentication when available
- Keep network equipment updated

### File and Project Security

- Validate project files from external sources
- Use antivirus software on shared systems
- Backup projects regularly
- Consider encryption for sensitive project files

## Contact Information

For questions about this security policy or to report security issues:

- **Email**: security@tixl-project.org
- **GitHub Security**: [Security Advisories](https://github.com/tixl3d/tixl/security/advisories)
- **Discord**: #security channel in our community Discord

## References

- [OWASP Vulnerability Disclosure](https://owasp.org/www-project-vulnerability-disclosure-cheat-sheet/)
- [GitHub Security Advisories](https://docs.github.com/en/code-security/security-advisories)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)

---

*This security disclosure process is based on industry best practices and adapted for the TiXL community. We regularly review and update this process to ensure it remains effective.*