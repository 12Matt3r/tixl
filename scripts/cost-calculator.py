#!/usr/bin/env python3
"""
TiXL Cost Calculator
A comprehensive tool for calculating project costs, development time, and funding requirements.
"""

import math
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple
from datetime import datetime, timedelta
import json

@dataclass
class TeamMember:
    """Data class for team member information"""
    role: str
    hourly_rate: float
    availability: float  # Percentage (0-100)
    experience_level: str  # junior, mid, senior, lead

@dataclass
class ProjectPhase:
    """Data class for project phases"""
    name: str
    duration_weeks: int
    team_size: int
    resources: Dict[str, float]  # Additional resource costs
    risk_multiplier: float = 1.0  # Risk buffer

@dataclass
class ProjectRequirement:
    """Data class for project requirements"""
    category: str  # technical, design, marketing, legal, etc.
    complexity: str  # low, medium, high, very_high
    estimated_hours: float
    dependencies: List[str]

class CostCalculator:
    """Main class for cost calculations and project planning"""
    
    def __init__(self):
        self.team_members = {}
        self.project_phases = []
        self.requirements = []
        self.overhead_rates = {
            'administrative': 0.15,  # 15% overhead
            'facilities': 0.10,     # 10% facilities
            'technology': 0.08,     # 8% technology
            'contingency': 0.20     # 20% contingency buffer
        }
    
    def add_team_member(self, member: TeamMember):
        """Add a team member to the project"""
        self.team_members[member.role] = member
        print(f"Added team member: {member.role} at ${member.hourly_rate}/hour")
    
    def add_project_phase(self, phase: ProjectPhase):
        """Add a project phase"""
        self.project_phases.append(phase)
        print(f"Added phase: {phase.name} ({phase.duration_weeks} weeks)")
    
    def add_requirement(self, requirement: ProjectRequirement):
        """Add a project requirement"""
        self.requirements.append(requirement)
        print(f"Added requirement: {requirement.category} ({requirement.complexity})")
    
    def calculate_labor_costs(self) -> Dict:
        """Calculate total labor costs for the project"""
        total_labor_cost = 0
        phase_costs = {}
        
        for phase in self.project_phases:
            phase_cost = 0
            weeks_in_phase = phase.duration_weeks
            
            # Calculate team member costs for this phase
            if self.team_members:
                avg_hourly_rate = sum(member.hourly_rate for member in self.team_members.values()) / len(self.team_members)
                effective_rate = avg_hourly_rate * (phase.team_size / len(self.team_members))
                
                # Calculate hours per week
                hours_per_week = 40  # Standard work week
                total_hours = weeks_in_phase * hours_per_week * phase.team_size
                
                # Apply risk multiplier
                adjusted_hours = total_hours * phase.risk_multiplier
                phase_cost = adjusted_hours * effective_rate
            
            # Add resource costs
            resource_cost = sum(phase.resources.values())
            phase_total = phase_cost + resource_cost
            
            phase_costs[phase.name] = {
                'labor_cost': phase_cost,
                'resource_cost': resource_cost,
                'total_cost': phase_total,
                'duration_weeks': phase.duration_weeks,
                'team_size': phase.team_size
            }
            
            total_labor_cost += phase_total
        
        return {
            'total_labor_cost': total_labor_cost,
            'phase_breakdown': phase_costs
        }
    
    def calculate_overhead_costs(self, base_cost: float) -> Dict:
        """Calculate overhead costs"""
        overhead_costs = {}
        total_overhead = 0
        
        for category, rate in self.overhead_rates.items():
            cost = base_cost * rate
            overhead_costs[category] = cost
            total_overhead += cost
        
        return {
            'breakdown': overhead_costs,
            'total_overhead': total_overhead,
            'total_with_overhead': base_cost + total_overhead
        }
    
    def calculate_requirement_costs(self) -> Dict:
        """Calculate costs based on project requirements"""
        complexity_multipliers = {
            'low': 1.0,
            'medium': 1.5,
            'high': 2.2,
            'very_high': 3.5
        }
        
        category_rates = {
            'technical': 85,      # $85/hour
            'design': 75,         # $75/hour
            'marketing': 65,      # $65/hour
            'legal': 250,         # $250/hour
            'testing': 70,        # $70/hour
            'documentation': 55   # $55/hour
        }
        
        total_requirement_cost = 0
        category_costs = {}
        
        for req in self.requirements:
            base_rate = category_rates.get(req.category, 70)  # Default rate
            complexity_mult = complexity_multipliers.get(req.complexity, 1.0)
            
            # Adjust hours based on complexity and dependencies
            adjusted_hours = req.estimated_hours * complexity_mult
            
            # Add dependency overhead (10% per dependency)
            dependency_overhead = len(req.dependencies) * 0.1
            final_hours = adjusted_hours * (1 + dependency_overhead)
            
            cost = final_hours * base_rate
            total_requirement_cost += cost
            
            if req.category not in category_costs:
                category_costs[req.category] = {'hours': 0, 'cost': 0}
            
            category_costs[req.category]['hours'] += final_hours
            category_costs[req.category]['cost'] += cost
        
        return {
            'total_cost': total_requirement_cost,
            'category_breakdown': category_costs,
            'total_hours': sum(req.estimated_hours for req in self.requirements)
        }
    
    def calculate_funding_requirements(self, project_duration_months: int = None) -> Dict:
        """Calculate comprehensive funding requirements"""
        # Calculate all cost components
        labor_costs = self.calculate_labor_costs()
        requirement_costs = self.calculate_requirement_costs()
        
        # Base project cost
        base_cost = labor_costs['total_labor_cost'] + requirement_costs['total_cost']
        
        # Calculate overhead
        overhead = self.calculate_overhead_costs(base_cost)
        
        # Calculate funding phases
        funding_phases = self.calculate_funding_phases(overhead['total_with_overhead'])
        
        # Calculate cash flow requirements
        cash_flow = self.calculate_cash_flow(funding_phases, project_duration_months)
        
        total_cost = overhead['total_with_overhead']
        
        return {
            'base_cost_breakdown': {
                'labor_costs': labor_costs['total_labor_cost'],
                'requirement_costs': requirement_costs['total_cost'],
                'subtotal': base_cost
            },
            'overhead': overhead,
            'total_project_cost': total_cost,
            'funding_phases': funding_phases,
            'cash_flow_requirements': cash_flow,
            'cost_per_deliverable': total_cost / len(self.project_phases) if self.project_phases else total_cost,
            'risk_analysis': self.calculate_risk_analysis(total_cost)
        }
    
    def calculate_funding_phases(self, total_cost: float) -> List[Dict]:
        """Calculate funding phases based on project milestones"""
        if not self.project_phases:
            return [{'phase': 'Single Phase', 'percentage': 100, 'amount': total_cost}]
        
        phases = []
        cumulative_percentage = 0
        
        for i, phase in enumerate(self.project_phases):
            # Funding percentage based on phase completion
            if i == 0:
                # First phase gets more funding for setup
                percentage = 30
            elif i == len(self.project_phases) - 1:
                # Last phase gets remaining funding
                percentage = 100 - cumulative_percentage
            else:
                # Middle phases get proportional funding
                phase_weight = phase.duration_weeks / sum(p.duration_weeks for p in self.project_phases)
                percentage = min(25, phase_weight * 40)  # Cap at 25% per phase
            
            amount = total_cost * (percentage / 100)
            
            phases.append({
                'phase': phase.name,
                'phase_number': i + 1,
                'percentage': percentage,
                'amount': amount,
                'milestones': self.get_phase_milestones(phase.name, i, len(self.project_phases))
            })
            
            cumulative_percentage += percentage
        
        return phases
    
    def get_phase_milestones(self, phase_name: str, phase_index: int, total_phases: int) -> List[str]:
        """Generate milestones for each phase"""
        if 'planning' in phase_name.lower():
            return ['Requirements approved', 'Architecture designed', 'Project plan finalized']
        elif 'development' in phase_name.lower():
            return ['MVP completed', 'Core features implemented', 'Testing completed']
        elif 'deployment' in phase_name.lower():
            return ['Production ready', 'User training complete', 'Go-live achieved']
        else:
            return [f'Milestone 1', f'Milestone 2', f'Final delivery']
    
    def calculate_cash_flow(self, funding_phases: List[Dict], project_duration_months: int) -> Dict:
        """Calculate monthly cash flow requirements"""
        if not project_duration_months:
            # Estimate based on phase durations
            total_weeks = sum(phase.duration_weeks for phase in self.project_phases)
            project_duration_months = math.ceil(total_weeks / 4.33)  # Convert weeks to months
        
        monthly_cash_flow = {}
        remaining_phases = funding_phases.copy()
        
        for month in range(1, project_duration_months + 1):
            monthly_need = 0
            
            # Process funding phases
            if remaining_phases:
                current_phase = remaining_phases[0]
                
                # Distribute funding over the phase duration
                phase_months = max(1, len(remaining_phases) * 2)  # Estimate 2 months per phase
                monthly_funding = current_phase['amount'] / phase_months
                monthly_need = max(monthly_funding, 50000)  # Minimum monthly requirement
            
            monthly_cash_flow[month] = {
                'cash_required': monthly_need,
                'funding_phase': remaining_phases[0]['phase'] if remaining_phases else 'Final delivery',
                'accumulated_need': sum(monthly_cash_flow[m]['cash_required'] for m in monthly_cash_flow) + monthly_need
            }
        
        return {
            'monthly_requirements': monthly_cash_flow,
            'peak_monthly_need': max(req['cash_required'] for req in monthly_cash_flow.values()),
            'total_funding_needed': sum(req['cash_required'] for req in monthly_cash_flow.values())
        }
    
    def calculate_risk_analysis(self, base_cost: float) -> Dict:
        """Calculate risk-based cost adjustments"""
        risk_scenarios = {
            'optimistic': {'multiplier': 0.9, 'description': 'Best case scenario'},
            'realistic': {'multiplier': 1.0, 'description': 'Most likely scenario'},
            'conservative': {'multiplier': 1.25, 'description': 'Conservative estimate'},
            'pessimistic': {'multiplier': 1.5, 'description': 'Worst case scenario'}
        }
        
        risk_analysis = {}
        
        for scenario, data in risk_scenarios.items():
            adjusted_cost = base_cost * data['multiplier']
            risk_analysis[scenario] = {
                'cost': adjusted_cost,
                'description': data['description'],
                'extra_cost': adjusted_cost - base_cost,
                'confidence_level': {
                    'optimistic': 0.1,
                    'realistic': 0.6,
                    'conservative': 0.25,
                    'pessimistic': 0.05
                }[scenario]
            }
        
        return risk_analysis
    
    def generate_cost_report(self, project_duration_months: int = None) -> str:
        """Generate comprehensive cost report"""
        funding_req = self.calculate_funding_requirements(project_duration_months)
        labor_costs = self.calculate_labor_costs()
        requirement_costs = self.calculate_requirement_costs()
        
        report = f"""
TiXL Project Cost Analysis Report
Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
{'=' * 60}

PROJECT COST SUMMARY
Base Project Cost: ${funding_req['base_cost_breakdown']['subtotal']:,.2f}
Overhead Costs: ${funding_req['overhead']['total_overhead']:,.2f}
Total Project Cost: ${funding_req['total_project_cost']:,.2f}

COST BREAKDOWN
Labor Costs: ${funding_req['base_cost_breakdown']['labor_costs']:,.2f}
Requirement Costs: ${funding_req['base_cost_breakdown']['requirement_costs']:,.2f}
Administrative (15%): ${funding_req['overhead']['breakdown']['administrative']:,.2f}
Facilities (10%): ${funding_req['overhead']['breakdown']['facilities']:,.2f}
Technology (8%): ${funding_req['overhead']['breakdown']['technology']:,.2f}
Contingency (20%): ${funding_req['overhead']['breakdown']['contingency']:,.2f}

PHASE BREAKDOWN
"""
        
        for phase_name, phase_data in labor_costs['phase_breakdown'].items():
            report += f"\n{phase_name}:\n"
            report += f"  Duration: {phase_data['duration_weeks']} weeks\n"
            report += f"  Team Size: {phase_data['team_size']} people\n"
            report += f"  Labor Cost: ${phase_data['labor_cost']:,.2f}\n"
            report += f"  Resource Cost: ${phase_data['resource_cost']:,.2f}\n"
            report += f"  Total: ${phase_data['total_cost']:,.2f}\n"
        
        report += f"""
REQUIREMENT COSTS
"""
        for category, data in requirement_costs['category_breakdown'].items():
            report += f"{category.capitalize()}: {data['hours']:.1f} hours - ${data['cost']:,.2f}\n"
        
        report += f"""
FUNDING PHASES
"""
        for phase in funding_req['funding_phases']:
            report += f"\nPhase {phase['phase_number']}: {phase['phase']}\n"
            report += f"  Funding: {phase['percentage']:.1f}% (${phase['amount']:,.2f})\n"
            report += f"  Milestones: {', '.join(phase['milestones'])}\n"
        
        report += f"""
CASH FLOW ANALYSIS
Peak Monthly Requirement: ${funding_req['cash_flow_requirements']['peak_monthly_need']:,.2f}
Total Funding Needed: ${funding_req['cash_flow_requirements']['total_funding_needed']:,.2f}

RISK ANALYSIS
Optimistic (10% confidence): ${funding_req['risk_analysis']['optimistic']['cost']:,.2f}
Realistic (60% confidence): ${funding_req['risk_analysis']['realistic']['cost']:,.2f}
Conservative (25% confidence): ${funding_req['risk_analysis']['conservative']['cost']:,.2f}
Pessimistic (5% confidence): ${funding_req['risk_analysis']['pessimistic']['cost']:,.2f}

RECOMMENDATIONS
1. Secure funding for at least the conservative estimate: ${funding_req['risk_analysis']['conservative']['cost']:,.2f}
2. Establish contingency reserves of 20% of base cost
3. Implement milestone-based funding releases
4. Monitor cash flow monthly and adjust as needed
5. Consider phased development to spread costs over time

COST PER DELIVERABLE: ${funding_req['cost_per_deliverable']:,.2f}
{'=' * 60}
"""
        
        return report
    
    def save_cost_analysis(self, filename: str = None):
        """Save cost analysis to JSON file"""
        if filename is None:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            filename = f"cost_analysis_{timestamp}.json"
        
        analysis_data = {
            'team_members': {role: {'hourly_rate': member.hourly_rate, 
                                  'availability': member.availability,
                                  'experience_level': member.experience_level}
                           for role, member in self.team_members.items()},
            'project_phases': [{'name': phase.name, 'duration_weeks': phase.duration_weeks,
                              'team_size': phase.team_size, 'resources': phase.resources,
                              'risk_multiplier': phase.risk_multiplier}
                             for phase in self.project_phases],
            'requirements': [{'category': req.category, 'complexity': req.complexity,
                            'estimated_hours': req.estimated_hours, 'dependencies': req.dependencies}
                           for req in self.requirements],
            'funding_requirements': self.calculate_funding_requirements(),
            'timestamp': datetime.now().isoformat()
        }
        
        with open(filename, 'w') as f:
            json.dump(analysis_data, f, indent=2)
        
        print(f"Cost analysis saved to {filename}")

