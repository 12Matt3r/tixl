# TiXL Documentation Quality and Developer Experience Audit

## Executive Summary

TiXL (Tooll 3) is an ambitious open-source platform for real-time motion graphics that blends procedural graph-based composition with timeline editing and live-performance features. The project demonstrates notable strengths: clear positioning on its official website, a comprehensive wiki structure that anticipates user and developer needs, an up-to-date installation guide for Windows, and active release notes that double as a functional changelog. Together, these assets set a solid foundation for both users and developers, and they signal a mature, product-minded project with a meaningful user base and community energy[^2][^3][^4][^6].

However, the developer experience is constrained by critical gaps. The GitHub wiki Home page—despite listing extensive sections—fails to load detailed page content, leaving much of the “how-to” and API reference inaccessible to new contributors and power users. Several high-value topics are explicitly marked work-in-progress (WIP), and the operator reference appears incomplete. Additionally, contributor workflow guidance (branch strategy, pull request process, code review conventions, issue triage, and coding conventions) is visible as section titles but the substantive content is not retrievable due to the wiki loading problem[^3]. These gaps manifest as real friction: onboarding blockers (for example, a new user issue reporting “Cannot create a project”), ambiguous naming and specialization of operators, and missing central documentation for special (context) variables—all of which impede productivity and slow contributions[^8][^4].

The recommendations prioritized in this audit focus on (1) fixing wiki loading and stabilizing documentation endpoints, (2) publishing a canonical “Contributing Guide” with a clear developer workflow, (3) completing the operator reference and adding task-focused tutorials for common developer tasks, (4) adding a central “Special Variables” reference linked from operator pages and the developer hub, and (5) codifying naming and conventions. These steps, implemented in a disciplined 30–60–90-day roadmap, will reduce ramp-up time, improve maintainability, and enable confident contributions from both artists and developers[^3][^8][^13].


## Scope, Methodology, and Source Reliability

This audit examines TiXL’s documentation ecosystem with an emphasis on developer onboarding and contribution velocity. The assessment spans the main repository and organization metadata, the GitHub wiki (structure and content availability), the installation guide, releases/changelog, open issues, and project community signals (Discord). Because the GitHub wiki’s Home page presents a robust structure yet repeatedly returns load errors for linked pages, many detailed topics are visible as titles but not assessable for depth or accuracy. The installation guide, however, is accessible and reviewed in full. Releases are used to reconstruct feature momentum, stability indicators, and explicit mentions of documentation-related updates. Open issues are analyzed to surface onboarding pain points and documentation gaps. Video tutorials are referenced as qualitative signals of learning resources and developer guidance[^1][^3][^4][^5][^6][^8][^10][^11][^12][^13][^14].

To orient the reader to the evidence base, the following table inventories the primary sources and what each contributes to this audit.

To illustrate coverage and reliability, Table 1 summarizes the sources used, their type, what they substantiate, and any reliability considerations.

