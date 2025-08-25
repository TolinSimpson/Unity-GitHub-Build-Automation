# GitHub Build Pipeline

A comprehensive Unity build pipeline that supports multi-platform builds, code signing, packaging, and GitHub releases.

## Features

- ✅ Multi-platform builds (Windows, macOS, Linux)
- ✅ macOS code signing and notarization
- ✅ GitHub Actions-powered DMG creation with comprehensive debug logging
- ✅ Windows installer generation (Inno Setup)
- ✅ Automated GitHub releases
- ✅ Build profiles support
- ✅ Version management
- ✅ Dynamic configuration from Unity build settings
- ✅ Enhanced error reporting and troubleshooting

## Platform-Specific Outputs

### Windows
- **Executable**: `.exe` file
- **Package**: `.zip` archive
- **Installer**: `.exe` installer (when Inno Setup is available)

### macOS
- **Executable**: `.app` bundle
- **Package**: `.zip` archive (local) + `.dmg` disk image (via GitHub Actions)
- **Features**: Professional DMG with drag-and-drop installation interface

### Linux
- **Executable**: Binary file
- **Package**: `.zip` archive

## Setup

### Prerequisites

1. **Unity 2022.3+** with target platform modules installed
2. **GitHub repository** with appropriate secrets configured
3. **Platform-specific tools** (see below)

### GitHub Secrets Required

Add these secrets to your GitHub repository:

```
UNITY_LICENSE    # Your Unity license (Personal/Plus/Pro)
UNITY_EMAIL      # Unity account email
UNITY_PASSWORD   # Unity account password
```

### macOS Code Signing Secrets (Optional)

For professional macOS distribution with proper code signing:

```
P12_CERT         # Your Developer ID certificate as base64-encoded P12 file
P12_PASSWORD     # Password for the P12 certificate (if required)
APPLE_ID_PASSWORD # App-specific password for notarization
```


### GitHub Workflows Setup

DMG creation is now handled by GitHub Actions. The workflow files are automatically set up when you import the build pipeline.

#### Automatic Setup (Recommended)
When you open a Unity project with the GitHub Build Pipeline for the first time, the workflow files will be **automatically extracted** to `.github/workflows/` at your project root.

#### Manual Setup
If you need to regenerate the workflow files, you can call the regeneration method directly:

1. Open Unity
2. In the Console, run: `GitHub_Build_Pipeline.WorkflowAutoSetup.RegenerateWorkflowsZip()`
3. Commit and push the generated `.github/workflows/` files to your repository

**Note**: No local tools are required - DMG creation happens entirely on GitHub's macOS runners.

### How Auto-Setup Works

The build pipeline includes a smart auto-setup system:

1. **Bundled Workflows**: The package includes a `workflows.zip` file containing the latest GitHub Actions workflows
2. **Auto-Extraction**: When Unity loads, the `WorkflowAutoSetup` script automatically checks if `.github/workflows/` exists
3. **Seamless Setup**: If the folder is missing, the workflows are extracted automatically
4. **Always Up-to-Date**: When you generate new workflow files, the ZIP is automatically updated

This ensures that anyone importing your build pipeline gets the correct workflow files without manual setup.

## Usage

### Local Building

1. Open the Build Pipeline window: `Build → Build Pipeline`
2. Configure your settings:
   - **Version**: Auto-increment or manual
   - **Platforms**: Select Windows, macOS, and/or Linux
   - **Pipeline Steps**: Enable signing, installers, GitHub upload as needed
   - **DMG Creation**: Enable to trigger GitHub Actions DMG workflow
3. Click "Start Build Pipeline"

### Manual DMG Creation

To create a DMG from an existing GitHub release:

1. Configure GitHub repository URL and token
2. Click "Create DMG" in the Individual Steps section
3. The DMG will be automatically attached to the latest release

### GitHub Actions

The pipeline includes comprehensive GitHub Actions workflows:

#### Unity Build Workflow
1. **Builds** on native runners (Windows, macOS, Linux)
2. **Creates ZIP packages** for all platforms
3. **Uploads to GitHub Releases** automatically

#### DMG Creation Workflow
1. **Downloads** macOS ZIP from the latest GitHub release
2. **Creates professional DMG** with drag-and-drop interface
3. **Attaches DMG** directly to the GitHub release
4. **Comprehensive logging** with emoji-rich debug output for easy troubleshooting
5. **Dynamic configuration** using parameters from Unity build settings
6. **Enhanced error reporting** with actionable solutions

#### Triggering Builds

- **Push to main/develop**: Builds all platforms
- **Create Release**: Builds and publishes release assets
- **Manual Trigger**: Go to Actions → Unity Build Pipeline → Run workflow

### DMG Creation Details

The macOS DMG creation process:

1. **Professional DMG Creation**: Uses `create-dmg` for polished DMG files with:
   - Custom volume name and icon
   - Drag-and-drop interface with Applications folder shortcut
   - Proper window sizing and positioning
   - Professional appearance for distribution

2. **Enhanced Code Signing Integration**: 
   - Dynamic configuration from Unity build settings (Team ID, Apple ID)
   - Supports both GitHub secrets and local certificate signing
   - Automatic fallback to ad-hoc signing if certificate issues occur
   - Timeout protection prevents hanging on security commands

   - Proper GitHub secrets management for sensitive data

3. **Comprehensive Debug Logging**:
   - Emoji-rich output for easy visual scanning (🔧 🔐 📦 ✅ ❌)
   - Step-by-step progress tracking
   - Detailed error messages with actionable solutions
   - Asset discovery and validation logging
   - Certificate import and validation progress

