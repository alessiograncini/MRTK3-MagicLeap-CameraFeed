## MRTK3-MagicLeap-CameraFeed

This repo is based on 
https://github.com/magicleap/MixedRealityToolkit-Unity/tree/mrtk3_MagicLeap2
And 
https://devpost.com/software/mistral-oui

Aims to have a nice template for Vision models on Magic Leap 2 


##How to run it locally 

Go into MRTKDevTemplate>server>main.py 

Make sure you got your environment and module installed 
Make sure you got the credentials for Anthropic and Mistral in MRTKDevTemplate>server>index.ts

Run the main.py server 

Make sure you change the addresses in 
MRTKDevTemplate>Assets>_Connector>ServerUnityBridge

Make sure the camera intervals of 
Assets>_CameraFeed>Scripts>SimpleRGBCamCapture 
is high to avoid crashes 

#Build and run the APK on device 

You will see a capture view on the left 
You will see a web browser view on the right 

Every x seconds 
The captured image is described 
The captured image generates a new UI on the web browser 

 

