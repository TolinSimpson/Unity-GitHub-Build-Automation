# GitHub Build Pipeline - Auto-Setup

This document explains how the automatic workflow setup system works.

## Overview

The GitHub Build Pipeline includes an intelligent auto-setup system that ensures users always have the necessary GitHub Actions workflow files, even when importing the build pipeline from external sources.

## How It Works

### 1. Bundled Workflows
- The package includes `Resources/workflows.zip` containing the latest workflow files
- This ZIP is automatically updated whenever you generate new workflow files
- The ZIP is included when sharing or packaging the build pipeline

### 2. Auto-Extraction Process
```csharp
[InitializeOnLoad]
public class WorkflowAutoSetup
{
    static WorkflowAutoSetup()
    {
        EditorApplication.delayCall += CheckAndSetupWorkflows;
    }
}
```

- Runs automatically when Unity loads the project
- Checks if `.github/workflows/` exists at the project root
- If missing, extracts workflows from the bundled ZIP
- Logs the process to Unity console

### 3. Files Included
- `unity-build.yml` - Main Unity build workflow
- `create-dmg.yml` - DMG creation workflow with comprehensive debug logging

## For Package Authors

When distributing the GitHub Build Pipeline:

1. **Generate Latest Workflows**: Call `WorkflowAutoSetup.RegenerateWorkflowsZip()` in Unity Console
2. **Verify ZIP Creation**: Ensure `workflows.zip` is created in `Resources/`
3. **Include All Files**: Make sure to include the entire `GitHub_Build_Pipeline/` folder
4. **Test Auto-Setup**: Delete `.github/workflows/` and restart Unity to test extraction

## For Package Users

When importing the build pipeline:

1. **Automatic Setup**: Workflow files are extracted automatically on first Unity load
2. **No Manual Steps**: No need to manually copy workflow files
3. **Commit Workflows**: Add the extracted `.github/workflows/` files to your repository
4. **Push to GitHub**: Ensure workflows are available in your GitHub repository

## Troubleshooting

### Workflows Not Extracted
- Check Unity Console for error messages
- Verify `workflows.zip` exists in `Assets/GitHub_Build_Pipeline/Resources/`
- Try manually calling `WorkflowAutoSetup.RegenerateWorkflowsZip()` in Unity Console

### Permission Issues
- Ensure Unity has write permissions to the project root directory
- Check that `.github/workflows/` is not read-only

### Missing Files
- Re-import the GitHub Build Pipeline package
- Regenerate workflow files using the setup tool

## Technical Details

### File Locations
- **Source ZIP**: `Assets/GitHub_Build_Pipeline/Resources/workflows.zip`
- **Extraction Target**: `{ProjectRoot}/.github/workflows/`
- **Auto-Setup Script**: `Assets/GitHub_Build_Pipeline/Editor/WorkflowAutoSetup.cs`

### Dependencies
- Uses `System.IO.Compression` for ZIP extraction
- Requires Unity Editor (Editor-only script)
- No external dependencies

### Safety Features
- Only extracts if workflows don't exist
- Overwrites files if they exist but are outdated
- Logs all operations to Unity Console
- Graceful error handling
- Workflows include comprehensive debug logging for troubleshooting 