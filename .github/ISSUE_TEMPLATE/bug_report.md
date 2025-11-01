---
name: Bug Report
about: Create a report to help us improve TiXL
title: '[BUG] '
labels: ['bug', 'needs-triage']
assignees: ''
---

<!-- 
Please read our CONTRIBUTING_GUIDELINES.md for detailed reporting instructions.

Before submitting, please check if you've:
1. Searched existing issues to avoid duplicates
2. Reviewed our troubleshooting guide
3. Confirmed this is a bug in the latest stable version
-->

## Bug Description

**A clear and concise description of what the bug is.**

<!-- Example: The RenderQuad operator crashes when connecting a null texture input, causing the entire node graph to freeze. -->

## To Reproduce

**Steps to reproduce the behavior:**
1. Go to '...'
2. Click on '....'
3. Connect '....'
4. See error

<!-- Provide step-by-step instructions with specific operator names, settings, and input values. Include exact mouse clicks, keyboard actions, and parameter values. -->

### Minimal Reproduction Project

**Please attach or link to a minimal reproduction project that demonstrates the issue:**

<!--
- Create a new TiXL project with minimal setup
- Save the .tixl project file and any required resources
- Ensure the project can be opened and the issue reproduced
-->

## Expected Behavior

**A clear description of what you expected to happen.**

<!-- Example: The operator should handle null inputs gracefully by logging a warning and skipping rendering, without freezing the node graph. -->

## Actual Behavior

**What actually happened instead.**

<!-- Include exact error messages, stack traces, or description of unexpected behavior. -->

### Error Messages

```
Please paste any error messages, stack traces, or logs here.
```

### Screenshots/Recordings

<!-- If applicable, add screenshots or screen recordings to help explain the issue. -->

## Environment Information

**Please complete the following environment details:**

- **Operating System:** [e.g., Windows 11 22H2, Windows 10 21H2, etc.]
- **TiXL Version:** [e.g., v4.2.1.0, latest development build]
- **Build Type:** [e.g., Release x64, Debug, or git commit hash]
- **.NET Version:** [e.g., .NET 9.0.0, .NET 8.0.x]
- **GPU:** [e.g., RTX 3070, GTX 970, Intel UHD 620]
- **GPU Driver Version:** [e.g., NVIDIA 537.13, AMD 23.5.2]

### Graphics Configuration

- **Graphics API:** [e.g., DirectX 12, OpenGL]
- **Display Resolution:** [e.g., 1920x1080, 3840x2160]
- **Refresh Rate:** [e.g., 60Hz, 144Hz]

### System Performance

- **RAM:** [e.g., 16GB, 32GB]
- **CPU:** [e.g., Intel i7-12700K, AMD Ryzen 7 5800X]

## Module Classification

**Based on the architectural boundaries defined in TiXL, which module(s) does this issue affect?**

<!-- Select all that apply and provide reasoning -->
- [ ] **Core** - Core engine functionality, data types, resource management
- [ ] **Operators** - Operator system, evaluation, plugin architecture
- [ ] **Gfx** - Graphics pipeline, DirectX 12, shader management
- [ ] **Gui** - User interface, ImGui components, input handling
- [ ] **Editor** - Application orchestration, project management

## Issue Severity

**How does this bug impact your workflow or use case?**

- [ ] **Critical** - Complete application crash, data loss, or security vulnerability
- [ ] **High** - Major functionality broken, significant performance degradation
- [ ] **Medium** - Feature not working as expected, workaround available
- [ ] **Low** - Minor inconvenience, cosmetic issue, or documentation problem

## Frequency & Reproducibility

**How often does this issue occur?**

- [ ] **Always** - Happens every time with the same steps
- [ ] **Often** - Happens frequently but not always
- [ ] **Sometimes** - Occasional occurrence under specific conditions
- [ ] **Rarely** - Very rare, hard to reproduce
- [ ] **One-time** - Happened once, cannot reproduce

**Can you reproduce this issue on different systems?**

- [ ] **Yes** - Reproduced on multiple machines/configurations
- [ ] **No** - Only occurs on specific setup
- [ ] **Unknown** - Not tested on other systems

## Additional Context

**Any other relevant context, information, or workarounds:**

<!--
- Has this issue existed since a specific TiXL version?
- Does it occur with specific project types or configurations?
- Are there any known workarounds?
- Does it affect specific GPU vendors or models?
- Any patterns you notice (time of day, specific operations, etc.)?
-->

## Log Files

**Please attach relevant log files if available:**

<!--
- TiXL application logs (usually in %APPDATA%/TiXL/logs/)
- Debug output from the console
- Graphics driver debug logs
- Performance profiler data if related to performance issues
-->

## Attempted Solutions

**What have you already tried to resolve this issue?**

<!--
- Restarted TiXL
- Updated GPU drivers
- Tried different project files
- Checked for known issues in documentation
- Tested with clean project file
-->

---

## For Contributors

**If you're submitting this bug report as a TiXL contributor, please also include:**

### Development Environment

- **IDE:** [e.g., Visual Studio 2022, VS Code, Rider]
- **Branch:** [e.g., main, develop, feature/branch-name]
- **Build Configuration:** [e.g., Debug, Release, with specific flags]

### Additional Investigation

**What have you already done to investigate this issue?**

- [ ] Checked architectural boundaries for violations
- [ ] Reviewed related code changes in recent commits
- [ ] Attempted to fix but need guidance
- [ ] Created failing test case
- [ ] Profiling or performance analysis completed

**Related PRs/Issues:**
<!-- Link to any related pull requests or issues that might be relevant -->

---

**Thank you for helping improve TiXL!** 🙏

Your detailed bug report helps us identify, reproduce, and fix issues more quickly, making TiXL more stable for everyone.