def create_sample_project() -> CostCalculator:
    """Create a sample project for demonstration"""
    calculator = CostCalculator()
    
    # Add team members
    team = [
        TeamMember('Project Manager', 95, 100, 'senior'),
        TeamMember('Senior Developer', 85, 100, 'senior'),
        TeamMember('Junior Developer', 55, 100, 'junior'),
        TeamMember('UI/UX Designer', 75, 80, 'mid'),
        TeamMember('QA Engineer', 65, 90, 'mid'),
        TeamMember('DevOps Engineer', 90, 80, 'senior')
    ]
    
    for member in team:
        calculator.add_team_member(member)
    
    # Add project phases
    phases = [
        ProjectPhase('Planning & Design', 4, 4, {'software_licenses': 5000, 'consulting': 8000}),
        ProjectPhase('Development Phase 1', 8, 6, {'cloud_infrastructure': 3000}),
        ProjectPhase('Testing & QA', 3, 4, {'testing_tools': 2000}),
        ProjectPhase('Deployment & Launch', 2, 5, {'deployment_services': 1500})
    ]
    
    for phase in phases:
        calculator.add_project_phase(phase)
    
    # Add requirements
    requirements = [
        ProjectRequirement('technical', 'high', 320, ['design']),
        ProjectRequirement('design', 'medium', 120, []),
        ProjectRequirement('testing', 'medium', 80, ['development']),
        ProjectRequirement('marketing', 'low', 40, []),
        ProjectRequirement('documentation', 'low', 60, ['technical'])
    ]
    
    for req in requirements:
        calculator.add_requirement(req)
    
    return calculator

