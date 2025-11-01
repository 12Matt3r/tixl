# TiXL Content Automation Scripts

This directory contains scripts for automating content creation, scheduling, and management as part of the TIXL-097 Content Cadence Policy implementation.

## Scripts Overview

### 📅 Content Calendar Generator (`content-calendar-generator.py`)

Generates automated content calendars with templates for different content types based on the TIXL-097 Content Cadence Policy.

**Features:**
- Generates quarterly and annual content calendars
- Supports multiple content types (blog posts, tutorials, releases, etc.)
- Creates both Markdown and JSON output formats
- Includes content themes and priorities
- Automated scheduling based on publication frequency

**Usage:**
```bash
# Generate quarterly calendar
python scripts/content-calendar-generator.py \
  --year 2025 \
  --quarter Q1 \
  --output docs/calendars/calendar_2025_q1.md

# Generate full year calendar
python scripts/content-calendar-generator.py \
  --year 2025 \
  --yearly \
  --output docs/calendars/calendar_2025.md

# Generate JSON calendar for automation
python scripts/content-calendar-generator.py \
  --year 2025 \
  --quarter Q1 \
  --output calendar_data.json \
  --format json

# List available options
python scripts/content-calendar-generator.py --help
```

**Output:**
- Structured content calendar with scheduled dates
- Content type breakdown and summaries
- Template assignments for each content item
- Priority and audience targeting information

### 🎨 Content Template Generator (`content-template-generator.py`)

Creates standardized templates for blog posts, tutorials, documentation updates, and other content types.

**Features:**
- Generates templates for all content types defined in TIXL-097
- Includes metadata, structure, and quality guidelines
- Supports both individual template generation and bulk creation
- Follows editorial standards and accessibility requirements
- Includes SEO optimization elements

**Usage:**
```bash
# Generate specific template
python scripts/content-template-generator.py \
  --type blog_post \
  --category technical_deep_dive \
  --output docs/CONTENT_TEMPLATES/blog_post/technical_deep_dive.md

# List available templates
python scripts/content-template-generator.py --list-types

# Generate all templates
python scripts/content-template-generator.py \
  --generate-all \
  --output-dir docs/CONTENT_TEMPLATES

# Generate tutorial template
python scripts/content-template-generator.py \
  --type tutorial \
  --category getting_started \
  --output my_tutorial.md
```

**Template Types:**
- **Blog Posts:** Technical deep dives, feature spotlights, industry insights
- **Tutorials:** Getting started guides, advanced use cases, video tutorials
- **Release Notes:** Major releases, minor updates, security patches
- **Community Spotlights:** Developer showcases, team interviews, contributions
- **Educational:** White papers, case studies, documentation updates

## Content Creation Workflow

### 1. Planning Phase
```bash
# Generate content calendar for next quarter
python scripts/content-calendar-generator.py \
  --year $(date +%Y) \
  --quarter Q$(($(date +%m) / 4 + 1)) \
  --output content_calendar.md
```

### 2. Content Creation Phase
```bash
# Get appropriate template
python scripts/content-template-generator.py \
  --type blog_post \
  --category technical_deep_dive \
  --output my_technical_post.md

# Edit the template with your content
# Follow the template structure and guidelines
```

### 3. Quality Assurance
- Review content against template checklist
- Verify technical accuracy
- Check SEO optimization
- Ensure accessibility compliance

### 4. Publication
- Submit for editorial review
- Schedule publication
- Distribute across channels
- Monitor engagement

## Automation Integration

### GitHub Actions Integration

The scripts are designed to work with the GitHub Actions workflow (`.github/workflows/content-scheduler.yml`):

**Automated Scheduling:**
- Daily content calendar updates
- Weekly content status reports
- Automated issue creation for scheduled content
- Reminder notifications for upcoming deadlines

**Manual Triggers:**
```bash
# Trigger calendar generation
gh workflow run content-scheduler.yml -f action=generate_calendar

# Send content reminders
gh workflow run content-scheduler.yml -f action=send_reminders

# Check content status
gh workflow run content-scheduler.yml -f action=check_status
```

