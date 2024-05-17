using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class SimplifiedServerUnityBridge : MonoBehaviour
{
    //public RawImage cameraImage;
    public TextMeshProUGUI descriptionText;
    public WebViewManager webViewManager;
    public RawImage imageRaw;

    private void Start()
    {
        StartCoroutine(SendImagePeriodically());
    }

    private IEnumerator SendImagePeriodically()
    {
        while (true)
        {
            yield return SendImageToServer();
            yield return new WaitForSeconds(20); // Wait for 20 seconds before sending the next image
        }
    }

    Texture2D GetReadableTexture(Texture2D source)
    {
        if (source.format == TextureFormat.RGBA32 || source.format == TextureFormat.RGB24)
        {
            return source; // No conversion needed
        }

        // Convert to a readable format
        Texture2D newTexture = new Texture2D(
            source.width,
            source.height,
            TextureFormat.RGBA32,
            false
        );
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 32);
        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;
        newTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        newTexture.Apply();
        RenderTexture.active = currentRT; // Reset the active render texture
        RenderTexture.ReleaseTemporary(renderTexture);
        return newTexture;
    }

    private IEnumerator SendImageToServer()
    {
        string serverUrl = "http://192.168.68.105:3000/process_image";

        // Ensure ImageRaw.texture is not null
        if (imageRaw.texture == null)
        {
            Debug.LogError("ImageRaw texture is null");
            yield break;
        }

        // Get readable texture and encode to PNG
        Texture2D texture = GetReadableTexture(imageRaw.texture as Texture2D);
        byte[] imageData = texture.EncodeToPNG();

        // Create form data with image binary
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageData, "image.png", "image/png");

        // Send request to the server
        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {www.error}");
            }
            else
            {
                // Parse and handle the server response
                var response = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);
                descriptionText.text = response.description;
                webViewManager.UpdateLink(response.web_ui_url);
            }
        }
    }
}

[System.Serializable]
public class ServerResponse
{
    public string description;
    public string web_ui_url;
}