Table 1. Source inventory and reliability considerations
| Source (short title) | Type | What it supports | Reliability notes |
|---|---|---|---|
| Main repository (TiXL) [^1] | Repo | Project scope, tech stack, status, community signals, link hub | High reliability; primary project artifact |
| Official website [^2] | Web | Product positioning, value proposition, target users | High reliability; official marketing site |
| Wiki Home [^3] | Wiki | Documentation structure, presence of sections (titles) | Content loading fails; only structure is visible |
| Installation guide [^4] | Wiki | Windows requirements, dependencies, non-Windows notes | High reliability; accessible and current |
| Releases/Changelog [^6] | Repo | Feature additions, fixes, docs mentions | High reliability; canonical change record |
| Open issues [^8] | Repo | Onboarding friction, docs gaps (variables, naming) | High reliability; direct user/developer feedback |
| Operators list [^15] | Wiki | Operator catalog structure and WIP status | Partial; list visible; deep pages inaccessible |
| Art-Net/DMX [^16] | Wiki | I/O feature documentation reference | Inaccessible during wiki load failures |
| Convert SDFs [^17] | Wiki | Shader conversion topic reference | Inaccessible during wiki load failures |
| Coding conventions [^18] | Wiki | Developer conventions topic | Inaccessible during wiki load failures |
| Operator conventions [^19] | Wiki | Operator authoring standards | Inaccessible during wiki load failures |
| Homepage repo [^20] | Repo | Related site artifacts | High reliability; contextual |
| Resources repo [^21] | Repo | Supporting materials | High reliability; contextual |
| Operators repo [^22] | Repo | Operator code | High reliability; contextual |
| Homepage-alt repo [^23] | Repo | Additional site artifacts | High reliability; contextual |
| Releases download [^24] | Web | Installer for Windows | High reliability; binary artifact |
| Discord [^10] | Community | Support channel | Access constrained; page content not extractable |
| YouTube overview [^11] | Video | Orientation tutorial | High reliability; watchable content |
| Shader Graph deep dive [^12] | Video | Developer-oriented deep dive | High reliability; watchable content |
| HLSL compute shader tutorial [^13] | Video | Custom force development walkthrough | High reliability; watchable content |
| C# operator development [^14] | Video | Operator authoring walkthrough | High reliability; watchable content |

Limitations: The wiki loading failures prevent content-level assessment for most pages. Inline code comments were not reviewed due to repository access constraints. Community channels provide partial visibility into support patterns. These limitations are addressed through triangulation: the installation guide and releases offer reliable ground truth; issues highlight reproducible pain; and video resources indicate learning pathways. Where unknowns remain, they are called out explicitly.


## Project Overview and Documentation Ecosystem Map

TiXL is a real-time motion graphics platform designed for live performance, installations, and VJing. It combines a node-based (operator) graph model, a timeline for keyframing, and strong real-time rendering. The project emphasizes extensibility—supporting custom shaders and input devices—and targets artists and technical artists who want both speed and fine-grained control[^1][^2]. The codebase and repository signals indicate an actively developed product with a meaningful contributor base and an engaged user community[^1].

The documentation ecosystem is organized across several hubs: the main repository (for code, issues, releases, and security policy), the GitHub wiki (for user and developer guides), and the official website (for positioning, quick overview, and links to community channels). A YouTube channel and playlists provide video-based tutorials and deep dives, covering everything from the “Tixl in 15min” overview to detailed sessions on shader graphs and operator development[^1][^2][^3][^11].

To clarify the current state of each documentation artifact, Table 2 summarizes the primary assets, their maturity, and key observations.

Table 2. Documentation artifacts and current state
| Artifact | Purpose | Audience | Completeness | Last known update | Observed issues |
|---|---|---|---|---|---|
| Main repository [^1] | Code, issues, releases, security | Developers | Complete | Active (2025) | None specific to docs; general dev hub |
| GitHub wiki Home [^3] | Entry to all docs | Users, Developers | Partial (structure only) | Home page edited Oct 2025 | Linked pages fail to load; content mostly inaccessible |
| Installation guide [^4] | Setup on Windows, notes for Linux/Mac | Users, Developers | Complete | Edited Sep 2025 | None observed |
| Operators list [^15] | Catalog of operators | Developers | Partial | Ongoing | Marked WIP; deep pages inaccessible |
| Style guides [^18][^19] | Conventions for code and operators | Developers | Partial (titles visible) | Unknown | Content not accessible due to wiki load failure |
| Developer guides (IDE, tests, RenderDoc) [^3] | Setup and advanced tasks | Developers | Partial (titles visible) | Unknown | Content not accessible due to wiki load failure |
| Releases/Changelog [^6] | Changes per version | All | Complete | v4.0.6 (Aug 2024) | None; strong narrative and doc mentions |
| Official website [^2] | Positioning, overview, links | Users | Complete | Active (2025) | None |
| Video tutorials [^11][^12][^13][^14] | Guided learning | Users, Developers | Complete (playlist) | Ongoing | YouTube playlist page access constrained |

Key observation: The wiki’s structure suggests a comprehensive plan for user guidance, operator references, and developer workflows. Yet content accessibility issues prevent new contributors from easily converting intent into action.


