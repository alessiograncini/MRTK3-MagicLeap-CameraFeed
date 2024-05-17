## Latest Update (05.17.2024)

I took a pass and simplified the set up for this project 
If you don't want to go through the longer setup explained below

Run the server located in 

- `MRTK3-MagicLeap-CameraFeed/UnityProjects/MRTKDevTemplate>SimplifiedServer.py`

Change the IP address in both (use the one of the machine running the server)

- `SimplifiedServerUnityBridge.cs`
- `SimplifiedServer.py`

Build app, run the server before to launch it from the device. 

Notes:

- A custom script provided by Magic Leap Developers allows to capture the demo meanwhile executing capture from the device. This script is named `SimpleRGBCameraCapture` and its used is mainly for documentation purposes - this scripts tends to eventually crash the app after a while, needs more work. If not just go with the default camera capture, this will avoid crashes.

- We make use of the web browser by Vuplex 
Credits: https://developer.vuplex.com/webview/overview

- This latest version take advantage of ChatGPT-4o, ChatGPT-4, not using the models listed below

- Do you want to customize your UI generation? Look into `SimplifiedServer.py` in the latest section `def generate_additional_ui(description, category):`



# MRTK3-MagicLeap-CameraFeed

This repository enhances the integration of vision models on Magic Leap 2 devices, utilizing the capabilities of the Mixed Reality Toolkit (MRTK3) based on the following sources:
- [MRTK3 for Magic Leap 2 on GitHub](https://github.com/magicleap/MixedRealityToolkit-Unity/tree/mrtk3_MagicLeap2)
- [Mistral Project on Devpost](https://devpost.com/software/mistral-oui)

The aim is to provide a robust template for developers to implement advanced vision models in Magic Leap 2 applications.

## Features
- Integration with the latest MRTK3 updates.
- Support for dynamic UI generation based on vision model outputs.
- Efficient handling of high-frequency camera feeds to prevent application crashes.

## Local Setup

### Prerequisites
Ensure you have the following prerequisites installed:
- Required environment and modules for your project.
- Credentials for Anthropic and Mistral services.

### Running the Project Locally

1. **Prepare Your Environment:**
   - Navigate to `MRTKDevTemplate > server`.
   - Ensure `main.py` and `index.ts` are properly set up with the necessary credentials and configurations.

2. **Start the Server:**
   - Run `main.py` to start the server.

3. **Configure Connections:**
   - Update the address configurations in `MRTKDevTemplate > Assets > _Connector > ServerUnityBridge`.

4. **Adjust Camera Settings:**
   - Modify the camera intervals in `Assets > _CameraFeed > Scripts > SimpleRGBCamCapture` to a high value to ensure stability.

## Building and Running on Device

- **Build the APK:** Follow your usual build process to create the APK for Magic Leap 2.
- **Run on Device:**
  - Upon running, you will observe:
    - A capture view on the left side of the screen.
    - A web browser view on the right side of the screen.
  - Every few seconds, the application:
    - Describes the captured image.
    - Updates the UI on the web browser based on the image analysis.

## Contribute

Contributions are welcome! Please fork the repository and submit pull requests, or create issues for bugs and feature requests.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
