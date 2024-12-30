using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace BattlePhaze.SettingsManager
{
    public class SettingsManagerRuntimeMenuIntegration : MonoBehaviour
    {
        [SerializeField]
        public List<OptionConfiguration> ToModifyOption = new List<OptionConfiguration>();

        [System.Serializable]
        public struct OptionConfiguration
        {
            public string Option;
            public UnityEngine.Object TextDescription;
            public UnityEngine.Object ObjectInput;
        }

        public void Awake()
        {
            SettingsManager Manager = SettingsManager.Instance;
            if (Manager != null)
            {
                for (int ToModifyOptionIndex = 0; ToModifyOptionIndex < ToModifyOption.Count; ToModifyOptionIndex++)
                {
                    int Output = SettingsManagerLoop(ToModifyOption[ToModifyOptionIndex].Option, Manager);
                    if (Output != int.MinValue)
                    {
                        Manager.Options[Output].ObjectInput = ToModifyOption[ToModifyOptionIndex].ObjectInput;
                        Manager.Options[Output].TextDescription = ToModifyOption[ToModifyOptionIndex].TextDescription;
                        SettingsManagerDescriptionSystem.TxtDescriptionSetText(Manager, Output);
                    }
                }
                Manager.Initalize(true);
                SettingsManagerDescriptionSystem.ExplanationSetup(Manager);
            }
        }

        public int SettingsManagerLoop(string LookUpName, SettingsManager Manager)
        {
            for (int SettingsOptionsIndex = 0; SettingsOptionsIndex < Manager.Options.Count; SettingsOptionsIndex++)
            {
                if (Manager.Options[SettingsOptionsIndex].Name == LookUpName)
                {
                    return SettingsOptionsIndex;
                }
            }
            return int.MinValue;
        }

        public void OnDestroy()
        {
            SettingsManager Manager = SettingsManager.Instance;
            if (Manager != null)
            {
                for (int ToModifyOptionIndex = 0; ToModifyOptionIndex < ToModifyOption.Count; ToModifyOptionIndex++)
                {
                    int Output = SettingsManagerLoop(ToModifyOption[ToModifyOptionIndex].Option, Manager);
                    if (Output != int.MinValue)
                    {
                        Manager.Options[Output].ObjectInput = null;
                        Manager.Options[Output].TextDescription = null;
                    }
                }
            }
        }

        public void LoadSettings()
        {
            SettingsManager Manager = SettingsManager.Instance;
            if (Manager == null)
            {
                Manager  = FindFirstObjectByType<SettingsManager>();
            }
            if (Manager != null)
            {
                Debug.Log("Found Manager!");
                ToModifyOption.Clear(); // Clear current list to load fresh data from Manager
                for (int SettingsOptionsIndex = 0; SettingsOptionsIndex < Manager.Options.Count; SettingsOptionsIndex++)
                {
                    var option = Manager.Options[SettingsOptionsIndex];
                    Debug.Log("Loading Options " + option.Name);
                    OptionConfiguration config = new OptionConfiguration
                    {
                        Option = option.Name,
                        TextDescription = option.TextDescription,
                        ObjectInput = option.ObjectInput
                    };
                    ToModifyOption.Add(config);
                }
            }
        }
    }
}
#if UNITY_EDITOR

namespace BattlePhaze.SettingsManager
{
    [CustomEditor(typeof(SettingsManagerRuntimeMenuIntegration))]
    public class SettingsManagerRuntimeMenuIntegrationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SettingsManagerRuntimeMenuIntegration myScript = (SettingsManagerRuntimeMenuIntegration)target;
            if (GUILayout.Button("Load Settings"))
            {
                Debug.Log("Load Settings");
                myScript.LoadSettings();
            }
        }
    }
}
#endif