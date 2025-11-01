# Wiki Migration and Deployment Guide

## Overview

This guide provides step-by-step instructions for migrating the organized TiXL wiki structure to GitHub and deploying the automation scripts for ongoing maintenance.

## Pre-Migration Checklist

- [ ] Review wiki structure in `/workspace/wiki-structure/`
- [ ] Test automation scripts with sample content
- [ ] Prepare GitHub repository for wiki upload
- [ ] Coordinate with team for migration timeline
- [ ] Create backup of existing wiki content

## Migration Steps

### Phase 1: Content Preparation

1. **Review Wiki Structure**
   ```bash
   cd /workspace/wiki-structure
   find . -name "*.md" | head -20  # Review organized content
   ```

2. **Validate Content Quality**
   ```bash
   ./scripts/validate-wiki-links.sh .
   ./scripts/check-accessibility.sh .
   ```

3. **Generate Link Map**
   ```bash
   ./scripts/generate-link-map.sh .
   ```

### Phase 2: GitHub Wiki Deployment

1. **Create Wiki Repository**
   - Navigate to GitHub repository: `https://github.com/tixl3d/tixl`
   - Click "Wiki" tab
   - Click "Create the first page" if empty, or edit existing

2. **Upload Core Structure**
   Upload files in this order:
   ```
   1. Home.md (Main landing page)
   2. Getting-Started/ directory (Critical for onboarding)
   3. Architecture/ directory (Essential for developers)
   4. Security/ directory (Important for compliance)
   5. Search-Optimization.md (Maintenance guide)
   ```

3. **Upload Remaining Content**
   - Operators/ directory
   - User-Guide/ directory
   - Community/ directory
   - Research-Analysis/ directory
   - Implementation/ directory
   - Tools-Automation/ directory
   - Best-Practices/ directory

### Phase 3: Automation Setup

1. **Deploy Scripts to Repository**
   ```bash
   # Add scripts to repository
   mkdir -p .github/wiki-scripts
   cp scripts/* .github/wiki-scripts/
   git add .github/wiki-scripts/
   git commit -m "docs: add wiki maintenance scripts (TIXL-066)"
   git push
   ```

2. **Set Up GitHub Actions**
   Create `.github/workflows/wiki-maintenance.yml`:
   ```yaml
   name: Wiki Maintenance
   on:
     schedule:
       - cron: '0 0 * * 1'  # Weekly on Monday
     workflow_dispatch:
   
   jobs:
     wiki-health-check:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v2
           with:
             repository: 'tixl3d/tixl.wiki'
             token: ${{ secrets.GITHUB_TOKEN }}
             path: wiki
         
         - name: Validate Links
           run: |
             cd wiki
             bash .github/wiki-scripts/validate-wiki-links.sh .
         
         - name: Check Content Freshness
           run: |
             cd wiki
             bash .github/wiki-scripts/content-freshness-check.sh .
         
         - name: Accessibility Check
           run: |
             cd wiki
             bash .github/wiki-scripts/check-accessibility.sh .
         
         - name: Generate Link Map
           run: |
             cd wiki
             bash .github/wiki-scripts/generate-link-map.sh .
         
         - name: Commit Reports
           run: |
             git config --local user.email "action@github.com"
             git config --local user.name "GitHub Action"
             git add .
             git diff --quiet && git diff --staged --quiet || (
               git commit -m "docs: weekly wiki health check"
               git push
             )
   ```

### Phase 4: Team Training

1. **Content Creation Guidelines**
   - Train team on new structure
   - Provide content templates
   - Establish review process

2. **Script Usage Training**
   - Demonstrate automation scripts
   - Show how to interpret reports
   - Establish maintenance schedules

## Quality Assurance Process

### Pre-Deployment Testing

1. **Link Validation**
   ```bash
   ./scripts/validate-wiki-links.sh wiki-structure/
   ```
   Expected: <1% broken links

2. **Content Freshness Check**
   ```bash
   ./scripts/content-freshness-check.sh wiki-structure/
   ```
   Expected: All critical content <90 days old

3. **Accessibility Compliance**
   ```bash
   ./scripts/check-accessibility.sh wiki-structure/
   ```
   Expected: WCAG 2.1 AA compliance

4. **Internal Link Mapping**
   ```bash
   ./scripts/generate-link-map.sh wiki-structure/
   ```
   Expected: No orphaned pages

### Post-Deployment Monitoring

