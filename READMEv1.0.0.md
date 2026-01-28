# Intelligent Virtual Human SDK


<img src="./Documentations~/images/Teaser.png" alt="teaser"
    style="float: center; margin-right: 10px; " /> 

The package contains the Intelligent Virtual Human SDK developed by the [human computer interaction group](https://www.inf.uni-hamburg.de/en/inst/ab/hci.html) at Hamburg University. 

<span style="color:red"> ***Please note that the usage of the SDK requires ethical & responsible use. Details can be found [here](./LICENSE.md).***</span>


***For more detail on the ethical Issues of impersonation and AI fakes we refer to the following [paper](https://zenodo.org/records/15413114):*** 

Oliva, R., Wiesing, M., Gállego, J., Inami, M., Interrante, V., Lecuyer, A., McDonnell, R., Nouviale, F., Pan, X., Steinicke, F., & Slater, M. (2025). Where Extended Reality and AI May Take Us: Ethical Issues of Impersonation and AI Fakes in Social Virtual Reality (Version 1). Zenodo. 



##  Demo

<img src="./Documentations~/images/interoperability.gif" alt="teaser"
    style="float: center; margin-right: 10px; " /> 

Our toolkit is compatible with CC4, Microsoft-rocketbox, and DIDIMO 3D virtual humans. Due to the license restriction, we only include an example character and animations from Rocketbox characters. 

See [here](https://www.youtube.com/watch?v=RDT30Evpmic&feature=youtu.be) for full demo video of the possibilities with this toolkit.


## Table of content 
- [Requirements](#requirements)
- [Dependencies](#dependencies)
- [Main Features](#main-features)
- [Development Road Map](#development-road-maps)
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
* Unity 2022.3 LTS, Universal Render Pipeline (URP)
*  ``iva-server`` [node.js REST server for communicating with LLM/STT/STT cloud services](https://github.com/uhhhci/iva-server) 
*  (``intelligent-virtual-agent-examples`` [unity example project](https://github.com/uhhhci/intelligent-virtual-agent-sdk-examples)) - not needed to run the SDK but provides a good starting point if you want to explore the SDK.

### Dependencies
#### Automatically Installed
* [com.unity.animation.rigging (1.2.1)](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/index.html) (Add package by name)
* [com.unity.nuget.newtonsoft-json (3.2.1)](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html) (Add package by name)

#### You Need To Import These Manually
* [com.meta.xr.sdk.interaction.ovr (69.0.1)](https://assetstore.unity.com/packages/tools/integration/meta-xr-interaction-sdk-265014) (Add by name: `com.meta.xr.sdk.interaction.ovr`)
* [com.oculus.unity.integration.lip-sync (29.0.0)](https://openupm.com/packages/com.oculus.unity.integration.lip-sync/) (Add from git URL: `https://github.com/Trisgram/com.oculus.unity.integration.lip-sync.git`)
* [com.gpt4all.unity](https://github.com/Macoron/gpt4all.unity) (Add package from git URL: `https://github.com/Macoron/gpt4all.unity.git?path=/Packages/com.gpt4all.unity`)
* [com.whisper.unity](https://github.com/Macoron/whisper.unity) (Add from git URL: `https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity`)
* [com.meta.movement](https://github.com/oculus-samples/Unity-Movement.git) (Add from git URL: `https://github.com/oculus-samples/Unity-Movement.git#v69.0.0`) - you need to import `com.meta.xr.sdk.interaction.ovr` first

### Main features

<img src="./Documentations~/images/System_Setup.png" alt="teaser"
    style="float: center; margin-right: 10px; " /> 
The image above shows the interaction loop of a conversational virtual agent. 

* Conversational intelligent virtual agents with human-AI interaction loop.
    - <b>multimodal prompting</b> : combining text-based user message with system prompt that contains a custom list of possible agent actions and facial expressions, with optional image prompt as support. 
    - <b>structure output from LLM/VLM</b>, containing selected action, facial expression, and text response.
    - <b>realistic IVA behavior</b> combining the multimodal output, including gaze, action, and facial expressions. 

* <b>Modulized cloud services</b> of speech-to-text, large language models and text to speech models to choose from
  * LLM/VLM: OpenAI GPT-4o (via server), Google Gemini, OpenAI GPT-4o, local LLM(GPT4All, very slow)
  * STT: Google cloud (via server), Whisper (local, but very slow!)
  * TTS: Eleven Labs, Google Cloud (via server), Azure

### Development Road Maps

Features coming soon: 
- update and improve documentation
- Add ``Unity_STT_GoogleCloud`` and ``Unity_STT_Azure``
- proximity detection in VR
- gesture recognition integration
- integration of more diverse publically avalibleble animations and more diverse ready-made animation controllers
- spatial understanding
- memory management for long term interaction

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

    ##### **Option N**
    Clone this repository to a folder of your preference. In the Unity Package Manager select `Add package from disk...`, navigate to your installation folder and select the `package.json` file.


4. #### Setup a scene
    ##### Services
    In the Project view, add `Packages/de.uhh.hci.ivh.core/Runtime/Prefabs/BasicServiceTemplate.prefab` to the scene. 
    ##### Simple Agent- Quick Start
    - An example agent can be added by creating an empty Gameobject in the scene and adding the script: `Packages/de.uhh.hci.ivh.core/Runtime/Scripts/IntelligentVirtualAgent/ConversationalAgent.cs` to it. 
    
    - In its field "Agent Prefab" you can drag e.g.: `Packages/de.uhh.hci.ivh.core/Runtime/Models/Rocketbox/Business_Female_01/Export/Business_Female_01_facial.fbx`.

    - In its field "Animator Controller" you need to drag in `Packages/de.uhh.hci.ivh.core/Runtime/AnimationControllers/RocketboxFemale.controller`.

    - In the Emotion Handler Type, choose FACS. If you want more diverse and sophisicated facial expression animations, and if you have a license for CC4 digital soul facial expression animation database, see [CC4 Characters and Animations](#reallusion-animation).

    - In the CharacterType, choose ``Rocketbox`` if you are using rocketbox character. Choose ``CC4orDIDIMO`` otherwise. 

    - STT Trigger Settings, in ``Automatic`` mode, the IVA does not need to be triggered via developer defined trigger phrase. In ``Trigger Always``, the IVA will only respond to you if you started the conversation with a trigger phrase. This is developer's choice for ensuring trustworthy AI best practice. 

    - Any ``Additional Description`` will be added to the IVA's system prompt. 

    - Under the `Cloud Service Settings` in the Editor, choose the UHAM services for TTS, Foundation Model and STT Service.

    - After you added both the Animator Controller and the agent model, click on `Setup Agent` in the Editor.

    - In the ``BasicServerManager`` game object and in ``Google TTS``, choose the desired voice for your character. 


    - Add the `Packages/de.uhh.hci.ivh.core/Runtime/Prefabs/PreviewScenePrefab.prefab` to the scene for better lighting and appearance of the scene. 

    ##### Advance Agent- with vision capability, proximity, etc (coming soon..)
    

## Connect to Cloud Services

For the main functions of the package you will need different API-keys. See the documentation in the [iva-server](https://github.com/uhhhci/iva-server) of how to get the API keys. 

- To connect to the core services you can use the [IVA Server](https://github.com/uhhhci/iva-server). In the BasicServiceTemplate in your scene you now need to set the IP to "localhost" the UHAMServerManager.

- If you are students at Hamburg University, you can connect via the UHAM hosted server, please contact your supervisor/admin for the IP adress. In the BasicServiceTemplate in your scene you can add this IP to the BasicServerManager.

If you are testing with the following cloud service, you should enter your API key in an ``auth.json`` file in the ``C:\Users\USER_NAME\.aiapi``
 directory for Unity to directly parse the API key: 

 ```json
{
    "openai_api_key": "YOUR API KEY",
    "gemini_api_key": "YOUR API KEY",
    "azure_subscription_key": "YOUR API KEY",
    "azure_endpoint_id": "YOUR API KEY",
    "eleven_labs_api_key": "YOUR API KEY"
}
 ```


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
@article{Mostajeran2025ATF,
  title={A Toolkit for Creating Intelligent Virtual Humans in Extended Reality},
  author={Fariba Mostajeran and Ke Li and Sebastian Rings and Lucie Kruse and Erik Wolf and Susanne Schmidt and Michael Arz and Joan Llobera and Pierre Nagorny and Caecilia Charbonnier and Hannes Fassold and Xenxo Alvarez and Andr{\'e} Tavares and Nuno Santos and Jo{\~a}o Orvalho and Sergi Fern{\'a}ndez and Frank Steinicke},
  journal={2025 IEEE Conference on Virtual Reality and 3D User Interfaces Abstracts and Workshops (VRW)},
  year={2025},
  pages={736-741},
  url={https://api.semanticscholar.org/CorpusID:278065150}
}

@article{Li2025IHS,
  title={I Hear, See, Speak \& Do: Bringing Multimodal Information Processing to Intelligent Virtual Agents for Natural Human-AI Communication},
  author={Ke Li and Fariba Mostajeran and Sebastian Rings and Lucie Kruse and Susanne Schmidt and Michael Arz and Erik Wolf and Frank Steinicke},
  journal={2025 IEEE Conference on Virtual Reality and 3D User Interfaces Abstracts and Workshops (VRW)},
  year={2025},
  pages={1648-1649},
  url={https://api.semanticscholar.org/CorpusID:278063630}
}
```


## Acknowledgement 

This work has received funding from the European Union’s Horizon Europe research and innovation program under grant agreement No 101135025, PRESENCE project. 
