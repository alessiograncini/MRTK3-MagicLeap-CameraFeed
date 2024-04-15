
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;

// This script exports ML2 camera data: images, instrinsics and extrinsics.
public class RGBCamCapture : MonoBehaviour
{
    private bool isCameraConnected = false;
    private MLCamera colorCamera;
    private bool cameraDeviceAvailable;
    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();

    [Header("Display")]
    [Tooltip("The canvas group that will flash each time an image is taken.")]
    public CanvasGroup Flash;
    [Tooltip("The image used to display the camera texture.")]
    public RawImage RawImageDisplay;
    [Tooltip("The text used to display how many images were captured")]
    public TextMeshProUGUI counterText;
    [Tooltip("The text used to display the status of the camera before the captures starts")]
    public TextMeshProUGUI InstructionText;
    [Tooltip("The text used to display where the frame was saved to")]
    public TextMeshProUGUI FileText;

    private int count;

    private Texture2D imageTexture;

    private static readonly Queue<CVFrameInfo> cvFrameQueue = new Queue<CVFrameInfo>();
    private ConcurrentQueue<CVFrameInfo> imageQueue = new ConcurrentQueue<CVFrameInfo>();

    private Thread imageSaveThread;
    private bool threadRunning = false;
    // To Avoid Blury Images
    private Vector3 lastPosition;
    // Cache the Unity Graphcis format to quickly save raw bytes into a file
    private GraphicsFormat graphicsFormat;


    struct CVFrameInfo
    {
        public MLCamera.CameraOutput capturedFrame;
        public MLCamera.ResultExtras resultExtras;
        public Matrix4x4 cameraTransform;
    }

    private void Awake()
    {
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;
    }

    void Start()
    {
        MLResult result = MLPermissions.RequestPermission(MLPermission.Camera, permissionCallbacks);
        if (!result.IsOk)
        {
            Debug.LogErrorFormat("Error: RGBCamCapture failed to get requested permissions, disabling script. Reason: {0}", result);
            enabled = false;
        }

        threadRunning = true;
        imageSaveThread = new Thread(new ThreadStart(ProcessImageQueue));
        imageSaveThread.Start();
    }


    void OnDisable()
    {
        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;

        if (colorCamera != null && isCameraConnected)
        {
            DisableMLCamera();
        }
    }

    private void OnPermissionDenied(string permission)
    {
        LogError($"{permission} denied, camera functionality most likley won't work.");
    }

    private void OnPermissionGranted(string permission)
    {
        StartCoroutine(EnableMLCamera());
    }

    private IEnumerator EnableMLCamera()
    {
        while (!cameraDeviceAvailable)
        {
            MLResult result = MLCamera.GetDeviceAvailabilityStatus(MLCamera.Identifier.Main, out cameraDeviceAvailable);
            if (!(result.IsOk && cameraDeviceAvailable))
            {
                yield return new WaitForSeconds(1.0f);
            }
            yield return null;
        }

        Log("Camera device available.");
        InstructionText.text = "Camera device available. Connecting...";
        yield return ConnectCamera();
        yield return new WaitForSeconds(1f);
        colorCamera.OnRawVideoFrameAvailable += OnCaptureRawImageComplete;

        InstructionText.gameObject.SetActive(false);

        while (true)
        {
            yield return DisplayAndWait();
            yield return null;
        }
    }

    private async Task ConnectCamera()
    {
        MLCamera.ConnectContext context = MLCamera.ConnectContext.Create();
        context.EnableVideoStabilization = false;
        context.Flags = MLCameraBase.ConnectFlag.CamOnly;
        context.CamId = MLCameraBase.Identifier.CV; 

        try
        {
            colorCamera = await MLCamera.CreateAndConnectAsync(context);
            if (colorCamera != null)
            {
                isCameraConnected = true;
                Log("Camera device connected.");
                // Optionally, configure and prepare capture here or elsewhere after connection
               await ConfigureAndPrepareCapture();
            }
            else
            {
                LogError("Failed to connect MLCamera: colorCamera is null.");
            }
        }
        catch (System.Exception e)
        {
            LogError($"Failed to connect MLCamera: {e.Message}");
        }
    }

