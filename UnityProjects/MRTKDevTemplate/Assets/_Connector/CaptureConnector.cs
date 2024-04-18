using System.Collections;
using TMPro;
using UnityEngine;

public class CaptureConnector : MonoBehaviour
{
    public ServerUnityBridge ServerUnityBridge;
    public UnityEngine.UI.Image ImageFeedbackUI;
    public TextMeshProUGUI ResponseText;
    public TextMeshProUGUI FeedbackText;

    void Start()
    {
        StartCoroutine(MistralRecursive());
    }

    private bool recordStarted;

    public IEnumerator MistralRecursive()
    {
        yield return new WaitForSeconds(4);
        if (ImageFeedbackUI.sprite!=null){
              ServerUnityBridge.UploadRecursive( ImageFeedbackUI.sprite.texture );
        }
        StartCoroutine(MistralRecursive());
    }

    void Update()
    {
        ResponseText.text = ServerUnityBridge.UpdateResponseConnector();
        FeedbackText.text = ServerUnityBridge.UpdateFeedback();
    }
}
