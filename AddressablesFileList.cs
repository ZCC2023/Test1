using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using System.Linq;
using System.IO;
using static UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema;

public class AddressablesTool{

    [MenuItem("Tools/SetAddressableGroups")]
    static void SetAddressablesGroup()
    {
        List<GroupConfig> groupConfigs = new List<GroupConfig>
        {
             GroupConfig.FolderGroups("Resources","Assets/_Res/_Resources","resources",BundlePackingMode.PackTogether,true),
             GroupConfig.FolderGroups("SpriteAnimations","Assets/_Res/Anim2D/SpriteAnimations",null,BundlePackingMode.PackTogether),
             GroupConfig.FilesGroup("Font","Assets/_Res/Font","*.*","font",BundlePackingMode.PackTogether),
             GroupConfig.FilesGroup("VFX","Assets/_Res/VFX","*.*","vfx",BundlePackingMode.PackTogether),
             GroupConfig.FilesGroup("Scene","Assets/_Res/Scenes/","*.unity","scene",BundlePackingMode.PackTogether),
             GroupConfig.FilesGroup("Shader","Packages/com.unity.shadergraph/Editor/Resources/Shaders/FallbackError.shader",null,"shader",BundlePackingMode.PackTogether),//ShaderGraph
             GroupConfig.FilesGroup("Shader","Packages/com.unity.render-pipelines.core/Runtime/RenderPipelineResources/FallbackShader.shader",null,"shader",BundlePackingMode.PackTogether),//URP
             GroupConfig.FilesGroup("Shader","Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader",null,"shader",BundlePackingMode.PackTogether),//URP
             GroupConfig.FilesGroup("Shader","Packages/com.unity.render-pipelines.universal/Shaders/Utils/FallbackError.shader",null,"shader",BundlePackingMode.PackTogether),//URP
             //GroupConfig.FilesGroup("Shader","Packages/com.unity.render-pipelines.universal","*.shader","shader",BundlePackingMode.PackTogether),//URP
        };
        Set(groupConfigs);
    }

    static void Set(List<GroupConfig> groupConfigs)
    {
        EditorUtility.DisplayProgressBar("SettingAddressables ...", "", 0);
        try
        {
            Debug.Log("SetAddressableGroups");
            groupConfigs.RemoveAll(x => x == null);
            if (groupConfigs.Count == 0)
            {
                Debug.Log("groupConfigs is null");
                EditorUtility.ClearProgressBar();
                return;
            };
            Dictionary<string, GroupConfig> allConfigs = new Dictionary<string, GroupConfig>();
            groupConfigs.ForEach(x =>
            {
                if (allConfigs.ContainsKey(x.name))
                {
                    allConfigs[x.name].entrieDatas.AddRange(x.entrieDatas);
                }
                else
                {
                    allConfigs[x.name] = x;
                }
            });
            //获取配置
            AddressableAssetSettings aaSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            foreach (var str in aaSettings.GetLabels())
            {
                aaSettings.RemoveLabel(str);
            }

            int sum = allConfigs.Sum(x => x.Value.entrieDatas.Count);
            int index = 0;

            Action<EntryData> onSettingEntry = (data) =>
            {
                EditorUtility.DisplayProgressBar("SettingAddressables ...", data.address, index / (float)sum);
                index++;
            };
            foreach (var config in allConfigs.Values)
            {
                SetGroup(aaSettings, config, onSettingEntry);
            }

            //清理错误group
            if (aaSettings != null && aaSettings.groups.Count > 0)
            {
                for (int i = aaSettings.groups.Count - 1; i >= 0; i--)
                {
                    var g = aaSettings.groups[i];
                    if (g == null || g.entries.Count == 0 || !allConfigs.ContainsKey(g.Name))
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
        catch (Exception e)
        {
            Debug.LogError(e);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", e.ToString(), "ok");
        }
    }

    static void SetGroup(AddressableAssetSettings aaSettings, GroupConfig config, Action<EntryData> onSettingEntry)
    {
        //设置group
        AddressableAssetGroup group = aaSettings.groups.Find((g) => g.Name.Equals(config.name));

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
        //Debug.Log(config.mode);
        schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        schema.BundleMode = config.mode;
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
            if (!config.entrieDatas.Any(x => x.guid.Equals(entry.guid)))
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

    //[Serializable]
    class EntryData
    {
        public string label;//label标签
        public string guid;//通过文件的guid去绑定
        public string address;//显示的名字
    }
    class GroupConfig
    {
        public string name;
        public BundlePackingMode mode;
        public bool isDefault;
        public List<EntryData> entrieDatas = new List<EntryData>();
        private GroupConfig(string groupName, BundlePackingMode mode, bool isDefault)
        {
            this.name = groupName;
            this.mode = mode;
            this.isDefault = isDefault;
        }
        /// <summary>
        /// 文件夹下所有的符合searchPattern 的文件作为一个group  如果path是文件路径 则searchPattern可以不传
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="path"></param>
        /// <param name="searchPattern"></param>
        /// <param name="label"></param>
        /// <param name="mode"></param>
        /// <param name="isDefault"></param>
        /// <returns></returns>
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
                    entry.guid = AssetDatabase.AssetPathToGUID(ConvertAbsolutePathToRelativePath(path, fileInfo.FullName));
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
        /// <summary>
        /// 设置某一个文件夹作为一个group
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="folderPath"></param>
        /// <param name="label"></param>
        /// <param name="mode"></param>
        /// <param name="isDefault"></param>
        /// <returns></returns>
        public static GroupConfig FolderGroup(string groupName, string folderPath, string label, BundlePackingMode mode, bool isDefault = false)
        {
            if (!Directory.Exists(folderPath))
            {
                return null;
            }
            GroupConfig group = new GroupConfig(groupName, mode, isDefault);
            var resourcesDir = new DirectoryInfo(folderPath);
            var entry = new EntryData();
            entry.guid = AssetDatabase.AssetPathToGUID(folderPath);
            entry.label = string.IsNullOrEmpty(label) ? resourcesDir.Name : label;
            entry.address = resourcesDir.Name;
            group.entrieDatas.Add(entry);
            return group;
        }
        /// <summary>
        /// 设置某个文件夹下的所有文件夹作为一个group
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="folderPath"></param>
        /// <param name="label"></param>
        /// <param name="mode"></param>
        /// <param name="isDefault"></param>
        /// <returns></returns>
        public static GroupConfig FolderGroups(string groupName, string folderPath, string label, BundlePackingMode mode, bool isDefault = false)
        {
            GroupConfig group = new GroupConfig(groupName, mode, isDefault);
            var resourcesDir = new DirectoryInfo(folderPath);
            var folderInfos = resourcesDir.GetDirectories();
            foreach (var info in folderInfos)
            {
                var entry = new EntryData();
                entry.guid = AssetDatabase.AssetPathToGUID(ConvertAbsolutePathToRelativePath(folderPath, info.FullName));
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

        /// <summary>
        /// 绝对路径转换成相对路径
        /// </summary>
        /// <param name="relativePath">相对路径的文件夹(文件在此文件夹下 可能有嵌套文件夹)</param>
        /// <param name="absolutePath">文件的绝对路径</param>
        /// <returns></returns>
        static string ConvertAbsolutePathToRelativePath(string relativePath, string absolutePath)
        {
            var fileInfo = new FileInfo(absolutePath);
            var dirInfo = new DirectoryInfo(relativePath);
            return relativePath + fileInfo.FullName.Substring(dirInfo.FullName.Length);
        }
    }
}


