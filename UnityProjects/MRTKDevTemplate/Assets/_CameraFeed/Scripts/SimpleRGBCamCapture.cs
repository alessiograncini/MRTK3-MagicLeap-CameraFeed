
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;

// This script exports ML2 camera data: images, instrinsics and extrinsics.
public class SimpleRGBCamCapture : MonoBehaviour
{
    private bool isCameraConnected = false;
    private MLCamera colorCamera;
    private bool cameraDeviceAvailable;
    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();

    [Tooltip("The image used to display the camera texture.")]
    public RawImage RawImageDisplay;

    public MLCamera.Identifier CameraIdentifier = MLCamera.Identifier.Main;

    [SerializeField]
    private float maxSpeed = 0.15f;
    private Texture2D imageTexture;

    private ConcurrentQueue<CVFrameInfo> imageQueue = new ConcurrentQueue<CVFrameInfo>();

    private Thread imageSaveThread;
    private bool threadRunning = false;
    // To Avoid Blury Images
    private Vector3 lastPosition;
    // Cache the Unity Graphcis format to quickly save raw bytes into a file
    private GraphicsFormat graphicsFormat;

    struct CVFrameInfo
    {
        public byte[] capturedFrame;
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
            MLResult result = MLCamera.GetDeviceAvailabilityStatus(CameraIdentifier, out cameraDeviceAvailable);
            if (!(result.IsOk && cameraDeviceAvailable))
            {
                yield return new WaitForSeconds(1.0f);
            }
            yield return null;
        }

        Log("Camera device available.");
        yield return ConnectCamera();
        colorCamera.OnRawVideoFrameAvailable += OnRawImageAvailable;
    }

    private async Task ConnectCamera()
    {
        MLCamera.ConnectContext context = MLCamera.ConnectContext.Create();
        context.Flags = MLCameraBase.ConnectFlag.CamOnly;
        context.CamId = CameraIdentifier;
        Log("context " );

        try
        {
            colorCamera =  MLCamera.CreateAndConnect(context);
            Log("colorCamera device connected." + (colorCamera !=null));

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
            colorCamera.OnRawImageAvailable -= OnRawImageAvailable;
        }
    }



    private void OnRawImageAvailable(MLCamera.CameraOutput capturedImage, MLCamera.ResultExtras resultExtras, MLCamera.Metadata metadataHandle)
    {

        MLResult mlResult = MLCVCamera.GetFramePose(resultExtras.VCamTimestamp, out Matrix4x4 outTransform);
        if (mlResult.IsOk)
        {
            // The position of the camera when the iamge was taken + the forward and up vectors to account for rotation
            Vector3 position = outTransform.GetPosition() + outTransform.rotation*Vector3.forward + outTransform.rotation * Vector3.up;
            float speed = Vector3.Distance(position, lastPosition) / Time.deltaTime;

            if (speed < maxSpeed)
            {
                if (resultExtras.VCamTimestamp > 0)
                {

                    UpdateRGBTexture(ref imageTexture, capturedImage.Planes[0]);
                    graphicsFormat = imageTexture.graphicsFormat;

                    if ((imageTexture.width != 8 && imageTexture.height != 8))
                    {
                        RawImageDisplay.texture = imageTexture;
                        byte[] nativeArray = imageTexture.GetRawTextureData();
                        CVFrameInfo frameInfo = new CVFrameInfo()
                        {
                            capturedFrame = nativeArray,
                            resultExtras = resultExtras,
                            cameraTransform = outTransform
                        };
                        imageQueue.Enqueue(frameInfo);
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
        // Create a Texture2D from byte array

        byte[] imageBytes = ImageConversion.EncodeArrayToJPG(imageData.capturedFrame, graphicsFormat, 1920, 1080,quality:100);

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

        int height = (int)imagePlane.Height;
        byte[] sourceData = imagePlane.Data;
        int rowLength = actualWidth; // Assuming 'actualWidth' accounts for pixel format (e.g., width * 4 for RGBA)
        if (imagePlane.Stride != actualWidth)
        {
            // Adjust rowLength if necessary based on the image format and stride discrepancies
            rowLength = (int)imagePlane.Stride; // Use stride as row length if it differs from actualWidth
        }

        // Create a new array to hold the vertically flipped image data
        byte[] flippedImage = new byte[sourceData.Length];

        for (int y = 0; y < height; y++)
        {
            int originalRowStartIndex = y * rowLength;
            int flippedRowStartIndex = (height - 1 - y) * actualWidth; // Calculate index for flipped image

            // Copy the current row from the original image to the corresponding row in the flipped image
            Buffer.BlockCopy(sourceData, originalRowStartIndex, flippedImage, flippedRowStartIndex, actualWidth);
        }

        // Load the flipped image data into the texture
        videoTextureRGB.LoadRawTextureData(flippedImage);
        videoTextureRGB.Apply();
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