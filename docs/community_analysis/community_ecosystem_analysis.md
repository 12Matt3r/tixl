# TiXL (Tooll 3) Community Engagement and Ecosystem Development: Analysis and Growth Strategy

## Executive Summary

TiXL (Tooll 3) is an open-source platform for real-time motion graphics that sits at the intersection of artist-friendly node-based creation and developer-friendly extensibility. The project already exhibits healthy foundations: a modern C#/.NET codebase, an operator-driven plugin architecture, practical live-show integrations (MIDI, OSC, Spout, NDI, ArtNet/DMX, webcam), a visible GitHub presence, and a welcoming community posture anchored by Discord, tutorials, and workshops. With v4.0.6 identified as the latest stable release and a steady cadence of pre-releases and alphas, TiXL is technically vibrant and gaining momentum. However, gaps in governance artifacts, contributor onboarding, and structured community metrics present near-term risks to scalability and sustainability.[^2][^4]

The market context is favorable. Motion graphics software is forecast to more than double from 2024 to 2033, growing at an 11.4% compound annual growth rate (CAGR), with cloud deployment accelerating adoption and North America representing the largest regional share today. This trendline favors free, open-source tools that can iterate quickly and meet creators where they are—onstage, in clubs, in studios, and in classrooms.[^11] Against established competitors such as Resolume and TouchDesigner, TiXL’s differentiators—open-source permissiveness, a graph + shader pipeline, and a growing set of real-time I/O operators—position it credibly, provided the project invests in a more explicit ecosystem story (plugins, SDK, examples) and a repeatable content and event strategy.[^7][^8][^9]

Key findings:
- Community engagement is active and grounded in Discord and YouTube, with workshops adding important in-person depth; documentation and issues are visible on GitHub. What is missing are metrics instrumentation (e.g., DAU/MAU in Discord, contributor funnel analytics) and structured programs (e.g., ambassador roles, recurring shows, release parties) to drive retention and participation.[^1][^2][^3][^6]
- The operator ecosystem and integrations are a strength: MIDI, OSC, Spout, NDI, ArtNet/DMX, and webcam operators are in active use and evolving. To accelerate third-party additions, TiXL would benefit from formalizing an operator plugin Software Development Kit (SDK), packaging guidelines, a curated gallery, and a review process for community operators.[^2][^4]
- Contribution health is conceptually open but operationally under-documented. The project lacks a CONTRIBUTING.md, Code of Conduct (CoC), and issue/PR templates. Adopting these artifacts and a conventional branching model would lower friction and improve throughput, in line with community health best practices.[^2][^10]
- Release management demonstrates clarity around pre-releases and change communication, including explicit warnings about breaking changes in preview series. The next step is to define semantic versioning (SemVer), codify backporting, and publish a formal release policy and changelog style guide.[^4][^10]
- Sustainability is supported by development velocity and contributor acknowledgments, but governance clarity, maintainer capacity planning, and diversified funding are not yet formalized. Introducing lightweight governance, metrics dashboards, and sponsor tiers will help TiXL mature and endure.[^2][^4][^11][^13][^15]
- Licensing under MIT is strong for adoption and commercial use. To protect the trademark “TiXL” while preserving the open-source ethos, the project should clarify trademark usage and contributor intellectual property (IP) expectations (e.g., developer certificate of origin).[^2]

Top eight recommendations prioritized by impact and feasibility:
1) Publish governance essentials: CONTRIBUTING.md, Code of Conduct, issue/PR templates, and security disclosure process (Impact: High; Effort: Low; Owner: Maintainers; Timeline: 30 days).[^10]  
2) Launch a Discord engagement program: weekly office hours, “Show & Tell,” release parties, and roles (Impact: High; Effort: Low–Medium; Owner: Community team; Timeline: 30–60 days).[^1]  
3) Stand up a plugin/operator SDK hub: quick-start, API stubs, samples, and publishing steps (Impact: High; Effort: Medium; Owner: Devrel + Maintainers; Timeline: 60–90 days).[^2][^4]  
4) Curate an Examples Gallery: filter by use case and integration, with contributing guidelines and contributor credits (Impact: High; Effort: Low–Medium; Owner: Devrel + Community; Timeline: 60–90 days).[^4]  
5) Release policy and SemVer: adopt SemVer, codify backporting, document breaking-change policy, and standardize release notes (Impact: Medium–High; Effort: Low; Owner: Maintainers; Timeline: 30 days).[^4][^10]  
6) Metrics instrumentation: issue-metrics GitHub Action, basic Discord analytics, and community health dashboard (Impact: Medium; Effort: Low–Medium; Owner: Maintainers + Devrel; Timeline: 60 days).[^12][^13][^14]  
7) Content cadence and events: monthly tutorial drops, quarterly workshops, and a yearly community challenge (Impact: Medium; Effort: Medium; Owner: Devrel + Community; Timeline: 60–120 days).[^3]  
8) Funding and sponsorship: Open Collective (one-time and monthly tiers), hardware lending library, and micro-grants (Impact: Medium–High; Effort: Medium; Owner: Maintainers + Finance; Timeline: 90–120 days).[^15]

