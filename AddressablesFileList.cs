using System;
using System.Collections;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

using UnityEngine;
using System.Linq;
using System.IO;
using BestHTTP.JSON.LitJson;
using static UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema;

public class FrameAnimSettingTool
{

    [MenuItem("Tools/SetAddressableGroups")]
    public static void Set()
    {
        List<GroupConfig> groupConfigs = new List<GroupConfig>
        {
             GroupConfig.FolderGroup("Resources","Assets/_Res/_Resources","resources",BundlePackingMode.PackSeparately,true),
             GroupConfig.FolderGroup("SpriteAnimations","Assets/_Res/Anim2D/SpriteAnimations",null,BundlePackingMode.PackTogether),
             GroupConfig.FilesGroup("Scenes","Assets/_Res/Scenes","*.unity","Scene",BundlePackingMode.PackSeparately),
        };
        Debug.Log("SetAddressableGroups");
        groupConfigs.RemoveAll(x => x == null);
        if (groupConfigs.Count == 0)
        {
            Debug.Log("groupConfigs is null");
            return;
        };
        //获取配置
        AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        foreach (var str in aaSettings.GetLabels())
        {
            aaSettings.RemoveLabel(str);
        }

        int sum = groupConfigs.Sum(x => x.entrieDatas.Count);
        int index = 0;

        Action<EntryData> onSettingEntry = (data) =>
        {
            EditorUtility.DisplayProgressBar("SettingAddressables ...", data.address, index / (float)sum);
            index++;
        };
        foreach (var config in groupConfigs)
        {
            SetGroup(aaSettings, config, onSettingEntry);
        }

        //清理错误group
        if (aaSettings != null && aaSettings.groups.Count > 0)
        {
            for (int i = aaSettings.groups.Count - 1; i >= 0; i--)
            {
                var g = aaSettings.groups[i];
                if (g == null || g.entries.Count == 0)
                {
                    aaSettings.RemoveGroup(g);
                }
            }
        }
        EditorUtility.SetDirty(aaSettings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
        Debug.Log("Complete ! ! !");
    }

    static void SetGroup(AddressableAssetSettings aaSettings, GroupConfig config, Action<EntryData> onSettingEntry)
    {
        //设置group
        AddressableAssetGroup group = aaSettings.groups.Find((g) => g.Name == config.name);

        if (group == null)
        {
            group = aaSettings.CreateGroup(config.name, config.isDefault, false, false, null);
        }
        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
        {
            schema = group.AddSchema<BundledAssetGroupSchema>();
        }
        ContentUpdateGroupSchema contentUpdateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
        if (contentUpdateGroupSchema == null)
        {
            contentUpdateGroupSchema = group.AddSchema<ContentUpdateGroupSchema>();
        }

        schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        schema.IncludeInBuild = true;
        schema.UseAssetBundleCache = true;
        schema.UseAssetBundleCrc = true;
        schema.UseAssetBundleCrcForCachedBundles = true;
        schema.Timeout = 3;
        schema.BuildPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalBuildPath);
        schema.LoadPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalLoadPath);
        schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;


        contentUpdateGroupSchema.StaticContent = true;

        foreach (var entry in group.entries.ToList())
        {
            if (!config.entrieDatas.Any(x => x.address == entry.address))
            {
                group.RemoveAssetEntry(entry);
            }
        }
        //group 添加Entry
        foreach (var entryData in config.entrieDatas)
        {
            onSettingEntry?.Invoke(entryData);
            AddressableAssetEntry entry = aaSettings.CreateOrMoveEntry(entryData.guid, group);
            entry.SetAddress(entryData.address);
            entry.labels.Clear();
            entry.SetLabel(entryData.label, true);
            if (!aaSettings.GetLabels().Contains(entryData.label))
            {
                aaSettings.AddLabel(entryData.label);
            }
        }
    }

    [Serializable]
    class EntryData
    {
        public string label;//label标签
        public string guid;//通过文件的guid去绑定
        public string address;//显示的名字
    }
    class GroupConfig
    {
        private GroupConfig(string groupName, BundlePackingMode mode, bool isDefault)
        {
            this.name = groupName;
            this.mode = mode;
            this.isDefault = isDefault;

        }
        public string name;
        public BundlePackingMode mode;
        public bool isDefault;
        public List<EntryData> entrieDatas = new List<EntryData>();
        public static GroupConfig FilesGroup(string groupName, string path, string searchPattern, string label, BundlePackingMode mode, bool isDefault = false)
        {
            GroupConfig group = new GroupConfig(groupName, mode, isDefault);
            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                var entry = new EntryData();
                entry.guid = AssetDatabase.AssetPathToGUID(path);
                entry.label = string.IsNullOrEmpty(label) ? fileInfo.Name : label;
                entry.address = fileInfo.Name;
                group.entrieDatas.Add(entry);
            }
            else if (Directory.Exists(path))
            {
                searchPattern = string.IsNullOrEmpty(searchPattern) ? "*.*" : searchPattern;
                var resourcesDir = new DirectoryInfo(path);
                var fileInfos = resourcesDir.GetFiles(searchPattern, SearchOption.AllDirectories);
                Debug.Log(fileInfos.Length);

                foreach (var fileInfo in fileInfos)
                {
                    if (fileInfo.Extension.Equals(".meta"))
                    {
                        continue;
                    }
                    var entry = new EntryData();
                    entry.guid = AssetDatabase.AssetPathToGUID(ConvertAbsolutePathToRelativePath(fileInfo.FullName));
                    if (string.IsNullOrEmpty(entry.guid))
                    {
                        Debug.Log("guid is null  " + fileInfo);
                        continue;
                    }
                    entry.label = string.IsNullOrEmpty(label) ? fileInfo.Name : label;
                    entry.address = fileInfo.Name;
                    group.entrieDatas.Add(entry);
                }
            }
            else
            {
                return null;
            }

            return group;
        }

        public static GroupConfig FolderGroup(string groupName, string folderPath, string label, BundlePackingMode mode, bool isDefault = false)
        {
            GroupConfig group = new GroupConfig(groupName, mode, isDefault);
            var resourcesDir = new DirectoryInfo(folderPath);
            var folderInfos = resourcesDir.GetDirectories();
            foreach (var info in folderInfos)
            {
                var entry = new EntryData();
                entry.guid = AssetDatabase.AssetPathToGUID(ConvertAbsolutePathToRelativePath(info.FullName));
                if (string.IsNullOrEmpty(entry.guid))
                {
                    Debug.Log("guid is null  " + info);
                    continue;
                }
                entry.label = string.IsNullOrEmpty(label) ? info.Name : label;
                entry.address = info.Name;
                group.entrieDatas.Add(entry);
            }
            return group;
        }
        static string ConvertAbsolutePathToRelativePath(string absoluteFolderPath)
        {
            if (absoluteFolderPath.StartsWith("Assets/"))
                return absoluteFolderPath;
            string assetsFolderPath = Application.dataPath;
            return "Assets" + absoluteFolderPath.Substring(assetsFolderPath.Length);
        }
    }
}


