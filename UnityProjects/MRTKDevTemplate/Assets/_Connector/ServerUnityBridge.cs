using System.Collections;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

public class ServerUnityBridge : MonoBehaviour
{
    public WebViewManager WebViewManager;
    public string Response;
    public string Feedback = "";
    public float WaitTime = 39;
    private string pythonServerURL = "http://192.168.68.100:8001/upload-image/";
    private string responseID;
    private string caption;
    private string url;
    
    
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
                responseID = jsonResponse["id"]; // Capture the ID returned by the server
                caption = jsonResponse["caption"];
                url = jsonResponse["url"];
                Debug.Log("Received ID: " + responseID);
                Debug.Log("Received Caption: " + caption);
                Debug.Log("Received URL: " + url);
                Response = caption;// see in editor
                yield return new WaitForSeconds(WaitTime);
                WebViewManager.UpdateLink("http://192.168.68.100:8001/render/"+responseID);
            }
        }
    }
    
}
