using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;

public class FileUtility
{
    public static void OpenPath(string path)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            Verb = "open"
        });
#elif UNITY_ANDROID
        UnityEngine.Debug.Log($"Opening path not supported directly on Android. Path: {path}");
#elif UNITY_IOS
        UnityEngine.Debug.Log($"Opening path not supported directly on iOS. Path: {path}");
#endif
    }

    /// <summary>
    /// Yields until data has been written to disk.
    /// Note: this method is blocking.
    /// </summary>
    public static IEnumerator WriteToDisk(string directoryName, string path, byte[] data)
    {
        string fullPath = Path.Combine(Application.persistentDataPath, directoryName, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(data, 0, data.Length);
            fs.Flush(true); // Ensure data is written to disk
        }

        yield break;
    }

    /// <summary>
    /// Moves all contents from source folder to destination folder.
    /// </summary>
    /// <param name="sourcePath">The folder to move contents from.</param>
    /// <param name="destinationPath">The folder to move contents to.</param>
    public static void MoveFolderContents(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            UnityEngine.Debug.LogError($"Source directory does not exist: {sourcePath}");
            return;
        }

        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }

        // Move all files
        foreach (string file in Directory.GetFiles(sourcePath))
        {
            string destFile = Path.Combine(destinationPath, Path.GetFileName(file));
            File.Move(file, destFile);
        }

        // Move all subdirectories
        foreach (string dir in Directory.GetDirectories(sourcePath))
        {
            string destDir = Path.Combine(destinationPath, Path.GetFileName(dir));
            Directory.Move(dir, destDir);
        }

        UnityEngine.Debug.Log($"Moved contents from {sourcePath} to {destinationPath}");
    }

    /// <summary>
    /// Checks if a given file path is valid.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if the file path is valid, otherwise false.</returns>
    public static bool IsValidFilePath(string filePath)
    {
        try
        {
            // Check for invalid characters
            return !string.IsNullOrWhiteSpace(filePath) &&
                   filePath.IndexOfAny(Path.GetInvalidPathChars()) == -1 &&
                   Path.IsPathRooted(filePath);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error validating file path: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> DownloadFileAsync(string url, string outputPath, Action<float> onProgress = null)
    {
        try
        {
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && onProgress != null;
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (canReportProgress)
                            onProgress?.Invoke((float)totalRead / totalBytes);
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Download failed: {ex.Message}");
            return false;
        }
    }

    public static bool VerifyFileSystemAccess(string path)
    {
        try
        {
            // Check if we can access the parent directory
            string parentDir = Path.GetDirectoryName(path);
            if (!Directory.Exists(parentDir))
            {
                UnityEngine.Debug.LogError($"Parent directory does not exist: {parentDir}");
                return false;
            }

            // Try to create a test file
            string testFile = Path.Combine(parentDir, "test_write.tmp");
            File.WriteAllText(testFile, "test");
            if (!File.Exists(testFile))
            {
                UnityEngine.Debug.LogError($"Failed to create test file in: {parentDir}");
                return false;
            }

            // Try to read the test file
            string content = File.ReadAllText(testFile);
            if (content != "test")
            {
                UnityEngine.Debug.LogError($"Failed to read test file in: {parentDir}");
                File.Delete(testFile);
                return false;
            }

            // Clean up test file
            File.Delete(testFile);
            UnityEngine.Debug.Log($"Successfully verified write access to: {parentDir}");
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to verify file system access: {e.Message}");
            return false;
        }
    }

    public static bool EnsureTempDirectory(string tempDir)
    {
        try
        {
            // First verify we can access the parent directory
            string parentDir = Path.GetDirectoryName(tempDir);
            if (!VerifyFileSystemAccess(parentDir))
            {
                return false;
            }

            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                    UnityEngine.Debug.Log($"Deleted existing temp directory: {tempDir}");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to delete existing temp directory: {e.Message}");
                    return false;
                }
            }

            try
            {
                Directory.CreateDirectory(tempDir);
                UnityEngine.Debug.Log($"Created temp directory: {tempDir}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to create temp directory: {e.Message}");
                return false;
            }

            // Verify the directory was created
            if (!Directory.Exists(tempDir))
            {
                UnityEngine.Debug.LogError($"Directory not found after creation: {tempDir}");
                return false;
            }

            // Try to create a test file to verify write permissions
            string testFile = Path.Combine(tempDir, "test.txt");
            try
            {
                File.WriteAllText(testFile, "test");
                if (!File.Exists(testFile))
                {
                    UnityEngine.Debug.LogError($"Test file not found after creation: {testFile}");
                    return false;
                }

                string content = File.ReadAllText(testFile);
                if (content != "test")
                {
                    UnityEngine.Debug.LogError($"Failed to read test file: {testFile}");
                    File.Delete(testFile);
                    return false;
                }

                File.Delete(testFile);
                UnityEngine.Debug.Log($"Successfully verified temp directory: {tempDir}");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to verify temp directory: {e.Message}");
                return false;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to create/verify temp directory: {e.Message}");
            return false;
        }
    }

    public static bool VerifyFileExists(string filePath, string operation)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                UnityEngine.Debug.LogError($"File not found during {operation}: {filePath}");
                return false;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                UnityEngine.Debug.LogError($"File is empty during {operation}: {filePath}");
                return false;
            }

            UnityEngine.Debug.Log($"Verified file exists during {operation}: {filePath} ({fileInfo.Length / 1024 / 1024} MB)");
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to verify file during {operation}: {e.Message}");
            return false;
        }
    }

    public static async Task<bool> ExtractZip(string zipPath, string extractPath, System.Action<float> onProgress = null)
    {
        const int MAX_RETRIES = 3;
        const int RETRY_DELAY_MS = 1000;

        for (int retry = 0; retry < MAX_RETRIES; retry++)
        {
            try
            {
                if (!File.Exists(zipPath))
                {
                    if (retry < MAX_RETRIES - 1)
                    {
                        UnityEngine.Debug.LogWarning($"Zip file not found on attempt {retry + 1}, retrying in {RETRY_DELAY_MS}ms...");
                        await Task.Delay(RETRY_DELAY_MS);
                        continue;
                    }
                    UnityEngine.Debug.LogError($"Zip file not found after {MAX_RETRIES} attempts: {zipPath}");
                    return false;
                }

                // Verify zip file integrity and accessibility
                try
                {
                    using (var fileStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                    {
                        if (archive.Entries.Count == 0)
                        {
                            UnityEngine.Debug.LogError("Zip file is empty or corrupted");
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (retry < MAX_RETRIES - 1)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to verify zip file on attempt {retry + 1}: {e.Message}, retrying in {RETRY_DELAY_MS}ms...");
                        await Task.Delay(RETRY_DELAY_MS);
                        continue;
                    }
                    UnityEngine.Debug.LogError($"Zip file is corrupted or inaccessible after {MAX_RETRIES} attempts: {e.Message}");
                    return false;
                }

                // Create or clean extraction directory
                if (Directory.Exists(extractPath))
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                    }
                    catch (Exception e)
                    {
                        if (retry < MAX_RETRIES - 1)
                        {
                            UnityEngine.Debug.LogWarning($"Failed to clean extraction directory on attempt {retry + 1}: {e.Message}, retrying in {RETRY_DELAY_MS}ms...");
                            await Task.Delay(RETRY_DELAY_MS);
                            continue;
                        }
                        UnityEngine.Debug.LogError($"Failed to clean extraction directory after {MAX_RETRIES} attempts: {e.Message}");
                        return false;
                    }
                }

                try
                {
                    Directory.CreateDirectory(extractPath);
                }
                catch (Exception e)
                {
                    if (retry < MAX_RETRIES - 1)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to create extraction directory on attempt {retry + 1}: {e.Message}, retrying in {RETRY_DELAY_MS}ms...");
                        await Task.Delay(RETRY_DELAY_MS);
                        continue;
                    }
                    UnityEngine.Debug.LogError($"Failed to create extraction directory after {MAX_RETRIES} attempts: {e.Message}");
                    return false;
                }

                // Extract with progress reporting
                try
                {
                    using (var fileStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                    {
                        int totalEntries = archive.Entries.Count;
                        int processedEntries = 0;

                        foreach (var entry in archive.Entries)
                        {
                            try
                            {
                                string fullPath = Path.Combine(extractPath, entry.FullName);
                                string directory = Path.GetDirectoryName(fullPath);

                                // Create directory if it doesn't exist
                                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }

                                // Skip directories
                                if (string.IsNullOrEmpty(entry.Name))
                                {
                                    continue;
                                }

                                // Extract file
                                using (var entryStream = entry.Open())
                                using (var fileStream2 = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await entryStream.CopyToAsync(fileStream2);
                                }

                                // Verify extracted file
                                if (!File.Exists(fullPath))
                                {
                                    throw new Exception($"Failed to extract file: {entry.FullName}");
                                }

                                // Update progress
                                processedEntries++;
                                onProgress?.Invoke((float)processedEntries / totalEntries);
                            }
                            catch (Exception e)
                            {
                                UnityEngine.Debug.LogError($"Failed to extract entry {entry.FullName}: {e.Message}");
                                // Clean up partial extraction
                                try
                                {
                                    if (Directory.Exists(extractPath))
                                    {
                                        Directory.Delete(extractPath, true);
                                    }
                                }
                                catch (Exception cleanupEx)
                                {
                                    UnityEngine.Debug.LogError($"Failed to clean up after extraction error: {cleanupEx.Message}");
                                }
                                return false;
                            }
                        }
                    }

                    // Verify extraction was successful
                    string[] extractedFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                    if (extractedFiles.Length == 0)
                    {
                        UnityEngine.Debug.LogError("No files were extracted");
                        return false;
                    }

                    UnityEngine.Debug.Log($"Successfully extracted {extractedFiles.Length} files to {extractPath}");
                    return true;
                }
                catch (Exception e)
                {
                    if (retry < MAX_RETRIES - 1)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to extract zip file on attempt {retry + 1}: {e.Message}, retrying in {RETRY_DELAY_MS}ms...");
                        await Task.Delay(RETRY_DELAY_MS);
                        continue;
                    }
                    UnityEngine.Debug.LogError($"Failed to extract zip file after {MAX_RETRIES} attempts: {e.Message}");
                    // Clean up partial extraction
                    try
                    {
                        if (Directory.Exists(extractPath))
                        {
                            Directory.Delete(extractPath, true);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        UnityEngine.Debug.LogError($"Failed to clean up after extraction error: {cleanupEx.Message}");
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                if (retry < MAX_RETRIES - 1)
                {
                    UnityEngine.Debug.LogWarning($"Error during zip extraction on attempt {retry + 1}: {e.Message}, retrying in {RETRY_DELAY_MS}ms...");
                    await Task.Delay(RETRY_DELAY_MS);
                    continue;
                }
                UnityEngine.Debug.LogError($"Error during zip extraction after {MAX_RETRIES} attempts: {e.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a zip file from a directory.
    /// </summary>
    /// <param name="sourcePath">Path to the directory to zip</param>
    /// <param name="zipPath">Path where the zip file will be created</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool CreateZipFile(string sourcePath, string zipPath)
    {
        return CreateZipFile(sourcePath, zipPath, null);
    }

    /// <summary>
    /// Creates a zip file from a directory with optional exclusion patterns.
    /// </summary>
    /// <param name="sourcePath">Path to the directory to zip</param>
    /// <param name="zipPath">Path where the zip file will be created</param>
    /// <param name="excludePatterns">Array of patterns to exclude from the zip (e.g., "*_BurstDebugInformation_DoNotShip")</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool CreateZipFile(string sourcePath, string zipPath, string[] excludePatterns)
    {
        try
        {
            if (!Directory.Exists(sourcePath))
            {
                UnityEngine.Debug.LogError($"Source directory does not exist: {sourcePath}");
                return false;
            }

            // Ensure the target directory exists
            string targetDir = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Delete existing zip if it exists
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // If no exclusion patterns, use the simple method
            if (excludePatterns == null || excludePatterns.Length == 0)
            {
                ZipFile.CreateFromDirectory(sourcePath, zipPath);
                UnityEngine.Debug.Log($"Created zip file: {zipPath}");
                return true;
            }

            // Create zip with exclusions
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                AddDirectoryToZip(archive, sourcePath, "", excludePatterns);
            }

            UnityEngine.Debug.Log($"Created filtered zip file: {zipPath} (excluded: {string.Join(", ", excludePatterns)})");
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to create zip file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Recursively adds files and directories to a zip archive, excluding specified patterns.
    /// </summary>
    private static void AddDirectoryToZip(ZipArchive archive, string sourcePath, string entryPath, string[] excludePatterns)
    {
        string[] files = Directory.GetFiles(sourcePath);
        string[] directories = Directory.GetDirectories(sourcePath);

        // Add files
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            string relativePath = string.IsNullOrEmpty(entryPath) ? fileName : Path.Combine(entryPath, fileName);
            
            // Check if file should be excluded
            if (!ShouldExclude(file, fileName, excludePatterns))
            {
                archive.CreateEntryFromFile(file, relativePath);
            }
            else
            {
                UnityEngine.Debug.Log($"Excluded file from zip: {relativePath}");
            }
        }

        // Add directories
        foreach (string directory in directories)
        {
            string dirName = Path.GetFileName(directory);
            string relativePath = string.IsNullOrEmpty(entryPath) ? dirName : Path.Combine(entryPath, dirName);
            
            // Check if directory should be excluded
            if (!ShouldExclude(directory, dirName, excludePatterns))
            {
                AddDirectoryToZip(archive, directory, relativePath, excludePatterns);
            }
            else
            {
                UnityEngine.Debug.Log($"Excluded directory from zip: {relativePath}");
            }
        }
    }

    /// <summary>
    /// Checks if a file or directory should be excluded based on patterns.
    /// </summary>
    private static bool ShouldExclude(string fullPath, string name, string[] excludePatterns)
    {
        if (excludePatterns == null || excludePatterns.Length == 0)
            return false;

        foreach (string pattern in excludePatterns)
        {
            // Simple wildcard matching
            if (pattern.Contains("*"))
            {
                string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                if (System.Text.RegularExpressions.Regex.IsMatch(name, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                // Exact match
                if (string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a zip file from a directory asynchronously.
    /// </summary>
    /// <param name="sourcePath">Path to the directory to zip</param>
    /// <param name="zipPath">Path where the zip file will be created</param>
    /// <returns>Task<bool> indicating success or failure</returns>
    public static async Task<bool> CreateZipFileAsync(string sourcePath, string zipPath)
    {
        return await Task.Run(() => CreateZipFile(sourcePath, zipPath));
    }

    /// <summary>
    /// Creates a DMG file from a macOS .app bundle (macOS only).
    /// </summary>
    /// <param name="appPath">Path to the .app bundle</param>
    /// <param name="dmgPath">Path where the DMG file will be created</param>
    /// <param name="volumeName">Volume name for the DMG (optional, defaults to app name)</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool CreateDMGFile(string appPath, string dmgPath, string volumeName = null)
    {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        try
        {
            if (!Directory.Exists(appPath))
            {
                UnityEngine.Debug.LogError($"App bundle does not exist: {appPath}");
                return false;
            }

            // Ensure the target directory exists
            string targetDir = Path.GetDirectoryName(dmgPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Delete existing DMG if it exists
            if (File.Exists(dmgPath))
            {
                File.Delete(dmgPath);
            }

            // Extract app name if volume name not provided
            if (string.IsNullOrEmpty(volumeName))
            {
                volumeName = Path.GetFileNameWithoutExtension(appPath);
            }

            // Try using create-dmg first (requires homebrew installation)
            if (TryCreateDMGWithCreateDMG(appPath, dmgPath, volumeName))
            {
                return true;
            }

            // Fallback to hdiutil (native macOS tool)
            return CreateDMGWithHdiutil(appPath, dmgPath, volumeName);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to create DMG file: {e.Message}");
            return false;
        }
#else
        UnityEngine.Debug.LogWarning("DMG creation is only supported on macOS");
        return false;
#endif
    }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    private static bool TryCreateDMGWithCreateDMG(string appPath, string dmgPath, string volumeName)
    {
        try
        {
            string appName = Path.GetFileName(appPath);
            string appDir = Path.GetDirectoryName(appPath);
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "create-dmg",
                Arguments = $"--volname \"{volumeName}\" " +
                           $"--window-pos 200 120 " +
                           $"--window-size 800 400 " +
                           $"--icon-size 100 " +
                           $"--icon \"{appName}\" 200 190 " +
                           $"--hide-extension \"{appName}\" " +
                           $"--app-drop-link 600 185 " +
                           $"--hdiutil-quiet " +
                           $"\"{dmgPath}\" " +
                           $"\"{appDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && File.Exists(dmgPath))
                {
                    UnityEngine.Debug.Log($"Created DMG with create-dmg: {dmgPath}");
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"create-dmg failed: {error}");
                    return false;
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"create-dmg not available: {e.Message}");
            return false;
        }
    }

    private static bool CreateDMGWithHdiutil(string appPath, string dmgPath, string volumeName)
    {
        try
        {
            string tempDmgPath = dmgPath + ".temp.dmg";
            string appDir = Path.GetDirectoryName(appPath);
            
            // Create a temporary DMG
            var createProcessInfo = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"create -volname \"{volumeName}\" -srcfolder \"{appDir}\" -ov -format UDZO \"{tempDmgPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = createProcessInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new System.Exception($"hdiutil create failed: {error}");
                }
            }

            // Convert to final DMG format
            var convertProcessInfo = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"convert \"{tempDmgPath}\" -format UDZO -o \"{dmgPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = convertProcessInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new System.Exception($"hdiutil convert failed: {error}");
                }
            }

            // Clean up temporary file
            if (File.Exists(tempDmgPath))
            {
                File.Delete(tempDmgPath);
            }

            if (File.Exists(dmgPath))
            {
                UnityEngine.Debug.Log($"Created DMG with hdiutil: {dmgPath}");
                return true;
            }
            else
            {
                throw new System.Exception("DMG file was not created");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to create DMG with hdiutil: {e.Message}");
            return false;
        }
    }
#endif

    /// <summary>
    /// Creates a DMG file from a macOS .app bundle asynchronously.
    /// </summary>
    /// <param name="appPath">Path to the .app bundle</param>
    /// <param name="dmgPath">Path where the DMG file will be created</param>
    /// <param name="volumeName">Volume name for the DMG (optional, defaults to app name)</param>
    /// <returns>Task<bool> indicating success or failure</returns>
    public static async Task<bool> CreateDMGFileAsync(string appPath, string dmgPath, string volumeName = null)
    {
        return await Task.Run(() => CreateDMGFile(appPath, dmgPath, volumeName));
    }

    private static string GetAppDataTempPath()
    {
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Application.productName,
            "Temp"
        );
        Directory.CreateDirectory(appDataPath);
        return appDataPath;
    }

    public static string GetTempFilePath(string fileName)
    {
        string tempPath = GetAppDataTempPath();
        return Path.Combine(tempPath, fileName);
    }

    public static string GetTempDirectoryPath(string directoryName)
    {
        string tempPath = GetAppDataTempPath();
        string dirPath = Path.Combine(tempPath, directoryName);
        if (Directory.Exists(dirPath))
        {
            try
            {
                Directory.Delete(dirPath, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to clean existing temp directory: {e.Message}");
            }
        }
        Directory.CreateDirectory(dirPath);
        return dirPath;
    }

    public static void CleanupTempFiles()
    {
        try
        {
            string tempPath = GetAppDataTempPath();
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to cleanup temp files: {e.Message}");
        }
    }
}