1. **Weekly Health Checks**
   - Automated GitHub Actions run
   - Reports generated and committed
   - Issues logged in repository

2. **Monthly Reviews**
   - Manual review of link map
   - Content gap analysis
   - User feedback integration

## Maintenance Schedule

### Daily (Automated)
- Link validation in CI/CD
- Broken link detection
- Accessibility monitoring

### Weekly (Automated)
- Content freshness check
- Internal link analysis
- Report generation and review

### Monthly (Manual)
- Content gap analysis
- User satisfaction review
- Structure optimization

### Quarterly (Manual)
- Comprehensive accessibility audit
- Search effectiveness review
- Major structural improvements

## Troubleshooting

### Common Issues and Solutions

#### Broken Links After Migration
**Problem**: Internal links don't work in GitHub wiki
**Solution**: 
1. Check link format: `[text](filename)` (no paths)
2. Use relative links between wiki pages
3. Update links to use GitHub wiki format

#### Accessibility Issues
**Problem**: Missing H1 headers or poor structure
**Solution**:
1. Ensure each page starts with `# Page Title`
2. Add table of contents for pages >100 lines
3. Use descriptive link text

#### Content Not Discoverable
**Problem**: Users can't find information
**Solution**:
1. Review internal link map
2. Add more cross-references
3. Improve search terms in content

### Script Error Handling

#### Permission Errors
```bash
# Fix script permissions
chmod +x scripts/*.sh
```

#### Path Issues
```bash
# Ensure scripts run from correct directory
cd wiki-structure
../scripts/validate-wiki-links.sh .
```

#### GitHub Actions Failures
- Check script output logs
- Verify repository permissions
- Ensure scripts are executable

## Success Metrics

### Content Quality
- **Link Accuracy**: >99%
- **Content Freshness**: <10% stale content (>180 days)
- **Accessibility**: 100% WCAG 2.1 AA compliance
- **Search Effectiveness**: <3 clicks to any information

### User Experience
- **Navigation Success**: >95% users find information
- **Time to Information**: <2 minutes average
- **Content Satisfaction**: >90% positive feedback

### Maintenance Efficiency
- **Automated Coverage**: 100% of validation processes
- **Manual Maintenance**: <2 hours per week
- **Issue Resolution**: <24 hours for broken links

## Rollback Plan

### Emergency Rollback
1. **Backup Current Wiki**
   ```bash
   # Download current wiki as backup
   git clone https://github.com/tixl3d/tixl.wiki.git backup-wiki
   ```

2. **Restore Previous Version**
   ```bash
   git checkout previous-commit-hash
   git push --force
   ```

3. **Investigate Issues**
   - Review automation reports
   - Identify root cause
   - Plan fix strategy

### Partial Rollback
1. **Identify Problem Areas**
   - Use validation reports to locate issues
   - Target specific categories or pages

2. **Fix and Redeploy**
   - Apply targeted fixes
   - Retest affected areas
   - Redeploy with monitoring

## Contact Information

### Team Contacts
- **Documentation Team**: docs@tixl.app
- **DevOps Team**: devops@tixl.app
- **Security Team**: security@tixl.app

### Emergency Contacts
- **Project Maintainers**: @tixl-maintainers
- **Critical Issues**: Create urgent GitHub issue
- **Wiki Emergency**: #wiki-support on Discord

---

## Appendix: Script Usage Reference

### validate-wiki-links.sh
```bash
# Basic usage
./scripts/validate-wiki-links.sh /path/to/wiki

# Generate detailed report
./scripts/validate-wiki-links.sh /path/to/wiki --detailed
```

### content-freshness-check.sh
```bash
# Check all content
./scripts/content-freshness-check.sh /path/to/wiki

# Check specific categories
./scripts/content-freshness-check.sh /path/to/wiki --category "Getting-Started"
```

### generate-link-map.sh
```bash
# Generate complete link map
./scripts/generate-link-map.sh /path/to/wiki

# Generate only orphaned pages report
./scripts/generate-link-map.sh /path/to/wiki --orphaned-only
```

### check-accessibility.sh
```bash
# Full accessibility audit
./scripts/check-accessibility.sh /path/to/wiki

# Check only critical issues
./scripts/check-accessibility.sh /path/to/wiki --critical-only
```

---

**Migration Guide Version**: 1.0
**Last Updated**: November 2, 2024
**Maintained By**: TiXL Documentation Team