90-day roadmap:
- Days 1–30: Publish governance files; define release policy and SemVer; stand up issue metrics; launch weekly Discord programming.  
- Days 31–60: Publish operator SDK and examples gallery; instrument Discord analytics; pilot micro-grants; run a release party and “Show & Tell.”  
- Days 61–90: Formalize maintainer roles and triage service-level agreements (SLAs); sponsor tiers; event series in two regions; publish community health dashboard.

KPIs to track:
- Community growth and activation: Discord MAU and weekly engagement; tutorial views and completion; workshop attendance.  
- Contribution health: new contributors/month, good first issues resolved, PR throughput and median review time, median issue first response time.  
- Release and quality: release frequency, changelog completeness, breaking-change notice lead time, regression rate post-release.  
- Ecosystem depth: number of curated third-party operators and integrations, SDK adoption proxies (stars, forks, samples contributed).

Information gaps to monitor:
- Verified Discord member count (1,500+ invite link vs. 2,516 snippet) and engagement rates.  
- Release management policy details (SemVer usage, backporting rules, release cadence targets).  
- Contributor roster transparency beyond release acknowledgments.  
- Governance and security process documentation.  
- Plugin/operator packaging, signing, and distribution guidelines.  
- Trademark policy for “TiXL.”

These initiatives will move TiXL from a technically strong project to a community-powered ecosystem with predictable cadence, developer clarity, and diversified participation—key conditions for sustained growth in a competitive and expanding market.[^2][^3][^4][^11]

## Methodology and Scope

This analysis synthesizes public sources, including TiXL’s official website and documentation, its GitHub repository and release history, the TiXL Discord invitation, and comparator ecosystems (Resolume and TouchDesigner). We reviewed community channels, extensibility signals from operators and I/O integrations, and release and governance artifacts where available. The review also draws on widely accepted community health practices and metrics frameworks to benchmark workflows and sustainability measures.[^2][^3][^4][^5][^6][^7][^8][^9][^10][^12][^13][^14][^15]

The scope addresses community engagement tools and programs; the plugin/operator ecosystem and third-party integration opportunities; contribution guidelines and onboarding; release management and versioning; sustainability and maintainer capacity; competition and market positioning; and licensing and IP considerations. The baseline date for all temporal references is 2025-11-01.

Constraints and information gaps:
- A definitive, up-to-date Discord member count and engagement metrics were not available.  
- Contributor roster transparency (roles, teams) is limited to acknowledgments in release notes.  
- There is no formal, published release policy (SemVer, backporting, cadence targets) nor a CONTRIBUTING.md, Code of CoC, or SECURITY files.  
- Packaging and distribution guidelines for third-party operators/plugins are not documented.  
- Trademark guidance for “TiXL” is not stated.

## Project Overview and Ecosystem Baseline

TiXL targets artists and technical artists seeking a real-time, graph-based environment that blends procedural generation, keyframe animation, and shader development. The codebase is modern C#/.NET with DirectX rendering, organized into Core, Editor, and Operators modules that reflect a pragmatic separation of concerns. The operator system underpins extensibility, while integrations expand interoperability with common show-control and media pipelines.[^2]

Public signals point to an active, growing project with broad extensibility:
- GitHub shows thousands of stars and hundreds of forks, dozens of releases, and a steady flow of issues and PRs, with v4.0.6 flagged as the latest stable release.[^2][^4]  
- The operator catalog and I/O operators have expanded meaningfully across pre-releases, with contributors credited across graphics, UI, audio, and stage lighting integrations.[^4]  
- Extensibility is visible in shader support, custom operator UIs, and integration with external protocols (MIDI, OSC, Spout, NDI, ArtNet/DMX) plus webcam input—useful for interactive installations and live performance.[^2][^4]

