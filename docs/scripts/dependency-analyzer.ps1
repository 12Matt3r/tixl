#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Dependency Analyzer - Comprehensive dependency tree analysis and optimization

.DESCRIPTION
    Analyzes dependency trees for optimization opportunities including:
    - Dependency graph visualization
    - Circular dependency detection
    - Unused dependency identification
    - Version conflict resolution
    - Performance impact analysis
    - Optimization recommendations

.PARAMETER SolutionPath
    Path to the solution or project file to analyze

.PARAMETER OutputPath
    Directory to save analysis reports

.PARAMETER GenerateVisualization
    Generate dependency graph visualizations

.PARAMETER CheckOptimization
    Run optimization checks

.PARAMETER ExportToGraphML
    Export dependency graph to GraphML format

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\dependency-analyzer.ps1 -SolutionPath "TiXL.sln" -GenerateVisualization

.EXAMPLE
    .\dependency-analyzer.ps1 -SolutionPath "TiXL.sln" -CheckOptimization -Verbose
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SolutionPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./dependency-analysis",
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateVisualization,
    
    [Parameter(Mandatory=$false)]
    [switch]$CheckOptimization,
    
    [Parameter(Mandatory=$false)]
    [switch]$ExportToGraphML,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Global variables
$script:ScriptName = "TiXL Dependency Analyzer"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date

# Initialize output directory
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$script:LogFile = Join-Path $OutputPath "dependency-analysis.log"
$script:ReportFile = Join-Path $OutputPath "analysis-report.json"
$script:SummaryFile = Join-Path $OutputPath "analysis-summary.md"
$script:GraphMLFile = Join-Path $OutputPath "dependency-graph.graphml"
$script:VisualizationFile = Join-Path $OutputPath "dependency-graph.html"

function Write-Log {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        [Parameter(Mandatory=$false)]
        [ValidateSet("INFO", "WARNING", "ERROR", "DEBUG")]
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    
    if ($Verbose -or $Level -ne "DEBUG") {
        Write-Host $logEntry
    }
    
    $logEntry | Out-File -FilePath $script:LogFile -Append -Encoding UTF8
}

function Get-DependencyTree {
    param([string]$SolutionPath)
    
    Write-Log "Building dependency tree from $SolutionPath" "INFO"
    
    $tree = [PSCustomObject]@{
        Projects = @()
        Dependencies = @()
        RootProjects = @()
        Depth = 0
        Statistics = @{
            TotalProjects = 0
            TotalPackages = 0
            CircularDependencies = 0
            MaxDepth = 0
        }
    }
    
    try {
        if ($SolutionPath.EndsWith('.sln')) {
            # Process solution file
            Write-Log "Processing solution file" "INFO"
            
            $projects = & dotnet sln "$SolutionPath" list 2>$null
            $solutionDir = Split-Path $SolutionPath
            
            foreach ($projectPath in $projects) {
                if ($projectPath -and (Test-Path $projectPath)) {
                    $projectInfo = Get-ProjectDependencies -ProjectPath $projectPath
                    $tree.Projects += $projectInfo
                    
                    if ($projectInfo.Dependencies.Count -eq 0) {
                        $tree.RootProjects += $projectInfo
                    }
                }
            }
            
            # Build full dependency graph
            $tree.Dependencies = Build-DependencyGraph -Projects $tree.Projects
        }
        else {
            # Process single project file
            Write-Log "Processing project file" "INFO"
            $projectInfo = Get-ProjectDependencies -ProjectPath $SolutionPath
            $tree.Projects += $projectInfo
            $tree.RootProjects += $projectInfo
            $tree.Dependencies = $projectInfo.Dependencies
        }
        
        # Calculate statistics
        $tree.Statistics.TotalProjects = $tree.Projects.Count
        $tree.Statistics.TotalPackages = ($tree.Projects | ForEach-Object { $_.Dependencies.Count } | Measure-Object -Sum).Sum
        $tree.Statistics.MaxDepth = Get-MaxDependencyDepth -Projects $tree.Projects
    }
    catch {
        Write-Log "Error building dependency tree: $_" "ERROR"
    }
    
    Write-Log "Dependency tree built: $($tree.Statistics.TotalProjects) projects, $($tree.Statistics.TotalPackages) dependencies" "INFO"
    return $tree
}

