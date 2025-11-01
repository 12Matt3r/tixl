# TiXL Issue Labeling System

## Overview

The TiXL issue labeling system provides a standardized taxonomy for categorizing, prioritizing, and managing all types of issues in our GitHub repository. This system ensures consistent communication between team members, contributors, and the community while enabling effective triage and resolution workflows.

## Label Categories

### 1. Issue Type Labels

Used to identify the fundamental category of each issue.

#### Primary Type Labels

| Label | Color | Usage | Examples |
|-------|-------|-------|----------|
| `bug` | d73a4a | Confirmed bugs in functionality | Crash on startup, incorrect rendering, memory leaks |
| `enhancement` | a2eeef | Feature requests and improvements | New operators, UI improvements, API enhancements |
| `documentation` | 0075ca | Documentation-related issues | Missing docs, outdated guides, API reference gaps |
| `question` | d876e3 | Support and help requests | How-to questions, troubleshooting help |
| `security` | ee0701 | Security vulnerabilities or concerns | Input validation issues, privilege escalation |
| `performance` | fbca04 | Performance problems or optimizations | Memory usage, CPU bottlenecks, rendering speed |
| `architecture` | 0366d6 | Architectural decisions or changes | Module boundaries, design patterns, refactoring |
| `testing` | fb8500 | Test-related issues or improvements | Missing tests, test failures, coverage gaps |

#### Secondary Type Labels

| Label | Color | Usage | Examples |
|-------|-------|-------|----------|
| `good-first-issue` | 7057ff | Issues suitable for new contributors | Simple bugs, documentation fixes, minor features |
| `help-wanted` | 008672 | Issues where community help is needed | Feature implementation, testing, documentation |
| `duplicate` | cfd3d7 | Issues that have been reported before | Multiple reports of same bug |
| `wontfix` | fffffff | Issues that won't be addressed | Invalid reports, edge cases, superseded by other features |
| `invalid` | e6e6e6 | Issues that aren't valid or reproducible | User error, configuration problems |

### 2. Module Labels

Used to identify which TiXL module(s) the issue affects, following our architectural boundaries.

#### Module-Specific Labels

| Label | Color | Usage | Examples |
|-------|-------|-------|----------|
| `module:core` | bfd1f0 | Issues affecting TiXL.Core module | Core data types, resource management, mathematical operations |
| `module:operators` | ff9999 | Issues affecting TiXL.Operators module | Operator evaluation, symbol/instance system, plugin architecture |
| `module:gfx` | 99ccff | Issues affecting TiXL.Gfx module | DirectX 12 pipeline, shader compilation, graphics rendering |
| `module:gui` | 99ffcc | Issues affecting TiXL.Gui module | User interface components, ImGui integration, input handling |
| `module:editor` | ffff99 | Issues affecting TiXL.Editor module | Application integration, project management, crash reporting |

#### Cross-Module Labels

| Label | Color | Usage | Examples |
|-------|-------|-------|----------|
| `cross-module` | c7def8 | Issues spanning multiple modules | Integration problems, architectural changes |
| `integration` | ffdd88 | Issues involving multiple system interactions | Core-Operators integration, Gui-Editor coordination |

### 3. Priority Labels

Used to indicate the priority and urgency of issue resolution.

#### Priority Levels

| Label | Color | Usage | Criteria |
|-------|-------|-------|----------|
| `priority:critical` | b60205 | Must be fixed immediately | Security vulnerabilities, data loss, complete system failure |
| `priority:high` | e99695 | Should be fixed soon | Major functionality broken, significant user impact |
| `priority:medium` | f9a825 | Normal development priority | Standard bugs and features, moderate user impact |
| `priority:low` | ccf0f8 | Nice-to-have improvements | Minor issues, cosmetic problems, documentation improvements |

### 4. Status Labels

Used to track the current state of an issue.

#### Status Labels

| Label | Color | Usage | Examples |
|-------|-------|-------|----------|
| `needs-triage` | fbca04 | New issues awaiting initial review | Recently created issues not yet classified |
| `needs-more-info` | fea2cc | Additional information required from reporter | Missing reproduction steps, environment details |
| `ready-for-review` | 0e8a16 | Code changes ready for review | PR created, implementation complete |
| `in-review` | 1f6feb | Actively being reviewed by maintainers | PR under review, code review in progress |
| `blocked` | d73a4a | Cannot proceed due to dependencies | Waiting for other issues/PRs, external dependencies |
| `waiting-for-author` | ee9900 | Waiting for original author action | Changes requested, feedback needed |
| `ready-to-close` | ffffff | Issue resolution complete, ready to close | PR merged, fix implemented and verified |

