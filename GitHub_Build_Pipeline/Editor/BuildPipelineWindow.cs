using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Profile;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

public class BuildPipelineWindow : EditorWindow
{
        #region Properties:

    // Version Settings
    private bool useAutoVersion = true;
    private string manualVersion = "";

    // Platform Selection
    private bool buildWindows = true;
    private bool buildMacOS = true;
    private bool buildLinux = true;

    // Build Profile Settings
    private bool useBuildProfiles = false;
    private BuildProfile windowsBuildProfile = null;
    private BuildProfile macOSBuildProfile = null;
    private BuildProfile linuxBuildProfile = null;
    private BuildProfile[] availableWindowsProfiles = new BuildProfile[0];
    private BuildProfile[] availableMacOSProfiles = new BuildProfile[0];
    private BuildProfile[] availableLinuxProfiles = new BuildProfile[0];
    private int selectedWindowsProfileIndex = 0;
    private int selectedMacOSProfileIndex = 0;
    private int selectedLinuxProfileIndex = 0;
    
    // Pipeline Steps
    private bool enableMacSigning = false;
    private bool enableWindowsInstaller = false;
    private bool enableGitHubRelease = false;
    
    // macOS Signing Settings
    private string p12FilePath = "";
    private string p12Password = "";
    private string macBundleIdentifier = "";
    private bool enableNotarization = false;
    private string providerShortName = "";
    private string appleIdUsername = "";
    private string appleIdPassword = "";
    private bool useCustomEntitlements = false;
    private string entitlementsFilePath = "";
    
    // Windows Installer Settings
    private string innoSetupPath = "";
    private string publisherName = "";
    private string appCopyright = "";
    private string publisherURL = "";
    private string supportURL = "";
    private string updatesURL = "";
    private bool createDesktopIcon = true;
    private bool createStartMenuIcon = true;
    private bool createUninstaller = true;
    private bool allowDirChange = true;
    
    // GitHub Settings
    private string repositoryUrl = "";
    private string githubToken = "";
    private string releaseTitle = "";
    private string releaseDescription = "";
    private bool includePrerelease = false;
    
    // DMG Settings
    private bool enableDMGCreation = false;
    
    // UI State
    private Vector2 scrollPosition;
    private bool isProcessing = false;
    private bool isCancellationRequested = false;
    private string currentBuildPlatform = "";
    private string statusMessage = "";
    private bool showPasswords = false;
    private bool showAdvancedSettings = false;
    private bool macSigningFoldout = false;
    private bool windowsInstallerFoldout = false;
    private bool githubReleaseFoldout = false;

    #endregion
    
    // Build paths
    private const string BUILD_PATH = "Builds";
    private const string RELEASE_PATH = "Releases";
    private static readonly string[] PLATFORMS = { "Windows", "MacOS", "Linux" };

    [MenuItem("Build/Build Pipeline")]
    public static void ShowWindow()
    {
        var window = GetWindow<BuildPipelineWindow>("Build Pipeline");
        window.minSize = new Vector2(450, 600);
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Version settings
        useAutoVersion = EditorPrefs.GetBool("BuildPipeline_UseAutoVersion", true);
        manualVersion = EditorPrefs.GetString("BuildPipeline_ManualVersion", Application.version);
        
        // Platform settings
        buildWindows = EditorPrefs.GetBool("BuildPipeline_BuildWindows", true);
        buildMacOS = EditorPrefs.GetBool("BuildPipeline_BuildMacOS", true);
        buildLinux = EditorPrefs.GetBool("BuildPipeline_BuildLinux", true);
        
        // Pipeline steps
        enableMacSigning = EditorPrefs.GetBool("BuildPipeline_EnableMacSigning", false);
        enableWindowsInstaller = EditorPrefs.GetBool("BuildPipeline_EnableWindowsInstaller", false);
        enableGitHubRelease = EditorPrefs.GetBool("BuildPipeline_EnableGitHubRelease", false);
        
        // macOS Signing
        p12FilePath = EditorPrefs.GetString("BuildPipeline_P12FilePath", "");
        macBundleIdentifier = EditorPrefs.GetString("BuildPipeline_MacBundleIdentifier", PlayerSettings.applicationIdentifier);
        enableNotarization = EditorPrefs.GetBool("BuildPipeline_EnableNotarization", false);
        providerShortName = EditorPrefs.GetString("BuildPipeline_ProviderShortName", "");
        appleIdUsername = EditorPrefs.GetString("BuildPipeline_AppleIdUsername", "");
        useCustomEntitlements = EditorPrefs.GetBool("BuildPipeline_UseCustomEntitlements", false);
        entitlementsFilePath = EditorPrefs.GetString("BuildPipeline_EntitlementsFilePath", "");
        
        // Windows Installer
        innoSetupPath = EditorPrefs.GetString("BuildPipeline_InnoSetupPath", @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe");
        publisherName = EditorPrefs.GetString("BuildPipeline_PublisherName", PlayerSettings.companyName);
        appCopyright = EditorPrefs.GetString("BuildPipeline_AppCopyright", $"Copyright © {DateTime.Now.Year} {PlayerSettings.companyName}");
        publisherURL = EditorPrefs.GetString("BuildPipeline_PublisherURL", "");
        supportURL = EditorPrefs.GetString("BuildPipeline_SupportURL", "");
        updatesURL = EditorPrefs.GetString("BuildPipeline_UpdatesURL", "");
        createDesktopIcon = EditorPrefs.GetBool("BuildPipeline_CreateDesktopIcon", true);
        createStartMenuIcon = EditorPrefs.GetBool("BuildPipeline_CreateStartMenuIcon", true);
        createUninstaller = EditorPrefs.GetBool("BuildPipeline_CreateUninstaller", true);
        allowDirChange = EditorPrefs.GetBool("BuildPipeline_AllowDirChange", true);
        
        // Build Profiles
        useBuildProfiles = EditorPrefs.GetBool("BuildPipeline_UseBuildProfiles", false);
        selectedWindowsProfileIndex = EditorPrefs.GetInt("BuildPipeline_SelectedWindowsProfile", 0);
        selectedMacOSProfileIndex = EditorPrefs.GetInt("BuildPipeline_SelectedMacOSProfile", 0);
        selectedLinuxProfileIndex = EditorPrefs.GetInt("BuildPipeline_SelectedLinuxProfile", 0);
        
        // GitHub Settings
        repositoryUrl = EditorPrefs.GetString("BuildPipeline_RepositoryUrl", "");
        githubToken = EditorPrefs.GetString("BuildPipeline_GithubToken", "");
        includePrerelease = EditorPrefs.GetBool("BuildPipeline_IncludePrerelease", false);
        
        // DMG Settings
        enableDMGCreation = EditorPrefs.GetBool("BuildPipeline_EnableDMGCreation", false);
        
        // UI State
        macSigningFoldout = EditorPrefs.GetBool("BuildPipeline_MacSigningFoldout", false);
        windowsInstallerFoldout = EditorPrefs.GetBool("BuildPipeline_WindowsInstallerFoldout", false);
        githubReleaseFoldout = EditorPrefs.GetBool("BuildPipeline_GitHubReleaseFoldout", false);
        
        // Load build profiles
        LoadBuildProfiles();
    }

    private void LoadBuildProfiles()
    {
        string profilesPath = "Assets/Settings/Build Profiles";
        if (!Directory.Exists(profilesPath))
        {
            UnityEngine.Debug.LogWarning($"Build profiles directory not found: {profilesPath}");
            return;
        }

        // Load Windows profiles
        var windowsProfiles = new List<BuildProfile>();
        var macOSProfiles = new List<BuildProfile>();
        var linuxProfiles = new List<BuildProfile>();

        string[] profileFiles = Directory.GetFiles(profilesPath, "*.asset");
        foreach (string profileFile in profileFiles)
        {
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profileFile);
            if (profile != null)
            {
                if (profile.name.ToLower().Contains("windows"))
                {
                    windowsProfiles.Add(profile);
                }
                else if (profile.name.ToLower().Contains("macos") || profile.name.ToLower().Contains("mac"))
                {
                    macOSProfiles.Add(profile);
                }
                else if (profile.name.ToLower().Contains("linux"))
                {
                    linuxProfiles.Add(profile);
                }
            }
        }

        availableWindowsProfiles = windowsProfiles.ToArray();
        availableMacOSProfiles = macOSProfiles.ToArray();
        availableLinuxProfiles = linuxProfiles.ToArray();

        // Set selected profiles
        if (availableWindowsProfiles.Length > 0 && selectedWindowsProfileIndex < availableWindowsProfiles.Length)
            windowsBuildProfile = availableWindowsProfiles[selectedWindowsProfileIndex];
        if (availableMacOSProfiles.Length > 0 && selectedMacOSProfileIndex < availableMacOSProfiles.Length)
            macOSBuildProfile = availableMacOSProfiles[selectedMacOSProfileIndex];
        if (availableLinuxProfiles.Length > 0 && selectedLinuxProfileIndex < availableLinuxProfiles.Length)
            linuxBuildProfile = availableLinuxProfiles[selectedLinuxProfileIndex];
    }