Table: TiXL at-a-glance (public repository signals and examples)
| Attribute | Observation | Source |
|---|---|---|
| Public signals | ~4k stars, ~230 forks, ~58 watchers; 28 releases; 33 contributors | [^2] |
| Latest stable release | v4.0.6 (Aug 30, 2025) | [^4] |
| Open issues | ~177 | [^2] |
| Open PRs | ~7 | [^2] |
| Operator ecosystem examples | Operators spanning SDF/fields, points/particles, mesh/rendering, animation, colors, math, images; multiple operator namespaces and examples | [^4] |
| I/O integrations | MIDI, OSC, Spout, NDI, ArtNet/DMX, webcam (VideoDeviceInput) | [^4] |

To anchor directory-level understanding, the following representative views illustrate the repository structure and operator organization:

![Representative directory view of TiXL repository.](browser/screenshots/tixl_repository_main.png)

![Operators directory structure (TypeOperators and examples).](browser/screenshots/tixl_operators_directory.png)

![Example graphics operator categories.](browser/screenshots/tixl_gfx_operators.png)

These baselines underscore TiXL’s mature internal architecture and the breadth of extensibility already available. The project is positioned to scale its ecosystem further by formalizing developer paths (SDK, samples, publishing) and improving community measurement and governance.

## Community Engagement Assessment

TiXL’s community infrastructure combines Discord, GitHub, the official website, and YouTube tutorials/workshops. This mix enables asynchronous collaboration, fast feedback, and hands-on learning—crucial for a tool that serves both stage and studio. The current posture is welcoming, but the engagement flywheel can be made more repeatable and measurable with light structure: channel purpose statements, weekly programming, role-based recognition, and content calendars. Community health guidance emphasizes exactly these kinds of operational rituals to drive participation and retention over time.[^1][^2][^3][^6][^10][^13]

### Discord Engagement

Discord is the primary channel for real-time support, ideation, and show-and-tell. Public invitations indicate active community presence, though the exact membership size requires verification due to inconsistent public counts. What matters more than raw size is the cadence and predictability of engagement.[^1]

To illustrate an initial structure, the following channels are recommended:
- Announcements (maintained by maintainers; no chat)  
- General Q&A (moderated; use threads)  
- Show & Tell (weekly thread; curated reposts to a “Best of” channel)  
- Help & Support (guidelines; tag helpers and maintainers)  
- Releases & Beta (release notes, known issues, upgrade tips)  
- Operators & Plugins (developer-centric; SDK updates, samples)  
- Jobs & Gigs (community board)

Table: Proposed Discord channel plan
| Channel | Purpose | Owner | Cadence |
|---|---|---|---|
| Announcements | Release notes, events, important updates | Maintainers | As needed |
| General Q&A | Q&A, tips, casual chat | Moderators + Ambassadors | Daily |
| Show & Tell | Weekly showcase and feedback | Ambassadors | Weekly |
| Help & Support | Structured support; reproducible steps | Moderators + Helpers | Daily |
| Releases & Beta | Pre-release testing, upgrade guidance | Maintainers | Per release |
| Operators & Plugins | SDK, samples, publishing, API questions | Devrel + Maintainers | Weekly office hours |
| Jobs & Gigs | Community board for paid gigs | Community | Monthly |

A simple weekly ritual—office hours on Tuesdays, “Show & Tell” on Thursdays—creates touchpoints for learning and recognition. Role badges (Ambassador, Mentor, Release Steward) celebrate contributors and encourage leadership from within the community.[^1][^10]

### Forums and Discussion Platforms

Given the visual and performance nature of TiXL, GitHub issues/discussions remain the most actionable venue for structured discussions, while Discord serves real-time exchange. A brief forum scan shows no dedicated third-party forum as a primary channel; thus, GitHub Discussions can fulfill that role with topic templates and light moderation.[^2]

Table: Discussion venues matrix
| Venue | Use case | Moderation |
|---|---|---|
| GitHub Discussions | RFCs, architecture, operator proposals | Maintainers + Devrel |
| Discord | Live help, social, showcases | Moderators + Ambassadors |
| YouTube comments | Tutorial feedback | Devrel monitors and curates |