### 5. Component Labels

Used to identify specific components or features within modules.

#### Graphics Components

| Label | Color | Usage |
|-------|-------|-------|
| `component:rendering` | 8b949e | Graphics rendering pipeline |
| `component:shaders` | 8b949e | Shader compilation and management |
| `component:materials` | 8b949e | Material system and PBR workflows |
| `component:textures` | 8b949e | Texture loading, management, and processing |

#### UI Components

| Label | Color | Usage |
|-------|-------|-------|
| `component:editor-ui` | 8b949e | Main editor interface |
| `component:node-editor` | 8b949e | Node graph visualization and editing |
| `component:properties-panel` | 8b949e | Properties and inspector panels |
| `component:menus-dialogs` | 8b949e | Menu systems and modal dialogs |

#### Operator Components

| Label | Color | Usage |
|-------|-------|-------|
| `component:operator-system` | 8b949e | Core operator architecture |
| `component:evaluation` | 8b949e | Operator evaluation engine |
| `component:slots` | 8b949e | Input/output slot system |
| `component:registry` | 8b949e | Operator discovery and registration |

### 6. Platform/Environment Labels

Used to identify issues specific to certain platforms, configurations, or environments.

#### Platform Labels

| Label | Color | Usage |
|-------|-------|-------|
| `platform:windows` | 1f883d | Windows-specific issues |
| `platform:linux` | 0969da | Linux-specific issues |
| `platform:macos` | 8250df | macOS-specific issues |

#### Configuration Labels

| Label | Color | Usage |
|-------|-------|-------|
| `config:debug` | 0969da | Debug build-specific issues |
| `config:release` | 1f883d | Release build-specific issues |
| `config:x64` | 8250df | 64-bit specific issues |
| `config:x86` | 0e8a16 | 32-bit specific issues |

#### Hardware Labels

| Label | Color | Usage |
|-------|-------|-------|
| `gpu:nvidia` | 76b900 | NVIDIA GPU-specific issues |
| `gpu:amd` | d85131 | AMD GPU-specific issues |
| `gpu:intel` | 0969da | Intel GPU-specific issues |

### 7. Special Purpose Labels

Used for issues with special handling requirements.

#### Community Labels

| Label | Color | Usage |
|-------|-------|-------|
| `community` | 6f42c1 | Issues reported by or affecting community members |
| `hacktoberfest` | ff7b72 | Issues participating in Hacktoberfest |
| `good-first-issue` | 7057ff | Issues suitable for first-time contributors |
| `help-wanted` | 008672 | Issues where community assistance is requested |

#### Release Labels

| Label | Color | Usage |
|-------|-------|-------|
| `release-blocker` | b31f28 | Issues that must be fixed before next release |
| `changelog-needed` | fea2cc | Changes requiring documentation in release notes |
| `version-bump` | fb8500 | Issues requiring version number updates |

#### Process Labels

| Label | Color | Usage |
|-------|-------|-------|
| `needs-design` | ab6721 | Requires UI/UX design input |
| `needs-architecture` | 004a80 | Requires architectural review |
| `needs-security-review` | d73a4a | Requires security assessment |
| `needs-performance-review` | fbca04 | Requires performance analysis |

## Label Usage Guidelines

### Combination Rules

1. **Every issue must have exactly one Type label**
   - Choose the most appropriate primary type (bug, enhancement, documentation, etc.)
   - Never use multiple primary type labels on the same issue

2. **Use multiple relevant labels when applicable**
   - Combine Type + Module + Priority for most issues
   - Add Component labels for specific functionality areas
   - Include Status labels to track progress

3. **Maintain consistency across similar issues**
   - Use the same label combinations for similar problems
   - Reference this document for label selection decisions

### Label Application Workflow

#### For Issue Reporters
1. Choose appropriate Type label based on issue category
2. Select relevant Module labels if you know which components are affected
3. Set Priority based on impact and urgency
4. Add any Component labels for specific functionality

