# TiXL Content Templates

This directory contains standardized templates for creating consistent, high-quality content for the TiXL project. All templates follow the guidelines outlined in [TIXL-097 Content Cadence Policy](../TIXL-097_Content_Cadence_Policy.md).

## Template Overview

### 📝 Blog Posts (`blog_post/`)
Templates for various blog post types targeting different audiences.

- **[technical_deep_dive.md](blog_post/technical_deep_dive.md)** - In-depth technical analysis for experienced developers
- **[feature_spotlight.md](blog_post/feature_spotlight.md)** - Highlight new features and improvements  
- **[industry_insights.md](blog_post/industry_insights.md)** - Analysis of trends and best practices

### 🎓 Tutorials (`tutorial/`)
Step-by-step guides for different skill levels and learning objectives.

- **[getting_started.md](tutorial/getting_started.md)** - Beginner-friendly introduction tutorials
- **[advanced_use_case.md](tutorial/advanced_use_case.md)** - Complex scenarios for experienced developers
- **[video_tutorial.md](tutorial/video_tutorial.md)** - Visual demonstrations and walkthroughs

### 📋 Release Notes (`release_notes/`)
Templates for announcing updates, new features, and changes.

- **[major_release.md](release_notes/major_release.md)** - Comprehensive notes for major versions
- **[minor_update.md](release_notes/minor_update.md)** - Brief notes for minor releases
- **[security_update.md](release_notes/security_update.md)** - Urgent security patch information

### 👥 Community Spotlights (`community_spotlight/`)
Templates for highlighting community contributions and achievements.

- **[developer_showcase.md](community_spotlight/developer_showcase.md)** - Feature community projects
- **[team_interview.md](community_spotlight/team_interview.md)** - Interview team members
- **[community_contribution.md](community_spotlight/community_contribution.md)** - Recognize contributor achievements

### 📚 Educational Materials (`educational/`)
In-depth content for learning and reference.

- **[white_paper.md](educational/white_paper.md)** - Research and analysis papers
- **[case_study.md](educational/case_study.md)** - Real-world implementation examples
- **[documentation_update.md](educational/documentation_update.md)** - Documentation improvements

## Using Templates

### Quick Start
1. Copy the appropriate template to your content directory
2. Rename the file with your specific topic
3. Fill in the content following the template structure
4. Review against the [Quality Standards](../TIXL-097_Content_Cadence_Policy.md#quality-standards)

### Template Generation
Use the content template generator to create new templates:

```bash
# Generate a specific template
python scripts/content-template-generator.py \
  --type blog_post \
  --category technical_deep_dive \
  --output my_technical_post.md

# List available templates
python scripts/content-template-generator.py --list-types

# Generate all templates
python scripts/content-template-generator.py --generate-all
```

### Template Structure

Each template includes:
- **Metadata Section** - Content type, audience, reading time
- **Structured Sections** - Consistent sections for logical flow
- **Code Examples** - Placeholder sections for technical content
- **SEO Elements** - Meta tags and optimization hints
- **Engagement Features** - Calls-to-action and community integration
- **Quality Checklists** - Review criteria and best practices

## Content Guidelines

### Writing Standards
- Follow the [Style Guide Requirements](../TIXL-097_Content_Cadence_Policy.md#style-guide-requirements)
- Maintain technical accuracy and provide working code examples
- Use clear, concise language appropriate for the target audience
- Include visual elements (diagrams, screenshots, code blocks)

### Content Lifecycle
1. **Planning** - Use content calendar for scheduling
2. **Creation** - Follow template structure and guidelines
3. **Review** - Technical accuracy and editorial review
4. **Publication** - Multi-channel distribution
5. **Promotion** - Cross-platform marketing and engagement
6. **Analysis** - Performance tracking and optimization

### Quality Gates
- [ ] Technical accuracy verified by subject matter expert
- [ ] Editorial review for clarity and style compliance
- [ ] SEO optimization completed
- [ ] Accessibility standards met (WCAG 2.1 AA)
- [ ] Community feedback incorporated (for key content)

## Best Practices

### Before Writing
- [ ] Review the [Content Cadence Policy](../TIXL-097_Content_Cadence_Policy.md)
- [ ] Check content calendar for scheduling requirements
- [ ] Identify target audience and their needs
- [ ] Gather supporting materials and resources

### During Writing
- [ ] Follow the template structure
- [ ] Include relevant code examples
- [ ] Add visual elements to support content
- [ ] Use consistent terminology and style
- [ ] Provide actionable takeaways

### After Writing
- [ ] Self-review against quality checklist
- [ ] Request technical review from SME
- [ ] Get editorial review for clarity
- [ ] Test all code examples
- [ ] Optimize for SEO and accessibility

## Content Distribution

### Primary Channels
- **TiXL Blog** - Long-form technical content
- **Documentation** - Tutorials and reference materials
- **GitHub** - Code examples and quick tips
- **Discord** - Community updates and announcements

### Secondary Channels
- **Social Media** - LinkedIn, Twitter for broader reach
- **Newsletter** - Weekly digest and updates
- **YouTube** - Video tutorials and demonstrations
- **Industry Publications** - Guest posts and thought leadership

### Optimization
- **SEO** - Keyword research and optimization
- **Analytics** - Track performance and engagement
- **A/B Testing** - Test different formats and topics
- **Community Feedback** - Iterate based on user input

## Getting Help

### Template Questions
- Review the [Content Cadence Policy](../TIXL-097_Content_Cadence_Policy.md)
- Ask in the #content channel on Discord
- Check existing examples in the repository

### Technical Questions
- GitHub Discussions for technical questions
- Stack Overflow for implementation help
- Community Discord for quick questions

### Content Strategy
- Content team meetings (schedule TBD)
- Monthly strategy reviews
- Quarterly planning sessions

---

**Template Version**: 1.0  
**Last Updated**: November 2025  
**Maintained By**: TiXL Content Team