    private void SaveSettings()
    {
        // Version settings
        EditorPrefs.SetBool("BuildPipeline_UseAutoVersion", useAutoVersion);
        EditorPrefs.SetString("BuildPipeline_ManualVersion", manualVersion);
        
        // Platform settings
        EditorPrefs.SetBool("BuildPipeline_BuildWindows", buildWindows);
        EditorPrefs.SetBool("BuildPipeline_BuildMacOS", buildMacOS);
        EditorPrefs.SetBool("BuildPipeline_BuildLinux", buildLinux);
        
        // Pipeline steps
        EditorPrefs.SetBool("BuildPipeline_EnableMacSigning", enableMacSigning);
        EditorPrefs.SetBool("BuildPipeline_EnableWindowsInstaller", enableWindowsInstaller);
        EditorPrefs.SetBool("BuildPipeline_EnableGitHubRelease", enableGitHubRelease);
        
        // macOS Signing
        EditorPrefs.SetString("BuildPipeline_P12FilePath", p12FilePath);
        EditorPrefs.SetString("BuildPipeline_MacBundleIdentifier", macBundleIdentifier);
        EditorPrefs.SetBool("BuildPipeline_EnableNotarization", enableNotarization);
        EditorPrefs.SetString("BuildPipeline_ProviderShortName", providerShortName);
        EditorPrefs.SetString("BuildPipeline_AppleIdUsername", appleIdUsername);
        EditorPrefs.SetBool("BuildPipeline_UseCustomEntitlements", useCustomEntitlements);
        EditorPrefs.SetString("BuildPipeline_EntitlementsFilePath", entitlementsFilePath);
        
        // Windows Installer
        EditorPrefs.SetString("BuildPipeline_InnoSetupPath", innoSetupPath);
        EditorPrefs.SetString("BuildPipeline_PublisherName", publisherName);
        EditorPrefs.SetString("BuildPipeline_AppCopyright", appCopyright);
        EditorPrefs.SetString("BuildPipeline_PublisherURL", publisherURL);
        EditorPrefs.SetString("BuildPipeline_SupportURL", supportURL);
        EditorPrefs.SetString("BuildPipeline_UpdatesURL", updatesURL);
        EditorPrefs.SetBool("BuildPipeline_CreateDesktopIcon", createDesktopIcon);
        EditorPrefs.SetBool("BuildPipeline_CreateStartMenuIcon", createStartMenuIcon);
        EditorPrefs.SetBool("BuildPipeline_CreateUninstaller", createUninstaller);
        EditorPrefs.SetBool("BuildPipeline_AllowDirChange", allowDirChange);
        
        // Build Profiles
        EditorPrefs.SetBool("BuildPipeline_UseBuildProfiles", useBuildProfiles);
        EditorPrefs.SetInt("BuildPipeline_SelectedWindowsProfile", selectedWindowsProfileIndex);
        EditorPrefs.SetInt("BuildPipeline_SelectedMacOSProfile", selectedMacOSProfileIndex);
        EditorPrefs.SetInt("BuildPipeline_SelectedLinuxProfile", selectedLinuxProfileIndex);
        
        // GitHub Settings
        EditorPrefs.SetString("BuildPipeline_RepositoryUrl", repositoryUrl);
        EditorPrefs.SetString("BuildPipeline_GithubToken", githubToken);
        EditorPrefs.SetBool("BuildPipeline_IncludePrerelease", includePrerelease);
        
        // DMG Settings
        EditorPrefs.SetBool("BuildPipeline_EnableDMGCreation", enableDMGCreation);
        
        // UI State
        EditorPrefs.SetBool("BuildPipeline_MacSigningFoldout", macSigningFoldout);
        EditorPrefs.SetBool("BuildPipeline_WindowsInstallerFoldout", windowsInstallerFoldout);
        EditorPrefs.SetBool("BuildPipeline_GitHubReleaseFoldout", githubReleaseFoldout);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        DrawVersionSettings();
        DrawPlatformSelection();
        DrawPipelineSteps();
        
        if (showAdvancedSettings)
        {
            DrawAdvancedSettings();
        }
        
        DrawStatusAndControls();

        EditorGUILayout.EndScrollView();
        
        if (GUI.changed)
        {
            // Clear validation error messages when settings change
            if (statusMessage.Contains("Validation failed") || statusMessage.Contains("is enabled but"))
            {
                statusMessage = "";
            }
            SaveSettings();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Build Pipeline", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Automated build pipeline: Build → Sign → Package → Release", MessageType.Info);
        EditorGUILayout.Space(5);
    }

    private void DrawVersionSettings()
    {
        EditorGUILayout.LabelField("Version Settings", EditorStyles.boldLabel);
        
        useAutoVersion = EditorGUILayout.Toggle(new GUIContent("Auto-increment Version", "Automatically increment the patch version number (e.g., 1.0.0 → 1.0.1)"), useAutoVersion);
        
        if (useAutoVersion)
        {
            string currentVersion = PlayerSettings.bundleVersion;
            string[] parts = currentVersion.Split('.');
            if (parts.Length == 3 && int.TryParse(parts[2], out int patch))
            {
                string nextVersion = $"{parts[0]}.{parts[1]}.{patch + 1}";
                EditorGUILayout.LabelField(new GUIContent($"Next Version: {nextVersion}", "The version number that will be used for this build"));
            }
            else
            {
                EditorGUILayout.LabelField(new GUIContent("Current Version", "The current version from PlayerSettings"), currentVersion);
            }
        }
        else
        {
            manualVersion = EditorGUILayout.TextField(new GUIContent("Manual Version", "Specify a custom version number (e.g., 1.0.0)"), manualVersion);
        }
        
        EditorGUILayout.Space(5);
    }

