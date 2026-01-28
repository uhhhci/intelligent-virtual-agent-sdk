# Intelligent Virtual Human SDK (v2.0.0)


<img src="./Documentations/images/Teaser.png" alt="teaser"
    style="float: center; margin-right: 10px; " /> 

The package contains the Intelligent Virtual Human SDK developed by the [human computer interaction group](https://www.inf.uni-hamburg.de/en/inst/ab/hci.html) at Hamburg University. 

<span style="color:red"> ***Please note that the usage of the SDK requires ethical & responsible use. Details can be found [here](./LICENSE.md).***</span>


***For more detail on the ethical Issues of impersonation and AI fakes we refer to the following [paper](https://zenodo.org/records/15413114):*** 

Oliva, R., Wiesing, M., Gállego, J., Inami, M., Interrante, V., Lecuyer, A., McDonnell, R., Nouviale, F., Pan, X., Steinicke, F., & Slater, M. (2025). Where Extended Reality and AI May Take Us: Ethical Issues of Impersonation and AI Fakes in Social Virtual Reality (Version 1). Zenodo. 



##  Demo

<img src="./Documentations/images/interoperability.gif" alt="teaser"
    style="float: center; margin-right: 10px; " /> 

Our toolkit is compatible with CC4, Microsoft-rocketbox, and DIDIMO 3D virtual humans. Due to the license restriction, we only include an example character and animations from Rocketbox characters. 


## Table of content 
- [Requirements](#requirements)
- [Dependencies](#dependencies)
- [Main Features](#main-features)
- [Quick Start](#quick-start)
- [Documentation](#documentation)
- [DIDIMO Character License Notice](#didimo-character-license-notice)
- [Rocketbox Character License Notice](#rocketbox-characters-license-notice)
- [Mixamo Animations](#mixamo-animations)
- [CC4 Characters and Animations](#reallusion-animation)
- [License of this toolkit](#license)
- [Citation](#citation)
- [Acknowledgement](#acknowledgement)

### Requirements
* Unity 2022.3 LTS and above, Universal Render Pipeline (URP)
*  (``intelligent-virtual-agent-examples`` [unity example project](https://github.com/uhhhci/intelligent-virtual-agent-sdk-examples)) - not needed to run the SDK but provides a good starting point if you want to explore the SDK.

### Dependencies
#### Automatically Installed
* [com.unity.animation.rigging (1.2.1)](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/index.html) (Add package by name)
* [com.unity.nuget.newtonsoft-json (3.2.1)](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html) (Add package by name)

#### You Need To Import These Manually
* [com.meta.xr.sdk.interaction.ovr (69.0.1)](https://assetstore.unity.com/packages/tools/integration/meta-xr-interaction-sdk-265014) (Add by name: `com.meta.xr.sdk.interaction.ovr`)
* [com.oculus.unity.integration.lip-sync (29.0.0)](https://openupm.com/packages/com.oculus.unity.integration.lip-sync/) (Add from git URL: `https://github.com/Trisgram/com.oculus.unity.integration.lip-sync.git`)

### Main features

<img src=".//Documentations/images/v2.0_interaction_flow.png" alt="teaser"
    style="float: center; margin-right: 10px; " /> 
The image above shows the interaction loop of a conversational virtual agent. 

* Conversational intelligent virtual agents with human-AI interaction loop.
    - <b>multimodal prompting</b> : combining text-based user message with system prompt that contains a custom list of possible agent actions and facial expressions, with optional image prompt as support. 
    - <b>structure output from LLM/VLM</b>, containing selected action, facial expression, and text response.
    - <b>realistic IVA behavior</b> combining the multimodal output, including gaze, action, and facial expressions. 


If you want to have more modularized cloud services (e.g. using different STT, LLM, TTS models), checkout [documentation for v1.0.0](./READMEv1.0.0.md)


## Quick Start

The quickest way to test the toolkit is to directly open the ``intelligent-virtual-agent-examples`` [unity example project](https://github.com/uhhhci/intelligent-virtual-agent-sdk-examples) unity project we provided. If you want to install in an exsiting Unity project, following the below steps. 

### Import Package
1. #### Choose Project
    Select or create a Unity project to use the package in. (Unity 2022.3 LTS)

2. #### Install dependencies
    Install the dependencies via the Unity Package Manager.


2. #### Import IVH SDK Core Package 


    ##### **Option A**
    Add the package to the project the package by adding this by git URL to your Unity Package Manager:

    https://github.com/uhhhci/intelligent-virtual-agent-sdk.git

    For further information on how to add a Unity package from Git, please see the [Unity Documentation](https://docs.unity3d.com/Manual/upm-ui-giturl.html). Be aware that the imported package is immutable and won't be auto-upgraded. To upgrade to a new release, remove the package from the project and add it again.

    ##### **Option B**
    Clone this repository to a folder of your preference. In the Unity Package Manager select `Add package from disk...`, navigate to your installation folder and select the `package.json` file.


4. #### Setup a scene
    
    ##### Simple Agent- Quick Start
    - An example agent can be added by creating an empty Gameobject in the scene and adding the script: `Packages/de.uhh.hci.ivh.core/Runtime/Scripts/IntelligentVirtualAgent/GeminiLiveAgent.cs` to it. 
    
    - In its field "Agent Prefab" you can drag e.g.: `Packages/de.uhh.hci.ivh.core/Runtime/Models/Rocketbox/Business_Female_01/Export/Business_Female_01_facial.fbx`.

    - In its field "Animator Controller" you need to drag in `Packages/de.uhh.hci.ivh.core/Runtime/AnimationControllers/RocketboxFemale.controller`.

    - In the Emotion Handler Type, choose FACS. If you want more diverse and sophisicated facial expression animations, and if you have a license for CC4 digital soul facial expression animation database, see [CC4 Characters and Animations](#reallusion-animation).

    - In the CharacterType, choose ``Rocketbox`` if you are using rocketbox character. Choose ``CC4orDIDIMO`` otherwise. 


    - Any ``Additional Description`` will be added to the IVA's system prompt. 


    - After you added both the Animator Controller and the agent model, click on `Setup Agent` in the Editor.


    - Add the `Packages/de.uhh.hci.ivh.core/Runtime/Prefabs/PreviewScenePrefab.prefab` to the scene for better lighting and appearance of the scene. 
    

## Connect to Gemini Live Cloud Service

The current implementation supports 3 different live model. Two free-tier models from google AI studio and one paid model from Google's Vertex AI. 

| Model Variant | Source | Tier | Latency | Model ID / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Gemini Live 2.5 Flash** | Vertex AI | Paid | Low | [`gemini-live-2.5-flash-native-audio`](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/live-api) |
| **Gemini Live 2.5 Flash** | Google AI | Free | High | [`gemini-2.5-flash-native-audio-preview-12-2025`](https://ai.google.dev/gemini-api/docs/live?example=mic-stream) |
| **Gemini 2.0 Flash Exp** | Google AI | Free | Low | `gemini-2.0-flash-exp`<br>*(To be deprecated/terminated in March 2026)* |


To use the Google AI studio model:

You should enter your Google AI Studio API key in an ``auth.json`` file in the ``C:\Users\USER_NAME\.aiapi``
 directory for Unity to directly parse the API key: 

 ```json
{
    "gemini_api_key": "YOUR API KEY",
}
 ```

For using the Vertex AI model, you need to create a service account via Google Cloud, and put the ``service_account.json`` file in the ``C:\Users\USER_NAME\.aiapi`` directory to support VertexAI authentication 

## Documentation

- For the full Documentation, visit the [Wiki](https://github.com/uhhhci/intelligent-virtual-agent-sdk/wiki).
- [How to add more/custom animations to IVA actions](./Documentations/howToAddMoreAnimations.md)
- [How to develop the package while using it  in Unity](https://github.com/uhhhci/intelligent-virtual-agent-sdk/wiki/Development).

## [DIDIMO](https://www.didimo.co/) Character License Notice

 The DIDIMO asset is licensed solely for use within this repository and only to the extent necessary to build, test, and demonstrate this toolkit. You must not: resell the asset, redistribute the asset separately from this repository, or recreate, extract, or adapt the asset for use in any other project, product, or context.  See the detail [License](./LICENSE.md). 

## Rocketbox Characters License Notice

This repository includes 3D character assets sourced from the Rocketbox Avatar Library, originally developed by Rocketbox Studios and later made freely available by Microsoft for academic and non-commercial use.

The Rocketbox character models are provided under a **non-commercial license** and are intended **solely for academic, research, and educational purposes**. Use of these assets is subject to the original Rocketbox EULA provided by Microsoft, which can be found here:

https://github.com/microsoft/Microsoft-Rocketbox#license


## Mixamo Animations

Our package provides full support for mixamo animations. However, due to Adobe's redistribution limitation, we can not include the animation setup in the report. If you would like to use the different animations from Mixamo for your agents for your non-commerical research and academic work, please contact us. <b>We will be happy to provide you with a Unity-compatible animation package asset that has already been imported and configured, saving you the setup effort. </b>

## Reallusion Animation

The facial expressions of the Intelligent Virtual Agents (IVAs) you migh thave seen in many demo videos in this project are animated using Reallusion’s Digital Soul asset library, which is protected under a restricted usage license. 

As per Reallusion’s licensing terms, we are not permitted to redistribute this asset directly through the repository. However, if you hold a valid license and seats for Reallusion’s Digital Soul, please contact us. <b>We will be happy to provide you with a Unity-compatible animation asset that has already been imported and configured, saving you the setup effort. </b> 📧 For licensed access, contact the maintainence team. 

### Maintainer
Name: Ke Li, Sebastian Rings , Julia Hertel, Michael Arz<br>
Mail: ke.li@uni-hamburg.de, sebastian.rings@uni-hamburg.de, julia.hertel@uni-hamburg.de, michael.arz@uni-hamburg.de

### License
This toolkit is released for academic and research purposes only, free of charge. For commercial use, a seperate license must be obtained.  Please find detailed licensing information [here](./LICENSE.md)

### Citation
If this work helps your research, please cite the following papers:

```

@article{Li2025IHS,
  title={I Hear, See, Speak \& Do: Bringing Multimodal Information Processing to Intelligent Virtual Agents for Natural Human-AI Communication},
  author={Ke Li and Fariba Mostajeran and Sebastian Rings and Lucie Kruse and Susanne Schmidt and Michael Arz and Erik Wolf and Frank Steinicke},
  journal={2025 IEEE Conference on Virtual Reality and 3D User Interfaces Abstracts and Workshops (VRW)},
  year={2025},
  pages={1648-1649},
  url={https://api.semanticscholar.org/CorpusID:278063630}
}

@article{Mostajeran2025ATF,
  title={A Toolkit for Creating Intelligent Virtual Humans in Extended Reality},
  author={Fariba Mostajeran and Ke Li and Sebastian Rings and Lucie Kruse and Erik Wolf and Susanne Schmidt and Michael Arz and Joan Llobera and Pierre Nagorny and Caecilia Charbonnier and Hannes Fassold and Xenxo Alvarez and Andr{\'e} Tavares and Nuno Santos and Jo{\~a}o Orvalho and Sergi Fern{\'a}ndez and Frank Steinicke},
  journal={2025 IEEE Conference on Virtual Reality and 3D User Interfaces Abstracts and Workshops (VRW)},
  year={2025},
  pages={736-741},
  url={https://api.semanticscholar.org/CorpusID:278065150}
}

```


## Acknowledgement 

This work has received funding from the European Union’s Horizon Europe research and innovation program under grant agreement No 101135025, PRESENCE project. 
