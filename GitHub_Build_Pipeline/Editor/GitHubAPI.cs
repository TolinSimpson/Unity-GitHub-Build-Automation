using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;

public class GitHubAPI
{
    private const string GITHUB_API_BASE = "https://api.github.com";
    private readonly string token;
    private readonly string owner;
    private readonly string repo;

    public GitHubAPI(string token, string owner, string repo)
    {
        this.token = token;
        this.owner = owner;
        this.repo = repo;
    }

    public async Task<string> CreateRelease(string tagName, string title, string body, bool prerelease)
    {
        string url = $"{GITHUB_API_BASE}/repos/{owner}/{repo}/releases";
        string json = JsonUtility.ToJson(new ReleaseData
        {
            tag_name = tagName,
            name = title,
            body = body,
            prerelease = prerelease
        });

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"token {token}");
            request.SetRequestHeader("User-Agent", "Unity-Editor");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Failed to create release: {request.error}");
            }

            return request.downloadHandler.text;
        }
    }

    public async Task UploadReleaseAsset(string uploadUrl, string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string url = uploadUrl.Replace("{?name,label}", $"?name={fileName}");

        // Determine content type based on file extension
        string contentType = GetContentType(filePath);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            request.uploadHandler = new UploadHandlerRaw(fileData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("Authorization", $"token {token}");
            request.SetRequestHeader("User-Agent", "Unity-Editor");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"Failed to upload asset {fileName}: {request.error}");
            }
        }
    }

    private string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".zip" => "application/zip",
            ".dmg" => "application/x-apple-diskimage",
            ".exe" => "application/vnd-microsoft.portable-executable",
            ".iss" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Triggers a GitHub Actions workflow via API
    /// </summary>
    /// <param name="workflowFileName">Name of the workflow file (e.g., "create-dmg.yml")</param>
    /// <param name="gitRef">Git reference (branch/tag)</param>
    /// <param name="downloadUrl">Download URL for the macOS ZIP file</param>
    /// <param name="appName">Application name</param>
    /// <param name="version">Version string</param>
    /// <param name="releaseId">GitHub release ID</param>
    /// <param name="signingParams">Optional code signing parameters</param>
    /// <returns>True if workflow was triggered successfully</returns>
    public async Task<bool> TriggerWorkflow(string workflowFileName, string gitRef, string downloadUrl, string appName, string version, string releaseId, WorkflowSigningParams signingParams = null)
    {
        string url = $"{GITHUB_API_BASE}/repos/{owner}/{repo}/actions/workflows/{workflowFileName}/dispatches";
        
        // Build inputs dictionary (now limited to 5 parameters to stay well under GitHub's 10-input limit)
        var inputsDict = new System.Collections.Generic.Dictionary<string, string>
        {
            {"download_url", downloadUrl},
            {"app_name", appName},
            {"version", version},
            {"release_id", releaseId}
        };

        // Add signing parameters as a single JSON string if provided
        if (signingParams?.useProperSigning == true)
        {
            var signingJson = new StringBuilder();
            signingJson.Append("{");
            
            // Always include use_proper_signing
            signingJson.Append("\"use_proper_signing\":true");
            
            // Add other parameters if they have values
            if (!string.IsNullOrEmpty(signingParams.signingIdentity))
            {
                signingJson.Append(",\"signing_identity\":\"");
                signingJson.Append(EscapeJsonString(signingParams.signingIdentity));
                signingJson.Append("\"");
            }
            
            if (!string.IsNullOrEmpty(signingParams.bundleIdentifier))
            {
                signingJson.Append(",\"bundle_identifier\":\"");
                signingJson.Append(EscapeJsonString(signingParams.bundleIdentifier));
                signingJson.Append("\"");
            }
            
            if (signingParams.enableNotarization == true)
            {
                signingJson.Append(",\"enable_notarization\":true");
                
                if (!string.IsNullOrEmpty(signingParams.teamId))
                {
                    signingJson.Append(",\"team_id\":\"");
                    signingJson.Append(EscapeJsonString(signingParams.teamId));
                    signingJson.Append("\"");
                }
                
                if (!string.IsNullOrEmpty(signingParams.appleId))
                {
                    signingJson.Append(",\"apple_id\":\"");
                    signingJson.Append(EscapeJsonString(signingParams.appleId));
                    signingJson.Append("\"");
                }
            }
            
            if (!string.IsNullOrEmpty(signingParams.entitlementsContent))
            {
                signingJson.Append(",\"entitlements_content\":\"");
                signingJson.Append(EscapeJsonString(signingParams.entitlementsContent));
                signingJson.Append("\"");
            }
            
            if (signingParams.useGitHubSecrets == true)
            {
                signingJson.Append(",\"use_github_secrets\":true");
                
                if (!string.IsNullOrEmpty(signingParams.p12SecretName))
                {
                    signingJson.Append(",\"p12_secret_name\":\"");
                    signingJson.Append(EscapeJsonString(signingParams.p12SecretName));
                    signingJson.Append("\"");
                }
                
                if (signingParams.hasP12Password.HasValue)
                {
                    signingJson.Append($",\"has_p12_password\":{(signingParams.hasP12Password.Value ? "true" : "false")}");
                }
            }
            
            signingJson.Append("}");
            
            inputsDict["signing_params"] = signingJson.ToString();
            UnityEngine.Debug.Log($"Created signing parameters JSON ({signingJson.Length} chars): {signingJson}");
        }
        else
        {
            UnityEngine.Debug.Log("No signing parameters provided - DMG will use ad-hoc signing");
        }
        
        // Build JSON manually to ensure proper formatting
        var inputsJson = new StringBuilder();
        inputsJson.Append("{");
        bool first = true;
        foreach (var kvp in inputsDict)
        {
            if (!first) inputsJson.Append(",");
            inputsJson.Append($"\"{kvp.Key}\":\"{EscapeJsonString(kvp.Value)}\"");
            first = false;
        }
        inputsJson.Append("}");
        
        string json = $"{{\"ref\":\"{gitRef}\",\"inputs\":{inputsJson}}}";
        UnityEngine.Debug.Log($"Triggering workflow with {inputsDict.Count} parameters: {json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"token {token}");
            request.SetRequestHeader("User-Agent", "Unity-Editor");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.Log($"Successfully triggered workflow: {workflowFileName}");
                return true;
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to trigger workflow: {request.error} - {request.downloadHandler.text}");
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a release by tag name
    /// </summary>
    /// <param name="tagName">The release tag</param>
    /// <returns>Release data or null if not found</returns>
    public async Task<GitHubRelease> GetReleaseByTag(string tagName)
    {
        string url = $"{GITHUB_API_BASE}/repos/{owner}/{repo}/releases/tags/{tagName}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"token {token}");
            request.SetRequestHeader("User-Agent", "Unity-Editor");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                return JsonUtility.FromJson<GitHubRelease>(response);
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to get release: {request.error}");
                return null;
            }
        }
    }

    [Serializable]
    private class ReleaseData
    {
        public string tag_name;
        public string name;
        public string body;
        public bool prerelease;
    }




    [Serializable]
    public class GitHubRelease
    {
        public long id;
        public string tag_name;
        public string name;
        public string body;
        public bool prerelease;
        public string upload_url;
        public string html_url;
    }

    /// <summary>
    /// Parameters for code signing during DMG creation
    /// </summary>
    public class WorkflowSigningParams
    {
        public bool? useProperSigning;
        public string signingIdentity;
        public string bundleIdentifier;
        public bool? enableNotarization;
        public string teamId;
        public string appleId;
        public string entitlementsContent; // Base64 encoded entitlements file content
        public bool? useGitHubSecrets; // Whether to use GitHub secrets for P12 certificate
        public string p12SecretName; // Name of the GitHub secret containing the P12 certificate
        public bool? hasP12Password; // Whether the P12 certificate has a password
    }
} 