    private void DrawPlatformSelection()
    {
        EditorGUILayout.LabelField("Platform Selection", EditorStyles.boldLabel);
        
        buildWindows = EditorGUILayout.Toggle(new GUIContent("Build Windows", "Create a Windows executable (.exe) build"), buildWindows);
        buildMacOS = EditorGUILayout.Toggle(new GUIContent("Build macOS", "Create a macOS application bundle (.app) build"), buildMacOS);
        buildLinux = EditorGUILayout.Toggle(new GUIContent("Build Linux", "Create a Linux executable build"), buildLinux);
        
        if (!buildWindows && !buildMacOS && !buildLinux)
        {
            EditorGUILayout.HelpBox("Please select at least one platform to build.", MessageType.Warning);
        }
        
        EditorGUILayout.Space(5);
        
        // Build Profile Settings
        useBuildProfiles = EditorGUILayout.Toggle(new GUIContent("Use Build Profiles", "Use custom build profiles with platform-specific settings instead of global project settings"), useBuildProfiles);
        
        if (useBuildProfiles)
        {
            EditorGUI.indentLevel++;
            
            // Windows Build Profile
            if (buildWindows)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Windows Profile", "Select a build profile with Windows-specific settings"), GUILayout.Width(120));
                if (availableWindowsProfiles.Length > 0)
                {
                    string[] windowsProfileNames = availableWindowsProfiles.Select(p => p.name).ToArray();
                    int newIndex = EditorGUILayout.Popup(selectedWindowsProfileIndex, windowsProfileNames);
                    if (newIndex != selectedWindowsProfileIndex)
                    {
                        selectedWindowsProfileIndex = newIndex;
                        windowsBuildProfile = availableWindowsProfiles[selectedWindowsProfileIndex];
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No Windows profiles found");
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // macOS Build Profile
            if (buildMacOS)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("macOS Profile", "Select a build profile with macOS-specific settings"), GUILayout.Width(120));
                if (availableMacOSProfiles.Length > 0)
                {
                    string[] macOSProfileNames = availableMacOSProfiles.Select(p => p.name).ToArray();
                    int newIndex = EditorGUILayout.Popup(selectedMacOSProfileIndex, macOSProfileNames);
                    if (newIndex != selectedMacOSProfileIndex)
                    {
                        selectedMacOSProfileIndex = newIndex;
                        macOSBuildProfile = availableMacOSProfiles[selectedMacOSProfileIndex];
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No macOS profiles found");
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // Linux Build Profile
            if (buildLinux)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Linux Profile", "Select a build profile with Linux-specific settings"), GUILayout.Width(120));
                if (availableLinuxProfiles.Length > 0)
                {
                    string[] linuxProfileNames = availableLinuxProfiles.Select(p => p.name).ToArray();
                    int newIndex = EditorGUILayout.Popup(selectedLinuxProfileIndex, linuxProfileNames);
                    if (newIndex != selectedLinuxProfileIndex)
                    {
                        selectedLinuxProfileIndex = newIndex;
                        linuxBuildProfile = availableLinuxProfiles[selectedLinuxProfileIndex];
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No Linux profiles found");
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
            
            if (GUILayout.Button(new GUIContent("Refresh Build Profiles", "Reload build profiles from the Assets/Settings/Build Profiles folder")))
            {
                LoadBuildProfiles();
            }
        }
        
        EditorGUILayout.Space(5);
    }

    private void DrawPipelineSteps()
    {
        EditorGUILayout.LabelField("Pipeline Steps", EditorStyles.boldLabel);
        
        // macOS Signing
        enableMacSigning = EditorGUILayout.Toggle(new GUIContent("Sign macOS Build", "Code sign the macOS build with a Developer ID certificate for distribution outside the Mac App Store"), enableMacSigning);
        if (enableMacSigning && !buildMacOS)
        {
            EditorGUILayout.HelpBox("macOS signing enabled but macOS build is disabled.", MessageType.Warning);
        }
        
        if (enableMacSigning)
        {
            macSigningFoldout = EditorGUILayout.Foldout(macSigningFoldout, "macOS Signing Settings", true);
            if (macSigningFoldout)
            {
                EditorGUI.indentLevel++;
                DrawMacSigningSettings();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
        }
        
        // Windows Installer
        enableWindowsInstaller = EditorGUILayout.Toggle(new GUIContent("Create Windows Installer", "Generate a Windows installer package (.exe) using Inno Setup"), enableWindowsInstaller);
        if (enableWindowsInstaller && !buildWindows)
        {
            EditorGUILayout.HelpBox("Windows installer enabled but Windows build is disabled.", MessageType.Warning);
        }
        
        if (enableWindowsInstaller)
        {
            windowsInstallerFoldout = EditorGUILayout.Foldout(windowsInstallerFoldout, "Windows Installer Settings", true);
            if (windowsInstallerFoldout)
            {
                EditorGUI.indentLevel++;
                DrawWindowsInstallerSettings();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
        }
        
        // GitHub Release
        enableGitHubRelease = EditorGUILayout.Toggle(new GUIContent("Upload to GitHub", "Create a GitHub release and upload build files as assets"), enableGitHubRelease);
        
        if (enableGitHubRelease)
        {
            githubReleaseFoldout = EditorGUILayout.Foldout(githubReleaseFoldout, "GitHub Release Settings", true);
            if (githubReleaseFoldout)
            {
                EditorGUI.indentLevel++;
                DrawGitHubReleaseSettings();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
        }
        
        EditorGUILayout.Space(5);
        showAdvancedSettings = EditorGUILayout.Toggle(new GUIContent("Show General Settings", "Display additional build information and password visibility options"), showAdvancedSettings);
        EditorGUILayout.Space(5);
    }

    private void DrawMacSigningSettings()
    {
        EditorGUILayout.BeginHorizontal();
        p12FilePath = EditorGUILayout.TextField(new GUIContent("P12 Certificate", "Path to your Developer ID Application certificate file (.p12) exported from Keychain Access"), p12FilePath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select P12 Certificate", "", "p12");
            if (!string.IsNullOrEmpty(path)) p12FilePath = path;
        }
        EditorGUILayout.EndHorizontal();
        
        if (showPasswords)
            p12Password = EditorGUILayout.TextField(new GUIContent("P12 Password", "Password used when exporting the certificate from Keychain Access"), p12Password);
        else
            p12Password = EditorGUILayout.PasswordField(new GUIContent("P12 Password", "Password used when exporting the certificate from Keychain Access"), p12Password);
            
        macBundleIdentifier = EditorGUILayout.TextField(new GUIContent("Bundle Identifier", "Unique identifier for your macOS application (e.g., com.company.appname)"), macBundleIdentifier);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Entitlements", EditorStyles.boldLabel);
        
        useCustomEntitlements = EditorGUILayout.Toggle(new GUIContent("Use Custom Entitlements", "Use a custom entitlements file instead of the default Hardened Runtime requirements"), useCustomEntitlements);
        if (useCustomEntitlements)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            entitlementsFilePath = EditorGUILayout.TextField(new GUIContent("Entitlements File", "Path to your custom .entitlements file defining app permissions and capabilities"), entitlementsFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("Select Entitlements File", "", "entitlements");
                if (!string.IsNullOrEmpty(path)) entitlementsFilePath = path;
            }
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button(new GUIContent("Create Default Entitlements File", "Generate a basic entitlements file with common Unity requirements")))
            {
                CreateDefaultEntitlementsFile();
            }
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox("Default entitlements will be used (Hardened Runtime minimum requirements).", MessageType.Info);
        }
        
        EditorGUILayout.Space(5);
        enableNotarization = EditorGUILayout.Toggle(new GUIContent("Enable Notarization", "Submit the signed app to Apple for notarization to remove security warnings"), enableNotarization);
        if (enableNotarization)
        {
            EditorGUI.indentLevel++;
            providerShortName = EditorGUILayout.TextField(new GUIContent("Team ID", "Your Apple Developer Team ID (found in your Apple Developer account)"), providerShortName);
            appleIdUsername = EditorGUILayout.TextField(new GUIContent("Apple ID", "Your Apple ID email address used for the Developer account"), appleIdUsername);
            if (showPasswords)
                appleIdPassword = EditorGUILayout.TextField(new GUIContent("App Password", "App-specific password generated for command-line tools"), appleIdPassword);
            else
                appleIdPassword = EditorGUILayout.PasswordField(new GUIContent("App Password", "App-specific password generated for command-line tools"), appleIdPassword);
            EditorGUI.indentLevel--;
            
            EditorGUILayout.HelpBox("For notarization, you need an app-specific password. Generate one at appleid.apple.com.", MessageType.Info);
        }
    }

    private void CreateDefaultEntitlementsFile()
    {
        string entitlementsContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
    <dict>
        <key>com.apple.security.cs.disable-library-validation</key>
        <true/>
        <key>com.apple.security.cs.disable-executable-page-protection</key>
        <true/>
    </dict>
</plist>";

        string path = EditorUtility.SaveFilePanel("Save Entitlements File", "", "entitlements", "entitlements");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, entitlementsContent);
            entitlementsFilePath = path;
            statusMessage = "Default entitlements file created successfully.";
        }
    }

    private void DrawWindowsInstallerSettings()
    {
        EditorGUILayout.BeginHorizontal();
        innoSetupPath = EditorGUILayout.TextField(new GUIContent("Inno Setup Path", "Path to the Inno Setup compiler (ISCC.exe) used to generate Windows installers"), innoSetupPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select ISCC.exe", "", "exe");
            if (!string.IsNullOrEmpty(path)) innoSetupPath = path;
        }
        EditorGUILayout.EndHorizontal();
        
        if (!File.Exists(innoSetupPath))
        {
            EditorGUILayout.HelpBox("Inno Setup compiler not found. Download from https://jrsoftware.org/isinfo.php", MessageType.Warning);
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Publisher Information", EditorStyles.boldLabel);
        
        publisherName = EditorGUILayout.TextField(new GUIContent("Publisher Name", "Name of the company or individual publishing the application"), publisherName);
        publisherURL = EditorGUILayout.TextField(new GUIContent("Publisher URL", "Website URL for the publisher (optional)"), publisherURL);
        supportURL = EditorGUILayout.TextField(new GUIContent("Support URL", "URL where users can get support for the application (optional)"), supportURL);
        updatesURL = EditorGUILayout.TextField(new GUIContent("Updates URL", "URL where users can check for application updates (optional)"), updatesURL);
        appCopyright = EditorGUILayout.TextField(new GUIContent("Copyright", "Copyright notice for the application (e.g., © 2024 Company Name)"), appCopyright);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Installation Options", EditorStyles.boldLabel);
        
        createDesktopIcon = EditorGUILayout.Toggle(new GUIContent("Create Desktop Icon", "Add a shortcut to the desktop during installation"), createDesktopIcon);
        createStartMenuIcon = EditorGUILayout.Toggle(new GUIContent("Create Start Menu Icon", "Add a shortcut to the Start Menu during installation"), createStartMenuIcon);
        createUninstaller = EditorGUILayout.Toggle(new GUIContent("Create Uninstaller", "Include an uninstaller to allow users to remove the application"), createUninstaller);
        allowDirChange = EditorGUILayout.Toggle(new GUIContent("Allow Directory Change", "Allow users to choose a custom installation directory"), allowDirChange);
    }

    private void DrawGitHubReleaseSettings()
    {
        repositoryUrl = EditorGUILayout.TextField(new GUIContent("Repository URL", "GitHub repository URL (e.g., https://github.com/username/repository)"), repositoryUrl);
        if (showPasswords)
            githubToken = EditorGUILayout.TextField(new GUIContent("GitHub Token", "Personal access token with repo permissions for uploading releases"), githubToken);
        else
            githubToken = EditorGUILayout.PasswordField(new GUIContent("GitHub Token", "Personal access token with repo permissions for uploading releases"), githubToken);
            
        releaseTitle = EditorGUILayout.TextField(new GUIContent("Release Title (optional)", "Custom title for the GitHub release. Leave empty to auto-generate"), releaseTitle);
        EditorGUILayout.LabelField(new GUIContent("Release Description", "Markdown description for the GitHub release (changelog, features, etc.)"));
        releaseDescription = EditorGUILayout.TextArea(releaseDescription, GUILayout.Height(60));
        includePrerelease = EditorGUILayout.Toggle(new GUIContent("Mark as Prerelease", "Mark this release as a prerelease/beta version on GitHub"), includePrerelease);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("DMG Creation", EditorStyles.boldLabel);
        enableDMGCreation = EditorGUILayout.Toggle(new GUIContent("Create DMG via GitHub Actions", "Trigger GitHub Actions workflow to create macOS DMG files from ZIP builds and attach them to the release"), enableDMGCreation);
        
        if (enableDMGCreation)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("DMG creation uses GitHub Actions. The DMG will be automatically attached to the GitHub release.", MessageType.Info);
            EditorGUI.indentLevel--;
        }
        
        if (string.IsNullOrEmpty(releaseTitle))
        {
            string nextVersion = GetNextVersion();
            string autoTitle = $"{Application.productName}-{nextVersion}";
            EditorGUILayout.HelpBox($"Auto-generated title: {autoTitle}", MessageType.Info);
        }
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
        
        showPasswords = EditorGUILayout.Toggle(new GUIContent("Show Passwords", "Display password fields as plain text instead of masked"), showPasswords);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Build Information", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(new GUIContent($"Current Version: {GetNextVersion()}", "Current version number from PlayerSettings"));
        EditorGUILayout.LabelField(new GUIContent($"Product Name: {Application.productName}", "Application name from PlayerSettings"));
        EditorGUILayout.LabelField(new GUIContent($"Company Name: {PlayerSettings.companyName}", "Company name from PlayerSettings"));


        EditorGUILayout.Space(10);
    }

    private void DrawStatusAndControls()
    {
        // Status message
        if (!string.IsNullOrEmpty(statusMessage))
        {
            MessageType messageType = statusMessage.Contains("Error") || statusMessage.Contains("Failed") ? 
                MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(statusMessage, messageType);
        }
        
        EditorGUILayout.Space(10);
        
        // Main build button or cancel button
        if (isProcessing)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            GUILayout.Button("Build Pipeline Running...", GUILayout.Height(40));
            GUI.enabled = true;
            
            if (GUILayout.Button(new GUIContent("Cancel", "Cancel the current build process"), GUILayout.Height(40), GUILayout.Width(80)))
            {
                RequestCancellation();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            GUI.enabled = !isProcessing && (buildWindows || buildMacOS || buildLinux);
            if (GUILayout.Button("Start Build Pipeline", GUILayout.Height(40)))
            {
                StartBuildPipeline();
            }
            GUI.enabled = true;
        }
        
        // Individual step buttons
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !isProcessing;
        if (GUILayout.Button(new GUIContent("Build Only", "Build selected platforms without additional processing")))
        {
            StartIndividualBuildStep();
        }
        if (GUILayout.Button(new GUIContent("Create Release", "Package builds into ZIP files for distribution")))
        {
            CreateReleaseFiles();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(repositoryUrl) && !string.IsNullOrEmpty(githubToken);
        if (GUILayout.Button(new GUIContent("Create DMG", "Trigger GitHub Actions to create DMG from existing macOS ZIP")))
        {
            TriggerManualDMGCreation();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        
        // Folder access buttons
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Quick Access", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Open Builds Folder", "Open the folder containing built executables")))
        {
            OpenBuildsFolder();
        }
        if (GUILayout.Button(new GUIContent("Open Releases Folder", "Open the folder containing packaged releases")))
        {
            OpenReleasesFolder();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void RequestCancellation()
    {
        isCancellationRequested = true;
        statusMessage = "Cancellation requested. Stopping after current step...";
        
        // Re-enable compilation if it was locked
        EditorApplication.UnlockReloadAssemblies();
    }
    
    private async void StartIndividualBuildStep()
    {
        isCancellationRequested = false;
        isProcessing = true;
        
        // Prevent compilation during build process
        EditorApplication.LockReloadAssemblies();
        
        try
        {
            if (isCancellationRequested)
            {
                statusMessage = "Build cancelled.";
                return;
            }
            
            UpdateVersion();
            
            if (isCancellationRequested)
            {
                statusMessage = "Build cancelled.";
                return;
            }
            
            await BuildSelectedPlatforms();
            
            if (isCancellationRequested)
            {
                statusMessage = "Build cancelled.";
                return;
            }
            
            statusMessage = "Build completed successfully!";
        }
        catch (Exception ex)
        {
            if (isCancellationRequested)
            {
                statusMessage = "Build cancelled.";
            }
            else
            {
                statusMessage = $"Build failed: {ex.Message}";
                UnityEngine.Debug.LogError($"Build error: {ex}");
            }
        }
        finally
        {
            isProcessing = false;
            isCancellationRequested = false;
            // Re-enable compilation
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    private async void StartBuildPipeline()
    {
        // Validate settings before starting
        string validationError = ValidateSettings();
        if (!string.IsNullOrEmpty(validationError))
        {
            statusMessage = $"Validation failed: {validationError}";
            EditorUtility.DisplayDialog("Validation Error", validationError, "OK");
            return;
        }

        isCancellationRequested = false;
        isProcessing = true;
        
        // Prevent compilation during build process
        EditorApplication.LockReloadAssemblies();
        
        try
        {
            // Step 1: Update version
            if (isCancellationRequested)
            {
                statusMessage = "Build pipeline cancelled.";
                return;
            }
            UpdateVersion();
            
            // Step 2: Build selected platforms
            if (isCancellationRequested)
            {
                statusMessage = "Build pipeline cancelled.";
                return;
            }
            await BuildSelectedPlatforms();
            
            // Step 3: Sign macOS if enabled
            if (enableMacSigning && buildMacOS)
            {
                if (isCancellationRequested)
                {
                    statusMessage = "Build pipeline cancelled.";
                    return;
                }
                await SignMacOSBuild();
            }
            
            // Step 4: Create release files
            if (isCancellationRequested)
            {
                statusMessage = "Build pipeline cancelled.";
                return;
            }
            CreateReleaseFiles();
            
            // Step 5: Create Windows installer if enabled
            if (enableWindowsInstaller && buildWindows)
            {
                if (isCancellationRequested)
                {
                    statusMessage = "Build pipeline cancelled.";
                    return;
                }
                await CreateWindowsInstaller();
            }
            
            // Step 6: Upload to GitHub if enabled
            if (enableGitHubRelease)
            {
                if (isCancellationRequested)
                {
                    statusMessage = "Build pipeline cancelled.";
                    return;
                }
                await UploadToGitHub();
            }
            
            if (isCancellationRequested)
            {
                statusMessage = "Build pipeline cancelled.";
                return;
            }
            
            statusMessage = "Build pipeline completed successfully!";
            
            // Open the releases folder
            string version = PlayerSettings.bundleVersion;
            string releasePath = Path.Combine(RELEASE_PATH, $"v{version}");
            if (Directory.Exists(releasePath))
            {
                EditorUtility.RevealInFinder(releasePath);
            }
            
            EditorUtility.DisplayDialog("Build Complete", "The build pipeline has completed successfully!", "OK");
        }
        catch (Exception ex)
        {
            if (isCancellationRequested)
            {
                statusMessage = "Build pipeline cancelled.";
            }
            else
            {
                statusMessage = $"Build pipeline failed: {ex.Message}";
                UnityEngine.Debug.LogError($"Build pipeline error: {ex}");
                EditorUtility.DisplayDialog("Build Failed", $"The build pipeline failed:\n{ex.Message}", "OK");
            }
        }
        finally
        {
            isProcessing = false;
            isCancellationRequested = false;
            // Re-enable compilation
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    private string ValidateSettings()
    {
        // Check if at least one platform is selected
        if (!buildWindows && !buildMacOS && !buildLinux)
        {
            return "Please select at least one platform to build.";
        }

        // Validate macOS signing settings
        if (enableMacSigning && buildMacOS)
        {
            if (string.IsNullOrEmpty(p12FilePath) || !File.Exists(p12FilePath))
            {
                return "macOS signing is enabled but P12 certificate file is not found.";
            }
            if (string.IsNullOrEmpty(p12Password))
            {
                return "macOS signing is enabled but P12 password is not provided.";
            }
            if (enableNotarization)
            {
                if (string.IsNullOrEmpty(providerShortName))
                {
                    return "Notarization is enabled but Team ID is not provided.";
                }
                if (string.IsNullOrEmpty(appleIdUsername))
                {
                    return "Notarization is enabled but Apple ID is not provided.";
                }
                if (string.IsNullOrEmpty(appleIdPassword))
                {
                    return "Notarization is enabled but App-Specific Password is not provided.";
                }
            }
        }

        // Validate Windows installer settings
        if (enableWindowsInstaller && buildWindows)
        {
            if (string.IsNullOrEmpty(innoSetupPath) || !File.Exists(innoSetupPath))
            {
                return "Windows installer is enabled but Inno Setup compiler path is not found.";
            }
        }

        // Validate GitHub settings (only if GitHub release is enabled)
        if (enableGitHubRelease)
        {
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                return "GitHub release is enabled but repository URL is not provided.";
            }
            if (string.IsNullOrEmpty(githubToken))
            {
                return "GitHub release is enabled but GitHub token is not provided.";
            }
        }

        // Validate version settings
        if (!useAutoVersion && string.IsNullOrEmpty(manualVersion))
        {
            return "Manual version is selected but no version is provided.";
        }

        return null; // All validations passed
    }

    private string GetNextVersion()
    {
        if (useAutoVersion)
        {
            string[] versionParts = PlayerSettings.bundleVersion.Split('.');
            if (versionParts.Length == 3 && int.TryParse(versionParts[2], out int patch))
            {
                return $"{versionParts[0]}.{versionParts[1]}.{patch + 1}";
            }
            return PlayerSettings.bundleVersion;
        }
        else if (!string.IsNullOrEmpty(manualVersion))
        {
            return manualVersion;
        }
        return PlayerSettings.bundleVersion;
    }

    private void UpdateVersion()
    {
        if (useAutoVersion)
        {
            string[] versionParts = PlayerSettings.bundleVersion.Split('.');
            if (versionParts.Length == 3 && int.TryParse(versionParts[2], out int patch))
            {
                string newVersion = $"{versionParts[0]}.{versionParts[1]}.{patch + 1}";
                PlayerSettings.bundleVersion = newVersion;
                statusMessage = $"Version updated to {newVersion}";
            }
        }
        else if (!string.IsNullOrEmpty(manualVersion))
        {
            PlayerSettings.bundleVersion = manualVersion;
            statusMessage = $"Version set to {manualVersion}";
        }
        
        AssetDatabase.SaveAssets();
    }

    private async Task BuildSelectedPlatforms()
    {
        statusMessage = "Building selected platforms...";
        
        // Create build directory
        if (Directory.Exists(BUILD_PATH))
        {
            foreach (string file in Directory.GetFiles(BUILD_PATH))
            {
                File.Delete(file);
            }
        }
        else
        {
            Directory.CreateDirectory(BUILD_PATH);
        }

        var platformsToBuild = new List<string>();
        if (buildWindows) platformsToBuild.Add("Windows");
        if (buildMacOS) platformsToBuild.Add("MacOS");
        if (buildLinux) platformsToBuild.Add("Linux");

        foreach (string platform in platformsToBuild)
        {
            if (isCancellationRequested)
            {
                statusMessage = "Build cancelled.";
                return;
            }
            
            statusMessage = $"Building {platform}...";
            await BuildForPlatformAsync(platform);
        }
        
        if (isCancellationRequested)
        {
            statusMessage = "Build cancelled.";
            return;
        }
        
        statusMessage = "Platform builds completed.";
    }

    private Task BuildForPlatformAsync(string platform)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        currentBuildPlatform = platform;
        
        // Schedule build execution for the next editor update
        EditorApplication.CallbackFunction buildAction = null;
        buildAction = () =>
        {
            EditorApplication.update -= buildAction;
            
            try
            {
                if (isCancellationRequested)
                {
                    tcs.SetResult(false);
                    return;
                }
                
                BuildForPlatform(platform);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        };
        
        EditorApplication.update += buildAction;
        
        return tcs.Task;
    }

    private void BuildForPlatform(string platform)
    {
        BuildProfile profile = null;
        if (useBuildProfiles)
        {
            switch (platform)
            {
                case "Windows":
                    profile = windowsBuildProfile;
                    break;
                case "MacOS":
                    profile = macOSBuildProfile;
                    break;
                case "Linux":
                    profile = linuxBuildProfile;
                    break;
            }
        }

        // Update build profile version if using profiles
        if (profile != null)
        {
            // Update the version in the build profile
            UpdateBuildProfileVersion(profile);
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        string buildFolderName = $"{Application.productName}-{platform}";
        string buildPath = Path.Combine(BUILD_PATH, buildFolderName);
        
        if (Directory.Exists(buildPath))
        {
            Directory.Delete(buildPath, true);
        }
        Directory.CreateDirectory(buildPath);

        BuildTarget target;
        BuildOptions options = BuildOptions.CompressWithLz4HC;
        string executableName;

        switch (platform)
        {
            case "Windows":
                executableName = $"{Application.productName}.exe";
                PlayerSettings.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                target = BuildTarget.StandaloneWindows64;
                break;
            case "MacOS":
                executableName = $"{Application.productName}.app";
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                target = BuildTarget.StandaloneOSX;
                break;
            case "Linux":
                executableName = Application.productName;
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                target = BuildTarget.StandaloneLinux64;
                break;
            default:
                UnityEngine.Debug.LogError($"Unsupported platform: {platform}");
                return;
        }

        string executablePath = Path.Combine(buildPath, executableName);

                // Build using profile if available
        if (profile != null)
        {
            UnityEngine.Debug.Log($"Using build profile for {platform}: {profile.name}");
            
            // Get scenes from profile if it overrides global scenes
            string[] profileScenes = profile.overrideGlobalScenes ? 
                profile.GetScenesForBuild().Select(scene => scene.path).ToArray() : 
                scenes;

            BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = profileScenes,
                locationPathName = executablePath,
                target = target,
                options = options
            });
        }
        else
        {
            BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executablePath,
                target = target,
                options = options
            });
        }

        // Set executable permissions for macOS builds
        if (platform == "MacOS" && Directory.Exists(executablePath))
        {
            SetMacOSExecutablePermissions(executablePath);
        }

    UnityEngine.Debug.Log($"Build completed for {platform} in {buildPath}");
    }

    private void UpdateBuildProfileVersion(BuildProfile profile)
    {
        if (profile == null) return;

        try
        {
            // This is a simplified approach - in practice, you might need to 
            // modify the profile's PlayerSettings YAML to update the version
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Updated build profile version for: {profile.name}");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"Failed to update build profile version: {ex.Message}");
        }
    }

    private async Task SignMacOSBuild()
    {
        statusMessage = "Signing macOS build...";
        
        // Find macOS build
        string macBuildPath = Path.Combine(BUILD_PATH, $"{Application.productName}-MacOS", $"{Application.productName}.app");
        
        if (!Directory.Exists(macBuildPath))
        {
            throw new Exception("macOS build not found for signing");
        }
        
        if (string.IsNullOrEmpty(p12FilePath) || !File.Exists(p12FilePath))
        {
            throw new Exception("P12 certificate file not found");
        }
        
        if (string.IsNullOrEmpty(p12Password))
        {
            throw new Exception("P12 password is required");
        }

        try
        {
            // Extract signing identity from P12 file
            string signingIdentity = await ExtractSigningIdentity(p12FilePath, p12Password);
            
            if (string.IsNullOrEmpty(signingIdentity))
            {
                throw new Exception("Could not extract signing identity from P12 file");
            }

            // Create or use entitlements file
            string entitlementsFile = "";
            if (useCustomEntitlements && !string.IsNullOrEmpty(entitlementsFilePath) && File.Exists(entitlementsFilePath))
            {
                entitlementsFile = entitlementsFilePath;
            }
            else
            {
                // Create temporary default entitlements file
                entitlementsFile = Path.Combine(Path.GetDirectoryName(macBuildPath), "temp_entitlements.entitlements");
                string defaultEntitlements = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
    <dict>
        <key>com.apple.security.cs.disable-library-validation</key>
        <true/>
        <key>com.apple.security.cs.disable-executable-page-protection</key>
        <true/>
    </dict>
</plist>";
                File.WriteAllText(entitlementsFile, defaultEntitlements);
            }

            // Set permissions
            statusMessage = "Setting file permissions...";
            await RunCommand("chmod", $"-R a+xr \"{macBuildPath}\"");

            // Code sign the application
            statusMessage = "Code signing application...";
            string codesignArgs = $"--deep --force --verify --verbose --timestamp --options runtime --entitlements \"{entitlementsFile}\" --sign \"{signingIdentity}\" \"{macBuildPath}\"";
            await RunCommand("codesign", codesignArgs);

            // Verify signature
            statusMessage = "Verifying signature...";
            await RunCommand("codesign", $"--verify --deep --strict --verbose=2 \"{macBuildPath}\"");

            // Notarize if enabled
            if (enableNotarization && !string.IsNullOrEmpty(appleIdUsername) && !string.IsNullOrEmpty(appleIdPassword))
            {
                await NotarizeApplication(macBuildPath);
            }

            // Clean up temporary entitlements file (only if we created it)
            if (!useCustomEntitlements && File.Exists(entitlementsFile))
            {
                File.Delete(entitlementsFile);
            }

            statusMessage = "macOS signing completed successfully.";
        }
        catch (Exception ex)
        {
            throw new Exception($"macOS signing failed: {ex.Message}");
        }
    }

    private async Task<string> ExtractSigningIdentity(string p12Path, string password)
    {
        try
        {
            string tempKeychainName = $"temp_signing_{System.Guid.NewGuid().ToString("N")[..8]}";
            
            // Create temporary keychain
            await RunCommand("security", $"create-keychain -p \"temp_password\" \"{tempKeychainName}\"");
            
            // Import P12 to temporary keychain
            await RunCommand("security", $"import \"{p12Path}\" -k \"{tempKeychainName}\" -P \"{password}\" -T /usr/bin/codesign");
            
            // List identities in the keychain
            var result = await RunCommand("security", $"find-identity -v -p codesigning \"{tempKeychainName}\"");
            
            // Clean up temporary keychain
            await RunCommand("security", $"delete-keychain \"{tempKeychainName}\"");
            
            // Parse the identity from the output
            var lines = result.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("Developer ID Application"))
                {
                    int startIndex = line.IndexOf('"');
                    int endIndex = line.LastIndexOf('"');
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        return line.Substring(startIndex + 1, endIndex - startIndex - 1);
                    }
                }
            }
            
            throw new Exception("Could not find Developer ID Application identity in P12 file");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to extract signing identity: {ex.Message}");
        }
    }

    private async Task NotarizeApplication(string appPath)
    {
        try
        {
            // Create a zip file for notarization
            string zipPath = Path.ChangeExtension(appPath, ".zip");
            statusMessage = "Creating zip for notarization...";
            await RunCommand("ditto", $"-c -k --keepParent \"{appPath}\" \"{zipPath}\"");

            // Submit for notarization using notarytool
            statusMessage = "Submitting to Apple for notarization...";
            string notarizeArgs = $"notarytool submit \"{zipPath}\" --apple-id \"{appleIdUsername}\" --password \"{appleIdPassword}\" --team-id \"{providerShortName}\" --wait";
            await RunCommand("xcrun", notarizeArgs);

            // Staple the notarization
            statusMessage = "Stapling notarization...";
            await RunCommand("xcrun", $"stapler staple \"{appPath}\"");

            // Clean up zip file
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            statusMessage += " Notarization completed successfully!";
        }
        catch (Exception ex)
        {
            statusMessage += $" Notarization failed: {ex.Message}";
            UnityEngine.Debug.LogWarning($"Notarization failed: {ex.Message}");
        }
    }

    private void CreateReleaseFiles()
    {
        statusMessage = "Creating release files...";
        
        string version = PlayerSettings.bundleVersion;
        string releasePath = Path.Combine(RELEASE_PATH, $"v{version}");

        if (Directory.Exists(releasePath))
        {
            Directory.Delete(releasePath, true);
        }
        Directory.CreateDirectory(releasePath);

        var platformsToBuild = new List<string>();
        if (buildWindows) platformsToBuild.Add("Windows");
        if (buildMacOS) platformsToBuild.Add("MacOS");
        if (buildLinux) platformsToBuild.Add("Linux");

        foreach (string platform in platformsToBuild)
        {
            string buildFolderName = $"{Application.productName}-{platform}";
            string buildPath = Path.Combine(BUILD_PATH, buildFolderName);
            if (!Directory.Exists(buildPath)) continue;

            // Create ZIP files for all platforms (DMG creation now handled by GitHub Actions)
            string zipPath = Path.Combine(releasePath, $"{buildFolderName}.zip");
            if (FileUtility.CreateZipFile(buildPath, zipPath))
            {
                UnityEngine.Debug.Log($"Created release ZIP for {platform}");
            }
        }
        
        statusMessage = "Release files created.";
    }
    
    private void OpenBuildsFolder()
    {
        string buildsPath = Path.Combine(Application.dataPath, "..", BUILD_PATH);
        
        if (!Directory.Exists(buildsPath))
        {
            Directory.CreateDirectory(buildsPath);
        }
        
        // Convert to absolute path for opening
        string fullPath = Path.GetFullPath(buildsPath);
        
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            System.Diagnostics.Process.Start("explorer.exe", fullPath.Replace('/', '\\'));
        }
        else if (Application.platform == RuntimePlatform.OSXEditor)
        {
            System.Diagnostics.Process.Start("open", fullPath);
        }
        else
        {
            // Linux/other
            System.Diagnostics.Process.Start("xdg-open", fullPath);
        }
    }
    
    private void OpenReleasesFolder()
    {
        string releasesPath = Path.Combine(Application.dataPath, "..", RELEASE_PATH);
        
        if (!Directory.Exists(releasesPath))
        {
            Directory.CreateDirectory(releasesPath);
        }
        
        // Convert to absolute path for opening
        string fullPath = Path.GetFullPath(releasesPath);
        
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            System.Diagnostics.Process.Start("explorer.exe", fullPath.Replace('/', '\\'));
        }
        else if (Application.platform == RuntimePlatform.OSXEditor)
        {
            System.Diagnostics.Process.Start("open", fullPath);
        }
        else
        {
            // Linux/other
            System.Diagnostics.Process.Start("xdg-open", fullPath);
        }
    }