## Findings: Documentation Quality by Artifact

Overall, TiXL’s documentation surface area is impressive. The installation guide is clear and practical; releases are detailed and include documentation mentions; the website succinctly captures the value proposition; and the wiki home reveals a well-organized taxonomy of topics. The principal drawback is execution: many wiki pages do not render, leaving core developer and API documentation largely out of reach.

### README and Repo Metadata

The main repository conveys scope, features, and status effectively. It signals active development, points to the wiki, and centralizes issue and PR links. Based on repository metadata, the project is a C# application (with HLSL components), has accrued several thousand stars, and shows a healthy volume of commits and contributors. The README acts as the conventional hub rather than an exhaustive quick start, which is appropriate given that the wiki is intended to host detailed guidance[^1].

### GitHub Wiki: Structure vs. Content Availability

The wiki Home page lays out a coherent structure across user, developer, and community sections. However, during this audit, linked pages consistently fail to load, returning error messages that block content extraction. The implications are straightforward: new contributors cannot validate how-to steps, operators lack browsable reference pages, and developers cannot reliably follow setup or testing procedures. The existence of a page title does not equate to usable documentation[^3].

To make the gap concrete, Table 3 contrasts expected content types against availability as observed.

Table 3. Wiki structure vs. content availability
| Expected page type | Example topics | Expected content | Observed availability |
|---|---|---|---|
| User guide | Timeline usage, rendering videos, presets, live performances | Step-by-step guidance | Titles visible; content inaccessible |
| Developer guide | IDE run, integration tests, RenderDoc, custom operators | Setup, workflow, troubleshooting | Titles visible; content inaccessible |
| API reference | List of Operators (WIP) | Parameters, inputs/outputs, examples | WIP flag visible; details inaccessible |
| Style guides | Coding and operator conventions | Rules and rationale | Titles visible; content inaccessible |
| Community and updates | Meet-up notes, release notes links | Announcements and process | Titles visible; content inaccessible |

The impact is material: users and contributors spend extra time resolving basics through trial-and-error or community channels, slowing adoption and increasing support load[^3][^15][^18][^19].

### Installation and System Requirements

The installation guide is a strength. It specifies Windows 10/11, a DirectX 11.3-capable GPU (GTX970 or later recommended), and states that the installer includes dependencies such as .NET and Windows Graphics Tools. It acknowledges non-Windows efforts via wrappers and links to Linux and macOS pages, and it references IDE-based development for C# operator authoring. This clarity lowers initial friction for Windows users and points developers to the right next step for deeper work[^4][^24].

Table 4. System requirements summary and installation options
| Category | Details |
|---|---|
| Operating system | Windows 10 or 11 |
| GPU | DirectX 11.3 compatible ( GTX970 or later recommended) |
| Dependencies | Included by installer: .NET, Windows Graphic Tools |
| Non-Windows | Wrappers noted; Linux and macOS installation pages referenced |
| Developer path | IDE-based setup for developing/debugging C# operators |

The only notable caveat is the wrapper-based approach for Linux/macOS; until native ports mature, users on those platforms should expect extra steps and potential incompatibilities[^4].

### API and Operator Reference

Operator documentation is explicitly marked WIP, and while a categorized list is visible, detailed parameter and usage pages are not accessible due to the wiki loading failures. The releases, however, show a steady cadence of operator additions and improvements, which heightens the urgency for an up-to-date, browsable operator catalog. Without a reference, developers struggle to discover capabilities, understand parameter semantics, and reuse patterns. A structured operator reference with inputs/outputs, examples, and cross-links to special variables and context concepts would materially improve developer velocity[^6][^15].

### Developer Guides and Tutorials

Titles indicate a comprehensive set of developer guides—IDE run, integration tests, RenderDoc usage, custom operator development, Git workflows, and more—but their content is not accessible in this review. In practice, the project supplements these gaps with high-quality video tutorials: a quick overview, a shader graph deep dive, a tutorial on custom HLSL compute shader forces, and a detailed walkthrough for developing C# operators. This video-first path is valuable, but it should be complemented by stable, searchable, and scannable written guides and references for repeatable developer workflows[^3][^11][^12][^13][^14].