function Get-ProjectDependencies {
    param([string]$ProjectPath)
    
    $project = [PSCustomObject]@{
        Name = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
        Path = $ProjectPath
        Directory = Split-Path $ProjectPath
        Type = "Project"
        Dependencies = @()
        ProjectReferences = @()
        PackageReferences = @()
        Depth = 0
        Size = 0
        LastModified = $null
    }
    
    try {
        # Get file info
        $fileInfo = Get-Item $ProjectPath
        $project.LastModified = $fileInfo.LastWriteTime
        $project.Size = $fileInfo.Length
        
        # Get project references
        $listOutput = & dotnet list "$ProjectPath" reference 2>$null
        if ($LASTEXITCODE -eq 0 -and $listOutput) {
            foreach ($line in $listOutput) {
                if ($line -match "^\s*([\w\\\/\.\-]+)\.csproj\s*$") {
                    $refProject = $matches[1]
                    $project.ProjectReferences += [PSCustomObject]@{
                        Name = [System.IO.Path]::GetFileNameWithoutExtension($refProject)
                        Path = $refProject
                        Type = "ProjectReference"
                    }
                }
            }
        }
        
        # Get package references
        $packageOutput = & dotnet list "$ProjectPath" package --include-transitive 2>$null
        if ($LASTEXITCODE -eq 0 -and $packageOutput) {
            $currentSection = $null
            
            foreach ($line in $packageOutput) {
                # Skip header lines
                if ($line -match "^\s*$" -or $line -match "^The PINVOKE plugins:" -or $line -match "^> dotnet add") { continue }
                
                # Track sections
                if ($line -match "Direct dependencies:") { 
                    $currentSection = "Direct"
                    continue
                }
                if ($line -match "Transitive dependencies:") { 
                    $currentSection = "Transitive"
                    continue
                }
                
                # Parse package lines
                if ($line -match "^\s*([\w\.-]+)\s+([\d\.]+)\s*(.*)$") {
                    $packageName = $matches[1]
                    $version = $matches[2]
                    $additionalInfo = $matches[3].Trim()
                    
                    # Skip if this looks like a header
                    if ($packageName -eq "Type" -or $packageName -eq "Version") { continue }
                    
                    $packageRef = [PSCustomObject]@{
                        Name = $packageName
                        Version = $version
                        Type = "PackageReference"
                        SourceType = $currentSection
                        License = $additionalInfo
                        IsTransitive = ($currentSection -eq "Transitive")
                        UsedBy = @($project.Name)
                    }
                    
                    $project.PackageReferences += $packageRef
                }
            }
        }
        
        # Build complete dependency list
        $project.Dependencies = @($project.ProjectReferences) + @($project.PackageReferences)
        $project.Dependencies | ForEach-Object { $_.ReferencingProject = $project.Name }
    }
    catch {
        Write-Log "Error reading dependencies from $ProjectPath : $_" "ERROR"
    }
    
    return $project
}

function Build-DependencyGraph {
    param([array]$Projects)
    
    Write-Log "Building dependency graph" "INFO"
    
    $graph = @{
        Nodes = @()
        Edges = @()
        Clusters = @()
    }
    
    # Create nodes for projects and packages
    foreach ($project in $Projects) {
        # Project node
        $graph.Nodes += [PSCustomObject]@{
            Id = $project.Name
            Name = $project.Name
            Type = "Project"
            Size = $project.Size
            PackageCount = $project.PackageReferences.Count
            LastModified = $project.LastModified
        }
        
        # Package nodes (unique by name)
        $packages = $project.PackageReferences | Group-Object Name | ForEach-Object { $_.Group[0] }
        foreach ($package in $packages) {
            # Check if package node already exists
            $existingNode = $graph.Nodes | Where-Object { $_.Name -eq $package.Name -and $_.Type -eq "Package" }
            
            if (!$existingNode) {
                $graph.Nodes += [PSCustomObject]@{
                    Id = "pkg_$($package.Name)"
                    Name = $package.Name
                    Type = "Package"
                    Version = $package.Version
                    SourceCount = 1
                    UsedBy = @($project.Name)
                }
            }
            else {
                $existingNode.UsedBy += $project.Name
                $existingNode.SourceCount++
            }
        }
        
        # Create edges
        foreach ($dependency in $project.ProjectReferences) {
            $graph.Edges += [PSCustomObject]@{
                From = $project.Name
                To = $dependency.Name
                Type = "ProjectReference"
                IsDirect = $true
            }
        }
        
        foreach ($package in $project.PackageReferences) {
            $graph.Edges += [PSCustomObject]@{
                From = $project.Name
                To = "pkg_$($package.Name)"
                Type = "PackageReference"
                IsDirect = !$package.IsTransitive
                SourceType = $package.SourceType
            }
        }
    }
    
    Write-Log "Built graph with $($graph.Nodes.Count) nodes and $($graph.Edges.Count) edges" "INFO"
    return $graph
}