    private async Task CreateWindowsInstaller()
    {
        statusMessage = "Creating Windows installer...";
        
        if (!File.Exists(innoSetupPath))
        {
            throw new Exception("Inno Setup compiler not found. Please install Inno Setup from https://jrsoftware.org/isinfo.php");
        }

        try
        {
            // Find Windows build
            string windowsBuildPath = Path.Combine(BUILD_PATH, $"{Application.productName}-Windows");
            if (!Directory.Exists(windowsBuildPath))
            {
                throw new Exception("Windows build not found for installer creation");
            }

            // Create versioned output directory
            string appVersion = PlayerSettings.bundleVersion;
            string versionedOutputPath = Path.Combine(RELEASE_PATH, $"v{appVersion}");
            string fullOutputPath = Path.GetFullPath(versionedOutputPath);
            if (!Directory.Exists(fullOutputPath))
            {
                Directory.CreateDirectory(fullOutputPath);
            }

            // Generate Inno Setup script
            statusMessage = "Generating installer script...";
            string scriptContent = GenerateInnoSetupScript(windowsBuildPath, fullOutputPath);
            string scriptPath = Path.Combine(fullOutputPath, $"{Application.productName}_installer.iss");
            File.WriteAllText(scriptPath, scriptContent);

            // Run Inno Setup compiler
            statusMessage = "Compiling installer...";
            await RunInnoSetupCompiler(scriptPath);

            statusMessage = "Windows installer created successfully.";
        }
        catch (Exception ex)
        {
            throw new Exception($"Windows installer creation failed: {ex.Message}");
        }
    }

