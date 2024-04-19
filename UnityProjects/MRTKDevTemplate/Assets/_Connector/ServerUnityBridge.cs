using System.Collections;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(CaptureConnector))]
public class ServerUnityBridge : MonoBehaviour
{
    public WebViewManager WebViewManager;
    public string Response;
    public string Feedback = "";
    public float WaitTime = 40;
    private string pythonServerURL = "";
    private string webRequestRefreshURL = "";
    private string responseID = "";
    private CaptureConnector captureConnector;
    private string caption;

    private void OnEnable()
    {
        captureConnector = GetComponent<CaptureConnector>();
    }

    public void GetResponseConnector()
    {
        if (string.IsNullOrEmpty(responseID))
        {
            Debug.LogError("Response ID is not set. Make sure you've uploaded the image first.");
            return;
        }
        StartCoroutine(GetResponseCoroutine(responseID));
    }

    public string UpdateResponseConnector()
    {
        return Response;
    }

    public string UpdateFeedback()
    {
        return Feedback;
    }

    public void UploadImageToServer(Texture2D image)
    {
        StartCoroutine(UploadCoroutine(image));
    }

    public IEnumerator UploadCoroutine(Texture2D image)
    {
        WWWForm form = new WWWForm();
        byte[] imageData = image.EncodeToPNG();
        form.AddBinaryData("file", imageData, "image.png");

        using (UnityWebRequest www = UnityWebRequest.Post(pythonServerURL, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + www.error);
            }
            else
            {
                Debug.Log("Image uploaded successfully!");
                var jsonResponse = JSON.Parse(www.downloadHandler.text);
                responseID = jsonResponse["url"]; // Capture the ID returned by the server
                Debug.Log("Received ID: " + responseID);
                caption = jsonResponse["caption"];
                // see in editor
                Response = caption;
                yield return new WaitForSeconds(40);
                WebViewManager.UpdateLink(webRequestRefreshURL);
            }
        }
    }

    private IEnumerator GetResponseCoroutine(string id)
    {
        string requestURL = $"{id}";

        while (true)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(requestURL))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error getting response: {www.error}");
                    break;
                }
                else
                {
                    yield return new WaitForSeconds(WaitTime);
                    WebViewManager.UpdateLink(requestURL);

                    Debug.Log("Feedback: " + Feedback);
                }
            }
        }
    }
}