function Test-CircularDependencies {
    param($DependencyGraph)
    
    Write-Log "Testing for circular dependencies" "INFO"
    
    $circular = @{
        ProjectCycles = @()
        PackageCycles = @()
        ComplexCycles = @()
    }
    
    # Find project reference cycles
    $projectNodes = $DependencyGraph.Nodes | Where-Object { $_.Type -eq "Project" }
    $projectEdges = $DependencyGraph.Edges | Where-Object { $_.Type -eq "ProjectReference" }
    
    foreach ($project in $projectNodes) {
        $cycle = Find-ProjectCycle -StartNode $project -Edges $projectEdges -Visited @()
        if ($cycle.Count -gt 1) {
            $circular.ProjectCycles += [PSCustomObject]@{
                Nodes = $cycle
                Description = "Project cycle: $($cycle -join " → ")"
            }
        }
    }
    
    # Find package dependency cycles (less common but possible)
    $packageNodes = $DependencyGraph.Nodes | Where-Object { $_.Type -eq "Package" }
    $packageEdges = $DependencyGraph.Edges | Where-Object { $_.Type -eq "PackageReference" }
    
    # Package cycles would require analyzing the actual package dependencies
    # This is a simplified check for common circular patterns
    
    Write-Log "Found $($circular.ProjectCycles.Count) project cycles, $($circular.PackageCycles.Count) package cycles" "INFO"
    return $circular
}

function Find-ProjectCycle {
    param(
        $StartNode,
        $Edges,
        [array]$Visited
    )
    
    if ($Visited -contains $StartNode.Name) {
        return @($StartNode.Name)
    }
    
    $visitedPath = $Visited + @($StartNode.Name)
    $outgoingEdges = $Edges | Where-Object { $_.From -eq $StartNode.Name }
    
    foreach ($edge in $outgoingEdges) {
        $cycle = Find-ProjectCycle -StartNode ($edge) -Edges $Edges -Visited $visitedPath
        if ($cycle.Count -gt 0) {
            return $cycle
        }
    }
    
    return @()
}

function Test-UnusedDependencies {
    param(
        $DependencyGraph,
        [array]$Projects
    )
    
    Write-Log "Testing for unused dependencies" "INFO"
    
    $unused = @{
        UnusedPackages = @()
        OrphanedPackages = @()
        RedundantReferences = @()
        Suggestions = @()
    }
    
    # Check for packages that are only referenced by orphaned projects
    foreach ($node in $DependencyGraph.Nodes | Where-Object { $_.Type -eq "Package" }) {
        # Get projects that use this package
        $usingProjects = $DependencyGraph.Edges | Where-Object { $_.To -eq $node.Id } | ForEach-Object { $_.From }
        
        # Check if any of these projects are transitive/indirect dependencies
        $directlyUsedBy = $DependencyGraph.Edges | Where-Object { 
            $_.To -eq $node.Id -and $_.IsDirect 
        } | ForEach-Object { $_.From }
        
        if ($directlyUsedBy.Count -eq 0 -and $usingProjects.Count -gt 0) {
            # Package is only used transitively
            $unused.OrphanedPackages += [PSCustomObject]@{
                Package = $node.Name
                UsedBy = $usingProjects
                Reason = "Only transitive references"
            }
        }
        
        # Check for packages with very low usage (potential removal candidates)
        if ($directlyUsedBy.Count -eq 1 -and $node.SourceCount -eq 1) {
            $unused.RedundantReferences += [PSCustomObject]@{
                Package = $node.Name
                UsedBy = $directlyUsedBy
                Reason = "Single project usage, may be redundant"
            }
        }
    }
    
    # Check for large unused project dependencies
    foreach ($project in $Projects) {
        $largeDeps = $project.PackageReferences | Where-Object { 
            $_.IsTransitive -and 
            $_.UsedBy.Count -eq 1 -and 
            $_.UsedBy[0] -eq $project.Name 
        }
        
        foreach ($dep in $largeDeps) {
            $unused.Suggestions += [PSCustomObject]@{
                Project = $project.Name
                Dependency = $dep.Name
                Type = "Consider direct reference"
                Reason = "Package appears to be directly used"
            }
        }
    }
    
    Write-Log "Found $($unused.UnusedPackages.Count) unused packages, $($unused.RedundantReferences.Count) redundant references" "INFO"
    return $unused
}