#### For Triagers
1. Review and correct Type label if needed
2. Add or update Module labels based on investigation
3. Set appropriate Priority level based on impact assessment
4. Add Status labels to track triage progress

#### For Developers
1. Update Status labels as work progresses
2. Add Component labels for specific areas being modified
3. Remove labels that no longer apply
4. Add release-related labels when appropriate

## Automated Labeling

### Auto-labeling Rules

GitHub Actions can automatically apply certain labels based on issue characteristics:

- **File Path Analysis**: Automatically add Module labels based on mentioned files
- **Keyword Detection**: Auto-apply Type and Component labels based on issue content
- **Security Scanning**: Automatically flag potential security issues
- **Performance Keywords**: Auto-apply performance labels for memory/CPU/GPU mentions

### Label Validation

Regular validation ensures label consistency:
- Check for issues without required labels
- Identify incorrectly applied labels
- Monitor label usage patterns
- Suggest label improvements

## Label Maintenance

### Regular Reviews

- **Monthly**: Review label usage patterns and effectiveness
- **Quarterly**: Evaluate need for new labels or label consolidation
- **Annually**: Complete review of entire labeling system

### Adding New Labels

To add new labels to the repository:

1. **Propose label** in maintainers meeting or discussion
2. **Provide rationale** for the new label
3. **Define usage guidelines** for the label
4. **Update documentation** to include new label
5. **Test application** with existing issues

### Deprecating Labels

To remove or rename existing labels:

1. **Identify all current uses** of the label
2. **Propose migration plan** for existing issues
3. **Update documentation** to remove references
4. **Execute migration** with maintainers
5. **Delete or rename** the label

## Integration with Other Tools

### Project Boards

Labels integrate with GitHub Projects for workflow management:
- **Status-based columns** using Status labels
- **Priority tracking** using Priority labels
- **Module-based grouping** using Module labels

### Milestones

Labels complement milestone-based organization:
- **Release planning** using Release labels
- **Sprint tracking** using Status labels
- **Feature grouping** using Component labels

### GitHub Actions

Labels trigger automated workflows:
- **Triage automation** based on Type and Priority labels
- **Review assignment** based on Module labels
- **Release preparation** using Release labels

## Best Practices

### For Effective Triage

1. **Always start with Type classification** - ensures proper issue routing
2. **Use consistent Priority assessment** - based on defined criteria
3. **Add Module labels early** - helps with assignment and expertise
4. **Update Status regularly** - keeps workflow visible
5. **Remove obsolete labels** - maintains clarity

### For Contributors

1. **Apply appropriate Type label** when creating issues
2. **Add relevant Module labels** if you understand the codebase
3. **Use Priority labels responsibly** - let maintainers assess urgency
4. **Include Component labels** for specific functionality areas
5. **Avoid over-labeling** - more labels ≠ better organization

### For Maintainers

1. **Review and correct labels** during triage process
2. **Use Status labels** to communicate progress clearly
3. **Monitor label usage patterns** for process improvements
4. **Train contributors** on effective label usage
5. **Maintain documentation** as labeling system evolves

## Label Reference

### Quick Selection Guide

#### For Bugs
```
bug + module:[core|operators|gfx|gui|editor] + priority:[critical|high|medium|low] + status:needs-triage
```

#### For Features
```
enhancement + module:[core|operators|gfx|gui|editor] + component:[specific-component] + priority:[high|medium|low] + good-first-issue (if applicable)
```

#### For Documentation
```
documentation + component:[specific-area] + priority:[medium|low] + help-wanted (if community help needed)
```

#### For Security
```
security + priority:[critical|high] + status:needs-triage + needs-security-review
```

#### For Performance
```
performance + component:[rendering|cpu|memory|gpu] + module:[affected-module] + priority:[high|medium|low]
```

## Troubleshooting

### Common Issues

**Issue without Type label**: Add appropriate Type label immediately
**Multiple Type labels**: Remove all but one most appropriate Type label
**Missing Module label**: Add relevant Module label based on investigation
**Incorrect Priority**: Review impact and adjust Priority label accordingly

### Help and Support

For questions about label usage:
- Reference this document
- Ask in maintainers discussion
- Review existing issues with similar characteristics
- Consult with experienced contributors

---

**This labeling system is maintained by the TiXL maintainers team and evolves based on project needs and community feedback.**