using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ModVersionChecker
{
    public static class HappyExtensions
    {
        public static void CopyTo(this Stream input, Stream output)
        {
            var buffer = new byte[16 * 1024]; // Fairly arbitrary size
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }

        public static bool IsGreaterThen(this System.Version @this, System.Version other)
        {
            if (@this.Major > other.Major) return true;
            // Built-in version comparison will assume 1.1 as 1.1.-1.-1, and 1.1.0 > 1.1.-1, so 1.1.0 > 1.1 
            if (@this.Minor > other.Minor)
            {
                return other.Minor != -1 || @this.Minor != 0;
            }

            if (@this.Build > other.Build)
            {
                return other.Build != -1 || @this.Build != 0;
            }

            if (@this.Revision > other.Revision)
            {
                return other.Revision != -1 || @this.Revision != 0;
            }

            return false;
        }
    }

    [XmlRoot]
    public class ModDataList
    {
        public ModDataList()
        {
            Items = new List<ModData>();
        }

        [XmlElement("Mod")] public List<ModData> Items { get; set; }
    }

    public class ModData
    {
        [XmlElement] public string GUID;
        [XmlElement] public string FullName;
        [XmlIgnore] public GameObject Button;

        public override bool Equals(object obj)
        {
            if (!(obj is ModData b))
                return false;
            return GUID == b.GUID && (FullName == b.FullName);
        }

        public bool Equals(ModData b)
        {
            if (b == null)
                return false;

            return GUID == b.GUID && (FullName == b.FullName);
        }

        public override int GetHashCode()
        {
            return new {GUID, FullName}.GetHashCode();
        }
    }

    /**
     * List to hold custom version of plugin
     * Checker will assume version set in here as installed plugin version
     * Mod developer sometimes release a new version but not changing the version fed into BepInEx
     */
    [XmlRoot]
    public class LocalModCustomVersionList
    {
        public LocalModCustomVersionList()
        {
            Items = new List<ModVersion>();
        }

        [XmlElement("Mod")] public List<ModVersion> Items { get; set; }
    }

    public class ModVersion
    {
        [XmlElement] public string GUID;
        [XmlElement] public string Version;
        [XmlElement] public string MetaVersion;
    }

    [BepInPlugin("com.github.yyuueexxiinngg.plugin.modversionchecker", "Mod Version Checker", "1.2")]
    public class ModVersionChecker : BaseUnityPlugin
    {
        private static ModVersionChecker _instance;

        private static Harmony _har;
        private const float Version = 1.2f;
        private static readonly int TranslationsVersion = 2;
        private static XmlSerializer _modDataSerializer;
        private static XmlSerializer _modVersionSerializer;

        // Hold all founded map of plugin full name to guid, item get removed one done checking 
        private static Dictionary<string, string> _tempModNameMapDictQueuedForChecking = new();
        private static Dictionary<string, GameObject> _modDataBtnDict = new();
        private static Dictionary<string, System.Version> _modCurrentVersionDict = new();

        private static Dictionary<string, System.Version> _modCustomVersionDict = new();

        // Keep the plugin meta version when setting custom version
        private static Dictionary<string, System.Version> _modMetaVersionDict = new();

        private ConfigEntry<KeyboardShortcut> _hotKey;
        private ConfigEntry<string> _modDataUrl;
        private ConfigEntry<string> _modDataUrlCn;
        private ConfigEntry<string> _apiEndpoint;
        private ConfigEntry<int> _timeoutSec;

        private GameObject _canvasPrefab;
        private GameObject _modDataPrefab;
        private Text _progressText;
        private GameObject _canvas;
        private GameObject _modDataHolder;
        private bool _uiInitialized;

        private void Init()
        {
            _hotKey = Config.Bind("config", "HotKey",
                KeyboardShortcut.Deserialize("F5 + LeftControl"),
                "Hotkey to open checker window");

            _apiEndpoint = Config.Bind("config", "ApiEndpoint",
                "https://dsp.thunderstore.io/api",
                "ThunderStore's API endpoint for this game");

            _modDataUrl = Config.Bind("config", "ModDataUrl",
                "https://raw.githubusercontent.com/yyuueexxiinngg/BepInEx-Plugins/master/DysonSphereProgram/ModVersionChecker/ModVersionChecker/Assets/ModDataList.xml",
                "URL to fetch mods' metadata for mapping into plugin info in BepInEx, i.e. data that telling checker which plugin is related to the package from ThunderStore by GUID and FullName.  `ModDataUrlCN` will be used instead when game language set to Chinese");

            _modDataUrlCn = Config.Bind("config", "ModDataUrlCN",
                "https://mod-version.xcpx.workers.dev/modverchecker/dyson",
                "Mod数据获取地址, 用来跟BepInEx中的插件相对应, 通过GUID和FullName来试检测器定位插件在ThunderStore的位置, 仅当语言设置为中文时使用此链接(上面那个链接可能国内访问速度慢)");

            _timeoutSec = Config.Bind("config", "TimeoutSecond", 60,
                "Timeout for mod update checking");

            UpdateAndLoadTranslations();
            InitUI();
            _modDataSerializer = new XmlSerializer(typeof(ModDataList));
            _modVersionSerializer = new XmlSerializer(typeof(LocalModCustomVersionList));
            _progressText.text = "Initializing".Translate();
            LoadModLocalCustomVersionList();
            StartCoroutine(InitModDataListThenCheckVersions());

            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                var modDataBtn = Instantiate(_modDataPrefab, _modDataHolder.transform, false);
                modDataBtn.transform
                    .Find("ModName")
                    .GetComponent<Text>().text = plugin.Value.Metadata.Name;
                modDataBtn.transform
                    .Find("LatestVersion")
                    .GetComponent<Text>().text = "Unsupported".Translate();
                modDataBtn.transform.SetAsLastSibling();

                // Use has set custom version for this mod
                if (_modCustomVersionDict.ContainsKey(plugin.Key))
                {
                    // CustomModCurrentVersionList was generated by 1.1 
                    if (!_modMetaVersionDict.ContainsKey(plugin.Key))
                    {
                        // Update CustomModCurrentVersionList to 1.2
                        _modMetaVersionDict.Add(plugin.Key, plugin.Value.Metadata.Version);
                        SaveLocalModCustomVersionList();
                        _modCurrentVersionDict.Add(plugin.Key, _modCustomVersionDict[plugin.Key]);
                    }
                    else if (plugin.Value.Metadata.Version.IsGreaterThen(_modMetaVersionDict[plugin.Key]))
                    {
                        // Plugin updated, ignore custom version
                        Debug.Log(
                            $"ModVersionChecker: {plugin.Value.Metadata.Name} Plugin updated, ignoring custom version");
                        modDataBtn.transform
                            .Find("CurrentVersion")
                            .GetComponent<Text>().text = plugin.Value.Metadata.Version.ToString();
                        _modCurrentVersionDict.Add(plugin.Key, plugin.Value.Metadata.Version);
                    }
                    else
                    {
                        modDataBtn.transform
                            .Find("CurrentVersion")
                            .GetComponent<Text>().text = _modCustomVersionDict[plugin.Key].ToString();
                        _modCurrentVersionDict.Add(plugin.Key, _modCustomVersionDict[plugin.Key]);
                    }
                }
                else
                {
                    modDataBtn.transform
                        .Find("CurrentVersion")
                        .GetComponent<Text>().text = plugin.Value.Metadata.Version.ToString();
                    _modCurrentVersionDict.Add(plugin.Key, plugin.Value.Metadata.Version);
                }

                _modDataBtnDict.Add(plugin.Key, modDataBtn);
            }
        }

        private void InitUI()
        {
            var ab = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ModVersionChecker.Assets.modverchecker.ab"));
            _canvasPrefab = ab.LoadAsset<GameObject>("ModVerChecker_Canvas");
            _modDataPrefab = ab.LoadAsset<GameObject>("ModData");

            _canvas = Instantiate(_canvasPrefab);
            DontDestroyOnLoad(_canvasPrefab);
            DontDestroyOnLoad(_canvas);

            _modDataHolder = _canvas.transform.Find("Panel/Scroll View/Viewport/Content").gameObject;
            _progressText = _canvas.transform.Find("Panel/Progress").GetComponent<Text>();

            _modDataPrefab.transform.Find("CurVerLabel").GetComponent<Text>().text = "Current\nVersion".Translate();
            _modDataPrefab.transform.Find("LatestVerLabel").GetComponent<Text>().text = "Latest\nVersion".Translate();
            _modDataPrefab.transform.Find("Btn_SetAsLatest/Text").GetComponent<Text>().text =
                "Set As Latest".Translate();

            ab.Unload(false);

            _canvas.transform.Find("Panel").gameObject.AddComponent<MouseDragBehaviour>();
            _canvas.transform
                .Find("Panel/Title")
                .GetComponent<Text>().text = "Mod Version Checker".Translate() + Version.ToString("0.0");
            _canvas.transform
                .Find("Panel/Btn_Close")
                .GetComponent<Button>().onClick.AddListener(CloseUI);

            _uiInitialized = true;
        }

        // ManualSetModDataList + Fetched mod data list => Merged mod data list
        private static ModDataList LoadManualSetModDataList()
        {
            var manualSetModDataListPath =
                $"{Paths.GameRootPath}/BepInEx/data/ModVersionChecker/PreservedModData.xml";
            if (File.Exists(manualSetModDataListPath))
            {
                var reader = new StreamReader(manualSetModDataListPath);
                var modDataList = (ModDataList) _modDataSerializer.Deserialize(reader);
                reader.Close();
                return modDataList;
            }
            else
            {
                var modDataList = new ModDataList
                {
                    Items = new()
                    {
                        new()
                        {
                            GUID = "com.github.yyuueexxiinngg.plugin.dyson.4dpocket",
                            FullName = "yyuueexxiinngg-4DPocket"
                        }
                    }
                };
                new FileInfo(manualSetModDataListPath).Directory?.Create();

                var writer = new StreamWriter(manualSetModDataListPath);
                _modDataSerializer.Serialize(writer, modDataList);
                writer.Close();
                return modDataList;
            }
        }

        private static void LoadModLocalCustomVersionList()
        {
            var localModVersionListPath =
                $"{Paths.GameRootPath}/BepInEx/data/ModVersionChecker/CustomModCurrentVersionList.xml";
            if (File.Exists(localModVersionListPath))
            {
                var reader = new StreamReader(localModVersionListPath);
                var modCustomVersionList = (LocalModCustomVersionList) _modVersionSerializer.Deserialize(reader);
                reader.Close();
                foreach (var customModVersion in modCustomVersionList.Items)
                {
                    if (!_modCustomVersionDict.ContainsKey(customModVersion.GUID))
                    {
                        _modCustomVersionDict.Add(customModVersion.GUID, new System.Version(customModVersion.Version));
                    }

                    if (customModVersion.MetaVersion != null &&
                        !_modMetaVersionDict.ContainsKey(customModVersion.GUID))
                    {
                        _modMetaVersionDict.Add(customModVersion.GUID,
                            new System.Version(customModVersion.MetaVersion));
                    }
                }
            }
        }

        private static void SaveLocalModCustomVersionList()
        {
            var localModVersionListPath =
                $"{Paths.GameRootPath}/BepInEx/data/ModVersionChecker/CustomModCurrentVersionList.xml";
            if (!File.Exists(localModVersionListPath))
            {
                new FileInfo(localModVersionListPath).Directory?.Create();
            }

            var modCustomVersionList = new LocalModCustomVersionList();
            foreach (var key in _modCustomVersionDict.Keys)
            {
                var modCustomVersion = new ModVersion
                {
                    GUID = key,
                    Version = _modCustomVersionDict[key].ToString()
                };

                if (_modMetaVersionDict.ContainsKey(key))
                {
                    modCustomVersion.MetaVersion = _modMetaVersionDict[key].ToString();
                }

                modCustomVersionList.Items.Add(modCustomVersion);
            }

            var writer = new StreamWriter(localModVersionListPath);
            _modVersionSerializer.Serialize(writer, modCustomVersionList);
            writer.Close();
        }

        private static void SaveMergedModDataList(ModDataList modDataList)
        {
            var mergedSetModDataListPath =
                $"{Paths.GameRootPath}/BepInEx/data/ModVersionChecker/MergedModData.xml.cache";
            if (!File.Exists(mergedSetModDataListPath))
            {
                new FileInfo(mergedSetModDataListPath).Directory?.Create();
            }

            var writer = new StreamWriter(mergedSetModDataListPath);
            _modDataSerializer.Serialize(writer, modDataList);
            writer.Close();
        }

        private static ModDataList LoadMergedModDataList()
        {
            var mergedSetModDataListPath =
                $"{Paths.GameRootPath}/BepInEx/data/ModVersionChecker/MergedModData.xml.cache";
            if (File.Exists(mergedSetModDataListPath))
            {
                var reader = new StreamReader(mergedSetModDataListPath);
                var modDataList = (ModDataList) _modDataSerializer.Deserialize(reader);
                reader.Close();
                return modDataList;
            }

            // If MergedModData not exist
            return LoadManualSetModDataList();
        }

        IEnumerator InitModDataListThenCheckVersions()
        {
            ModDataList mergedModDataList;
            var preservedModDataList = LoadManualSetModDataList();
            // Not sure if using a reverse proxy to fetch from thunder store in China are necessary
            var url = global::Localization.language == Language.zhCN ? _modDataUrlCn.Value : _modDataUrl.Value;
            var uwr = UnityWebRequest.Get(url);
            uwr.timeout = _timeoutSec.Value;
            _progressText.text = "Fetching latest mod data list".Translate();
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.Log($"ModVersionChecker: Error while checking update from {url}: {uwr.error}");
                _progressText.text = "Unable to fetch latest mod data list, try using saved cache instead".Translate();
                mergedModDataList = LoadMergedModDataList();
                foreach (var modData in preservedModDataList.Items)
                {
                    if (!mergedModDataList.Items.Contains(modData))
                    {
                        mergedModDataList.Items.Add(modData);
                    }
                }
            }
            else
            {
                mergedModDataList = preservedModDataList;
                var modDataXml = uwr.downloadHandler.text;
                try
                {
                    using var reader = new StringReader(modDataXml);
                    // https://answers.unity.com/questions/10904/xmlexception-text-node-canot-appear-in-this-state.html
                    reader.Read();
                    var modDataList = (ModDataList) _modDataSerializer.Deserialize(reader);
                    reader.Close();

                    foreach (var modData in modDataList.Items)
                    {
                        if (!mergedModDataList.Items.Contains(modData))
                        {
                            mergedModDataList.Items.Add(modData);
                        }
                    }

                    SaveMergedModDataList(mergedModDataList);
                }
                catch (Exception e)
                {
                    _progressText.text =
                        "Unable to fetch latest mod data list, try using saved cache instead".Translate();
                    Debug.Log("Unable to fetch latest mod data list, try using saved cache instead".Translate() +
                              e.Message + e.StackTrace);
                    mergedModDataList = LoadMergedModDataList();
                    foreach (var modData in preservedModDataList.Items)
                    {
                        if (!mergedModDataList.Items.Contains(modData))
                        {
                            mergedModDataList.Items.Add(modData);
                        }
                    }
                }

                _progressText.text = "Checking".Translate();
            }

            foreach (var modData in mergedModDataList.Items)
            {
                if (_modDataBtnDict.ContainsKey(modData.GUID))
                {
                    modData.Button = _modDataBtnDict[modData.GUID];
                    modData.Button.transform
                        .Find("LatestVersion")
                        .GetComponent<Text>().text = "Checking".Translate();
                    modData.Button.transform.SetAsFirstSibling();
                }

                _tempModNameMapDictQueuedForChecking.Add(modData.FullName, modData.GUID);
            }

            StartCoroutine(CheckVersions());
        }

        IEnumerator CheckVersions()
        {
            var url = $"{_apiEndpoint.Value}/experimental/package/";
            var uwr = UnityWebRequest.Get(url);
            uwr.timeout = _timeoutSec.Value;
            _progressText.text = "Fetching mods' latest version".Translate();
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.Log($"ModVersionChecker: Error while checking update from {url}: {uwr.error}");
                _progressText.text = "Unable to fetch mods' latest version".Translate();
            }
            else
            {
                var data = JSON.Parse(uwr.downloadHandler.text);
                foreach (var package in data)
                {
                    if (package.Value.HasKey("package") && package.Value["package"].HasKey("full_name"))
                    {
                        if (_tempModNameMapDictQueuedForChecking.ContainsKey(package.Value["package"]["full_name"]))
                        {
                            var guid = _tempModNameMapDictQueuedForChecking[package.Value["package"]["full_name"]];
                            if (_modDataBtnDict.ContainsKey(guid))
                            {
                                var latestVersion =
                                    new System.Version(package.Value["package"]["latest"]["version_number"]);
                                _modDataBtnDict[guid].transform
                                    .Find("LatestVersion")
                                    .GetComponent<Text>().text = latestVersion.ToString();

                                _modDataBtnDict[guid].GetComponent<Button>().onClick.AddListener(() =>
                                {
                                    Application.OpenURL(package.Value["package"]["package_url"]);
                                });

                                if (latestVersion.IsGreaterThen(_modCurrentVersionDict[guid]))
                                {
                                    _modDataBtnDict[guid].transform
                                        .Find("LatestVersion")
                                        .GetComponent<Text>().color = Color.red;
                                    _modDataBtnDict[guid].transform.SetAsFirstSibling();

                                    var btnSetAsLatest = _modDataBtnDict[guid].transform
                                        .Find("Btn_SetAsLatest")
                                        .GetComponent<Button>();

                                    // Set latest version as custom version
                                    btnSetAsLatest.onClick.AddListener(() =>
                                    {
                                        _modDataBtnDict[guid].transform
                                            .Find("CurrentVersion")
                                            .GetComponent<Text>().text = latestVersion.ToString();
                                        _modDataBtnDict[guid].transform
                                            .Find("LatestVersion")
                                            .GetComponent<Text>().color = Color.black;

                                        if (!_modCustomVersionDict.ContainsKey(guid))
                                        {
                                            _modCustomVersionDict.Add(guid, latestVersion);
                                        }
                                        else
                                        {
                                            _modCustomVersionDict[guid] = latestVersion;
                                        }

                                        if (!_modMetaVersionDict.ContainsKey(guid))
                                        {
                                            _modMetaVersionDict.Add(guid, _modCurrentVersionDict[guid]);
                                        }
                                        else
                                        {
                                            _modMetaVersionDict[guid] = _modCurrentVersionDict[guid];
                                        }

                                        SaveLocalModCustomVersionList();
                                        btnSetAsLatest.gameObject.SetActive(false);
                                    });
                                    btnSetAsLatest.gameObject.SetActive(true);
                                }
                            }

                            _tempModNameMapDictQueuedForChecking.Remove(package.Value["package"]["full_name"]);
                        }
                    }
                }

                foreach (var notFoundMod in _tempModNameMapDictQueuedForChecking)
                {
                    if (_modDataBtnDict.ContainsKey(notFoundMod.Value))
                    {
                        _modDataBtnDict[notFoundMod.Value].transform.transform
                            .Find("LatestVersion")
                            .GetComponent<Text>().text = "Failed".Translate();
                    }
                    else
                    {
                        Debug.Log($"ModVersionChecker: Unexpected mod entry {notFoundMod.Value}");
                    }
                }

                _progressText.fontSize = 11;
                _progressText.text = "Finish checking versions, click item to open mod's download page in browser"
                    .Translate();
            }
        }

        private void OpenUI()
        {
            _canvas.SetActive(true);
        }

        private void CloseUI()
        {
            _canvas.SetActive(false);
        }

        private void ToggleUI()
        {
            if (_canvas.activeSelf)
            {
                CloseUI();
            }
            else
            {
                OpenUI();
            }
        }

        private static void UpdateAndLoadTranslations()
        {
            var translationsPath = $"{Paths.GameRootPath}/BepInEx/data/ModVersionChecker/Strings.json";
            if (File.Exists(translationsPath))
            {
                Localization.LoadTranslations(File.ReadAllText(translationsPath));
                if (Localization._translations["Version"] < TranslationsVersion)
                {
                    Debug.Log($"Old translations found, updating to new version: {TranslationsVersion}");
                    var fs = File.OpenWrite(translationsPath);
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("ModVersionChecker.Assets.Strings.json")
                        .CopyTo(fs);
                    fs.Close();

                    Localization.LoadTranslations(File.ReadAllText(translationsPath));
                }
            }
            else
            {
                new FileInfo(translationsPath).Directory?.Create();
                var fs = File.OpenWrite(translationsPath);
                Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("ModVersionChecker.Assets.Strings.json")
                    .CopyTo(fs);
                fs.Close();
                Localization.LoadTranslations(File.ReadAllText(translationsPath));
            }
        }

        private void Start()
        {
            _har = Harmony.CreateAndPatchAll(typeof(ModVersionChecker));
            _instance = this;
            Init();
        }

        private void Update()
        {
            if (_hotKey.Value.IsDown())
            {
                ToggleUI();
            }
        }

        private void OnDestroy()
        {
            Debug.Log("Unloading Mod Version Checker from ScriptEngine");
            _har?.UnpatchSelf();
            Destroy(_canvasPrefab);
            Destroy(_canvas);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            if (_instance == null) return;
            if (_instance._uiInitialized)
            {
                _instance.CloseUI();
            }
        }
    }
}