### Releases and Changelog

Release notes are thorough and well-organized by theme (installer, projects and export, magnetic graph, timeline, UI, audio, performance, operators). They occasionally reference documentation updates and new tutorials (for example, a color tutorial), and they announce breaking changes and operator renames, which is critical for migration and trust. The only gap is the absence of consistent, per-release “documentation status” summaries that tie changes to updated wiki pages[^6].

Table 5. Selected releases and documentation mentions
| Version | Date | Documentation-related mentions | Notable developer-facing changes |
|---|---|---|---|
| v4.0.6 | 2024-08-30 | Added “[HowToUseColors] tutorial”; operator updates | Numerous operator additions/fixes; timeline and UI refinements |
| v4.0.4 | 2024-07-06 | Mentions documentation link in the context of improved time-clip editing | Compilation rewrites; new gizmos; keybinding editor |
| v4.0.3 | 2024-06-19 | “Almost all operators now have documentation”; doc fixes; SDF/shader conversion link | Art-Net/DMX control; audio analysis; new operators and examples |
| v4.0.2 | 2024-05-09 | — | UI improvements; MagGraph focus mode; operator additions |
| v4.0.1 | 2024-05-03 | — | Preview release addressing issues from v4.0.0 |
| v4.0.0 | 2024-04-30 | — | Early pre-release milestone |

The pattern is positive: documentation is improving alongside product maturity. Formalizing doc completeness tracking in releases will make this progress transparent to contributors and users[^6].

### Community and Support Channels

The project lists Discord as the primary community and support channel. In practice, Discord is effective for real-time help but does not replace durable, indexed documentation for onboarding and retention. The homepage also links tutorial videos, which serve as both onboarding and advanced learning materials. The risk is that knowledge remains in chat history and videos rather than in text-based references that are searchable and version-controlled[^2][^10][^11].


## Findings: Developer Onboarding and Learning Curve

For Windows users, installation is straightforward and requirements are clear, enabling a fast first run. The main obstacles emerge after installation: without accessible developer guides and a complete operator reference, new contributors lack the map they need to deepen expertise. This gap is visible in open issues that point to onboarding friction and documentation blind spots.

Table 6. Onboarding pain points and evidence
| Pain point | Evidence | Impact |
|---|---|---|
| Project creation fails for new users | Issue #738 “Cannot create a project” | Blocks first value; pushes users to Discord before they can evaluate TiXL[^8] |
| Operator naming and scope ambiguity | Issues #688, #673 propose renames (e.g., [CamPosition]) | Slows discovery and comprehension; increases cognitive load[^8] |
| Missing central documentation for special variables | Issue #681 requests a wiki page for special variables | Developers lack a reference to context and data flow; slows operator authoring[^8] |
| Wiki content largely inaccessible | Wiki Home page structure is visible; linked pages fail to load | Converts simple how-to questions into trial-and-error; raises barrier to contribution[^3] |

Taken together, these signals suggest that while TiXL’s core capabilities are strong, the documentation pipeline between “first install” and “confident developer contributor” is fragile. Community videos bridge part of the gap, but the project would benefit from a stable, cross-referenced written corpus that covers the most common developer tasks end-to-end[^3][^11][^12][^13][^14].


## Findings: Code Documentation Standards and Inline Comments

Although inline code comments were not directly reviewed due to repository access constraints, style guides for coding and operators are visible as wiki sections, indicating an intention to document conventions. What is missing is public, accessible guidance on documentation expectations (for example, XML documentation comments for public APIs/operators), docstring conventions for shaders and operators, and examples policies. Establishing and publishing these standards will improve the operator reference, facilitate auto-generation, and ensure consistency across contributions[^3][^18][^19].


## Findings: Missing or Incomplete Documentation in Critical Areas

Several critical topics are either incomplete or inaccessible, each impeding developer productivity:

