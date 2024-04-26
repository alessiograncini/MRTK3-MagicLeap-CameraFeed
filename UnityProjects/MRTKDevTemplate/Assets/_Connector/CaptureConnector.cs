using System.Collections;
using TMPro;
using UnityEngine;

public class CaptureConnector : MonoBehaviour
{
    public ServerUnityBridge ServerUnityBridge;
    public UnityEngine.UI.RawImage ImageFeedbackUI;
    public TextMeshProUGUI ResponseText;
    public TextMeshProUGUI FeedbackText;

    void Start()
    {
        StartCoroutine(UploadImageToServerRecursive());
    }

    public IEnumerator UploadImageToServerRecursive()
    {
        yield return new WaitForSeconds(4); // Delay to simulate capture timing or processing
       
        Texture2D texture = GetReadableTexture(ImageFeedbackUI.texture as Texture2D);
        if (texture != null)
        {
            ServerUnityBridge.UploadImageToServer(texture);
        }
        StartCoroutine(UploadImageToServerRecursive()); // Repeat the process
    }

    Texture2D GetReadableTexture(Texture2D source)
    {
        if (source.format == TextureFormat.RGBA32 || source.format == TextureFormat.RGB24)
        {
            return source; // No conversion needed
        }

        // Convert to a readable format
        Texture2D newTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
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

    void Update()
    {
        ResponseText.text = ServerUnityBridge.UpdateResponseConnector();
        FeedbackText.text = ServerUnityBridge.UpdateFeedback();
    }
}