    private string GenerateInnoSetupScript(string buildDirectory, string outputDirectory)
    {
        string appVersion = PlayerSettings.bundleVersion;
        string appName = Application.productName;
        string exeName = $"{appName}.exe";
        
        // Convert paths and ensure proper escaping
        string absoluteBuildDir = Path.GetFullPath(buildDirectory).Replace("\\", "\\\\");
        string absoluteOutputDir = outputDirectory.Replace("\\", "\\\\");

        // Clean up app name for filename safety
        string safeAppName = appName.Replace(" ", "").Replace("-", "").Replace("_", "");
        
        string script = $@"[Setup]
AppName={appName}
AppVersion={appVersion}
AppPublisher={publisherName}
DefaultDirName={{autopf}}\\{appName}
DefaultGroupName={publisherName}
AllowNoIcons=yes
OutputDir={absoluteOutputDir}
OutputBaseFilename={safeAppName}-Installer
Compression=lzma
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={appVersion}
VersionInfoCompany={publisherName}
VersionInfoDescription={appName} Installer
VersionInfoCopyright={appCopyright}";

        // Add optional URLs
        if (!string.IsNullOrEmpty(publisherURL))
            script += $"\nAppPublisherURL={publisherURL}";
        if (!string.IsNullOrEmpty(supportURL))
            script += $"\nAppSupportURL={supportURL}";
        if (!string.IsNullOrEmpty(updatesURL))
            script += $"\nAppUpdatesURL={updatesURL}";

        // Add directory and uninstaller options
        if (!allowDirChange)
            script += "\nDisableDirPage=yes";
        if (!createUninstaller)
            script += "\nUninstallable=no";

        script += $@"

[Languages]
Name: ""english""; MessagesFile: ""compiler:Default.isl""

[Tasks]";

        if (createDesktopIcon)
            script += $@"
Name: ""desktopicon""; Description: ""{{cm:CreateDesktopIcon}}""; GroupDescription: ""{{cm:AdditionalIcons}}""; Flags: unchecked";

        script += $@"

[Files]
Source: ""{absoluteBuildDir}\\*""; DestDir: ""{{app}}""; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]";