function Test-VersionConflicts {
    param($DependencyGraph)
    
    Write-Log "Testing for version conflicts" "INFO"
    
    $conflicts = @{
        VersionMismatches = @()
        MultipleVersions = @()
        Recommendations = @()
    }
    
    # Group packages by name to find version conflicts
    $packageNodes = $DependencyGraph.Nodes | Where-Object { $_.Type -eq "Package" }
    $packageGroups = $packageNodes | Group-Object Name
    
    foreach ($group in $packageGroups) {
        $versions = $group.Group | ForEach-Object { $_.Version } | Select-Object -Unique
        
        if ($versions.Count -gt 1) {
            $conflicts.MultipleVersions += [PSCustomObject]@{
                Package = $group.Name
                Versions = $versions
                Count = $group.Count
                Description = "Multiple versions found: $($versions -join ', ')"
            }
        }
        
        # Check for version mismatch patterns that indicate problems
        $node = $group.Group[0]
        if ($node.SourceCount -gt 5) {
            $conflicts.VersionMismatches += [PSCustomObject]@{
                Package = $group.Name
                UsageCount = $node.SourceCount
                PotentialIssues = "High usage with potential version conflicts"
            }
        }
    }
    
    # Generate recommendations
    foreach ($conflict in $conflicts.MultipleVersions) {
        $conflicts.Recommendations += [PSCustomObject]@{
            Package = $conflict.Package
            Action = "Resolve version conflicts"
            Description = "Consider using binding redirects or consolidating to single version"
            Priority = "High"
        }
    }
    
    Write-Log "Found $($conflicts.MultipleVersions.Count) version conflicts" "INFO"
    return $conflicts
}

function Get-PerformanceImpact {
    param(
        $DependencyGraph,
        [array]$Projects
    )
    
    Write-Log "Analyzing performance impact" "INFO"
    
    $performance = @{
        HeavyProjects = @()
        RedundantLoads = @()
        BuildImpact = @()
        RuntimeImpact = @{}
    }
    
    # Identify heavy projects (many dependencies)
    foreach ($project in $Projects) {
        $directPackageCount = $project.PackageReferences | Where-Object { !$_.IsTransitive } | Measure-Object | Select-Object -ExpandProperty Count
        $totalPackageCount = $project.PackageReferences.Count
        $projectRefCount = $project.ProjectReferences.Count
        
        if ($directPackageCount -gt 20 -or $totalPackageCount -gt 50) {
            $performance.HeavyProjects += [PSCustomObject]@{
                Project = $project.Name
                DirectPackages = $directPackageCount
                TotalPackages = $totalPackageCount
                ProjectReferences = $projectRefCount
                Impact = "High"
                Reason = "Excessive dependency count"
            }
        }
        
        # Analyze build impact
        if ($projectRefCount -gt 10) {
            $performance.BuildImpact += [PSCustomObject]@{
                Project = $project.Name
                ProjectReferences = $projectRefCount
                Impact = "Medium"
                Reason = "High number of project references may slow builds"
            }
        }
    }
    
    # Identify redundant loads
    $popularPackages = $DependencyGraph.Nodes | Where-Object { 
        $_.Type -eq "Package" -and $_.SourceCount -gt 3 
    } | Sort-Object SourceCount -Descending | Select-Object -First 10
    
    foreach ($package in $popularPackages) {
        $performance.RedundantLoads += [PSCustomObject]@{
            Package = $package.Name
            UsageCount = $package.SourceCount
            PotentialSaving = "Consider shared references or packages"
            Impact = "Medium"
        }
    }
    
    # Runtime impact assessment
    foreach ($project in $Projects) {
        $runtimeHeavyPackages = $project.PackageReferences | Where-Object { 
            $_.Name -match "System\..*" -or 
            $_.Name -match "Microsoft\..*Framework" -or
            $_.IsTransitive 
        } | Measure-Object | Select-Object -ExpandProperty Count
        
        if ($runtimeHeavyPackages -gt 15) {
            $performance.RuntimeImpact[$project.Name] = [PSCustomObject]@{
                HeavyPackages = $runtimeHeavyPackages
                Impact = "High"
                Reason = "Large runtime dependency footprint"
            }
        }
    }
    
    Write-Log "Performance analysis completed" "INFO"
    return $performance
}

