using UnityEngine;
using UnityEditor;
using System.IO;
using System.IO.Compression;

[InitializeOnLoad]
public class WorkflowAutoSetup
{
    private const string WORKFLOWS_ZIP_PATH = "Assets/GitHub_Build_Pipeline/Resources/workflows.zip";
    private const string TARGET_WORKFLOWS_DIR = ".github/workflows";
    
    static WorkflowAutoSetup()
    {
        // Run the setup check after Unity finishes loading
        EditorApplication.delayCall += CheckAndSetupWorkflows;
    }
    
    private static void CheckAndSetupWorkflows()
    {
        try
        {
            string projectRoot = Path.Combine(Application.dataPath, "..");
            string workflowsPath = Path.Combine(projectRoot, TARGET_WORKFLOWS_DIR);
            
            // Check if .github/workflows already exists
            if (Directory.Exists(workflowsPath))
            {
                return;
            }
            
            // Check if our workflows ZIP exists
            if (!File.Exists(WORKFLOWS_ZIP_PATH))
            {
                UnityEngine.Debug.LogWarning("[GitHub Build Pipeline] Workflow ZIP not found. Please call WorkflowAutoSetup.RegenerateWorkflowsZip() to regenerate workflow files.");
                return;
            }
            
            // Extract the workflows ZIP to project root
            ExtractWorkflowsZip(projectRoot);
            
            UnityEngine.Debug.Log("[GitHub Build Pipeline] GitHub Actions workflow files have been automatically extracted to .github/workflows/");
            
            // Refresh the project to show new files
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[GitHub Build Pipeline] Failed to auto-setup workflows: {ex.Message}");
        }
    }
    
    private static void ExtractWorkflowsZip(string projectRoot)
    {
        string zipPath = Path.GetFullPath(WORKFLOWS_ZIP_PATH);
        
        // Use System.IO.Compression to extract the ZIP
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                // Skip directories
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                
                string destinationPath = Path.Combine(projectRoot, entry.FullName);
                string destinationDir = Path.GetDirectoryName(destinationPath);
                
                // Create directory if it doesn't exist
                if (!Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }
                
                // Extract the file
                entry.ExtractToFile(destinationPath, overwrite: true);
                UnityEngine.Debug.Log($"[GitHub Build Pipeline] Extracted: {entry.FullName}");
            }
        }
    }
    
    /// <summary>
    /// Manual method to regenerate the workflows ZIP from current workflow files
    /// Regenerates the workflows ZIP file with current workflow content
    /// </summary>
    public static void RegenerateWorkflowsZip()
    {
        try
        {
            string projectRoot = Path.Combine(Application.dataPath, "..");
            string sourceWorkflowsPath = Path.Combine(projectRoot, TARGET_WORKFLOWS_DIR);
            
            if (!Directory.Exists(sourceWorkflowsPath))
            {
                UnityEngine.Debug.LogWarning("[GitHub Build Pipeline] No .github/workflows directory found to package.");
                return;
            }
            
            // Ensure Resources directory exists
            string resourcesDir = Path.GetDirectoryName(WORKFLOWS_ZIP_PATH);
            if (!Directory.Exists(resourcesDir))
            {
                Directory.CreateDirectory(resourcesDir);
            }
            
            // Create a new ZIP file
            if (File.Exists(WORKFLOWS_ZIP_PATH))
            {
                File.Delete(WORKFLOWS_ZIP_PATH);
            }
            
            // Create temporary directory structure
            string tempDir = FileUtility.GetTempDirectoryPath("workflows_packaging");
            string tempGithubDir = Path.Combine(tempDir, ".github");
            string tempWorkflowsDir = Path.Combine(tempGithubDir, "workflows");
            
            Directory.CreateDirectory(tempWorkflowsDir);
            
            // Copy workflow files to temp directory
            string[] workflowFiles = Directory.GetFiles(sourceWorkflowsPath, "*.yml");
            foreach (string file in workflowFiles)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(tempWorkflowsDir, fileName);
                File.Copy(file, destFile);
            }
            
            // Create ZIP from temp directory
            ZipFile.CreateFromDirectory(tempDir, WORKFLOWS_ZIP_PATH);
            
            // Clean up temp directory
            Directory.Delete(tempDir, true);
            
            UnityEngine.Debug.Log($"[GitHub Build Pipeline] Regenerated workflows.zip with {workflowFiles.Length} workflow files.");
            
            // Refresh to show the new ZIP file in Unity
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[GitHub Build Pipeline] Failed to regenerate workflows ZIP: {ex.Message}");
        }
    }
} 