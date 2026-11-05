using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace SportsTour.EditorTools
{
    // Menu: Sports Tour > 1. Apply Meta Quest 3 Settings, then Sports Tour > 2. Build Tour Scene.
    public static class SportsTourBuilder
    {
        const string Root = "Assets/SportsTour";
        const string ScenePath = "Assets/Scenes/SportsTour.unity";

        [MenuItem("Sports Tour/1. Apply Meta Quest 3 Settings")]
        public static void ApplyQuestSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.companyName = "College";
            PlayerSettings.productName = "Sports VR Tour";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.college.sportsvrtour");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

            EnableOpenXR(BuildTargetGroup.Android);
            if (Application.platform == RuntimePlatform.WindowsEditor)
                EnableOpenXR(BuildTargetGroup.Standalone); // Quest Link play mode; macOS has no OpenXR runtime for Quest
            AssetDatabase.SaveAssets();
            Debug.Log("[Sports Tour] Meta Quest 3 settings applied (Android, IL2CPP ARM64, OpenXR + Meta Quest support).");
        }

        static void EnableOpenXR(BuildTargetGroup group)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
            {
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(general.Manager, typeof(OpenXRLoader).FullName, group);
            EditorUtility.SetDirty(perTarget);

            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group); // creates the feature list if it doesn't exist yet
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (openxr == null) { Debug.LogWarning($"[Sports Tour] OpenXR settings for {group} not found – enable features by hand."); return; }
            openxr.renderMode = OpenXRSettings.RenderMode.MultiPass; // 3D text shader isn't single-pass-instanced safe
            foreach (var f in openxr.GetFeatures())
            {
                var n = f.GetType().Name;
                if (n == "OculusTouchControllerProfile" || n == "MetaQuestTouchPlusControllerProfile" ||
                    (group == BuildTargetGroup.Android && n == "MetaQuestFeature"))
                    f.enabled = true;
            }
            EditorUtility.SetDirty(openxr);
        }

        [MenuItem("Sports Tour/2. Build Tour Scene")]
        public static void BuildScene()
        {
            EnsureFolder(Root + "/Materials");
            EnsureFolder("Assets/Scenes");

            var rt = LoadOrCreate(Root + "/Materials/Video360.renderTexture",
                () => new RenderTexture(3072, 1536, 0, RenderTextureFormat.ARGB32) { name = "Video360" });

            var sky = LoadOrCreate(Root + "/Materials/Video360Skybox.mat", () => new Material(Shader.Find("Skybox/Panoramic")));
            sky.SetTexture("_MainTex", rt);
            sky.SetFloat("_Mapping", 1); // latitude-longitude (equirectangular)
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.SetFloat("_ImageType", 0); // full 360
            sky.SetFloat("_Layout", 0); // mono
            EditorUtility.SetDirty(sky);

            var ui = LoadOrCreate(Root + "/Materials/TourUI.mat", () => new Material(Shader.Find("Sprites/Default")));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rigGo = new GameObject("XR Rig");
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(rigGo.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            var rig = rigGo.AddComponent<VRRig>();
            rig.head = cam;
            rig.laserMaterial = ui;

            var tourGo = new GameObject("Sports Tour");
            var vp = tourGo.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.None; // videos muted, background music plays instead
            vp.isLooping = true;
            vp.skipOnDrop = true;

            var audio = tourGo.AddComponent<AudioSource>();
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/TourMusic.wav");
            audio.playOnAwake = false;
            audio.loop = true;
            audio.volume = 0.35f;
            audio.spatialBlend = 0;

            var tour = tourGo.AddComponent<TourManager>();
            tour.rig = rig;
            tour.video = vp;
            tour.music = audio;
            tour.skybox = sky;
            tour.uiMaterial = ui;
            tour.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = tourGo;
            Debug.Log($"[Sports Tour] Scene built: {ScenePath}. Select 'Sports Tour' to edit sections and hotspot arrows.");
        }

        // Command-line entry: Unity -batchmode -executeMethod SportsTour.EditorTools.SportsTourBuilder.BatchSetup
        public static void BatchSetup()
        {
            ApplyQuestSettings();
            BuildScene();
        }

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = create();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