### Social Media and Content (YouTube)

TiXL’s YouTube presence already includes tutorials such as “TiXL in 15 minutes,” version overviews, and feature deep dives. A consistent cadence—paired with playlists by use case (e.g., VJ sets, shader graphs, installation tips)—will increase discovery and help new users find relevant content quickly.[^3]

Table: Content calendar (example)
| Week | Topic | Format | CTA |
|---|---|---|---|
| 1 | TiXL in 15 minutes (updated) | Tutorial | Join Discord; download latest stable |
| 2 | Shader Graph basics | Deep dive + project file | Submit a sample to the gallery |
| 3 | Live I/O: OSC + Spout | Live demo + Q&A | Try pre-release; file feedback |
| 4 | Show & Tell highlights | Community reel | Vote next month’s topic |
| 5 | Release party (if applicable) | Stream + changelog walkthrough | Upgrade + report issues |
| 6 | Operator SDK teaser | Short + sample code | Sign up for beta testing |
| 7 | Art-Net/DMX stage lights | Tutorial + diagram | Share your rig |
| 8 | Points/Particles patterns | Deep dive + assets | Submit a tutorial idea |
| 9 | Micro-grant showcase | Case study | Apply for micro-grants |
| 10 | Performance tuning | Tips + benchmark | file a perf issue if reproducible |
| 11 | Gallery highlights | Montage + how-to | Contribute an example |
| 12 | Quarterly workshop | Live session | RSVP + feedback survey |

### Documentation and Tutorials

The wiki covers installation, operator references, and examples. To better support learners and contributors, pair tutorials with downloadable sample projects, operator index pages, and a FAQ that maps common tasks to operators and patterns. Shorten the time from “I want to do X” to “here is a project that shows X.”[^5]

### Events and Workshops

Workshops like the Berlin event serve multiple functions: onboarding, networking, and content generation (recorded sessions, templates, and new examples). Plan a recurring cadence—quarterly online workshops and at least two regional in-person events per year—to create predictable peaks in engagement. Use post-event surveys to prioritize future content.[^3]

Table: Event plan (example)
| City/Region | Format | Audience | Goals | Partners |
|---|---|---|---|---|
| Berlin | In-person | New users | Onboarding + templates | Local arts orgs |
| Online (EMEA/APAC) | Webinar | Global | Deep dives + Q&A | Communities |
| Online (Americas) | Webinar | Global | Release previews | User groups |
| Festival/meetup | Booth/workshop | VJ/AV | Showcases + recruitment | Festivals, venues |

## Plugin Ecosystem and Integration Opportunities

TiXL’s operator architecture and I/O operators already enable rich live-show workflows and third-party interoperability. The next step is to package this capability into a developer path that is discoverable, consistent, and safe.[^2][^4]

Table: Integration inventory (examples)
| Protocol/Tool | Operator(s) | Status | Use case | Example |
|---|---|---|---|---|
| MIDI | MidiInput | Active | Controller triggers, faders | Clip launching, BPM tap |
| OSC | OscOutput | Active | Talk to lighting/media | Parameter sync |
| Spout | SpoutInput/SpoutOutput | Active | Real-time video sharing | Cross-app textures |
| NDI | NDI dependencies/ops | Active | Networked video I/O | Multi-machine pipelines |
| Art-Net/DMX | ArtnetInput/Output; PointsToArtNetLights | Active | Stage lighting control | Pixel mapping |
| Webcam | VideoDeviceInput | Active | Camera capture | Visual input |
| Sensors (generic) | Via OSC/MIDI | Extensible | Interactive installations | Reaction to motion/sound |

Third-party opportunities include deeper stage-lighting integrations (e.g., more fixtures via Art-Net), media server interoperability (via NDI/Spout), and audio analysis/VST bridges to align with adjacent ecosystems. TouchDesigner’s long-standing interoperability documentation demonstrates how such cross-tool clarity can accelerate adoption and collaboration; a TiXL-specific interoperability page would serve a similar function.[^2][^4][^8]

Operator Plugin Ecosystem
- Developer path: Provide a lightweight SDK, code templates for custom operators, and clear guidance on custom operator UIs.  
- Packaging and distribution: Document operator folder structures, versioning, metadata, and an optional signing step.  
- Review and curation: Create a checklist for safety (permissions, resource use), API adherence, and documentation quality.  
- Discovery: Launch an Examples Gallery with categories (e.g., VJ, installations, education) and tags (OSC, MIDI, Spout).