- Operator API reference: The operator list is WIP and the detailed pages are inaccessible. Developers need a canonical, searchable catalog with signatures, parameters, and examples[^15].
- Special/context variables: The absence of a central reference leaves operator composition and data flow opaque, especially for custom operators and advanced graphs[^8].
- Contribution workflow: Titles suggest guidelines exist, but substantive pages are not accessible. Contributors need clear expectations for branching, commit style, PR templates, review conventions, and issue triage[^3].
- Build and developer environment: While IDE run and development setup are referenced, content could not be verified. Developers need deterministic steps for cloning, building, running tests, debugging with RenderDoc, and producing releases[^3].
- Non-Windows installation: Linux and macOS instructions are referenced but not verified here. Without tested, up-to-date steps, cross-platform contributors face uncertainty[^4].
- Security, reporting, and governance: Links exist in the repo metadata, but detailed policy content (security advisories, responsible disclosure, code of conduct) was not reviewed in this audit and should be confirmed and published centrally[^1].

Table 7. Critical documentation gaps and remediation priority
| Gap | Current state | Risk | Priority | Proposed remediation |
|---|---|---|---|---|
| Operator API reference | WIP; list visible; details inaccessible | Low discoverability; rework | P0 | Complete operator reference; add examples and cross-links |
| Special/context variables | Requested in issues; no central doc | Operator composition friction | P0 | Publish central reference; link from operator pages |
| Contribution workflow | Titles visible; content inaccessible | Inconsistent contributions | P0 | Create CONTRIBUTING.md with templates and workflow |
| Build/dev environment | Referenced; content inaccessible | Setup failures; lost time | P1 | Write and verify IDE/run/build guides; add troubleshooting |
| Non-Windows install | Pages referenced; unverified | Cross-platform friction | P1 | Update and test Linux/Mac guides; note limitations |
| Security/CoC/reporting | Links exist; content not reviewed | Governance uncertainty | P1 | Publish policies and link in README/wiki |
| Release docs linkage | No per-release doc status | Doc drift vs. code | P1 | Add “Docs updated” checklist to releases |


## Findings: Developer Tools, Build Processes, and Setup

The installation guide明确 points to IDE-based development for C# operators and references a development environment setup page. Scripting artifacts such as build and cleanup batch files are present in the repository structure, but the documentation around prerequisites, cloning, building, running tests, and troubleshooting is not accessible for validation. Similarly, advanced topics such as RenderDoc usage and integration tests are referenced but not verifiable here[^1][^3][^4].

To reduce friction, the project should publish a concise, reproducible “From zero to build” guide that includes:

- Prerequisites and versions (e.g., .NET SDK, Visual Studio/IDE expectations)
- Clone and build steps with expected outputs
- Running tests and loading the project in an IDE
- Debugging and profiling with RenderDoc
- Creating release builds and packaging
- Common pitfalls and troubleshooting

Even without seeing the existing pages, this standard developer readme structure will pay dividends in contributor ramp-up speed[^3].


## Contribution Guidelines and Development Workflow

Titles in the wiki and repository metadata imply the existence of contribution content. Given the wiki loading failures, the safest path is to create a canonical CONTRIBUTING.md file in the repository root that consolidates:

- How to file issues (reproductions, logs, environment)
- Branching model (e.g., main + feature branches; protected main)
- Commit conventions (Conventional Commits or similar)
- PR templates (change type, screenshots, doc impact)
- Code review expectations and owner approvals
- Coding and operator conventions with links to wiki details
- Response-time expectations and triage labels

This single file should be short, action-oriented, and cross-link to the wiki for depth. Doing so will normalize expectations and shorten the path from idea to merged contribution[^3][^18][^19].


## Prioritized Recommendations and 30–60–90 Day Roadmap

The following actions are prioritized to deliver fast user impact and sustained contributor velocity.