    private async Task ConfigureAndPrepareCapture()
    {
        MLCamera.CaptureConfig captureConfig = new MLCamera.CaptureConfig()
        {
            StreamConfigs = new[]
            {
                new MLCameraBase.CaptureStreamConfig()
                {
                    OutputFormat = MLCamera.OutputFormat.RGBA_8888,
                    CaptureType = MLCamera.CaptureType.Video,
                    Width = 1920,
                    Height = 1080
                }
            },
            CaptureFrameRate = MLCamera.CaptureFrameRate._30FPS
        };

        MLResult result = colorCamera.PrepareCapture(captureConfig, out MLCamera.Metadata _);

        if (!result.IsOk)
        {
            LogError("Failed to prepare camera for capture.");
        }
        else
        {
            Log("Prepared camera for capture.");
            var aeawb = await colorCamera.PreCaptureAEAWBAsync();

            if (aeawb.IsOk)
            {
                Log("Completed AEWB capture.");
            }

            await colorCamera.CaptureVideoStartAsync();
            Log("Completed Video Start.");
        }
    }
    

    private void DisableMLCamera()
    {
        if (colorCamera != null)
        {
            colorCamera.Disconnect();
            isCameraConnected = false;
            colorCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
        }
    }

    private IEnumerator DisplayAndWait()
    {
        while (cvFrameQueue.Count == 0)
        {
            yield return null;
        }

        while (cvFrameQueue.Count>0)
        { 
            bool isEmpty = !cvFrameQueue.TryDequeue(out var cameraFrame);
           if (isEmpty)
           {
               yield break;
           }
           CVFrameInfo frameInfo = new CVFrameInfo()
           {
               capturedFrame = cameraFrame.capturedFrame,
               resultExtras = cameraFrame.resultExtras,
               cameraTransform = cameraFrame.cameraTransform
           };

            count++;
            Flash.alpha = 1;
            yield return null;

            UpdateRGBTexture(ref imageTexture, cameraFrame.capturedFrame.Planes[0]);

            graphicsFormat = imageTexture.graphicsFormat;
            imageQueue.Enqueue(frameInfo);


            if ((imageTexture.width != 8 && imageTexture.height != 8))
            {
                counterText.text = count.ToString("D4");
          
                yield return null;

                float startTime = Time.time;
                float duration = 1.25f; // 1 second duration

                while (Flash.alpha != 0)
                {
                    float elapsedTime = Time.time - startTime;
                    float progress = Mathf.Clamp01(elapsedTime / duration); // Ensures progress stays between 0 and 1
                    Flash.alpha = Mathf.Lerp(1f, 0f, progress);
                    yield return null; // Wait for the next frame
                }

                RawImageDisplay.texture = imageTexture;
                Flash.alpha = 0;
            }
            
            yield return null;
        }
        yield return null;


    }

    private void OnCaptureRawImageComplete(MLCamera.CameraOutput capturedImage, MLCamera.ResultExtras resultExtras, MLCamera.Metadata metadataHandle)
    {
        MLResult mlResult = MLCVCamera.GetFramePose(resultExtras.VCamTimestamp, out Matrix4x4 outTransform);
        if (mlResult.IsOk)
        {
            // The position of the camera when the iamge was taken + the forward and up vectors to account for rotation
            Vector3 position = outTransform.GetPosition() + outTransform.rotation*Vector3.forward + outTransform.rotation * Vector3.up;
            float speed = Vector3.Distance(position, lastPosition) / Time.deltaTime;
            if (speed < 0.15f)
            {
                if (cvFrameQueue.Count == 0)
                {
                    if (resultExtras.VCamTimestamp > 0)
                    {

                        CVFrameInfo frameInfo = new CVFrameInfo()
                        {
                            capturedFrame = capturedImage,
                            resultExtras = resultExtras,
                            cameraTransform = outTransform
                        };

                        if (Flash.alpha == 0)
                        {
                            Flash.alpha = 1;
                            cvFrameQueue.Enqueue(frameInfo);
                            Log("Image capture complete.");
                        }
                    }
                }
            }
            lastPosition = position;
        }
    }

    void OnDestroy()
    {
        // Signal the thread to stop and wait for it to finish
        threadRunning = false;
        if (imageSaveThread != null && imageSaveThread.IsAlive)
        {
            imageSaveThread.Join();
        }
    }