        if (createStartMenuIcon)
            script += $@"
Name: ""{{group}}\\{appName}""; Filename: ""{{app}}\\{exeName}""";

        if (createDesktopIcon)
            script += $@"
Name: ""{{autodesktop}}\\{appName}""; Filename: ""{{app}}\\{exeName}""; Tasks: desktopicon";

        if (createUninstaller)
            script += $@"
Name: ""{{group}}\\{{cm:UninstallProgram,{appName}}}""; Filename: ""{{uninstallexe}}""";

        script += $@"

[Run]
Filename: ""{{app}}\\{exeName}""; Description: ""{{cm:LaunchProgram,{appName}}}""; Flags: nowait postinstall skipifsilent";

        return script;
    }

    private async Task RunInnoSetupCompiler(string scriptPath)
    {
        if (!File.Exists(innoSetupPath))
        {
            throw new Exception($"Inno Setup compiler not found at: {innoSetupPath}");
        }

        if (!File.Exists(scriptPath))
        {
            throw new Exception($"Script file not found at: {scriptPath}");
        }

        string workingDir = Path.GetDirectoryName(innoSetupPath);
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = innoSetupPath,
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        using (var process = new Process { StartInfo = processStartInfo })
        {
            process.Start();
            
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            
            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode != 0)
            {
                throw new Exception($"Inno Setup compilation failed with exit code {process.ExitCode}.\nOutput: {output}\nError: {error}");
            }

            UnityEngine.Debug.Log($"Inno Setup completed successfully. Output: {output}");
        }
    }

    private async Task UploadToGitHub()
    {
        statusMessage = "Uploading to GitHub...";
        
        if (string.IsNullOrEmpty(repositoryUrl) || string.IsNullOrEmpty(githubToken))
        {
            throw new Exception("GitHub repository URL and token are required");
        }

        // Prevent compilation during upload to avoid interruption
        EditorApplication.LockReloadAssemblies();

        try
        {
            // Extract owner and repo from repository URL
            string[] urlParts = repositoryUrl.Split(new[] { "github.com/" }, StringSplitOptions.RemoveEmptyEntries);
            if (urlParts.Length != 2)
            {
                throw new Exception("Invalid repository URL format. Expected: https://github.com/owner/repo");
            }

            string[] ownerRepo = urlParts[1].Split('/');
            if (ownerRepo.Length != 2)
            {
                throw new Exception("Invalid repository URL format. Expected: https://github.com/owner/repo");
            }

            string owner = ownerRepo[0];
            string repo = ownerRepo[1].Replace(".git", "");

            // Prepare release information
            string version = PlayerSettings.bundleVersion;
            string tagName = $"v{version}";
            string title = string.IsNullOrEmpty(releaseTitle) ? $"{Application.productName}-{version}" : releaseTitle;
            string description = string.IsNullOrEmpty(releaseDescription) ? $"Release {version}" : releaseDescription;

            // Create GitHub API instance
            var api = new GitHubAPI(githubToken, owner, repo);
            
            // Create the release
            statusMessage = "Creating GitHub release...";
            string releaseResponse = await api.CreateRelease(tagName, title, description, includePrerelease);

            // Parse the upload URL from the response
            var releaseData = JsonUtility.FromJson<ReleaseResponse>(releaseResponse);
            if (string.IsNullOrEmpty(releaseData.upload_url))
            {
                throw new Exception("Failed to get upload URL from release response");
            }

            // Find and upload release files
            string releasePath = Path.Combine(RELEASE_PATH, $"v{version}");
            if (!Directory.Exists(releasePath))
            {
                throw new Exception("Release folder not found. Please create release files first.");
            }

            var releaseFiles = new List<string>();
            releaseFiles.AddRange(Directory.GetFiles(releasePath, "*.zip"));
            releaseFiles.AddRange(Directory.GetFiles(releasePath, "*.dmg"));
            releaseFiles.AddRange(Directory.GetFiles(releasePath, "*.exe"));
            releaseFiles.AddRange(Directory.GetFiles(releasePath, "*.iss"));

            if (releaseFiles.Count == 0)
            {
                throw new Exception("No release files found to upload");
            }

            // Upload each release file
            foreach (string releaseFile in releaseFiles)
            {
                statusMessage = $"Uploading {Path.GetFileName(releaseFile)}...";
                await api.UploadReleaseAsset(releaseData.upload_url, releaseFile);
            }

            statusMessage = "GitHub release created successfully!";
            
            // Trigger DMG creation if enabled and macOS build exists
            if (enableDMGCreation && buildMacOS)
            {
                await TriggerDMGCreation(api, releaseData, version, releasePath);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"GitHub upload failed: {ex.Message}");
        }
        finally
        {
            // Re-enable compilation
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    private void SetMacOSExecutablePermissions(string appPath)
    {
        try
        {
            // Set executable permissions for the entire app bundle
            // This ensures the app can be opened by end users
            string chmodArgs = $"-R +x \"{appPath}\"";
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = chmodArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log($"Successfully set executable permissions for macOS app: {appPath}");
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    UnityEngine.Debug.LogWarning($"Failed to set executable permissions for macOS app: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"Exception while setting macOS executable permissions: {ex.Message}");
        }
    }

    private async Task<string> RunCommand(string command, string arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = processStartInfo })
        {
            process.Start();
            
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            
            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode != 0)
            {
                throw new Exception($"Command '{command} {arguments}' failed with exit code {process.ExitCode}. Error: {error}");
            }

            return output;
                }
    }

    private async Task TriggerDMGCreation(GitHubAPI api, ReleaseResponse releaseData, string version, string releasePath)
    {
        try
        {
            statusMessage = "Triggering DMG creation workflow...";
            
            // Find macOS ZIP file
            string macOSZipPath = Path.Combine(releasePath, $"{Application.productName}-MacOS.zip");
            if (!File.Exists(macOSZipPath))
            {
                UnityEngine.Debug.LogWarning("macOS ZIP file not found, cannot create DMG");
                return;
            }
            
            // Get the download URL for the ZIP file from the release
            string downloadUrl = $"{repositoryUrl}/releases/download/v{version}/{Application.productName}-MacOS.zip";
            
            // Extract release ID from upload URL
            string releaseId = releaseData.upload_url.Split('/')[^3];
            
            // Trigger the DMG creation workflow
            bool success = await api.TriggerWorkflow("create-dmg.yml", "main", downloadUrl, Application.productName, version, releaseId);
            
            if (success)
            {
                statusMessage = "DMG creation workflow triggered successfully! The DMG will be attached to the release automatically.";
                UnityEngine.Debug.Log("DMG creation workflow started. Check GitHub Actions for progress, and the DMG will be attached to the release when complete.");
            }
            else
            {
                statusMessage = "Failed to trigger DMG creation workflow.";
            }
        }
        catch (Exception ex)
        {
            statusMessage = $"DMG creation workflow failed: {ex.Message}";
            UnityEngine.Debug.LogError($"DMG workflow error: {ex}");
        }
    }

    private async void TriggerManualDMGCreation()
    {
        if (string.IsNullOrEmpty(repositoryUrl) || string.IsNullOrEmpty(githubToken))
        {
            statusMessage = "GitHub repository URL and token are required for DMG creation.";
            return;
        }

        isProcessing = true;
        
        try
        {
            statusMessage = "Triggering manual DMG creation...";

            // Extract owner and repo from repository URL
            string[] urlParts = repositoryUrl.Split(new[] { "github.com/" }, StringSplitOptions.RemoveEmptyEntries);
            if (urlParts.Length != 2)
            {
                throw new Exception("Invalid repository URL format. Expected: https://github.com/owner/repo");
            }

            string[] ownerRepo = urlParts[1].Split('/');
            if (ownerRepo.Length != 2)
            {
                throw new Exception("Invalid repository URL format. Expected: https://github.com/owner/repo");
            }

            string owner = ownerRepo[0];
            string repo = ownerRepo[1].Replace(".git", "");

            var api = new GitHubAPI(githubToken, owner, repo);

            // Use current version or check for existing release
            string version = PlayerSettings.bundleVersion;
            var release = await api.GetReleaseByTag($"v{version}");
            
            if (release == null)
            {
                statusMessage = "No release found for current version. Please create a release first.";
                return;
            }

            string releaseId = release.id.ToString();
            string downloadUrl = $"{repositoryUrl}/releases/download/v{version}/{Application.productName}-MacOS.zip";

            // Trigger the DMG creation workflow
            bool success = await api.TriggerWorkflow("create-dmg.yml", "main", downloadUrl, Application.productName, version, releaseId);

            if (success)
            {
                statusMessage = "DMG creation workflow triggered successfully! The DMG will be attached to the release automatically.";
                UnityEngine.Debug.Log("DMG creation workflow started. Check GitHub Actions for progress, and the DMG will be attached to the release when complete.");
            }
            else
            {
                statusMessage = "Failed to trigger DMG creation workflow.";
            }
        }
        catch (Exception ex)
        {
            statusMessage = $"DMG creation failed: {ex.Message}";
            UnityEngine.Debug.LogError($"DMG creation error: {ex}");
        }
        finally
        {
            isProcessing = false;
        }
    }

    [Serializable]
    private class ReleaseResponse
    {
        public string upload_url;
    }
} 