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
        StartCoroutine(UploadImageTopServerRecursive());
    }

    public IEnumerator UploadImageTopServerRecursive()
    {
        Texture2D texture = ImageFeedbackUI.texture as Texture2D;
        yield return new WaitForSeconds(4);
        if (texture = null)
        {
            ServerUnityBridge.UploadImageToServer(texture);
        }
        StartCoroutine(UploadImageTopServerRecursive());
    }

    void Update()
    {
        ResponseText.text = ServerUnityBridge.UpdateResponseConnector();
        FeedbackText.text = ServerUnityBridge.UpdateFeedback();
    }
}
