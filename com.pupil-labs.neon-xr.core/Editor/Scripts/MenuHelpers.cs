#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace PupilLabs
{
    public static class MenuHelpers
    {
        [MenuItem("Pupil Labs/Addressables/Import Groups", priority = 2063)]
        public static void ImportGroups()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Error", "Attempting to open Import Groups window, but no Addressables Settings file exists.  \n\nOpen 'Window/Asset Management/Addressables/Groups' for more info.", "Ok");
                return;
            }
            bool groupImported = ImportGroupInternal(settings, "Packages/com.pupil-labs.neon-xr.core/Runtime/Addressables/NeonXR Group.asset");
            if (groupImported)
            {
                ImportSchemasInternal(settings, "NeonXR Group", "Packages/com.pupil-labs.neon-xr.core/Runtime/Addressables/Schemas/");
            }
        }

        private static bool ImportGroupInternal(AddressableAssetSettings settings, string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath) || Path.GetExtension(groupPath).ToLower() != ".asset" || !File.Exists(groupPath))
            {
                Debug.LogError($"Group at '{groupPath}' not a valid group asset. Group will not be imported.");
                return false;
            }

            AddressableAssetGroup oldGroup = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(groupPath);
            if (oldGroup == null)
            {
                Debug.LogError($"Cannot load group asset at '{groupPath}'. Group will not be imported.");
                return false;
            }

            if (settings.FindGroup(oldGroup.Name) != null)
            {
                Debug.LogError($"Settings already contains group '{oldGroup.Name}'. Group will not be imported.");
                return false;
            }

            string groupFileName = Path.GetFileName(groupPath);
            string newGroupPath = $"{settings.GroupFolder}/{groupFileName}";
            newGroupPath = newGroupPath.Replace("\\", "/");
            if (File.Exists(newGroupPath))
            {
                Debug.LogError($"File already exists at '{newGroupPath}'. Group will not be imported.");
                return false;
            }

            if (!AssetDatabase.CopyAsset(groupPath, newGroupPath))
            {
                Debug.LogError("Failed to copy group asset. Importing group failed.");
                return false;
            }

            return true;
        }

        private static void ImportSchemasInternal(AddressableAssetSettings settings, string groupName, string schemaFolder)
        {
            if (string.IsNullOrEmpty(schemaFolder) || !Directory.Exists(schemaFolder))
            {
                Debug.LogError($"Schema folder path is not a valid folder '{schemaFolder}'. Schemas will not be imported.");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                Debug.LogError($"Settings does not contain group '{groupName}'. Schemas will not be imported.");
                return;
            }

            string[] schemaPaths = Directory.GetFiles(schemaFolder);
            group.ClearSchemas(false);
            foreach (string unparsedPath in schemaPaths)
            {
                if (Path.GetExtension(unparsedPath).ToLower() != ".asset")
                    continue;

                string path = unparsedPath.Replace("\\", "/");
                AddressableAssetGroupSchema schema = AssetDatabase.LoadAssetAtPath<AddressableAssetGroupSchema>(path);
                if (schema == null)
                {
                    Debug.LogError($"Cannot load schema asset at '{path}'. Schema will not be imported.");
                    continue;
                }

                group.AddSchema(schema);
            }
        }
    }
}
#endif