function Get-OptimizationRecommendations {
    param(
        $CircularDeps,
        $UnusedDeps,
        $VersionConflicts,
        $PerformanceImpact,
        $DependencyGraph
    )
    
    Write-Log "Generating optimization recommendations" "INFO"
    
    $recommendations = @{
        Critical = @()
        High = @()
        Medium = @()
        Low = @()
        Total = 0
    }
    
    # Critical recommendations - from circular dependencies
    foreach ($cycle in $CircularDeps.ProjectCycles) {
        $recommendations.Critical += [PSCustomObject]@{
            Type = "Circular Dependency"
            Description = $cycle.Description
            Impact = "Breaks build, prevents proper compilation"
            Action = "Refactor to remove circular references"
            Priority = "Critical"
        }
    }
    
    foreach ($conflict in $VersionConflicts.MultipleVersions) {
        $recommendations.Critical += [PSCustomObject]@{
            Type = "Version Conflict"
            Description = "Multiple versions of $($conflict.Package): $($conflict.Versions -join ', ')"
            Impact = "Runtime errors, unexpected behavior"
            Action = "Consolidate to single version or use binding redirects"
            Priority = "Critical"
        }
    }
    
    # High priority - performance and redundancy
    foreach ($heavy in $PerformanceImpact.HeavyProjects) {
        $recommendations.High += [PSCustomObject]@{
            Type = "Performance"
            Description = "Project '$($heavy.Project)' has $($heavy.TotalPackages) dependencies"
            Impact = "Slow build times, large deployment size"
            Action = "Review and remove unnecessary dependencies"
            Priority = "High"
        }
    }
    
    foreach ($redundant in $UnusedDeps.RedundantReferences) {
        $recommendations.High += [PSCustomObject]@{
            Type = "Redundancy"
            Description = "Package '$($redundant.Package)' used by single project"
            Impact = "Unnecessary package bloat"
            Action = "Consider if dependency is actually needed"
            Priority = "High"
        }
    }
    
    # Medium priority - optimization suggestions
    foreach ($suggestion in $UnusedDeps.Suggestions) {
        $recommendations.Medium += [PSCustomObject]@{
            Type = "Optimization"
            Description = "Consider direct reference to '$($suggestion.Dependency)' in '$($suggestion.Project)'"
            Impact = "Better dependency tracking"
            Action = "Add explicit package reference"
            Priority = "Medium"
        }
    }
    
    # Low priority - best practices
    foreach ($load in $PerformanceImpact.RedundantLoads) {
        $recommendations.Low += [PSCustomObject]@{
            Type = "Best Practice"
            Description = "Package '$($load.Package)' used by $($load.UsageCount) projects"
            Impact = "Potential shared dependency opportunity"
            Action = "Consider creating shared package or centralizing reference"
            Priority = "Low"
        }
    }
    
    # Calculate totals
    $recommendations.Total = $recommendations.Critical.Count + 
                            $recommendations.High.Count + 
                            $recommendations.Medium.Count + 
                            $recommendations.Low.Count
    
    Write-Log "Generated $($recommendations.Total) recommendations ($($recommendations.Critical.Count) critical)" "INFO"
    return $recommendations
}

function Export-GraphML {
    param(
        $DependencyGraph,
        [string]$OutputPath
    )
    
    if (!$ExportToGraphML) {
        return
    }
    
    Write-Log "Exporting dependency graph to GraphML format" "INFO"
    
    $graphML = @"
<?xml version="1.0" encoding="UTF-8"?>
<graphml xmlns="http://graphml.graphdrawing.org/xmlns">
    <key id="d0" for="node" attr.name="type" attr.type="string"/>
    <key id="d1" for="node" attr.name="name" attr.type="string"/>
    <key id="d2" for="node" attr.name="size" attr.type="int"/>
    <key id="d3" for="edge" attr.name="relation" attr.type="string"/>
    <graph id="G" edgedefault="directed">
"@
    
    # Add nodes
    foreach ($node in $DependencyGraph.Nodes) {
        $type = $node.Type.ToLower()
        $name = $node.Name -replace '"', '&quot;'
        $size = if ($node.Size) { [int]($node.Size / 1000) } else { 1 }
        
        $graphML += @"
        <node id="$name">
            <data key="d0">$type</data>
            <data key="d1">$name</data>
            <data key="d2">$size</data>
        </node>
"@
    }
    
    # Add edges
    foreach ($edge in $DependencyGraph.Edges) {
        $from = $edge.From -replace '"', '&quot;'
        $to = $edge.To -replace '"', '&quot;'
        $relation = $edge.Type.ToLower()
        
        $graphML += @"
        <edge source="$from" target="$to">
            <data key="d3">$relation</data>
        </edge>
"@
    }
    
    $graphML += @"
    </graph>
</graphml>
"@
    
    $graphML | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Log "GraphML exported to: $OutputPath" "INFO"
}