    private void ProcessImageQueue()
    {
        while (threadRunning || !imageQueue.IsEmpty)
        {
            if (imageQueue.TryDequeue(out CVFrameInfo cameraFrame))
            {
                SaveImage(cameraFrame);
            }
        }
    }

    private void SaveImage(CVFrameInfo imageData)
    {

        // Adjust the directory path to include 'capture'
        string directoryPath = Path.Combine(Application.persistentDataPath, "capture");

        // Check if the 'capture' directory exists, create it if needed
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Adjust file paths to use the updated directoryPath
        string fileName = $"{imageData.resultExtras.VCamTimestamp}.txt";
        string filePath = Path.Combine(directoryPath, fileName);

        string imagePath = Path.Combine(directoryPath, $"{imageData.resultExtras.VCamTimestamp}.jpg");
        FileText.text = $"FilePath: " + imagePath;
        // Create a Texture2D from byte array
        byte[] imageBytes = ImageConversion.EncodeArrayToJPG(FlipImageVertically(imageData.capturedFrame.Planes[0].Data, (int)imageData.capturedFrame.Planes[0].Width,
            (int)imageData.capturedFrame.Planes[0].Height, (int)imageData.capturedFrame.Planes[0].BytesPerPixel), graphicsFormat, 1920, 1080,quality:100);

        Log($"Image saved to {imagePath}");
        // Prepare the content for the file
        string fileContent = "intrinsics\n";
        fileContent += imageData.resultExtras.Intrinsics.HasValue ? imageData.resultExtras.Intrinsics.Value.ToString() : "Not Available";
        fileContent += "\nextrinsics\n";
        fileContent += imageData.cameraTransform.ToString();

        // Write to the file
        File.WriteAllText(filePath, fileContent);
        Log($"Data saved to {filePath}");

        File.WriteAllBytes(imagePath, imageBytes);
    }

    private void UpdateRGBTexture(ref Texture2D videoTextureRGB, MLCamera.PlaneInfo imagePlane)
    {
        int actualWidth = (int)(imagePlane.Width * imagePlane.PixelStride);

        if (videoTextureRGB != null &&
            (videoTextureRGB.width != imagePlane.Width || videoTextureRGB.height != imagePlane.Height))
        {
            Destroy(videoTextureRGB);
            videoTextureRGB = null;
        }

        if (videoTextureRGB == null)
        {
            videoTextureRGB = new Texture2D((int)imagePlane.Width, (int)imagePlane.Height, TextureFormat.RGBA32, false);
            videoTextureRGB.filterMode = FilterMode.Bilinear;
        }

        if (imagePlane.Stride != actualWidth)
        {
            var newTextureChannel = new byte[actualWidth * imagePlane.Height];
            for (int i = 0; i < imagePlane.Height; i++)
            {
                Buffer.BlockCopy(imagePlane.Data, (int)(i * imagePlane.Stride), newTextureChannel, i * actualWidth, actualWidth);
            }
            videoTextureRGB.LoadRawTextureData(newTextureChannel);
        }
        else
        {
            videoTextureRGB.LoadRawTextureData(imagePlane.Data);
        }
        videoTextureRGB.Apply();
    }

    public static byte[] FlipImageVertically(byte[] imageData, int width, int height, int bytesPerPixel)
    {
        int rowLength = width * bytesPerPixel;
        byte[] flippedImage = new byte[imageData.Length];

        for (int y = 0; y < height; y++)
        {
            int originalRowStartIndex = y * rowLength;
            // Calculate the starting index of the corresponding row in the flipped image
            int flippedRowStartIndex = (height - 1 - y) * rowLength;

            // Copy the current row from the original image to the corresponding row in the flipped image
            Buffer.BlockCopy(imageData, originalRowStartIndex, flippedImage, flippedRowStartIndex, rowLength);
        }

        return flippedImage;
    }

    private void Log(string message)
    {
        Debug.Log("[SIMPLE_CAMERA_CAPTURE] " + message);
    }

    private void LogError(string message)
    {
        Debug.LogError("[SIMPLE_CAMERA_CAPTURE] " + message);
    }

}