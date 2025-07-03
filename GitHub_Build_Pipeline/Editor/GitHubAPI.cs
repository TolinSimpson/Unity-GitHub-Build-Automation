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
            ".exe" => "application/vnd.microsoft.portable-executable",
            ".iss" => "text/plain",
            _ => "application/octet-stream"
        };
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
    /// <returns>True if workflow was triggered successfully</returns>
    public async Task<bool> TriggerWorkflow(string workflowFileName, string gitRef, string downloadUrl, string appName, string version, string releaseId)
    {
        string url = $"{GITHUB_API_BASE}/repos/{owner}/{repo}/actions/workflows/{workflowFileName}/dispatches";
        
        var requestData = new WorkflowDispatchData
        {
            @ref = gitRef,
            inputs = new WorkflowInputs
            {
                download_url = downloadUrl,
                app_name = appName,
                version = version,
                release_id = releaseId
            }
        };
        
        string json = JsonUtility.ToJson(requestData);
        UnityEngine.Debug.Log($"Triggering workflow with JSON: {json}");

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
    private class WorkflowDispatchData
    {
        [SerializeField] public string @ref;
        [SerializeField] public WorkflowInputs inputs;
    }

    [Serializable]
    private class WorkflowInputs
    {
        public string download_url;
        public string app_name;
        public string version;
        public string release_id;
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
} 