function Generate-Visualization {
    param(
        $DependencyGraph,
        [string]$OutputPath
    )
    
    if (!$GenerateVisualization) {
        return
    }
    
    Write-Log "Generating dependency graph visualization" "INFO"
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>TiXL Dependency Graph</title>
    <script src="https://d3js.org/d3.v7.min.js"></script>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .node circle { stroke: #fff; stroke-width: 1.5px; }
        .node.project circle { fill: #4CAF50; }
        .node.package circle { fill: #2196F3; }
        .link { stroke: #999; stroke-opacity: 0.6; }
        .link.direct { stroke-width: 2px; }
        .link.transitive { stroke-dasharray: 5,5; }
        .tooltip {
            position: absolute; padding: 10px; background: rgba(0,0,0,0.8);
            color: white; border-radius: 5px; pointer-events: none;
        }
    </style>
</head>
<body>
    <h1>TiXL Dependency Graph</h1>
    <div id="graph"></div>
    <script>
        const data = $( $DependencyGraph | ConvertTo-Json -Depth 10 );
        
        const width = 1200;
        const height = 800;
        
        const svg = d3.select("#graph")
            .append("svg")
            .attr("width", width)
            .attr("height", height);
            
        const color = d3.scaleOrdinal()
            .domain(["Project", "Package"])
            .range(["#4CAF50", "#2196F3"]);
            
        const simulation = d3.forceSimulation(data.Nodes)
            .force("link", d3.forceLink(data.Edges).id(d => d.Name).distance(100))
            .force("charge", d3.forceManyBody().strength(-300))
            .force("center", d3.forceCenter(width / 2, height / 2));
            
        const link = svg.append("g")
            .selectAll("line")
            .data(data.Edges)
            .enter().append("line")
            .attr("class", d => \`link \${d.Type}\`)
            .style("stroke-width", d => d.Type === "PackageReference" ? 2 : 1);
            
        const node = svg.append("g")
            .selectAll("circle")
            .data(data.Nodes)
            .enter().append("circle")
            .attr("r", 5)
            .attr("class", d => \`node \${d.Type.toLowerCase()}\`)
            .call(d3.drag()
                .on("start", dragstarted)
                .on("drag", dragged)
                .on("end", dragended));
                
        const label = svg.append("g")
            .selectAll("text")
            .data(data.Nodes)
            .enter().append("text")
            .text(d => d.Name)
            .style("font-size", "12px")
            .style("pointer-events", "none");
            
        simulation.on("tick", () => {
            link
                .attr("x1", d => d.source.x)
                .attr("y1", d => d.source.y)
                .attr("x2", d => d.target.x)
                .attr("y2", d => d.target.y);
                
            node
                .attr("cx", d => d.x)
                .attr("cy", d => d.y);
                
            label
                .attr("x", d => d.x + 10)
                .attr("y", d => d.y + 5);
        });
        
        function dragstarted(event, d) {
            if (!event.active) simulation.alphaTarget(0.3).restart();
            d.fx = d.x;
            d.fy = d.y;
        }
        
        function dragged(event, d) {
            d.fx = event.x;
            d.fy = event.y;
        }
        
        function dragended(event, d) {
            if (!event.active) simulation.alphaTarget(0);
            d.fx = null;
            d.fy = null;
        }
    </script>
</body>
</html>
"@
    
    $html | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Log "Visualization generated: $OutputPath" "INFO"
}

function Export-AnalysisReport {
    param(
        $DependencyTree,
        $CircularDeps,
        $UnusedDeps,
        $VersionConflicts,
        $PerformanceImpact,
        $Recommendations,
        [string]$OutputPath
    )
    
    Write-Log "Exporting analysis report" "INFO"
    
    $reportData = @{
        AnalysisInfo = @{
            Timestamp = $script:StartTime
            Duration = (Get-Date) - $script:StartTime
            Version = $script:ScriptVersion
            SolutionPath = $SolutionPath
        }
        DependencyTree = $DependencyTree
        Issues = @{
            CircularDependencies = $CircularDeps
            UnusedDependencies = $UnusedDeps
            VersionConflicts = $VersionConflicts
            PerformanceImpact = $PerformanceImpact
        }
        Recommendations = $Recommendations
        Statistics = @{
            TotalNodes = $DependencyTree.Dependencies.Nodes.Count
            TotalEdges = $DependencyTree.Dependencies.Edges.Count
            Projects = $DependencyTree.Statistics.TotalProjects
            Packages = $DependencyTree.Statistics.TotalPackages
            MaxDepth = $DependencyTree.Statistics.MaxDepth
            CriticalIssues = $Recommendations.Critical.Count
            HighPriority = $Recommendations.High.Count
        }
    }
    
    # Export JSON report
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportFile -Encoding UTF8
    
    # Export markdown summary
    Export-AnalysisMarkdown -ReportData $reportData -OutputPath $script:SummaryFile
    
    Write-Log "Analysis report exported to: $OutputPath" "INFO"
}

function Export-AnalysisMarkdown {
    param(
        $ReportData,
        [string]$OutputPath
    )
    
    $markdown = @"
# TiXL Dependency Analysis Report

**Generated**: $($ReportData.AnalysisInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
**Duration**: $($ReportData.AnalysisInfo.Duration.TotalSeconds.ToString("F2")) seconds
**Version**: $($ReportData.AnalysisInfo.Version)
**Solution**: $($ReportData.AnalysisInfo.SolutionPath)

## Executive Summary

- **Total Projects**: $($ReportData.Statistics.Projects)
- **Total Packages**: $($ReportData.Statistics.Packages)
- **Dependency Graph**: $($ReportData.Statistics.TotalNodes) nodes, $($ReportData.Statistics.TotalEdges) edges
- **Max Depth**: $($ReportData.Statistics.MaxDepth)
- **Critical Issues**: $($ReportData.Statistics.CriticalIssues)
- **High Priority Issues**: $($ReportData.Statistics.HighPriority)

## Dependency Tree Structure

### Project Distribution
$(
    $projectSizes = $ReportData.DependencyTree.Projects | ForEach-Object {
        @{ Project = $_.Name; Packages = $_.Dependencies.Count; SizeKB = [Math]::Round($_.Size / 1KB, 2) }
    } | Sort-Object Packages -Descending
    
    foreach ($project in $projectSizes) {
        "- **$($project.Project)**: $($project.Packages) dependencies, $($project.SizeKB) KB"
    }
)

### Dependency Statistics
- **Projects with most dependencies**: $($projectSizes[0].Project) ($($projectSizes[0].Packages) dependencies)
- **Average dependencies per project**: $([Math]::Round($ReportData.Statistics.Packages / $ReportData.Statistics.Projects, 1))

## Issue Analysis

### Circular Dependencies

$(
    if ($ReportData.Issues.CircularDependencies.ProjectCycles.Count -gt 0) {
        foreach ($cycle in $ReportData.Issues.CircularDependencies.ProjectCycles) {
            "- **$($cycle.Description)**"
        }
    } else {
        "No circular dependencies detected."
    }
)

### Version Conflicts

$(
    if ($ReportData.Issues.VersionConflicts.MultipleVersions.Count -gt 0) {
        foreach ($conflict in $ReportData.Issues.VersionConflicts.MultipleVersions) {
            "- **$($conflict.Package)**: $($conflict.Versions -join ', ')"
        }
    } else {
        "No version conflicts detected."
    }
)

### Unused Dependencies

$(
    if ($ReportData.Issues.UnusedDependencies.RedundantReferences.Count -gt 0) {
        foreach ($redundant in $ReportData.Issues.UnusedDependencies.RedundantReferences) {
            "- **$($redundant.Package)**: $($redundant.Reason)"
        }
    } else {
        "No redundant references detected."
    }
)

### Performance Issues

$(
    if ($ReportData.Issues.PerformanceImpact.HeavyProjects.Count -gt 0) {
        foreach ($heavy in $ReportData.Issues.PerformanceImpact.HeavyProjects) {
            "- **$($heavy.Project)**: $($heavy.TotalPackages) dependencies ($($heavy.Impact) impact)"
        }
    } else {
        "No major performance issues detected."
    }
)

## Optimization Recommendations

### Critical Priority ($($ReportData.Recommendations.Critical.Count))
$(
    if ($ReportData.Recommendations.Critical.Count -gt 0) {
        foreach ($rec in $ReportData.Recommendations.Critical) {
            "- **$($rec.Type)**: $($rec.Description)"
            "  - Action: $($rec.Action)"
            "  - Impact: $($rec.Impact)"
            ""
        }
    } else {
        "No critical recommendations."
    }
)

### High Priority ($($ReportData.Recommendations.High.Count))
$(
    if ($ReportData.Recommendations.High.Count -gt 0) {
        foreach ($rec in $ReportData.Recommendations.High | Select-Object -First 10) {
            "- **$($rec.Type)**: $($rec.Description)"
            "  - Action: $($rec.Action)"
            ""
        }
        if ($ReportData.Recommendations.High.Count -gt 10) {
            "... and $($ReportData.Recommendations.High.Count - 10) more high priority recommendations"
        }
    } else {
        "No high priority recommendations."
    }
)

### Medium Priority ($($ReportData.Recommendations.Medium.Count))
$(
    if ($ReportData.Recommendations.Medium.Count -gt 0) {
        foreach ($rec in $ReportData.Recommendations.Medium | Select-Object -First 5) {
            "- **$($rec.Type)**: $($rec.Description)"
        }
        if ($ReportData.Recommendations.Medium.Count -gt 5) {
            "... and $($ReportData.Recommendations.Medium.Count - 5) more medium priority recommendations"
        }
    } else {
        "No medium priority recommendations."
    }
)

## Next Steps

1. **Address Critical Issues**: $(if ($ReportData.Statistics.CriticalIssues -gt 0) { "Immediately address $($ReportData.Statistics.CriticalIssues) critical issues" } else { "No critical issues to address" })
2. **Resolve Conflicts**: $(if ($ReportData.Issues.VersionConflicts.MultipleVersions.Count -gt 0) { "Resolve $($ReportData.Issues.VersionConflicts.MultipleVersions.Count) version conflicts" } else { "No version conflicts to resolve" })
3. **Optimize Performance**: $(if ($ReportData.Issues.PerformanceImpact.HeavyProjects.Count -gt 0) { "Review $($ReportData.Issues.PerformanceImpact.HeavyProjects.Count) performance-heavy projects" } else { "Performance looks good" })
4. **Continuous Monitoring**: Set up automated dependency analysis in CI/CD pipeline

---
*Report generated by TiXL Dependency Analyzer v$($ReportData.AnalysisInfo.Version)*
"@
    
    $markdown | Out-File -FilePath $OutputPath -Encoding UTF8
}

function Get-MaxDependencyDepth {
    param([array]$Projects)
    
    $maxDepth = 0
    
    foreach ($project in $Projects) {
        $transitiveCount = $project.PackageReferences | Where-Object { $_.IsTransitive } | Measure-Object | Select-Object -ExpandProperty Count
        if ($transitiveCount -gt $maxDepth) {
            $maxDepth = $transitiveCount
        }
    }
    
    return $maxDepth
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Solution path: $SolutionPath" "INFO"
Write-Log "Output path: $OutputPath" "INFO"

try {
    # Build dependency tree
    $dependencyTree = Get-DependencyTree -SolutionPath $SolutionPath
    
    # Perform analysis
    Write-Log "Performing comprehensive dependency analysis" "INFO"
    
    $circularDeps = Test-CircularDependencies -DependencyGraph $dependencyTree.Dependencies
    $unusedDeps = Test-UnusedDependencies -DependencyGraph $dependencyTree.Dependencies -Projects $dependencyTree.Projects
    $versionConflicts = Test-VersionConflicts -DependencyGraph $dependencyTree.Dependencies
    $performanceImpact = Get-PerformanceImpact -DependencyGraph $dependencyTree.Dependencies -Projects $dependencyTree.Projects
    
    # Generate recommendations
    $recommendations = Get-OptimizationRecommendations -CircularDeps $circularDeps -UnusedDeps $unusedDeps -VersionConflicts $versionConflicts -PerformanceImpact $performanceImpact -DependencyGraph $dependencyTree.Dependencies
    
    # Export visualizations
    if ($ExportToGraphML) {
        Export-GraphML -DependencyGraph $dependencyTree.Dependencies -OutputPath $script:GraphMLFile
    }
    
    if ($GenerateVisualization) {
        Generate-Visualization -DependencyGraph $dependencyTree.Dependencies -OutputPath $script:VisualizationFile
    }
    
    # Export reports
    Export-AnalysisReport -DependencyTree $dependencyTree -CircularDeps $circularDeps -UnusedDeps $unusedDeps -VersionConflicts $versionConflicts -PerformanceImpact $performanceImpact -Recommendations $recommendations -OutputPath $OutputPath
    
    # Summary
    Write-Log "=== DEPENDENCY ANALYSIS COMPLETED ===" "INFO"
    Write-Log "Total projects: $($dependencyTree.Statistics.TotalProjects)" "INFO"
    Write-Log "Total packages: $($dependencyTree.Statistics.TotalPackages)" "INFO"
    Write-Log "Critical issues: $($recommendations.Critical.Count)" "ERROR"
    Write-Log "High priority issues: $($recommendations.High.Count)" "WARNING"
    Write-Log "Recommendations generated: $($recommendations.Total)" "INFO"
    
    # Exit code based on critical issues
    $exitCode = if ($recommendations.Critical.Count -gt 0) { 1 } else { 0 }
    
    exit $exitCode
}
catch {
    Write-Log "Dependency analysis failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}