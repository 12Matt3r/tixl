using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Safe project file I/O operations with versioning and backup support
    /// </summary>
    public static class ProjectFileIOSafety
    {
        private const string PROJECT_BACKUP_DIRECTORY = "backups";
        private const string PROJECT_EXTENSION = ".tixlproject";
        private const string SETTINGS_EXTENSION = ".tixlsettings";
        private const string WORKSPACE_EXTENSION = ".tixlworkspace";
        
        #region Project Management
        
        /// <summary>
        /// Safely creates a new project file with versioning and backup support
        /// </summary>
        public static async Task<ProjectCreateResult> CreateProjectAsync(ProjectMetadata project, string projectPath, bool createBackup = true)
        {
            try
            {
                if (project == null)
                {
                    return ProjectCreateResult.Failed("Project metadata cannot be null");
                }
                
                // Validate project path
                var safeFileIO = SafeFileIO.Instance;
                var pathValidation = safeFileIO.ValidateWritePath(projectPath);
                if (!pathValidation.IsValid)
                {
                    return ProjectCreateResult.Failed($"Invalid project path: {pathValidation.ErrorMessage}");
                }
                
                // Ensure project directory exists
                var directoryResult = await safeFileIO.SafeCreateDirectoryAsync(Path.GetDirectoryName(projectPath));
                if (!directoryResult.IsSuccess)
                {
                    return ProjectCreateResult.Failed($"Failed to create project directory: {directoryResult.ErrorMessage}");
                }
                
                // Validate project metadata
                var validation = ValidateProjectMetadata(project);
                if (!validation.IsValid)
                {
                    return ProjectCreateResult.Failed($"Invalid project metadata: {validation.ErrorMessage}");
                }
                
                // Create project file
                var projectData = CreateProjectData(project);
                var serializationResult = await SafeSerialization.SafeSerializeToJsonAsync(projectData, projectPath, prettyPrint: true, createBackup);
                
                if (!serializationResult.IsSuccess)
                {
                    return ProjectCreateResult.Failed($"Project serialization failed: {serializationResult.ErrorMessage}");
                }
                
                // Create backup directory and initial backup
                if (createBackup)
                {
                    var backupDir = GetProjectBackupDirectory(projectPath);
                    await safeFileIO.SafeCreateDirectoryAsync(backupDir);
                    
                    // Create initial backup
                    await SafeFileIO.Instance.SafeWriteAsync(
                        Path.Combine(backupDir, "initial_backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json"),
                        await GetProjectContentAsString(projectPath)
                    );
                }
                
                return ProjectCreateResult.Success(projectPath);
            }
            catch (Exception ex)
            {
                return ProjectCreateResult.Failed($"Project creation failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely loads a project file with validation
        /// </summary>
        public static async Task<ProjectLoadResult> LoadProjectAsync(string projectPath)
        {
            try
            {
                // Validate project path
                var pathValidation = SafeFileIO.Instance.ValidateWritePath(projectPath);
                if (!pathValidation.IsValid)
                {
                    pathValidation = SafeFileIO.Instance.ValidateReadPath(projectPath);
                    if (!pathValidation.IsValid)
                    {
                        return ProjectLoadResult.Failed($"Invalid project path: {pathValidation.ErrorMessage}");
                    }
                }
                
                if (!File.Exists(projectPath))
                {
                    return ProjectLoadResult.Failed("Project file not found");
                }
                
                // Check file signature and version compatibility
                var compatibilityCheck = await CheckProjectCompatibility(projectPath);
                if (!compatibilityCheck.IsCompatible)
                {
                    return ProjectLoadResult.Failed($"Incompatible project version: {compatibilityCheck.ErrorMessage}");
                }
                
                // Deserialize project
                var deserializationResult = await SafeSerialization.SafeDeserializeFromJsonAsync<ProjectData>(projectPath);
                if (!deserializationResult.IsSuccess)
                {
                    return ProjectLoadResult.Failed($"Project deserialization failed: {deserializationResult.ErrorMessage}");
                }
                
                // Validate loaded project data
                var validation = ValidateProjectData(deserializationResult.Data);
                if (!validation.IsValid)
                {
                    return ProjectLoadResult.Failed($"Invalid project data: {validation.ErrorMessage}");
                }
                
                // Extract metadata
                var metadata = ExtractProjectMetadata(deserializationResult.Data);
                
                return ProjectLoadResult.Success(metadata, deserializationResult.Data);
            }
            catch (Exception ex)
            {
                return ProjectLoadResult.Failed($"Project load failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely saves a project with versioning and backup
        /// </summary>
        public static async Task<ProjectSaveResult> SaveProjectAsync(ProjectMetadata metadata, ProjectData projectData, string projectPath, bool createBackup = true)
        {
            try
            {
                // Validate inputs
                if (metadata == null || projectData == null)
                {
                    return ProjectSaveResult.Failed("Project metadata and data cannot be null");
                }
                
                // Validate project path
                var pathValidation = SafeFileIO.Instance.ValidateWritePath(projectPath);
                if (!pathValidation.IsValid)
                {
                    return ProjectSaveResult.Failed($"Invalid project path: {pathValidation.ErrorMessage}");
                }
                
                // Create backup if requested and file exists
                if (createBackup && File.Exists(projectPath))
                {
                    var backupResult = await CreateBackupAsync(projectPath);
                    if (!backupResult.IsSuccess)
                    {
                        // Continue anyway, backup failure shouldn't block save
                    }
                }
                
                // Validate project data
                var validation = ValidateProjectData(projectData);
                if (!validation.IsValid)
                {
                    return ProjectSaveResult.Failed($"Invalid project data: {validation.ErrorMessage}");
                }
                
                // Update metadata with save information
                projectData.Metadata = UpdateMetadataForSave(metadata, projectData);
                
                // Serialize and save
                var serializationResult = await SafeSerialization.SafeSerializeToJsonAsync(projectData, projectPath, prettyPrint: true, createBackup: false);
                if (!serializationResult.IsSuccess)
                {
                    return ProjectSaveResult.Failed($"Project serialization failed: {serializationResult.ErrorMessage}");
                }
                
                return ProjectSaveResult.Success(projectPath);
            }
            catch (Exception ex)
            {
                return ProjectSaveResult.Failed($"Project save failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates a project backup with timestamp
        /// </summary>
        public static async Task<BackupResult> CreateBackupAsync(string projectPath)
        {
            try
            {
                var backupDir = GetProjectBackupDirectory(projectPath);
                var backupName = Path.GetFileNameWithoutExtension(projectPath) + "_backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff") + ".json";
                var backupPath = Path.Combine(backupDir, backupName);
                
                var readResult = await SafeFileIO.Instance.SafeReadAllTextAsync(projectPath);
                if (!readResult.IsSuccess)
                {
                    return BackupResult.Failed(readResult.ErrorMessage);
                }
                
                var writeResult = await SafeFileIO.Instance.SafeWriteAsync(backupPath, readResult.Data, createBackup: false);
                if (!writeResult.IsSuccess)
                {
                    return BackupResult.Failed(writeResult.ErrorMessage);
                }
                
                return BackupResult.Success(backupPath);
            }
            catch (Exception ex)
            {
                return BackupResult.Failed($"Backup creation failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Restores a project from backup
        /// </summary>
        public static async Task<ProjectRestoreResult> RestoreFromBackupAsync(string projectPath, string backupPath = null)
        {
            try
            {
                // Find backup if not specified
                if (backupPath == null)
                {
                    var backups = GetProjectBackups(projectPath);
                    if (backups.Count == 0)
                    {
                        return ProjectRestoreResult.Failed("No backups found");
                    }
                    backupPath = backups.OrderByDescending(b => new FileInfo(b).LastWriteTime).First();
                }
                
                if (!File.Exists(backupPath))
                {
                    return ProjectRestoreResult.Failed("Backup file not found");
                }
                
                // Validate backup file compatibility
                var compatibilityCheck = await CheckProjectCompatibility(backupPath);
                if (!compatibilityCheck.IsCompatible)
                {
                    return ProjectRestoreResult.Failed($"Incompatible backup version: {compatibilityCheck.ErrorMessage}");
                }
                
                // Create current project backup before restore
                if (File.Exists(projectPath))
                {
                    await CreateBackupAsync(projectPath);
                }
                
                // Restore from backup
                var readResult = await SafeFileIO.Instance.SafeReadAllTextAsync(backupPath);
                if (!readResult.IsSuccess)
                {
                    return ProjectRestoreResult.Failed(readResult.ErrorMessage);
                }
                
                var writeResult = await SafeFileIO.Instance.SafeWriteAsync(projectPath, readResult.Data, createBackup: false);
                if (!writeResult.IsSuccess)
                {
                    return ProjectRestoreResult.Failed(writeResult.ErrorMessage);
                }
                
                return ProjectRestoreResult.Success(projectPath, backupPath);
            }
            catch (Exception ex)
            {
                return ProjectRestoreResult.Failed($"Project restore failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Workspace Management
        
        /// <summary>
        /// Safely saves workspace settings
        /// </summary>
        public static async Task<WorkspaceSaveResult> SaveWorkspaceAsync(WorkspaceSettings settings, string workspacePath)
        {
            try
            {
                if (settings == null)
                {
                    return WorkspaceSaveResult.Failed("Workspace settings cannot be null");
                }
                
                // Validate workspace path
                var pathValidation = SafeFileIO.Instance.ValidateWritePath(workspacePath);
                if (!pathValidation.IsValid)
                {
                    return WorkspaceSaveResult.Failed($"Invalid workspace path: {pathValidation.ErrorMessage}");
                }
                
                // Validate settings
                var validation = ValidateWorkspaceSettings(settings);
                if (!validation.IsValid)
                {
                    return WorkspaceSaveResult.Failed($"Invalid workspace settings: {validation.ErrorMessage}");
                }
                
                // Serialize and save
                var serializationResult = await SafeSerialization.SafeSerializeToJsonAsync(settings, workspacePath, prettyPrint: true, createBackup: true);
                if (!serializationResult.IsSuccess)
                {
                    return WorkspaceSaveResult.Failed($"Workspace serialization failed: {serializationResult.ErrorMessage}");
                }
                
                return WorkspaceSaveResult.Success(workspacePath);
            }
            catch (Exception ex)
            {
                return WorkspaceSaveResult.Failed($"Workspace save failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely loads workspace settings
        /// </summary>
        public static async Task<WorkspaceLoadResult> LoadWorkspaceAsync(string workspacePath)
        {
            try
            {
                // Validate workspace path
                var pathValidation = SafeFileIO.Instance.ValidateWritePath(workspacePath);
                if (!pathValidation.IsValid)
                {
                    pathValidation = SafeFileIO.Instance.ValidateReadPath(workspacePath);
                    if (!pathValidation.IsValid)
                    {
                        return WorkspaceLoadResult.Failed($"Invalid workspace path: {pathValidation.ErrorMessage}");
                    }
                }
                
                if (!File.Exists(workspacePath))
                {
                    return WorkspaceLoadResult.Failed("Workspace file not found");
                }
                
                // Load and validate
                var loadResult = await SafeSerialization.SafeDeserializeFromJsonAsync<WorkspaceSettings>(workspacePath);
                if (!loadResult.IsSuccess)
                {
                    return WorkspaceLoadResult.Failed($"Workspace deserialization failed: {loadResult.ErrorMessage}");
                }
                
                var validation = ValidateWorkspaceSettings(loadResult.Data);
                if (!validation.IsValid)
                {
                    return WorkspaceLoadResult.Failed($"Invalid workspace settings: {validation.ErrorMessage}");
                }
                
                return WorkspaceLoadResult.Success(loadResult.Data);
            }
            catch (Exception ex)
            {
                return WorkspaceLoadResult.Failed($"Workspace load failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Project Discovery
        
        /// <summary>
        /// Finds all project files in a directory and subdirectories
        /// </summary>
        public static async Task<ProjectDiscoveryResult> DiscoverProjectsAsync(string directoryPath, bool recursive = true)
        {
            try
            {
                // Validate directory
                var pathValidation = SafeFileIO.Instance.ValidateReadPath(directoryPath);
                if (!pathValidation.IsValid)
                {
                    return ProjectDiscoveryResult.Failed($"Invalid directory path: {pathValidation.ErrorMessage}");
                }
                
                if (!Directory.Exists(directoryPath))
                {
                    return ProjectDiscoveryResult.Failed("Directory not found");
                }
                
                // Search for project files
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var projectFiles = Directory.GetFiles(directoryPath, "*" + PROJECT_EXTENSION, searchOption).ToList();
                
                var projects = new List<ProjectMetadata>();
                var errors = new List<string>();
                
                foreach (var projectFile in projectFiles)
                {
                    try
                    {
                        var loadResult = await LoadProjectAsync(projectFile);
                        if (loadResult.IsSuccess)
                        {
                            projects.Add(loadResult.Metadata);
                        }
                        else
                        {
                            errors.Add($"{Path.GetFileName(projectFile)}: {loadResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(projectFile)}: {ex.Message}");
                    }
                }
                
                return ProjectDiscoveryResult.Success(projects, errors);
            }
            catch (Exception ex)
            {
                return ProjectDiscoveryResult.Failed($"Project discovery failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets recent projects with metadata
        /// </summary>
        public static List<RecentProjectInfo> GetRecentProjects(string projectsDirectory, int maxCount = 10)
        {
            try
            {
                if (!Directory.Exists(projectsDirectory))
                {
                    return new List<RecentProjectInfo>();
                }
                
                var projectFiles = Directory.GetFiles(projectsDirectory, "*" + PROJECT_EXTENSION, SearchOption.TopDirectoryOnly)
                    .Select(file => new FileInfo(file))
                    .Where(info => info.Exists)
                    .OrderByDescending(info => info.LastWriteTime)
                    .Take(maxCount)
                    .ToList();
                
                var recentProjects = new List<RecentProjectInfo>();
                
                foreach (var fileInfo in projectFiles)
                {
                    try
                    {
                        var loadResult = LoadProjectAsync(fileInfo.FullName).Result;
                        if (loadResult.IsSuccess)
                        {
                            recentProjects.Add(new RecentProjectInfo
                            {
                                Path = fileInfo.FullName,
                                Name = loadResult.Metadata.Name,
                                ModifiedTime = fileInfo.LastWriteTime,
                                FileSize = fileInfo.Length
                            });
                        }
                    }
                    catch
                    {
                        // Skip projects that can't be loaded
                        recentProjects.Add(new RecentProjectInfo
                        {
                            Path = fileInfo.FullName,
                            Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
                            ModifiedTime = fileInfo.LastWriteTime,
                            FileSize = fileInfo.Length,
                            HasLoadError = true
                        });
                    }
                }
                
                return recentProjects;
            }
            catch
            {
                return new List<RecentProjectInfo>();
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private static ValidationResult ValidateProjectMetadata(ProjectMetadata metadata)
        {
            var errors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(metadata.Name))
                errors.Add("Project name cannot be empty");
            
            if (string.IsNullOrWhiteSpace(metadata.Id))
                errors.Add("Project ID cannot be empty");
            
            if (metadata.Version == null)
                errors.Add("Project version cannot be null");
            
            if (metadata.CreatedUtc == default)
                errors.Add("Project created time is invalid");
            
            if (errors.Count > 0)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = string.Join("; ", errors) };
            }
            
            return new ValidationResult { IsValid = true };
        }
        
        private static ValidationResult ValidateProjectData(ProjectData data)
        {
            var errors = new List<string>();
            
            if (data == null)
                return new ValidationResult { IsValid = false, ErrorMessage = "Project data cannot be null" };
            
            if (data.Metadata == null)
                errors.Add("Project metadata cannot be null");
            
            if (data.Version == null)
                errors.Add("Project version cannot be null");
            
            if (errors.Count > 0)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = string.Join("; ", errors) };
            }
            
            return new ValidationResult { IsValid = true };
        }
        
        private static ValidationResult ValidateWorkspaceSettings(WorkspaceSettings settings)
        {
            var errors = new List<string>();
            
            if (settings == null)
                return new ValidationResult { IsValid = false, ErrorMessage = "Workspace settings cannot be null" };
            
            // Validate UI settings if present
            if (settings.UISettings != null)
            {
                if (settings.UISettings.WindowWidth <= 0 || settings.UISettings.WindowHeight <= 0)
                    errors.Add("Invalid window dimensions");
            }
            
            if (errors.Count > 0)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = string.Join("; ", errors) };
            }
            
            return new ValidationResult { IsValid = true };
        }
        
        private static ProjectData CreateProjectData(ProjectMetadata metadata)
        {
            return new ProjectData
            {
                Metadata = metadata,
                Version = "1.0",
                SchemaVersion = 1,
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };
        }
        
        private static ProjectMetadata ExtractProjectMetadata(ProjectData data)
        {
            return data.Metadata ?? new ProjectMetadata
            {
                Name = "Unknown Project",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0",
                CreatedUtc = DateTime.UtcNow
            };
        }
        
        private static ProjectMetadata UpdateMetadataForSave(ProjectMetadata original, ProjectData projectData)
        {
            return original with
            {
                LastModifiedUtc = DateTime.UtcNow
            };
        }
        
        private static async Task<string> GetProjectContentAsString(string projectPath)
        {
            var readResult = await SafeFileIO.Instance.SafeReadAllTextAsync(projectPath);
            return readResult.IsSuccess ? readResult.Data : string.Empty;
        }
        
        private static string GetProjectBackupDirectory(string projectPath)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            return Path.Combine(projectDir, PROJECT_BACKUP_DIRECTORY);
        }
        
        private static List<string> GetProjectBackups(string projectPath)
        {
            var backupDir = GetProjectBackupDirectory(projectPath);
            if (!Directory.Exists(backupDir))
            {
                return new List<string>();
            }
            
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var backupPattern = $"{projectName}_backup_*.json";
            
            return Directory.GetFiles(backupDir, backupPattern, SearchOption.TopDirectoryOnly).ToList();
        }
        
        private static async Task<CompatibilityCheck> CheckProjectCompatibility(string projectPath)
        {
            try
            {
                var readResult = await SafeFileIO.Instance.SafeReadAllTextAsync(projectPath);
                if (!readResult.IsSuccess)
                {
                    return new CompatibilityCheck { IsCompatible = false, ErrorMessage = readResult.ErrorMessage };
                }
                
                // Basic compatibility check - validate structure
                using var document = JsonDocument.Parse(readResult.Data);
                var root = document.RootElement;
                
                // Check required fields
                if (!root.TryGetProperty("metadata", out var metadata) || 
                    !metadata.TryGetProperty("version", out var version))
                {
                    return new CompatibilityCheck { IsCompatible = false, ErrorMessage = "Invalid project format" };
                }
                
                var versionString = version.GetString();
                if (string.IsNullOrEmpty(versionString) || !Version.TryParse(versionString, out var _))
                {
                    return new CompatibilityCheck { IsCompatible = false, ErrorMessage = "Invalid project version" };
                }
                
                return new CompatibilityCheck { IsCompatible = true };
            }
            catch (Exception ex)
            {
                return new CompatibilityCheck { IsCompatible = false, ErrorMessage = ex.Message };
            }
        }
        
        #endregion
    }
    
    #region Data Models
    
    public class ProjectMetadata
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, string> Properties { get; set; } = new();
    }
    
    public class ProjectData
    {
        public ProjectMetadata Metadata { get; set; }
        public string Version { get; set; }
        public int SchemaVersion { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
    }
    
    public class WorkspaceSettings
    {
        public UISettings UISettings { get; set; }
        public Dictionary<string, object> Preferences { get; set; } = new();
        public List<string> RecentProjects { get; set; } = new();
        public Dictionary<string, object> EditorSettings { get; set; } = new();
    }
    
    public class UISettings
    {
        public int WindowWidth { get; set; } = 1200;
        public int WindowHeight { get; set; } = 800;
        public int WindowX { get; set; } = 100;
        public int WindowY { get; set; } = 100;
        public bool IsMaximized { get; set; } = false;
        public string Theme { get; set; } = "Default";
    }
    
    public class RecentProjectInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public DateTime ModifiedTime { get; set; }
        public long FileSize { get; set; }
        public bool HasLoadError { get; set; }
    }
    
    #endregion
    
    #region Result Classes
    
    public class ProjectCreateResult
    {
        public bool IsSuccess { get; set; }
        public string ProjectPath { get; set; }
        public string ErrorMessage { get; set; }
        
        public static ProjectCreateResult Success(string projectPath)
        {
            return new ProjectCreateResult { IsSuccess = true, ProjectPath = projectPath };
        }
        
        public static ProjectCreateResult Failed(string error)
        {
            return new ProjectCreateResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ProjectLoadResult
    {
        public bool IsSuccess { get; set; }
        public ProjectMetadata Metadata { get; set; }
        public ProjectData ProjectData { get; set; }
        public string ErrorMessage { get; set; }
        
        public static ProjectLoadResult Success(ProjectMetadata metadata, ProjectData projectData)
        {
            return new ProjectLoadResult { IsSuccess = true, Metadata = metadata, ProjectData = projectData };
        }
        
        public static ProjectLoadResult Failed(string error)
        {
            return new ProjectLoadResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ProjectSaveResult
    {
        public bool IsSuccess { get; set; }
        public string ProjectPath { get; set; }
        public string ErrorMessage { get; set; }
        
        public static ProjectSaveResult Success(string projectPath)
        {
            return new ProjectSaveResult { IsSuccess = true, ProjectPath = projectPath };
        }
        
        public static ProjectSaveResult Failed(string error)
        {
            return new ProjectSaveResult { IsValid = false, ErrorMessage = error };
        }
        
        public bool IsValid { get; set; } = true;
    }
    
    public class BackupResult
    {
        public bool IsSuccess { get; set; }
        public string BackupPath { get; set; }
        public string ErrorMessage { get; set; }
        
        public static BackupResult Success(string backupPath)
        {
            return new BackupResult { IsSuccess = true, BackupPath = backupPath };
        }
        
        public static BackupResult Failed(string error)
        {
            return new BackupResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ProjectRestoreResult
    {
        public bool IsSuccess { get; set; }
        public string ProjectPath { get; set; }
        public string BackupPath { get; set; }
        public string ErrorMessage { get; set; }
        
        public static ProjectRestoreResult Success(string projectPath, string backupPath)
        {
            return new ProjectRestoreResult { IsSuccess = true, ProjectPath = projectPath, BackupPath = backupPath };
        }
        
        public static ProjectRestoreResult Failed(string error)
        {
            return new ProjectRestoreResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class WorkspaceSaveResult
    {
        public bool IsSuccess { get; set; }
        public string WorkspacePath { get; set; }
        public string ErrorMessage { get; set; }
        
        public static WorkspaceSaveResult Success(string workspacePath)
        {
            return new WorkspaceSaveResult { IsSuccess = true, WorkspacePath = workspacePath };
        }
        
        public static WorkspaceSaveResult Failed(string error)
        {
            return new WorkspaceSaveResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class WorkspaceLoadResult
    {
        public bool IsSuccess { get; set; }
        public WorkspaceSettings Settings { get; set; }
        public string ErrorMessage { get; set; }
        
        public static WorkspaceLoadResult Success(WorkspaceSettings settings)
        {
            return new WorkspaceLoadResult { IsSuccess = true, Settings = settings };
        }
        
        public static WorkspaceLoadResult Failed(string error)
        {
            return new WorkspaceLoadResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ProjectDiscoveryResult
    {
        public bool IsSuccess { get; set; }
        public List<ProjectMetadata> Projects { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        
        public static ProjectDiscoveryResult Success(List<ProjectMetadata> projects, List<string> errors)
        {
            return new ProjectDiscoveryResult { IsSuccess = true, Projects = projects, Errors = errors };
        }
        
        public static ProjectDiscoveryResult Failed(string error)
        {
            return new ProjectDiscoveryResult { IsSuccess = false };
        }
    }
    
    public class CompatibilityCheck
    {
        public bool IsCompatible { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    #endregion
}