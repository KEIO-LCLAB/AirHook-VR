# AirHook: Simulating Multi-directional Wind Around Ears to Enhance Presence in VR

This repository contains the Unity project for AirHook [1], an ear-mounted wind display that renders multi-directional airflow around the ears for VR.

[1] Yuxuan Sun, Tian Min, Liwei Chan, Takashi Amesaka, and Yuta Sugiura. 2026. AirHook: Simulating Multi-directional Wind Around Ears to Enhance Presence in VR. Proc. ACM Hum.-Comput. Interact. 10, 5, Article MHCI9716 (August 2026), 26 pages. https://doi.org/10.1145/3821687

## Authors

- Yuxuan Sun
- Tian Min
- Liwei Chan
- Takashi Amesaka
- Yuta Sugiura

## Folders

- `3DModels/`: STL and STEP files of the 3D models for the wearable ear-hook parts of AirHook.

## Unity Version

- 2022.3.35f1

## Setup

1. Open the project in Unity 2022.3.35f1.
2. Import [Unity Terrain - URP Demo Scene](https://assetstore.unity.com/packages/3d/environments/unity-terrain-urp-demo-scene-213197) from the Unity Asset Store before running the scenes.
3. Connect both boards and open `Arduino/PWM_control/PWM_control.ino` in Arduino IDE. Select each board's model and port, then upload with the header on line 14 set to `0x01` for the board that controls the left ear hook and `0x02` for the board that controls the right ear hook.
4. In `Assets/Scripts/AirHook/AirHookPorts.cs`, set `Left` and `Right` to the corresponding ports shown under **Tools > Port** in Arduino IDE.
5. Close Arduino Serial Monitor / Serial Plotter before entering Unity Play mode.

## License

[MIT License](https://choosealicense.com/licenses/mit/)