## Contribution Guidelines and New Contributor Onboarding

TiXL is open to contributions, but formal artifacts and workflows are not yet published. Adding the essentials will improve clarity and throughput without imposing heavy process.[^2][^10]

Table: Contribution health checklist
| Artifact/Policy | Purpose | Current status | Action |
|---|---|---|---|
| CONTRIBUTING.md | Onboarding, branches, PR flow | Missing | Create from best practices |
| Code of Conduct | Community norms | Missing | Adopt and enforce |
| Issue/PR templates | Consistency, triage | Missing | Add templates and labels |
| Security disclosure | Vulnerability reporting | Missing | Publish process + contact |
| Conventional commits | Changelog automation | Not formalized | Encourage and document |
| Branching model | Protect main; releases | Informal | Define feature/release branches |
| Review SLAs | Throughput expectations | Unstated | Set targets and publish |
| Good first issues | Newcomer ramp | Ad hoc | Curate and tag |

New Contributor Journey
- First-run setup: script or detailed steps to clone, build, and run tests; basic project tour.  
- Guided tasks: 2–4 good first issues per month; small operator doc fixes with clear acceptance criteria.  
- Recognition: release notes shout-outs; role badges in Discord; “First PR merged” mention.  
- Mentorship: optional “buddy system” pairing with a contributor for the first month.[^10]

## Release Management and Versioning

TiXL uses a semantic-like version scheme with pre-release identifiers (alpha, preview) and clearly flags breaking changes in pre-releases. Changelogs are substantive and frequently acknowledge contributors—a strong practice. To scale, codify this into a policy.[^4][^10]

Table: Release timeline (representative public releases)
| Version | Date | Type | Notes |
|---|---|---|---|
| v4.0.6 | Aug 30, 2025 | Stable | Latest release; detailed notes; contributor credits |
| v4.0.5.0 | Jul 23 | Release | Noted as big; notes pending |
| v4.0.4 | Jul 6 | Pre-release | Stability focus; gizmos, time clips, snapping |
| v4.0.3 | Jun 19 | Pre-release | Art-Net/DMX, audio analysis; many UI/operator improvements |
| v4.0.2 (Preview3) | May 9 | Pre-release | MagGraph improvements; early GPU detection |
| v4.0.1 (Preview2) | May 3 | Pre-release | Issue stabilization |
| v4.0.0 (preview) | Apr 30 | Pre-release | Early pre-release |
| v3.10.8-preview | Apr 27 | Pre-release | Experimental; explicit breaking-change warning |

Recommended versioning and release practices:
- Adopt SemVer: MAJOR.MINOR.PATCH; pre-release tags for alpha/beta/rc; define breaking-change policy and migration notes.  
- Changelog style: group changes by Features, Improvements, Fixes, Breaking Changes; include upgrade notes and links to docs; credit contributors.  
- Backporting: publish criteria for backporting to stable maintenance branches.  
- Cadence: target a quarterly stable cadence with bi-weekly pre-releases; communicate expectations and deprecation timelines.

## Project Sustainability and Maintainer Involvement

TiXL shows strong development velocity and a pattern of acknowledging contributors in releases, which signals a healthy collaborative culture. To sustain momentum, codify governance roles, triage service-level agreements (SLAs), and diversify funding.[^2][^4][^11][^13][^15]

Table: Community health metrics plan
| Metric | Definition | Data source | Cadence |
|---|---|---|---|
| Discord DAU/MAU | Active users and ratio | Discord analytics | Weekly |
| First response time (issues) | Median time to first maintainer response | GitHub issues + issue-metrics | Weekly |
| Issue resolution time | Median time to close | GitHub issues + issue-metrics | Monthly |
| PR throughput | PRs merged per month | GitHub PRs | Monthly |
| Changelog completeness | % releases with standardized notes | Release page | Per release |
| Regression rate | % releases with regressions | Issue tracker | Per release |
| New contributors | First-time contributors per month | GitHub insights | Monthly |
| SDK adoption | Stars/forks/samples of operator SDK | GitHub repo metrics | Quarterly |

