#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

/// <summary>
/// Manages automatic updates for the application by checking GitHub releases.
/// Handles version comparison, download, and installation of updates.
/// </summary>
public class UpdateChecker : MonoBehaviour
{
    public static UpdateChecker Singleton { get; private set; }

    private const string GITHUB_API_URL = "https://api.github.com/repos/{owner}/{repo}/releases/latest";
    private const string GITHUB_REPO_URL = "https://api.github.com/repos/{owner}/{repo}";
    private const string GITHUB_RELEASES_URL = "https://api.github.com/repos/{owner}/{repo}/releases";

    [Header("GitHub Configuration")]
    [Tooltip("GitHub repository URL (e.g., https://github.com/username/repo)")]
    [SerializeField] private string githubRepoUrl = "";
    [Tooltip("Optional GitHub access token for private repositories")]
    [SerializeField] private string githubToken = "";

    public bool updateAvailable { get; private set; } = false;
    private bool isConfigured = false;
    private string githubOwner;
    private string githubRepo;

    /// <summary>
    /// Represents a GitHub release with its associated metadata and assets.
    /// </summary>
    [Serializable]
    private class GitHubRelease
    {
        public string tag_name;
        public string html_url;
        public string body;
        public bool prerelease;
        public GitHubAsset[] assets;
    }

    /// <summary>
    /// Represents a GitHub release asset with its name and download URL.
    /// </summary>
    [Serializable]
    private class GitHubAsset
    {
        public string name;
        public string url;
    }

    /// <summary>
    /// Represents a list of GitHub releases.
    /// </summary>
    [Serializable]
    private class GitHubReleaseList
    {
        public GitHubRelease[] releases;
    }