Table 8. Prioritized actions
| Item | Rationale | Audience | Effort | Dependencies | Expected impact |
|---|---|---|---|---|---|
| Fix wiki loading; audit all links | Restore access to user/developer guides | Users, Devs | Medium | Wiki platform | Immediate enablement of existing docs |
| Publish CONTRIBUTING.md with templates | Normalize workflow; reduce back-and-forth | Contributors | Low | Maintainer agreement | Fewer PR blocks; faster merges |
| Complete operator reference (WIP) | Enable discovery and reuse; reduce confusion | Developers | Medium-High | Subject-matter reviews | Faster development; fewer “how-to” questions |
| Add “Special Variables” reference | Clarify context and data flow for operators | Developers | Medium | Design note review | Reduced friction in operator authoring |
| Verify/update dev environment docs | Ensure reproducible builds and debug | Contributors | Medium | CI/SDK availability | Lower setup time; fewer issues |
| Add “Docs updated” to release notes | Make doc drift explicit | All | Low | Release process ownership | Transparency; community trust |
| Non-Windows install verification | Support cross-platform contributors | Users, Devs | Medium | Testing on Linux/Mac | Broaden contributor base |
| Governance docs (CoC, security) | Codify community norms and safety | All | Low | Maintainer consensus | Improved onboarding; risk reduction |

Table 9. 30–60–90 day roadmap
| Timeline | Milestones | Success metrics | Owner(s) |
|---|---|---|---|
| 30 days | Fix wiki loading; publish CONTRIBUTING.md; open “Special Variables” drafting; add release-notes doc checklist | Wiki pages accessible; CONTRIBUTING.md merged; one draft variable page | Docs maintainer + core dev |
| 60 days | Complete priority operator pages (top 20 by usage); verify dev environment guide; publish release-notes doc updates for next 2 releases | 20 operator pages with examples; dev guide tested by 2 new contributors; docs tracked in 2 releases | Dev rel lead + docs |
| 90 days | Close WIP on operator reference; finalize special variables; cross-link all operator pages to variables; verify Linux/Mac install guides | Operator reference complete; variable doc cross-linked; cross-platform guides verified | Core team + community |


## Appendices

### Appendix A: Release highlights and documentation mentions

Table 10. Selected release details with developer relevance
| Version | Date | Feature highlights | Documentation mentions | Developer impact |
|---|---|---|---|---|
| v4.0.6 | 2024-08-30 | Installer, project/export, magnetic graph, timeline, UI, performance, operators | Added “[HowToUseColors] tutorial” | Faster feature adoption via tutorials |
| v4.0.4 | 2024-07-06 | Compilation rewrites, gizmos, timeline improvements | Mentions documentation link for time-clip editing | More stable operator UIs; better editing |
| v4.0.3 | 2024-06-19 | Art-Net/DMX, audio analysis, new operators | “Almost all operators now have documentation”; spelling fixes; SDF conversion guide | Better lighting control; improved docs; shader conversion path |
| v4.0.2 | 2024-05-09 | MagGraph focus mode, UI improvements | — | Improved developer ergonomics |
| v4.0.1 | 2024-05-03 | Preview addressing issues | — | Stabilization |
| v4.0.0 | 2024-04-30 | Early pre-release | — | Initial v4 baseline |

Source: Releases/Changelog[^6].

### Appendix B: Evidence ledger

Table 11. Evidence ledger
| Claim | Source | Evidence location | Verification notes |
|---|---|---|---|
| Wiki has rich structure but content is inaccessible | Wiki Home [^3] | Section titles visible; linked pages fail to load | Consistent with extraction failures |
| Installation guide is clear and current | Installation guide [^4] | Windows requirements; dependencies; non-Windows notes | Page accessible and up-to-date |
| Release notes mention documentation improvements | Releases/Changelog [^6] | v4.0.3 (“almost all operators now have documentation”), v4.0.6 (color tutorial) | Direct mentions in notes |
| Onboarding blockers exist | Open issues [^8] | #738 “Cannot create a project” | Evidence of first-run friction |
| Missing central documentation for special variables | Open issues [^8] | #681 request for a wiki page | Clear developer need |
| Operator list is WIP | Operators list [^15] | WIP label | Confirms incompleteness |
| Developer topic titles exist but content inaccessible | Wiki Home [^3] | Titles for IDE run, tests, RenderDoc, Git, operator dev | Access blocked by wiki failures |
| Video tutorials support developer learning | YouTube videos [^11][^12][^13][^14] | Overview, shader graph, HLSL compute, C# operator dev | Accessible and high-quality |
| Non-Windows installation referenced but unverified | Installation guide [^4] | Linux/Mac pages referenced | Requires follow-up verification |
| Governance links exist but content not reviewed | Main repo [^1] | Issues/PRs/security links | Requires content audit |