Table: Funding options matrix
| Option | Audience | Effort | Potential impact |
|---|---|---|---|
| Open Collective (sponsors) | Users + studios | Medium | Steady core support |
| Hardware lending library | Regional communities | Medium | Low-cost content + goodwill |
| Micro-grants | Tool builders | Medium | Accelerate SDK adoption |
| Workshops (paid tickets) | New users | Medium | Revenue + recruitment |
| Crowdfunding (specific features) | Enthusiasts | Medium | Focus on high-demand items |
| Partnerships (schools/festivals) | Institutions | Medium–High | Reach + co-marketing |
| Merch (branded items) | Community | Low–Medium | Minor revenue + branding |

Governance and Roles
- Define maintainers, release stewards, and moderators; publish scope and decision-making.  
- Triage SLAs: e.g., first response within three days; route issues by labels.  
- Transparency: maintain a roadmap and release calendar; record major decisions in Discussions.

## Competition Analysis and Market Positioning

TiXL competes with professional tools in a growing market. Resolume is an industry leader for VJ workflows with a robust community across forums, Slack, and social media, plus Resolume Wire for custom effects. TouchDesigner offers a highly mature ecosystem with extensive documentation, a curriculum, forums, and events, and recently introduced Point Operators (POPs) with major graphics architecture updates. TiXL’s advantage is its open-source model, operator-based extensibility, and integrated live I/O—valuable for cost-sensitive creators and educational contexts.[^7][^8][^9][^11]

Table: Competitive snapshot
| Tool | Plugins/Extensibility | Community | Events/Training | Pricing |
|---|---|---|---|---|
| Resolume | Resolume Wire (node-based effects) | Forum + Slack + social | Articles, training | Commercial |
| TouchDesigner | Mature operator system; new POPs | Forum + YouTube + newsletter | Workshops + global events | Commercial |
| TiXL | Operator SDK; I/O (MIDI, OSC, Spout, NDI, ArtNet/DMX) | Discord + GitHub + YouTube | Workshops + tutorials | Free/open-source |

Positioning narratives:
- TiXL for live shows: a free, open platform with real-time I/O and shader graphs—ideal for VJing, clubs, and performances.  
- TiXL for education: accessible pricing and an operator ecosystem that encourages learning-by-making; align with schools and maker spaces.  
- TiXL for developers: extend via operators, contribute upstream, and shape the SDK with real-world use cases.

Partnerships with schools, festivals, and venues can accelerate adoption and create a pipeline of contributors.

## Licensing and Intellectual Property

TiXL is MIT-licensed, which enables broad adoption, commercial use, and downstream distribution with minimal friction. This is an excellent fit for growing an open ecosystem. To protect the project’s name and reputation while preserving openness, TiXL should publish a trademark usage policy for “TiXL” and adopt a lightweight contributor agreement such as a Developer Certificate of Origin (DCO) to document provenance. Security vulnerability reporting guidance should also be published for responsible disclosure.[^2]

## Strategic Recommendations and Roadmap

The following integrated plan aligns with TiXL’s strengths and the market’s trajectory, balancing developer-facing investments with community engagement and operational clarity.

1) Governance and policy
- Publish CONTRIBUTING.md, CoC, SECURITY, issue/PR templates, and branching model; set review SLAs.  
- Adopt SemVer, define a release calendar, and codify backporting criteria.[^10]

2) Developer ecosystem (operators/plugins)
- Launch an operator SDK (templates, API notes), packaging and signing guidance, and a curated Examples Gallery; encourage community operators with a review checklist and showcase channels.[^2][^4]

3) Community engagement
- Establish Discord rituals: office hours, “Show & Tell,” release parties; implement roles and badges.  
- Publish a content calendar and expand workshops to a quarterly cadence.[^1][^3]

4) Metrics and transparency
- Instrument GitHub issue metrics and Discord analytics; publish a lightweight community health dashboard; add a public roadmap.[^12][^13][^14]

5) Funding and sustainability
- Create Open Collective tiers, launch a hardware lending library, and pilot micro-grants for operator development.[^15]

