using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
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

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            request.uploadHandler = new UploadHandlerRaw(fileData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/zip");
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

    [Serializable]
    private class ReleaseData
    {
        public string tag_name;
        public string name;
        public string body;
        public bool prerelease;
    }
} 