def main():
    """Main function for interactive use"""
    calculator = CostCalculator()
    
    print("TiXL Cost Calculator")
    print("===================")
    print("1. Add Team Member")
    print("2. Add Project Phase")
    print("3. Add Project Requirement")
    print("4. Calculate Funding Requirements")
    print("5. Generate Cost Report")
    print("6. Save Analysis")
    print("7. Load Sample Project")
    print("8. Exit")
    
    while True:
        choice = input("\nSelect an option (1-8): ").strip()
        
        if choice == '1':
            # Add team member
            role = input("Role: ")
            hourly_rate = float(input("Hourly rate: $"))
            availability = float(input("Availability percentage: "))
            experience = input("Experience level (junior/mid/senior/lead): ")
            
            member = TeamMember(role, hourly_rate, availability, experience)
            calculator.add_team_member(member)
        
        elif choice == '2':
            # Add project phase
            name = input("Phase name: ")
            duration = int(input("Duration in weeks: "))
            team_size = int(input("Team size: "))
            
            resources = {}
            print("Additional resources (press Enter to skip):")
            resource_name = input("Resource name: ")
            if resource_name:
                resource_cost = float(input("Cost: $"))
                resources[resource_name] = resource_cost
            
            risk_mult = input("Risk multiplier (default 1.0): ") or "1.0"
            
            phase = ProjectPhase(name, duration, team_size, resources, float(risk_mult))
            calculator.add_project_phase(phase)
        
        elif choice == '3':
            # Add requirement
            category = input("Category (technical/design/marketing/legal/testing/documentation): ")
            complexity = input("Complexity (low/medium/high/very_high): ")
            hours = float(input("Estimated hours: "))
            
            dependencies = []
            print("Dependencies (press Enter to skip):")
            dep = input("Dependency: ")
            if dep:
                dependencies.append(dep)
            
            requirement = ProjectRequirement(category, complexity, hours, dependencies)
            calculator.add_requirement(requirement)
        
        elif choice == '4':
            # Calculate funding requirements
            project_duration = input("Project duration in months (optional): ")
            duration = int(project_duration) if project_duration else None
            
            funding_req = calculator.calculate_funding_requirements(duration)
            
            print(f"\nFunding Requirements:")
            print(f"Total Project Cost: ${funding_req['total_project_cost']:,.2f}")
            print(f"Base Cost: ${funding_req['base_cost_breakdown']['subtotal']:,.2f}")
            print(f"Overhead: ${funding_req['overhead']['total_overhead']:,.2f}")
            print(f"Peak Monthly Need: ${funding_req['cash_flow_requirements']['peak_monthly_need']:,.2f}")
        
        elif choice == '5':
            # Generate report
            project_duration = input("Project duration in months (optional): ")
            duration = int(project_duration) if project_duration else None
            
            report = calculator.generate_cost_report(duration)
            print(report)
        
        elif choice == '6':
            # Save analysis
            filename = input("Filename (optional): ")
            calculator.save_cost_analysis(filename)
        
        elif choice == '7':
            # Load sample project
            calculator = create_sample_project()
            print("Sample project loaded successfully!")
            print("Team: 6 members, 4 phases, 5 requirements")
        
        elif choice == '8':
            print("Exiting TiXL Cost Calculator. Goodbye!")
            break
        
        else:
            print("Invalid option. Please select 1-8.")

if __name__ == "__main__":
    main()