4. **Distribution Optimization**:
   - Sets proper file permissions (`chmod 644`) for cross-user compatibility
   - Removes quarantine attributes to reduce security warnings
   - Creates readable files for all users
   - Professional DMG formatting for end-user distribution

5. **Fallback Methods & Error Handling**: 
   - Uses native `hdiutil` if `create-dmg` is unavailable
   - Always creates ZIP file as backup option
   - Enhanced error reporting with available asset listings
   - Workflow summary with build information and download links
   - Graceful handling of missing dependencies

## Configuration

### Build Profiles

Enable "Use Build Profiles" to use platform-specific settings:

```
Assets/Settings/Build Profiles/
├── Windows Profile.asset
├── MacOS Profile.asset
└── Linux Profile.asset
```

### macOS Signing

For distribution outside the Mac App Store:

#### Local Signing (Unity Editor)
1. **Certificate**: Export Developer ID Application certificate as .p12
2. **Entitlements**: Use custom or default hardened runtime entitlements
3. **Configuration**: Set certificate path and password in build pipeline window

#### GitHub Actions Signing (Recommended)
1. **GitHub Secrets**: Configure `P12_CERT` secret with base64-encoded certificate
2. **Password Management**: Set `P12_PASSWORD` if your certificate requires a password
3. **Dynamic Configuration**: Team ID and Apple ID are automatically sourced from Unity build settings
4. **Automatic Fallback**: If signing fails, automatically falls back to ad-hoc signing
5. **Timeout Protection**: All signing operations have timeout protection to prevent hanging
6. **Enhanced Debugging**: Comprehensive logging with certificate validation and identity discovery

#### Notarization (Optional)
1. **Requirements**: Apple Developer account with notarization access
2. **Secrets**: `APPLE_ID_PASSWORD` (app-specific password)
3. **Configuration**: Set Apple ID and Team ID in build pipeline
4. **Automatic**: Runs after successful Developer ID signing

### Windows Installer

Requires [Inno Setup](https://jrsoftware.org/isinfo.php):

- Download and install Inno Setup
- Configure publisher information
- Set installation options

## File Structure

```
GitHub_Build_Pipeline/
├── Editor/
│   ├── BuildPipelineWindow.cs     # Main build pipeline UI
│   ├── GitHubAPI.cs              # GitHub API integration
│   └── WorkflowAutoSetup.cs      # Auto-setup workflow files
├── Runtime/
│   ├── FileUtility.cs            # File operations (ZIP creation)
│   └── UpdateChecker.cs          # Auto-update functionality
├── Resources/
│   └── workflows.zip             # Bundled GitHub Actions workflows
└── README.md                     # This file
```

## Troubleshooting

### DMG Creation Issues

**⚠️ Important: If your DMG files fail to open on Mac, see the [macOS DMG Troubleshooting Guide](MACOS_DMG_TROUBLESHOOTING.md) for detailed solutions.**

Common issues and quick fixes:

**Problem**: DMG workflow fails to trigger
**Solutions**:
1. Verify GitHub repository URL and token are configured
2. Ensure workflow files exist in `.github/workflows/`
3. Check repository permissions for the GitHub token
4. Look for the comprehensive debug output in GitHub Actions logs

**Problem**: DMG creation workflow fails
**Solutions**:
1. Check GitHub Actions logs for detailed error messages with emoji indicators
2. Verify the macOS ZIP file exists in the release (workflow will list available assets)
3. Ensure the ZIP contains a valid .app bundle
4. Check the workflow summary for build information and error details

**Problem**: Code signing issues
**Solutions**:
1. Review certificate validation logs in the workflow output
2. Verify Team ID and Apple ID are properly configured in Unity build settings
3. Ensure `APPLE_ID_PASSWORD` is set as a GitHub secret
4. Check for timeout errors in the enhanced debug logs

**Problem**: Missing debug information
**Solutions**:
1. All workflow steps now include comprehensive emoji-rich logging
2. Look for step-specific indicators: 🔧 (setup), 🔐 (signing), 📦 (packaging), ✅ (success), ❌ (errors)
3. Check the workflow summary for a complete overview of the build process



### GitHub Actions Issues

**Problem**: Build fails with license error
**Solution**:
- Verify `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secrets
- Ensure license supports target platforms

**Problem**: macOS DMG creation fails in GitHub Actions
**Solution**:
- Check the workflow log for create-dmg installation (look for 🍺 brew install logs)
- Verify GitHub secrets are properly configured (P12_CERT, P12_PASSWORD, APPLE_ID_PASSWORD)
- The workflow will automatically fall back to ad-hoc signing if certificate issues occur
- DMG creation will fall back to ZIP if all methods fail
- Review the comprehensive workflow summary for detailed error information

**Problem**: Code signing hangs or times out
**Solution**:
- The workflow now includes automatic timeout protection (30-120 seconds)
- Check GitHub Actions logs for specific timeout errors with emoji indicators
- Verify certificate format and password are correct
- Look for certificate validation messages in the debug output
- Ensure Team ID and Apple ID are properly configured in Unity build settings

**Problem**: Configuration issues
**Solution**:
- Workflow now dynamically reads Team ID and Apple ID from Unity build settings
- No more hardcoded values - all configuration comes from the build pipeline
- Check the setup step logs for parameter parsing details
- Verify Unity build pipeline settings are properly configured

### General Build Issues

**Problem**: Build fails on specific platform
**Solutions**:
1. Ensure platform module is installed
2. Check build profiles configuration
3. Verify PlayerSettings for target platform

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test on multiple platforms
5. Submit a pull request

## License

MIT 