Table: Impact–Effort matrix (summary)
| Initiative | Impact | Effort | Owner | Timeline |
|---|---|---|---|---|
| Governance docs (CoC, CONTRIBUTING, SECURITY) | High | Low | Maintainers | 30 days |
| Release policy (SemVer, changelog) | Medium–High | Low | Maintainers | 30 days |
| Discord weekly programming | High | Low–Medium | Community | 30–60 days |
| Operator SDK + gallery | High | Medium | Devrel + Maintainers | 60–90 days |
| Issue metrics + dashboard | Medium | Low–Medium | Maintainers + Devrel | 60 days |
| Content calendar + tutorials | Medium | Medium | Devrel | 60–120 days |
| Sponsor tiers + micro-grants | Medium–High | Medium | Maintainers + Finance | 90–120 days |
| Regional workshops | Medium | Medium | Community + Partners | 90–120 days |

### 90-Day Implementation Plan

Table: 90-day plan (milestones)
| Milestone | Deliverables | Dependencies | Owner | Due date |
|---|---|---|---|---|
| Governance launch | CoC, CONTRIBUTING, SECURITY, templates | None | Maintainers | Day 30 |
| SemVer policy | Release policy page + changelog style | Governance | Maintainers | Day 30 |
| Weekly Discord | Office hours + Show & Tell | Governance | Community | Day 30 |
| Metrics instrumentation | issue-metrics + Discord analytics | Governance | Maintainers | Day 60 |
| Operator SDK | SDK docs + samples + publishing guide | Governance | Devrel | Day 90 |
| Examples gallery | Curated gallery + submission PR template | SDK draft | Devrel + Community | Day 90 |
| Sponsor tiers | Open Collective + tiers live | Governance | Maintainers | Day 90 |
| Regional workshops | 2 workshops scheduled/recorded | Content cadence | Community | Day 90 |

### Growth and Engagement KPI Framework

- Discord: MAU growth +20%; weekly active participants +30%; average messages/week +25% (baseline to be established).  
- Tutorials: monthly views +40%; average watch time +15%.  
- Contributions: new contributors/month +30%; good first issues resolved +50%.  
- Releases: maintain at least quarterly stable; 100% standardized changelogs.  
- Ecosystem: 10 new curated examples and 5 community operators by Day 120.

## Appendices

Artifacts to publish
- CONTRIBUTING.md, CoC, SECURITY, issue templates, PR templates, labels, and a roadmap page.  
- Operator SDK and Examples Gallery guidelines.

Extraction notes and data limitations
- Discord member counts vary in public snippets; engage Discord analytics and verify numbers.  
- Contributor roster beyond acknowledgments in release notes is not transparent; define roles and publish a team page.

Reference index
- See References for all external sources cited in this report.

Illustrative repository and operator views

![Core directory overview.](browser/screenshots/tixl_core_directory.png)

![Editor/GUI directory structure.](browser/screenshots/tixl_editor_directory.png)

![UI/graph components.](browser/screenshots/tixl_gui_directory.png)

![Type operators categories.](browser/screenshots/tixl_typeoperators_directory.png)

---

## References

[^1]: TiXL Discord Invite. https://discord.com/invite/tooll3-823853172619083816  
[^2]: TiXL (Tooll 3) GitHub Repository. https://github.com/tixl3d/tixl  
[^3]: TiXL Official Website. https://tixl.app/  
[^4]: TiXL Releases and Changelogs. https://github.com/tixl3d/tixl/releases  
[^5]: TiXL Wiki (Documentation). https://github.com/tixl3d/tixl/wiki  
[^6]: TiXL YouTube Channel. https://youtube.com/pixtur  
[^7]: Resolume Official Website. https://www.resolume.com/  
[^8]: TouchDesigner (Derivative) Official Website. https://derivative.ca/  
[^9]: Derivative Community Forum. https://forum.derivative.ca  
[^10]: Community Health Toolkit: Development Workflow. https://docs.communityhealthtoolkit.org/community/contributing/code/workflow/  
[^11]: Motion Graphics Software Market Research Report (MarketIntelo). https://marketintelo.com/report/motion-graphics-software-market  
[^12]: GitHub Issue Metrics Action. https://github.com/github/issue-metrics  
[^13]: GitHub Blog: Metrics for Issues, PRs, and Discussions. https://github.blog/open-source/maintainers/metrics-for-issues-pull-requests-and-discussions/  
[^14]: CHAOSS Augur. https://github.com/chaoss/augur  
[^15]: Creative Ways to Fund Open Source Projects (Pragmatic Engineer). https://blog.pragmaticengineer.com/creative-ways-to-fund-open-source-projects/