### CI/CD Pipeline Integration

**Content Quality Checks:**
- Automated linting for content structure
- Template compliance verification
- Code example validation
- Accessibility standards checking

**Publishing Pipeline:**
- Automated content validation
- Multi-channel publishing
- Social media integration
- Analytics tracking setup

## Content Types and Schedules

### Weekly Schedule
- **Monday:** Feature Spotlight / Community Contribution
- **Wednesday:** Getting Started Tutorial / Blog Post
- **Friday:** Documentation Update / Quick Tip

### Bi-weekly Schedule
- **Week 1:** Technical Deep Dive + Getting Started Tutorial
- **Week 2:** Feature Spotlight + Developer Showcase

### Monthly Schedule
- **Week 1:** Industry Insights / Case Study
- **Week 2:** Team Interview / Advanced Use Case
- **Week 3:** White Paper / Research Analysis
- **Week 4:** Monthly Summary / Community Roundup

### Quarterly Themes
- **Q1:** Foundation & Growth
- **Q2:** Performance & Scale
- **Q3:** Innovation & Integration
- **Q4:** Community & Future

## Configuration

### Environment Variables

```bash
# Discord webhook for notifications
export DISCORD_WEBHOOK_URL="your-webhook-url"

# Content templates directory
export CONTENT_TEMPLATES_DIR="docs/CONTENT_TEMPLATES"

# Calendar output directory
export CALENDAR_OUTPUT_DIR="docs/calendars"
```

### Script Configuration

Edit the script files to customize:
- Content types and categories
- Publication schedules
- Template structures
- Output formats
- Quality standards

## Troubleshooting

### Common Issues

**Calendar Generation Errors:**
```bash
# Ensure Python dependencies are installed
pip install python-dateutil jinja2 pyyaml

# Check date format
python scripts/content-calendar-generator.py --year 2025 --quarter Q1
```

**Template Generation Issues:**
```bash
# Verify content type and category
python scripts/content-template-generator.py --list-types

# Check file permissions
chmod +x scripts/content-template-generator.py
```

**GitHub Actions Failures:**
- Check workflow permissions
- Verify secret configurations
- Review action logs for errors
- Ensure proper file paths

### Getting Help

1. Check the [Content Cadence Policy](../docs/TIXL-097_Content_Cadence_Policy.md)
2. Review existing templates in `docs/CONTENT_TEMPLATES/`
3. Ask in the #content Discord channel
4. Create an issue for bugs or feature requests

## Best Practices

### Content Creation
- Always start with the appropriate template
- Follow the content structure guidelines
- Include relevant code examples
- Add visual elements to support content
- Include calls-to-action and engagement elements

### Calendar Management
- Review and update calendars regularly
- Adjust content based on priorities and feedback
- Monitor engagement and adjust scheduling
- Coordinate with team members on assignments

### Automation
- Set up proper GitHub Actions permissions
- Configure webhook notifications
- Monitor automation logs
- Update scripts based on workflow changes

## Examples

### Generate Content Calendar for 2025 Q2

```bash
python scripts/content-calendar-generator.py \
  --year 2025 \
  --quarter Q2 \
  --output docs/calendars/calendar_2025_q2.md \
  --format markdown

# Review generated calendar
cat docs/calendars/calendar_2025_q2.md
```

### Create Technical Blog Post Template

```bash
python scripts/content-template-generator.py \
  --type blog_post \
  --category technical_deep_dive \
  --output technical_post_template.md

# Copy template to your working directory
cp technical_post_template.md my_technical_analysis.md
```

### Generate All Templates

```bash
python scripts/content-template-generator.py \
  --generate-all \
  --output-dir custom_templates

# List generated templates
ls -la custom_templates/*/
```

---

**Scripts Version**: 1.0  
**Last Updated**: November 2025  
**Maintained By**: TiXL Content Team