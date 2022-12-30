using System;
using System.IO;
using UnityEngine;

namespace Pancake.Feedback
{
    public class FeedbackSettings : ScriptableObject
    {
        public string token;
        public bool includeScreenshot = true;
        public FeedbackBoard board;
        
        private static FeedbackSettings instance;

        public static FeedbackSettings Instance
        {
            get
            {
                if (instance != null) return instance;

                instance = LoadSettings();
                if (instance == null)
                {
#if UNITY_EDITOR
                    CreateSettingAssets();
                    instance = LoadSettings();
#else
                    instance = UnityEngine.ScriptableObject.CreateInstance<T>();
                    Debug.LogWarning($"{nameof(T)} not found! Please create instance to setup and build again!");
#endif
                }

                return instance;
            }
        }

        public static FeedbackSettings LoadSettings()
        {
            return Resources.Load<FeedbackSettings>(nameof(FeedbackSettings));
        }

#if UNITY_EDITOR
        // ReSharper disable once StaticMemberInGenericType
        // flagCreateSetting do not same value of each type
        private static bool flagCreateSetting;

        private static void CreateSettingAssets()
        {
            // if (flagCreateSetting) return;
            // flagCreateSetting = true;

            var setting = UnityEngine.ScriptableObject.CreateInstance<FeedbackSettings>();
            UnityEditor.AssetDatabase.CreateAsset(setting, $"{DefaultResourcesPath()}/{nameof(FeedbackSettings)}.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log($"{nameof(FeedbackSettings)} was created ad {DefaultResourcesPath()}/{nameof(FeedbackSettings)}.asset");
        }

        private static string DefaultResourcesPath()
        {
            const string defaultResourcePath = "Assets/_Root/Resources";
            if (!Directory.Exists(defaultResourcePath)) Directory.CreateDirectory(defaultResourcePath);
            return defaultResourcePath;
        }
#endif
        
        
    }

    [Serializable]
    public class FeedbackBoard
    {
        public string id;

        public string[] listNames;
        public string[] listIds;

        public string[] categoryNames = new string[] {"Feedback", "Bug"};
        public string[] categoryIds = new string[] {null, null};

        public Label[] labels = new Label[] {new Label("1", null, "Low Priority"), new Label("2", null, "Medium Priority"), new Label("3", null, "High Priority")};
    }
}