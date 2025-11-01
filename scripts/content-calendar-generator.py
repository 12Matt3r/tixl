#!/usr/bin/env python3
"""
TiXL Content Calendar Generator

This script generates automated content calendars based on the TIXL-097 Content Cadence Policy.
It creates structured schedules for blog posts, tutorials, releases, community spotlights, and 
educational materials with specific publication dates and templates.

Usage:
    python content-calendar-generator.py --year 2025 --quarter Q1 --output calendar_2025_q1.md
    python content-calendar-generator.py --year 2025 --yearly --output calendar_2025.md
"""

import argparse
import calendar
import json
import os
from datetime import datetime, timedelta
from typing import Dict, List, Optional
from dataclasses import dataclass, asdict
from pathlib import Path


@dataclass
class ContentItem:
    """Represents a single content item in the calendar."""
    title: str
    type: str
    category: str
    target_date: str
    template: str
    audience: str
    estimated_length: str
    priority: str
    status: str = "planned"
    assignee: str = ""
    notes: str = ""


@dataclass
class ContentTheme:
    """Represents a quarterly content theme."""
    quarter: str
    theme: str
    focus_areas: List[str]
    key_messages: List[str]


class ContentCalendarGenerator:
    """Generates content calendars based on TIXL-097 policy."""
    
    def __init__(self):
        self.current_year = datetime.now().year
        self.content_types = {
            'blog_post': {
                'technical_deep_dive': {
                    'frequency': 'bi_weekly',
                    'length': '1,500-3,000 words',
                    'audience': 'experienced developers',
                    'day_of_week': 2  # Wednesday
                },
                'feature_spotlight': {
                    'frequency': 'weekly',
                    'length': '800-1,200 words',
                    'audience': 'existing users',
                    'day_of_week': 0  # Monday
                },
                'industry_insights': {
                    'frequency': 'monthly',
                    'length': '1,000-2,000 words',
                    'audience': 'technical leaders',
                    'day_of_week': 1  # Tuesday
                }
            },
            'tutorial': {
                'getting_started': {
                    'frequency': 'bi_weekly',
                    'length': '2,000-4,000 words',
                    'audience': 'beginners',
                    'day_of_week': 2  # Wednesday
                },
                'advanced_use_case': {
                    'frequency': 'monthly',
                    'length': '3,000-5,000 words',
                    'audience': 'experienced developers',
                    'day_of_week': 3  # Thursday
                },
                'video_tutorial': {
                    'frequency': 'monthly',
                    'length': '10-30 minutes',
                    'audience': 'all levels',
                    'day_of_week': 4  # Friday
                }
            },
            'release_notes': {
                'major_release': {
                    'frequency': 'quarterly',
                    'length': '1,000-2,000 words',
                    'audience': 'all users',
                    'day_of_week': 1  # Tuesday
                },
                'minor_update': {
                    'frequency': 'as_needed',
                    'length': '500-800 words',
                    'audience': 'active users',
                    'day_of_week': 2  # Wednesday
                },
                'security_update': {
                    'frequency': 'as_needed',
                    'length': '300-500 words',
                    'audience': 'all users',
                    'day_of_week': 1  # Tuesday
                }
            },
            'community_spotlight': {
                'developer_showcase': {
                    'frequency': 'bi_weekly',
                    'length': '800-1,200 words',
                    'audience': 'community members',
                    'day_of_week': 5  # Saturday
                },
                'team_interview': {
                    'frequency': 'monthly',
                    'length': '1,000-1,500 words',
                    'audience': 'community',
                    'day_of_week': 2  # Wednesday
                },
                'community_contribution': {
                    'frequency': 'weekly',
                    'length': '300-500 words',
                    'audience': 'contributors',
                    'day_of_week': 6  # Sunday
                }
            },
            'educational': {
                'white_paper': {
                    'frequency': 'quarterly',
                    'length': '5,000-8,000 words',
                    'audience': 'technical leaders',
                    'day_of_week': 1  # Tuesday
                },
                'case_study': {
                    'frequency': 'monthly',
                    'length': '2,000-3,000 words',
                    'audience': 'decision makers',
                    'day_of_week': 3  # Thursday
                },
                'documentation_update': {
                    'frequency': 'weekly',
                    'length': 'variable',
                    'audience': 'all users',
                    'day_of_week': 5  # Saturday
                }
            }
        }
        
        self.quarterly_themes = {
            'Q1': ContentTheme(
                quarter='Q1',
                theme='Foundation & Growth',
                focus_areas=['onboarding', 'basic concepts', 'getting started', 'community building'],
                key_messages=['TiXL is easy to get started', 'Strong community support', 'Growing ecosystem']
            ),
            'Q2': ContentTheme(
                quarter='Q2',
                theme='Performance & Scale',
                focus_areas=['optimization', 'enterprise use', 'performance', 'scalability'],
                key_messages=['Enterprise-ready performance', 'Scalable solutions', 'Optimization expertise']
            ),
            'Q3': ContentTheme(
                quarter='Q3',
                theme='Innovation & Integration',
                focus_areas=['advanced features', 'partnerships', 'innovation', 'integration'],
                key_messages=['Cutting-edge innovation', 'Seamless integration', 'Partner ecosystem']
            ),
            'Q4': ContentTheme(
                quarter='Q4',
                theme='Community & Future',
                focus_areas=['community contributions', 'future plans', 'year in review', 'roadmap'],
                key_messages=['Community-driven development', 'Transparent roadmap', 'Future-focused']
            )
        }
    
    def get_quarter_dates(self, year: int, quarter: str) -> tuple:
        """Get start and end dates for a quarter."""
        quarter_map = {
            'Q1': (1, 3),
            'Q2': (4, 6),
            'Q3': (7, 9),
            'Q4': (10, 12)
        }
        
        start_month, end_month = quarter_map[quarter]
        start_date = datetime(year, start_month, 1)
        end_date = datetime(year, end_month, calendar.monthrange(year, end_month)[1])
        
        return start_date, end_date
    
    def get_date_for_frequency(self, start_date: datetime, frequency: str, 
                              occurrence: int, day_of_week: int) -> datetime:
        """Get the date for a specific occurrence based on frequency."""
        if frequency == 'weekly':
            return start_date + timedelta(weeks=occurrence - 1, 
                                        days=(day_of_week - start_date.weekday()) % 7)
        elif frequency == 'bi_weekly':
            return start_date + timedelta(weeks=(occurrence - 1) * 2,
                                        days=(day_of_week - start_date.weekday()) % 7)
        elif frequency == 'monthly':
            # For monthly, we take the first occurrence and add months
            first_date = start_date.replace(day=1) + timedelta(days=day_of_week)
            if first_date < start_date:
                first_date += timedelta(days=7)
            
            # Add months for subsequent occurrences
            target_month = first_date.month + (occurrence - 1)
            target_year = first_date.year + (target_month - 1) // 12
            target_month = (target_month - 1) % 12 + 1
            
            # Get the day of month, clamp to last day if necessary
            try:
                return target_year, target_month, first_date.day
            except ValueError:
                last_day = calendar.monthrange(target_year, target_month)[1]
                return target_year, target_month, min(first_date.day, last_day)
        
        return start_date
    
    def generate_content_items(self, year: int, quarter: str, 
                             content_type: str, category: str) -> List[ContentItem]:
        """Generate content items for a specific type and category."""
        start_date, end_date = self.get_quarter_dates(year, quarter)
        config = self.content_types[content_type][category]
        frequency = config['frequency']
        
        items = []
        
        if frequency == 'weekly':
            occurrences = ((end_date - start_date).days // 7) + 1
            for i in range(occurrences):
                target_date = start_date + timedelta(weeks=i)
                items.append(ContentItem(
                    title=f"{category.replace('_', ' ').title()} #{i+1}",
                    type=content_type,
                    category=category,
                    target_date=target_date.strftime('%Y-%m-%d'),
                    template=f"{content_type}_{category}.md",
                    audience=config['audience'],
                    estimated_length=config['length'],
                    priority='medium'
                ))
        
        elif frequency == 'bi_weekly':
            occurrences = ((end_date - start_date).days // 14) + 1
            for i in range(occurrences):
                target_date = start_date + timedelta(weeks=i*2)
                items.append(ContentItem(
                    title=f"{category.replace('_', ' ').title()} #{i+1}",
                    type=content_type,
                    category=category,
                    target_date=target_date.strftime('%Y-%m-%d'),
                    template=f"{content_type}_{category}.md",
                    audience=config['audience'],
                    estimated_length=config['length'],
                    priority='high'
                ))
        
        elif frequency == 'monthly':
            occurrences = 3  # Roughly 3 months per quarter
            for i in range(occurrences):
                target_month = start_date.month + i
                target_year = start_date.year + (target_month - 1) // 12
                target_month = (target_month - 1) % 12 + 1
                
                # Find the first occurrence of the target day of week in the month
                first_day = datetime(target_year, target_month, 1)
                days_ahead = config['day_of_week'] - first_day.weekday()
                if days_ahead < 0:
                    days_ahead += 7
                
                target_date = first_day + timedelta(days=days_ahead)
                if target_date > end_date:
                    break
                
                items.append(ContentItem(
                    title=f"{category.replace('_', ' ').title()} {target_date.strftime('%B %Y')}",
                    type=content_type,
                    category=category,
                    target_date=target_date.strftime('%Y-%m-%d'),
                    template=f"{content_type}_{category}.md",
                    audience=config['audience'],
                    estimated_length=config['length'],
                    priority='high'
                ))
        
        elif frequency == 'quarterly':
            # Place major releases at the start of the quarter
            items.append(ContentItem(
                title=f"{category.replace('_', ' ').title()} {quarter} {year}",
                type=content_type,
                category=category,
                target_date=start_date.strftime('%Y-%m-%d'),
                template=f"{content_type}_{category}.md",
                audience=config['audience'],
                estimated_length=config['length'],
                priority='critical'
            ))
        
        return items
    
    def generate_calendar(self, year: int, quarter: Optional[str] = None) -> List[ContentItem]:
        """Generate a complete content calendar."""
        if quarter:
            quarters = [quarter]
        else:
            quarters = ['Q1', 'Q2', 'Q3', 'Q4']
        
        all_items = []
        
        for q in quarters:
            for content_type, categories in self.content_types.items():
                for category, config in categories.items():
                    # Skip as-needed content types for automated scheduling
                    if config['frequency'] == 'as_needed':
                        continue
                    
                    items = self.generate_content_items(year, q, content_type, category)
                    all_items.extend(items)
        
        # Sort by date
        all_items.sort(key=lambda x: x.target_date)
        return all_items
    
    def generate_markdown_calendar(self, items: List[ContentItem], year: int, 
                                  quarter: Optional[str] = None) -> str:
        """Generate a markdown-formatted content calendar."""
        theme = None
        if quarter:
            theme = self.quarterly_themes[quarter]
        
        md_content = f"""# TiXL Content Calendar - {year}{f" {quarter}" if quarter else ""}

"""
        
        if theme:
            md_content += f"""## Quarterly Theme: {theme.theme}

**Focus Areas:** {', '.join(theme.focus_areas)}

**Key Messages:**
"""
            for msg in theme.key_messages:
                md_content += f"- {msg}\n"
            md_content += "\n"
        
        md_content += f"""## Content Schedule

Generated on: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

### Content Overview

| Date | Type | Category | Title | Audience | Length | Status | Priority |
|------|------|----------|--------|----------|---------|---------|----------|
"""
        
        for item in items:
            md_content += f"| {item.target_date} | {item.type.replace('_', ' ').title()} | {item.category.replace('_', ' ').title()} | {item.title} | {item.audience} | {item.estimated_length} | {item.status} | {item.priority.title()} |\n"
        
        # Group by month for detailed view
        current_month = ""
        for item in items:
            month = datetime.strptime(item.target_date, '%Y-%m-%d').strftime('%B %Y')
            if month != current_month:
                current_month = month
                md_content += f"\n### {month}\n\n"
            
            md_content += f"#### {item.target_date} - {item.title}\n"
            md_content += f"- **Type:** {item.type.replace('_', ' ').title()}\n"
            md_content += f"- **Category:** {item.category.replace('_', ' ').title()}\n"
            md_content += f"- **Template:** `{item.template}`\n"
            md_content += f"- **Target Audience:** {item.audience}\n"
            md_content += f"- **Estimated Length:** {item.estimated_length}\n"
            md_content += f"- **Priority:** {item.priority.title()}\n"
            if item.notes:
                md_content += f"- **Notes:** {item.notes}\n"
            md_content += "\n"
        
        # Add workflow summary
        md_content += """## Content Creation Workflow

### Weekly Schedule
- **Monday:** Feature Spotlight / Community Contribution
- **Wednesday:** Getting Started Tutorial / Blog Post  
- **Friday:** Documentation Update / Quick Tip

### Bi-weekly Schedule (Alternating)
- **Week 1:** Technical Deep Dive + Getting Started Tutorial
- **Week 2:** Feature Spotlight + Developer Showcase

### Monthly Schedule
- **Week 1:** Industry Insights / Case Study
- **Week 2:** Team Interview / Advanced Use Case
- **Week 3:** White Paper / Research Analysis
- **Week 4:** Monthly Summary / Community Roundup

## Next Steps

1. Review and customize content titles based on current priorities
2. Assign content creators to specific items
3. Schedule content creation sessions
4. Set up automated reminders and deadlines
5. Monitor progress and adjust as needed

## Template Links

- [Blog Post Templates](../CONTENT_TEMPLATES/blog/)
- [Tutorial Templates](../CONTENT_TEMPLATES/tutorials/)
- [Release Note Templates](../CONTENT_TEMPLATES/releases/)
- [Community Spotlight Templates](../CONTENT_TEMPLATES/community/)
- [Educational Material Templates](../CONTENT_TEMPLATES/educational/)

"""
        
        return md_content
    
    def generate_json_calendar(self, items: List[ContentItem], year: int, 
                              quarter: Optional[str] = None) -> str:
        """Generate a JSON-formatted content calendar."""
        calendar_data = {
            'metadata': {
                'generated_at': datetime.now().isoformat(),
                'year': year,
                'quarter': quarter,
                'total_items': len(items),
                'theme': self.quarterly_themes[quarter].theme if quarter else 'Annual Overview'
            },
            'items': [asdict(item) for item in items],
            'content_type_summary': {},
            'audience_summary': {},
            'priority_summary': {}
        }
        
        # Generate summaries
        for item in items:
            # Content type summary
            if item.type not in calendar_data['content_type_summary']:
                calendar_data['content_type_summary'][item.type] = 0
            calendar_data['content_type_summary'][item.type] += 1
            
            # Audience summary
            if item.audience not in calendar_data['audience_summary']:
                calendar_data['audience_summary'][item.audience] = 0
            calendar_data['audience_summary'][item.audience] += 1
            
            # Priority summary
            if item.priority not in calendar_data['priority_summary']:
                calendar_data['priority_summary'][item.priority] = 0
            calendar_data['priority_summary'][item.priority] += 1
        
        return json.dumps(calendar_data, indent=2)


def main():
    """Main function to generate content calendars."""
    parser = argparse.ArgumentParser(description='Generate TiXL Content Calendar')
    parser.add_argument('--year', type=int, default=datetime.now().year,
                       help='Year for the calendar (default: current year)')
    parser.add_argument('--quarter', choices=['Q1', 'Q2', 'Q3', 'Q4'],
                       help='Specific quarter to generate')
    parser.add_argument('--output', '-o', required=True,
                       help='Output file path')
    parser.add_argument('--format', choices=['markdown', 'json'], default='markdown',
                       help='Output format (default: markdown)')
    parser.add_argument('--template-dir', default='docs/CONTENT_TEMPLATES',
                       help='Content templates directory')
    parser.add_argument('--yearly', action='store_true',
                       help='Generate full year calendar')
    
    args = parser.parse_args()
    
    # Initialize generator
    generator = ContentCalendarGenerator()
    
    # Generate calendar items
    items = generator.generate_calendar(args.year, args.quarter if not args.yearly else None)
    
    # Generate output
    if args.format == 'markdown':
        content = generator.generate_markdown_calendar(items, args.year, 
                                                     args.quarter if not args.yearly else None)
    else:
        content = generator.generate_json_calendar(items, args.year, 
                                                  args.quarter if not args.yearly else None)
    
    # Ensure output directory exists
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    # Write to file
    with open(output_path, 'w') as f:
        f.write(content)
    
    print(f"Content calendar generated successfully!")
    print(f"Output: {output_path}")
    print(f"Total content items: {len(items)}")
    
    # Print summary by type
    type_summary = {}
    for item in items:
        type_summary[item.type] = type_summary.get(item.type, 0) + 1
    
    print("\nContent type summary:")
    for content_type, count in type_summary.items():
        print(f"  {content_type.replace('_', ' ').title()}: {count}")


if __name__ == '__main__':
    main()