    /// <summary>
    /// Initializes the UpdateChecker singleton.
    /// </summary>
    private void Awake()
    {
        if (Singleton == null)
        {
            Singleton = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Configures the update checker with settings a url source.
    /// </summary>
    public void ConfigureUpdateSource(string url, string token = "")
    {
        githubRepoUrl = url;
        githubToken = token;
        ParseGitHubUrl();
        isConfigured = true;
    }

    /// <summary>
    /// Parses the GitHub repository URL to extract owner and repository names.
    /// </summary>
    private void ParseGitHubUrl()
    {
        if (string.IsNullOrEmpty(githubRepoUrl)) return;

        try
        {
            // Handle both https://github.com/owner/repo and owner/repo formats
            string[] parts = githubRepoUrl.Split(new[] { "github.com/" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                string[] ownerRepo = parts[1].Split('/');
                if (ownerRepo.Length >= 2)
                {
                    githubOwner = ownerRepo[0];
                    // Take the entire repository name, only remove .git if it's at the end
                    githubRepo = ownerRepo[1].EndsWith(".git") ? ownerRepo[1].Substring(0, ownerRepo[1].Length - 4) : ownerRepo[1];
                }
            }
            else
            {
                // Try direct owner/repo format
                string[] ownerRepo = githubRepoUrl.Split('/');
                if (ownerRepo.Length >= 2)
                {
                    githubOwner = ownerRepo[0];
                    // Take the entire repository name, only remove .git if it's at the end
                    githubRepo = ownerRepo[1].EndsWith(".git") ? ownerRepo[1].Substring(0, ownerRepo[1].Length - 4) : ownerRepo[1];
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to parse GitHub URL: {e.Message}");
            githubOwner = "";
            githubRepo = "";
        }
    }

    /// <summary>
    /// Validates the GitHub repository configuration.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(githubRepoUrl))
        {
            UnityEngine.Debug.LogError("UpdateChecker: GitHub repository URL is not configured. Please set it in the Inspector or call ConfigureUpdateSource()");
            return;
        }

        ParseGitHubUrl();
        if (string.IsNullOrEmpty(githubOwner) || string.IsNullOrEmpty(githubRepo))
        {
            UnityEngine.Debug.LogError("UpdateChecker: Invalid GitHub repository URL format. Please use format: https://github.com/username/repo or username/repo");
            return;
        }
    }

    /// <summary>
    /// Checks if the GitHub repository configuration is valid.
    /// </summary>
    /// <returns>True if the configuration is valid, false otherwise.</returns>
    private bool IsConfigurationValid()
    {
        ParseGitHubUrl();
        return !string.IsNullOrEmpty(githubOwner) && !string.IsNullOrEmpty(githubRepo);
    }

    /// <summary>
    /// Gets the possible platform-specific asset names for the update package.
    /// For Mac, returns both .dmg and .zip options in priority order.
    /// </summary>
    /// <returns>Array of possible asset names for the current platform in priority order.</returns>
    private string[] GetPlatformAssetNames()
    {
        string platform = "";
        #if UNITY_STANDALONE_WIN
            platform = "Windows";
            return new string[] { $"{Application.productName}-{platform}.zip" };
        #elif UNITY_STANDALONE_LINUX
            platform = "Linux";
            return new string[] { $"{Application.productName}-{platform}.zip" };
        #elif UNITY_STANDALONE_OSX
            platform = "Mac";
            // Try .dmg first, fallback to .zip
            return new string[] { 
                $"{Application.productName}-{platform}.dmg",
                $"{Application.productName}-{platform}.zip"
            };
        #else
            return new string[] { $"{Application.productName}.zip" };
        #endif
    }

    /// <summary>
    /// Finds the appropriate platform asset from the release assets.
    /// For Mac, tries .dmg first, then falls back to .zip.
    /// </summary>
    /// <param name="assets">The array of release assets to search through.</param>
    /// <param name="foundAssetType">Output parameter indicating the type of asset found ("dmg" or "zip").</param>
    /// <returns>The matching asset, or null if none found.</returns>
    private GitHubAsset FindPlatformAsset(GitHubAsset[] assets, out string foundAssetType)
    {
        foundAssetType = null;
        if (assets == null) return null;
        
        string[] possibleNames = GetPlatformAssetNames();
        
        foreach (string assetName in possibleNames)
        {
            foreach (var asset in assets)
            {
                if (asset.name == assetName)
                {
                    foundAssetType = assetName.EndsWith(".dmg") ? "dmg" : "zip";
                    UnityEngine.Debug.Log($"Found platform asset: {asset.name} (type: {foundAssetType})");
                    return asset;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Compares version numbers to determine if an update is available.
    /// </summary>
    /// <param name="newVersion">The version number of the update.</param>
    /// <param name="currentVersion">The current application version number.</param>
    /// <returns>True if the new version is higher than the current version.</returns>
    private bool IsNewerVersion(string newVersion, string currentVersion)
    {
        try
        {
            // Remove any 'v' prefix and ensure we don't have a leading dot
            newVersion = newVersion.TrimStart('v').TrimStart('.');
            currentVersion = currentVersion.TrimStart('v').TrimStart('.');
            
            // Split version into components
            string[] newParts = newVersion.Split('.');
            string[] currentParts = currentVersion.Split('.');
            
            // Compare each component
            for (int i = 0; i < Math.Max(newParts.Length, currentParts.Length); i++)
            {
                int newPart = i < newParts.Length ? int.Parse(newParts[i]) : 0;
                int currentPart = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;
                
                if (newPart > currentPart) return true;
                if (newPart < currentPart) return false;
            }
            
            return false; // Versions are equal
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to parse version numbers: new={newVersion}, current={currentVersion}, error={e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks GitHub for available updates.
    /// </summary>
    public async void CheckForUpdates()
    {
        if (!isConfigured || !IsConfigurationValid())
        {
            UnityEngine.Debug.LogError("UpdateChecker: Cannot check for updates - configuration is invalid or not set.");
            return;
        }

        try
        {
            // First check if repository exists
            string repoUrl = GITHUB_REPO_URL.Replace("{owner}", githubOwner).Replace("{repo}", githubRepo);

            using (UnityWebRequest repoRequest = UnityWebRequest.Get(repoUrl))
            {
                repoRequest.SetRequestHeader("User-Agent", "Unity-WebRequest");
                repoRequest.SetRequestHeader("Accept", "application/vnd.github.v3+json");
                
                if (!string.IsNullOrEmpty(githubToken))
                {
                    repoRequest.SetRequestHeader("Authorization", $"token {githubToken}");
                }
                
                var repoOperation = repoRequest.SendWebRequest();
                while (!repoOperation.isDone)
                    await System.Threading.Tasks.Task.Yield();

                if (repoRequest.result != UnityWebRequest.Result.Success)
                {
                    HandleRepositoryError(repoRequest);
                    return;
                }

                // If we get here, repository exists, now check for releases
                string releasesUrl = GITHUB_RELEASES_URL.Replace("{owner}", githubOwner).Replace("{repo}", githubRepo);
                UnityEngine.Debug.Log($"Checking for releases at: {releasesUrl}");

                using (UnityWebRequest request = UnityWebRequest.Get(releasesUrl))
                {
                    request.SetRequestHeader("User-Agent", "Unity-WebRequest");
                    request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
                    
                    if (!string.IsNullOrEmpty(githubToken))
                    {
                        request.SetRequestHeader("Authorization", $"token {githubToken}");
                    }
                    
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                        await System.Threading.Tasks.Task.Yield();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string jsonResponse = request.downloadHandler.text;
                        jsonResponse = "{\"releases\":" + jsonResponse + "}";
                        GitHubReleaseList releaseList = JsonUtility.FromJson<GitHubReleaseList>(jsonResponse);
                        
                        if (releaseList.releases == null || releaseList.releases.Length == 0)
                        {
                            UnityEngine.Debug.LogError("No releases found in the repository.");
                            return;
                        }

                        string currentVersion = Application.version;
                        GitHubRelease latestRelease = null;
                        string latestVersion = currentVersion;

                        foreach (var release in releaseList.releases)
                        {
                            // Skip pre-releases unless explicitly enabled
                            if (release.prerelease) continue;

                            string releaseVersion = release.tag_name.TrimStart('v');
                            if (IsNewerVersion(releaseVersion, latestVersion))
                            {
                                latestVersion = releaseVersion;
                                latestRelease = release;
                            }
                        }

                        if (latestRelease != null && IsNewerVersion(latestVersion, currentVersion))
                        {
                            string foundAssetType;
                            GitHubAsset targetAsset = FindPlatformAsset(latestRelease.assets, out foundAssetType);

                            if (targetAsset != null)
                            {
                                UnityEngine.Debug.Log($"<color=yellow>New version {latestVersion} available! Current version: {currentVersion}</color>");
                                UnityEngine.Debug.Log($"<color=yellow>Release notes: {latestRelease.body}</color>");
                                UnityEngine.Debug.Log($"<color=yellow>Download URL: {targetAsset.url}</color>");
                                UnityEngine.Debug.Log($"<color=yellow>Asset type: {foundAssetType}</color>");
                                #if UNITY_EDITOR
                                    UnityEngine.Debug.Log("<color=yellow>Updates cannot be installed from the Unity Editor. This will work in standalone builds.</color>");
#else
                                    UnityEngine.Debug.Log("<color=yellow>Run the 'UpdateApplication' command to update.</color>");
                                                                    updateAvailable = true;
#endif
                            }
                            else
                            {
                                string[] possibleNames = GetPlatformAssetNames();
                                string expectedNames = string.Join(", ", possibleNames);
                                UnityEngine.Debug.LogError($"No matching asset found for platform. Expected asset names: {expectedNames}");
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.Log("Application is up to date.");
                        }
                    }
                    else
                    {
                        HandleReleaseError(request);
                    }
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error checking for updates: {e.Message}");
        }
    }

    /// <summary>
    /// Handles errors that occur when checking the repository.
    /// </summary>
    /// <param name="request">The web request that failed.</param>
    private void HandleRepositoryError(UnityWebRequest request)
    {
        string errorMessage = request.error;
        string responseBody = request.downloadHandler?.text ?? "No response body";
        
        if (request.responseCode == 404)
        {
            errorMessage = $"Repository not found. Please verify:\n1. The repository '{githubRepo}' exists under user/organization '{githubOwner}'\n2. The repository name is exactly correct (case-sensitive)\n3. The repository is accessible with your token";
        }
        else if (request.responseCode == 401)
        {
            errorMessage = "Authentication failed. Please check your GitHub token. Make sure it has the 'repo' scope and is valid.";
        }
        else if (request.responseCode == 403)
        {
            errorMessage = "Access forbidden. The token may not have the required permissions or the repository may be private.";
        }
        
        UnityEngine.Debug.LogError($"Failed to access repository: {errorMessage}");
        UnityEngine.Debug.LogError($"Response code: {request.responseCode}");
        UnityEngine.Debug.LogError($"Response body: {responseBody}");
    }

    /// <summary>
    /// Handles errors that occur when checking releases.
    /// </summary>
    /// <param name="request">The web request that failed.</param>
    private void HandleReleaseError(UnityWebRequest request)
    {
        string errorMessage = request.error;
        string responseBody = request.downloadHandler?.text ?? "No response body";
        
        if (request.responseCode == 404)
        {
            errorMessage = "No releases found for this repository. Please create at least one release.";
        }
        else if (request.responseCode == 401)
        {
            errorMessage = "Authentication failed. Please check your GitHub token. Make sure it has the 'repo' scope and is valid.";
        }
        else if (request.responseCode == 403)
        {
            errorMessage = "Access forbidden. The token may not have the required permissions or the repository may be private.";
        }
        else if (request.responseCode == 429)
        {
            errorMessage = "GitHub API rate limit exceeded. Please try again later.";
        }
        else if (request.responseCode >= 500)
        {
            errorMessage = "GitHub API server error. Please try again later.";
        }
        
        UnityEngine.Debug.LogError($"Failed to check for releases: {errorMessage}");
        UnityEngine.Debug.LogError($"Response code: {request.responseCode}");
        UnityEngine.Debug.LogError($"Response body: {responseBody}");
    }

    #if UNITY_STANDALONE_OSX
    /// <summary>
    /// Installs a .dmg update on macOS by mounting the disk image and handling the installation.
    /// </summary>
    /// <param name="dmgPath">Path to the downloaded .dmg file.</param>
    /// <param name="tempDir">Temporary directory for extraction.</param>
    /// <returns>True if installation was successful or initiated.</returns>
    private async Task<bool> InstallDmgUpdate(string dmgPath, string tempDir)
    {
        try
        {
            UnityEngine.Debug.Log("Mounting .dmg file...");
            
            // Mount the .dmg file using hdiutil
            ProcessStartInfo mountInfo = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"attach \"{dmgPath}\" -nobrowse -quiet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process mountProcess = Process.Start(mountInfo))
            {
                await Task.Run(() => mountProcess.WaitForExit());
                
                if (mountProcess.ExitCode != 0)
                {
                    string error = await mountProcess.StandardError.ReadToEndAsync();
                    UnityEngine.Debug.LogError($"Failed to mount .dmg: {error}");
                    return false;
                }

                string mountOutput = await mountProcess.StandardOutput.ReadToEndAsync();
                UnityEngine.Debug.Log($"Mount output: {mountOutput}");
                
                // Parse mount output to find the mount point
                string mountPoint = ExtractMountPoint(mountOutput);
                if (string.IsNullOrEmpty(mountPoint))
                {
                    UnityEngine.Debug.LogError("Could not determine mount point from hdiutil output");
                    return false;
                }

                UnityEngine.Debug.Log($"Mounted at: {mountPoint}");
                
                // Find .app bundle in mounted volume
                string appPath = FindAppBundle(mountPoint);
                if (string.IsNullOrEmpty(appPath))
                {
                    UnityEngine.Debug.LogError("No .app bundle found in mounted .dmg");
                    // Still try to open the mounted volume for user interaction
                    OpenMountedVolume(mountPoint);
                    return true; // Consider this successful as user can manually install
                }

                UnityEngine.Debug.Log($"Found app bundle: {appPath}");
                
                // Attempt automatic installation to Applications folder
                bool autoInstallSuccess = await AttemptAutomaticInstall(appPath);
                
                if (!autoInstallSuccess)
                {
                    // Fall back to opening the mounted volume for user interaction
                    UnityEngine.Debug.Log("Automatic installation failed, opening mounted volume for user interaction...");
                    OpenMountedVolume(mountPoint);
                }
                
                // Schedule cleanup (unmount) after a delay to allow user interaction
                _ = Task.Run(async () => {
                    await Task.Delay(30000); // Wait 30 seconds
                    UnmountDmg(mountPoint);
                });
                
                return true;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error processing .dmg file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extracts the mount point from hdiutil output.
    /// </summary>
    private string ExtractMountPoint(string hdiutilOutput)
    {
        try
        {
            string[] lines = hdiutilOutput.Split('\n');
            foreach (string line in lines)
            {
                if (line.Contains("/Volumes/"))
                {
                    // Find the mount point (typically the last part of the line)
                    string[] parts = line.Split('\t');
                    for (int i = parts.Length - 1; i >= 0; i--)
                    {
                        if (parts[i].StartsWith("/Volumes/"))
                        {
                            return parts[i].Trim();
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error parsing mount point: {e.Message}");
        }
        return null;
    }

    /// <summary>
    /// Finds the .app bundle in the mounted volume.
    /// </summary>
    private string FindAppBundle(string mountPoint)
    {
        try
        {
            if (!Directory.Exists(mountPoint))
                return null;

            string[] appBundles = Directory.GetDirectories(mountPoint, "*.app");
            return appBundles.Length > 0 ? appBundles[0] : null;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error finding app bundle: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to automatically install the app to the Applications folder.
    /// </summary>
    private async Task<bool> AttemptAutomaticInstall(string appPath)
    {
        try
        {
            string appName = Path.GetFileName(appPath);
            string applicationsPath = "/Applications";
            string destinationPath = Path.Combine(applicationsPath, appName);
            
            UnityEngine.Debug.Log($"Attempting to install {appName} to Applications folder...");
            
            // Check if we have write permissions to Applications folder
            if (!Directory.Exists(applicationsPath))
            {
                UnityEngine.Debug.LogError("Applications folder not found");
                return false;
            }

            // Remove existing installation if it exists
            if (Directory.Exists(destinationPath))
            {
                UnityEngine.Debug.Log("Removing existing installation...");
                ProcessStartInfo removeInfo = new ProcessStartInfo
                {
                    FileName = "rm",
                    Arguments = $"-rf \"{destinationPath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process removeProcess = Process.Start(removeInfo))
                {
                    await Task.Run(() => removeProcess.WaitForExit());
                    if (removeProcess.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogWarning("Failed to remove existing installation, continuing anyway...");
                    }
                }
            }

            // Copy the app bundle to Applications
            ProcessStartInfo copyInfo = new ProcessStartInfo
            {
                FileName = "cp",
                Arguments = $"-R \"{appPath}\" \"{applicationsPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process copyProcess = Process.Start(copyInfo))
            {
                await Task.Run(() => copyProcess.WaitForExit());
                
                if (copyProcess.ExitCode == 0)
                {
                    UnityEngine.Debug.Log($"Successfully installed {appName} to Applications folder!");
                    
                    // Launch the new version
                    LaunchNewApplication(destinationPath);
                    return true;
                }
                else
                {
                    string error = await copyProcess.StandardError.ReadToEndAsync();
                    UnityEngine.Debug.LogError($"Failed to copy app to Applications: {error}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error during automatic installation: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Opens the mounted volume in Finder for user interaction.
    /// </summary>
    private void OpenMountedVolume(string mountPoint)
    {
        try
        {
            ProcessStartInfo openInfo = new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{mountPoint}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(openInfo);
            UnityEngine.Debug.Log($"Opened mounted volume: {mountPoint}");
            UnityEngine.Debug.Log("Please drag the application to your Applications folder to complete the update.");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to open mounted volume: {e.Message}");
        }
    }

    /// <summary>
    /// Launches the newly installed application.
    /// </summary>
    private void LaunchNewApplication(string appPath)
    {
        try
        {
            ProcessStartInfo launchInfo = new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{appPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(launchInfo);
            UnityEngine.Debug.Log($"Launched new application: {appPath}");
            
            // Schedule quit of current application
            Application.Quit();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to launch new application: {e.Message}");
        }
    }

    /// <summary>
    /// Unmounts the .dmg file.
    /// </summary>
    private void UnmountDmg(string mountPoint)
    {
        try
        {
            ProcessStartInfo unmountInfo = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"detach \"{mountPoint}\" -quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process unmountProcess = Process.Start(unmountInfo);
            unmountProcess.WaitForExit();
            
            if (unmountProcess.ExitCode == 0)
            {
                UnityEngine.Debug.Log($"Successfully unmounted: {mountPoint}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Failed to unmount: {mountPoint}");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error unmounting .dmg: {e.Message}");
        }
    }
    #endif

    /// <summary>
    /// Downloads a GitHub release asset.
    /// </summary>
    /// <param name="asset">The asset to download.</param>
    /// <returns>The downloaded asset data as a byte array.</returns>
    private async Task<byte[]> DownloadAssetData(GitHubAsset asset)
    {
        using (UnityWebRequest downloadRequest = UnityWebRequest.Get(asset.url))
        {
            downloadRequest.SetRequestHeader("User-Agent", "Unity-WebRequest");
            downloadRequest.SetRequestHeader("Accept", "application/octet-stream");
            downloadRequest.redirectLimit = 5;
            
            if (!string.IsNullOrEmpty(githubToken))
            {
                downloadRequest.SetRequestHeader("Authorization", $"token {githubToken}");
            }
            
            var downloadOperation = downloadRequest.SendWebRequest();
            while (!downloadOperation.isDone)
                await Task.Yield();

            if (downloadRequest.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Download failed: {downloadRequest.error}");
                UnityEngine.Debug.LogError($"Response code: {downloadRequest.responseCode}");
                return null;
            }

            return downloadRequest.downloadHandler.data;
        }
    }

    /// <summary>
    /// Gets the base path of the application.
    /// </summary>
    /// <returns>The application's base directory path.</returns>
    private string GetApplicationBasePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "..");
#else
        return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
#endif
    }

    /// <summary>
    /// Downloads and installs the latest update.
    /// </summary>
    public async void UpdateApplication()
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogWarning("UpdateApplication cannot be run from the Unity Editor. This command only works in standalone builds.");
        return;
#endif

#pragma warning disable CS0162 // Unreachable code detected
        if (!IsConfigurationValid())
#pragma warning restore CS0162 // Unreachable code detected
        {
            UnityEngine.Debug.LogError("UpdateChecker: Cannot update - configuration is invalid.");
            return;
        }

        string basePath = GetApplicationBasePath();
        string tempDir = FileUtility.GetTempDirectoryPath("update");
        // We'll set the actual download path after we know the asset type

        UnityEngine.Debug.Log($"Base path: {basePath}");
        UnityEngine.Debug.Log($"Temp directory: {tempDir}");

        try
        {
            // Get the latest release information
            string releasesUrl = GITHUB_RELEASES_URL.Replace("{owner}", githubOwner).Replace("{repo}", githubRepo);
            
            using (UnityWebRequest request = UnityWebRequest.Get(releasesUrl))
            {
                request.SetRequestHeader("User-Agent", "Unity-WebRequest");
                request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
                
                if (!string.IsNullOrEmpty(githubToken))
                {
                    request.SetRequestHeader("Authorization", $"token {githubToken}");
                }
                
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await System.Threading.Tasks.Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    HandleReleaseError(request);
                    return;
                }

                string jsonResponse = request.downloadHandler.text;
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    UnityEngine.Debug.LogError("Received empty response from GitHub API");
                    return;
                }

                try
                {
                    jsonResponse = "{\"releases\":" + jsonResponse + "}";
                    GitHubReleaseList releaseList = JsonUtility.FromJson<GitHubReleaseList>(jsonResponse);

                    if (releaseList.releases == null || releaseList.releases.Length == 0)
                    {
                        UnityEngine.Debug.LogError("No releases found in the repository.");
                        return;
                    }

                    // Find the latest non-prerelease version
                    GitHubRelease latestRelease = null;
                    foreach (var release in releaseList.releases)
                    {
                        if (!release.prerelease)
                        {
                            latestRelease = release;
                            break;
                        }
                    }

                    if (latestRelease == null)
                    {
                        UnityEngine.Debug.LogError("No stable releases found in the repository.");
                        return;
                    }

                    UnityEngine.Debug.Log($"Found release: {latestRelease.tag_name}");

                    string foundAssetType;
                    GitHubAsset targetAsset = FindPlatformAsset(latestRelease.assets, out foundAssetType);

                    if (targetAsset == null)
                    {
                        string[] possibleNames = GetPlatformAssetNames();
                        string expectedNames = string.Join(", ", possibleNames);
                        UnityEngine.Debug.LogError($"No matching asset found in release {latestRelease.tag_name}. Expected asset names: {expectedNames}");
                        return;
                    }

                    // Set download path based on actual asset name
                    string downloadPath = FileUtility.GetTempFilePath(targetAsset.name);
                    UnityEngine.Debug.Log($"Download path: {downloadPath}");

                    UnityEngine.Debug.Log("Downloading update...");
                    byte[] assetData = await DownloadAssetData(targetAsset);
                    if (assetData == null)
                    {
                        UnityEngine.Debug.LogError("Failed to download asset data");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    try
                    {
                        File.WriteAllBytes(downloadPath, assetData);
                        UnityEngine.Debug.Log($"Downloaded {assetData.Length / 1024 / 1024} MB to {downloadPath}");
                        
                        // Verify the file was written correctly
                        if (!FileUtility.VerifyFileExists(downloadPath, "download"))
                        {
                            FileUtility.CleanupTempFiles();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Failed to save downloaded file: {e.Message}");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    // Verify downloaded file exists and is accessible
                    if (!FileUtility.VerifyFileExists(downloadPath, "pre-extraction"))
                    {
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    // Handle extraction based on file type
                    bool extractSuccess = false;
                    
                    if (foundAssetType == "dmg")
                    {
                        #if UNITY_STANDALONE_OSX
                            UnityEngine.Debug.Log("Processing .dmg update...");
                            extractSuccess = await InstallDmgUpdate(downloadPath, tempDir);
                        #else
                            UnityEngine.Debug.LogError(".dmg files are only supported on macOS");
                            extractSuccess = false;
                        #endif
                    }
                    else if (foundAssetType == "zip")
                    {
                        UnityEngine.Debug.Log("Extracting .zip update...");
                        extractSuccess = await FileUtility.ExtractZip(downloadPath, tempDir, (progress) => {
                            UnityEngine.Debug.Log($"Extraction progress: {progress:P0}");
                        });
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Unsupported asset type: {foundAssetType}");
                        extractSuccess = false;
                    }
                    
                    if (!extractSuccess)
                    {
                        UnityEngine.Debug.LogError("Extraction failed. Aborting update.");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    // Verify extracted files
                    if (!Directory.Exists(tempDir))
                    {
                        UnityEngine.Debug.LogError($"Extraction directory not found: {tempDir}");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    string[] extractedFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
                    if (extractedFiles.Length == 0)
                    {
                        UnityEngine.Debug.LogError("No files were extracted. Aborting update.");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    if (foundAssetType == "dmg")
                    {
                        UnityEngine.Debug.Log("DMG processing completed. Installation may have been automatic or user interaction required.");
                        // For .dmg files, installation is handled by InstallDmgUpdate method
                        // We can safely return here as the .dmg installation process is complete
                        return;
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"Extracted {extractedFiles.Length} files successfully");
                    }

                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    if (!File.Exists(exePath))
                    {
                        UnityEngine.Debug.LogError($"Current executable not found at: {exePath}");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    // Create updater script
                    string updaterPath = FileUtility.GetTempFilePath("updater.bat");
                    string updaterScript = $"@echo off\n" +
                        "echo Waiting for application to exit...\n" +
                        $"ping 127.0.0.1 -n 3 > nul\n" +
                        $":loop\n" +
                        $"tasklist | findstr /I \"{Path.GetFileName(exePath)}\" >nul\n" +
                        $"if not errorlevel 1 (\n" +
                        $"    ping 127.0.0.1 -n 2 > nul\n" +
                        $"    goto loop\n" +
                        $")\n" +
                        $"echo Copying new files...\n" +
                        $"xcopy /Y /E \"{tempDir}\\*\" \"{Path.GetDirectoryName(exePath)}\"\n" +
                        $"if errorlevel 1 (\n" +
                        $"    echo Failed to copy files. Aborting update.\n" +
                        $"    rmdir /S /Q \"{tempDir}\"\n" +
                        $"    del \"{downloadPath}\"\n" +
                        $"    exit /b 1\n" +
                        $")\n" +
                        $"echo Cleaning up...\n" +
                        $"rmdir /S /Q \"{tempDir}\"\n" +
                        $"del \"{downloadPath}\"\n" +
                        $"echo Starting application...\n" +
                        $"start \"\" \"{exePath}\"\n" +
                        $"echo Update complete!\n" +
                        $"del \"%~f0\"\n";

                    try
                    {
                        File.WriteAllText(updaterPath, updaterScript);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Failed to write updater script: {e.Message}");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    UnityEngine.Debug.Log("Launching updater and quitting application...");

                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = updaterPath, UseShellExecute = true });
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"Failed to start update process: {e.Message}");
                        FileUtility.CleanupTempFiles();
                        return;
                    }

                    Application.Quit();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Error processing release information: {e.Message}");
                    FileUtility.CleanupTempFiles();
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error during update process: {e.Message}");
            FileUtility.CleanupTempFiles();
        }
    }

    /// <summary>
    /// Sets the GitHub repository information.
    /// </summary>
    /// <param name="repoUrl">The GitHub repository URL.</param>
    /// <param name="token">Optional GitHub access token.</param>
    public void SetRepositoryInfo(string repoUrl, string token = null)
    {
        if (string.IsNullOrEmpty(repoUrl))
        {
            UnityEngine.Debug.LogError("UpdateChecker: Repository URL cannot be empty.");
            return;
        }

        githubRepoUrl = repoUrl;
        if (!string.IsNullOrEmpty(token))
            githubToken = token;

        ParseGitHubUrl();

        UnityEngine.Debug.Log($"UpdateChecker: Repository information updated to {githubOwner}/{githubRepo}");
        ValidateConfiguration();
    }

    /// <summary>
    /// Gets the configured GitHub repository URL.
    /// </summary>
    /// <returns>The GitHub repository URL.</returns>
    public string GetGitHubRepoUrl() => githubRepoUrl;

    /// <summary>
    /// Gets the configured GitHub access token.
    /// </summary>
    /// <returns>The GitHub access token.</returns>
    public string GetGitHubToken() => githubToken;
}
#endif 