### Appendix C: Glossary

- Operator: A node in TiXL’s graph that performs a specific operation (e.g., image transform, math, field generation). Operators can have inputs, outputs, and parameters. The operator library is categorized (e.g., Lib.image, Lib.render) and a list is available, though the full reference is WIP[^15].  
- Special variables (context variables): Named values supplied by the runtime or project that can be accessed by operators to drive behavior (e.g., camera properties, time, audio metrics). A central documentation page is needed to describe names, types, scopes, and usage patterns; this is explicitly requested by maintainers[^8].  
- MagGraph (magnetic graph): The node graph canvas with “magnetic” connection behaviors and interaction modes introduced to improve authoring ergonomics (e.g., focus mode, snapping). Multiple releases refine this experience[^6].  
- TimeClips: Timeline constructs that encapsulate animation segments, enabling block-based editing and composition on the timeline; ongoing improvements target stability and usability[^6].  
- Fields and SDF (signed distance function): Mechanisms for procedural modeling and effects, including raymarching and shader conversion workflows; releases add operators and conversion guidance[^6][^17].


## References

[^1]: tixl3d/tixl: Tooll 3 – GitHub. https://github.com/tixl3d/tixl  
[^2]: TiXL Official Website. https://tixl.app  
[^3]: TiXL Wiki Home. https://github.com/tixl3d/tixl/wiki  
[^4]: Installation Guide (TiXL v4.x). https://github.com/tixl3d/tixl/wiki/help.Installation  
[^5]: TiXL GitHub Organization. https://github.com/tixl3d  
[^6]: Releases · tixl3d/tixl. https://github.com/tixl3d/tixl/releases  
[^8]: Issues · tixl3d/tixl. https://github.com/tixl3d/tixl/issues  
[^9]: Pull Requests · tixl3d/tixl. https://github.com/tixl3d/tixl/pulls  
[^10]: TiXL Discord Community. https://discord.gg/YmSyQdeH3S  
[^11]: TiXL Overview Tutorial – YouTube. https://www.youtube.com/watch?v=eH2E02U6P5Q  
[^12]: TiXL Shader Graph Deep Dive – YouTube. https://www.youtube.com/watch?v=6-8PKxPztl8  
[^13]: Tooll3 HLSL Compute Shader Tutorial – YouTube. https://www.youtube.com/watch?v=j95VZXGAbwE  
[^14]: Tooll3 C# Operator Development – YouTube. https://www.youtube.com/watch?v=JesK2jtc99w  
[^15]: List of Operators (WIP). https://github.com/tixl3d/tixl/wiki/List-of-Operators  
[^16]: Art-Net/DMX Documentation. https://github.com/tixl3d/tixl/wiki/help.ArtnetAndDMX  
[^17]: Converting SDFs. https://github.com/tixl3d/tixl/wiki/help.ConvertSDFs  
[^18]: Coding Conventions. https://github.com/tixl3d/tixl/wiki/CodingConventions  
[^19]: Operator Conventions. https://github.com/tixl3d/tixl/wiki/OperatorConventions  
[^20]: tixl3d/homepage – GitHub. https://github.com/tixl3d/homepage  
[^21]: tixl3d/Resources – GitHub. https://github.com/tixl3d/Resources  
[^22]: tixl3d/Operators – GitHub. https://github.com/tixl3d/Operators  
[^23]: tixl3d/tixl-homapage – GitHub. https://github.com/tixl3d/tixl-homapage  
[^24]: TiXL v4.0.6.3 Installer (Windows). https://github.com/tixl3d/tixl/releases/download/v4.0.6.1/Tixl-v4.0.6.3.exe