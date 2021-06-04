using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace Dyson4DPocket
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
    }

    public static class Localization
    {
        public static JSONNode _translations;

        public static void LoadTranslations(string json)
        {
            _translations = JSON.Parse(json);
        }

        public static string Translate(this string s)
        {
            try
            {
                if (_translations.HasKey(s))
                {
                    var currentLocale = global::Localization.language.ToString();
                    if (_translations[s].HasKey(currentLocale))
                    {
                        if (_translations[s][currentLocale] != null)
                        {
                            return _translations[s][currentLocale];
                        }
                    }

                    if (_translations[s].HasKey("Other"))
                    {
                        if (_translations[s]["Other"] != null)
                        {
                            return _translations[s]["Other"];
                        }
                    }

                    if (global::Localization.language == Language.zhCN)
                    {
                        return s;
                    }

                    if (_translations[s].HasKey("enUS"))
                    {
                        if (_translations[s]["enUS"] != null)
                        {
                            return _translations[s]["enUS"];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return s;
            }

            // Fallback
            return s;
        }
    }

    [BepInPlugin("com.github.yyuueexxiinngg.plugin.dyson.4dpocket", "4D Pocket", "1.5")]
    public class The4DPocket : BaseUnityPlugin
    {
        public static ConfigEntry<KeyCode> HotKey;
        public static FavStorageItemList FavStorages;
        private static XmlSerializer _favStorageItemsSerializer;
        private static Harmony _har;
        private static Pocket _p;
        private static readonly int TranslationsVersion = 3;

        private static void Init()
        {
            LoadFavoriteStorages();
            UpdateAndLoadTranslations();
            _har = Harmony.CreateAndPatchAll(typeof(Pocket));
            _p = new GameObject(typeof(Pocket).FullName).AddComponent<Pocket>();
            DontDestroyOnLoad(_p);
            Pocket.Instance = _p;
        }

        private static void LoadFavoriteStorages()
        {
            _favStorageItemsSerializer = new XmlSerializer(typeof(FavStorageItemList));
            var favStorageItemsPath = $"{Paths.GameRootPath}/BepInEx/data/4DPocket/FavoriteStorageItems.xml";
            if (File.Exists(favStorageItemsPath))
            {
                using (var reader = new StreamReader(favStorageItemsPath))
                {
                    FavStorages = (FavStorageItemList) _favStorageItemsSerializer.Deserialize(reader);
                    reader.Close();
                }
            }
            else
            {
                FavStorages = new FavStorageItemList();
            }
        }

        public static void SaveFavoriteStorages()
        {
            var favStorageItemsPath = $"{Paths.GameRootPath}/BepInEx/data/4DPocket/FavoriteStorageItems.xml";
            if (!File.Exists(favStorageItemsPath))
            {
                new FileInfo(favStorageItemsPath).Directory?.Create();
            }

            using (var writer = new StreamWriter(favStorageItemsPath))
            {
                _favStorageItemsSerializer.Serialize(writer, FavStorages);
                writer.Close();
            }
        }

        private static void UpdateAndLoadTranslations()
        {
            var translationsPath = $"{Paths.GameRootPath}/BepInEx/data/4DPocket/Strings.json";
            if (File.Exists(translationsPath))
            {
                Localization.LoadTranslations(File.ReadAllText(translationsPath));
                if (Localization._translations["Version"] < TranslationsVersion)
                {
                    Debug.Log($"Old translations found, updating to new version: {TranslationsVersion}");
                    var fs = File.OpenWrite(translationsPath);
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("Dyson4DPocket.Assets.Strings.json")
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
                    .GetManifestResourceStream("Dyson4DPocket.Assets.Strings.json")
                    .CopyTo(fs);
                fs.Close();

                Localization.LoadTranslations(File.ReadAllText(translationsPath));
            }
        }

        void Start()
        {
            HotKey = Config.Bind("config", "HotKey", KeyCode.F5, "插件按键");
            Init();
        }

        private void OnDestroy()
        {
            Debug.Log("Unloading 4D Pocket from ScriptEngine");
            _har?.UnpatchSelf();
            if (_p != null)
            {
                Destroy(_p);
            }
        }

        public static void Main()
        {
            Debug.Log("4D Pocket loading from ScriptLoader");
            HotKey = new ConfigFile(
                    $"{Paths.GameRootPath}/BepInEx/config/com.github.yyuueexxiinngg.plugin.dyson.4dpocket,cfg", true)
                .Bind("config", "HotKey", KeyCode.F5, "插件按键");
            Init();
        }

        public static void Unload()
        {
            Debug.Log("Unloading 4D Pocket from ScriptLoader");
            _har?.UnpatchSelf();
            if (_p != null)
            {
                Destroy(_p);
            }
        }
    }

    [XmlRoot]
    public class FavStorageItemList
    {
        public FavStorageItemList()
        {
            Items = new List<FavStorageItem>();
        }

        [XmlElement("Storage")] public List<FavStorageItem> Items { get; set; }
    }

    public class InputItem
    {
        public int FactoryIndex;
        public int ItemID;
        public InputItemType ItemType;
    }

    public enum InputItemType
    {
        Storage,
        Station
    }

    public class FavStorageItem
    {
        [XmlElement] public int FactoryIndex;
        [XmlElement] public int StorageID;
        [XmlElement] public string Remark;
        [XmlElement] public InputItemType Type = InputItemType.Storage;
    }

    public class Pocket : MonoBehaviour
    {
        public static Pocket Instance;

        private const float Version = 1.5f;

        private bool _uiInitialized;

        // Keep reference to unload for Script Loader 
        private AssetBundle _4dAssetBundle;

        private UIGame _uiGame;
        private UIStorageWindow _uiStorage;
        private UIStationWindow _uiStation;
        private Text _cursorTextObj;
        private string _cursorText;
        private GameObject _canvas;
        private GameObject _favItem;
        private GameObject _favItemHolder;
        private GameObject _canvasInstance;
        private InputField _inputField;
        private InputField _inputFieldRemark;
        private Text _inputText;
        private Text _remarkInputText;
        private Text _placeholderText;

        private bool _inspectingStorage;
        private bool _inspectingStation;
        private int _lastFactoryIndex = -1;
        private int _lastStorageId = -1;
        private int _lastStationFactoryIndex = -1;
        private int _lastStationId = -1;

        private bool _uiActive;

        private void InitUI()
        {
            _4dAssetBundle = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dyson4DPocket.Assets.4dpocket.ab"));

            _canvas = _4dAssetBundle.LoadAsset<GameObject>("4DPocket_Canvas");
            _favItem = _4dAssetBundle.LoadAsset<GameObject>("4DPocket_FavItem");
            _canvas.SetActive(false);

            _canvasInstance = Instantiate(_canvas);

            DontDestroyOnLoad(_canvas);
            DontDestroyOnLoad(_canvasInstance);

            _canvasInstance.transform.Find("Panel").gameObject.AddComponent<MouseDragBehaviour>();
            _inputField = _canvasInstance.transform.Find("Panel/4DPocket_Input").GetComponent<InputField>();
            _inputFieldRemark = _canvasInstance.transform.Find("Panel/4DPocket_InputRemark").GetComponent<InputField>();

            _inputText = _inputField.transform
                .Find("4DPocket_InputText")
                .GetComponent<Text>();

            _remarkInputText = _inputFieldRemark.transform
                .Find("4DPocket_InputRemarkText")
                .GetComponent<Text>();

            _placeholderText = _inputField.transform
                .Find("4DPocket_Placeholder")
                .GetComponent<Text>();

            _inputFieldRemark.transform
                .Find("4DPocket_Placeholder")
                .GetComponent<Text>().text = "输入备注(可选)".Translate();

            _canvasInstance.transform
                .Find("Panel/4DPocket_Title")
                .GetComponent<Text>().text = "四次元口袋".Translate() + Version.ToString("0.0");

            _canvasInstance.transform
                .Find("Panel/4DPocket_FavsTitle")
                .GetComponent<Text>().text = "收藏夹".Translate();

            _canvasInstance.transform
                    .Find("Panel/4DPocket_Help")
                    .GetComponent<Text>().text =
                "HelpText".Translate().Replace("${Key}", The4DPocket.HotKey.Value.ToString());

            _canvasInstance.transform
                .Find("Panel/4DPocket_AddFavBtn/Text")
                .GetComponent<Text>().text = "添加至收藏夹".Translate();

            _canvasInstance.transform
                .Find("Panel/4DPocket_AddFavBtn")
                .GetComponent<Button>().onClick
                .AddListener(OnAddFavBtnClick);

            _favItemHolder = _canvasInstance.transform
                .Find("Panel/4DPocket_Favs/Viewport/Content")
                .gameObject;

            InitFavStorages();
            _uiInitialized = true;

            StartCoroutine(CheckUpdate());
        }

        private void InitFavStorages()
        {
            foreach (var favStorageItem in The4DPocket.FavStorages.Items)
            {
                AddFavorite(favStorageItem, true);
            }
        }

        private bool TryParseInput(string inputText, out InputItem inputItem)
        {
            inputText = inputText.Trim().ToLower();
            inputItem = null;
            var split = inputText.Split('.');
            if (split.Length == 2)
            {
                if (
                    int.TryParse(split[0], out var factoryIndex) &&
                    int.TryParse(split[1], out var itemId)
                )
                {
                    inputItem = new InputItem
                    {
                        FactoryIndex = factoryIndex,
                        ItemID = itemId,
                        ItemType = InputItemType.Storage
                    };
                    return true;
                }

                if (split[0].StartsWith("s") &&
                    int.TryParse(split[0].Replace("s", ""), out factoryIndex) &&
                    int.TryParse(split[1], out itemId))
                {
                    inputItem = new InputItem
                    {
                        FactoryIndex = factoryIndex,
                        ItemID = itemId,
                        ItemType = InputItemType.Station
                    };
                    return true;
                }
            }

            return false;
        }

        private void OnAddFavBtnClick()
        {
            if (TryParseInput(_inputText.text, out var item))
            {
                AddFavorite(
                    new FavStorageItem
                    {
                        FactoryIndex = item.FactoryIndex,
                        StorageID = item.ItemID,
                        Remark = _remarkInputText.text,
                        Type = item.ItemType
                    }
                );
            }
            else
            {
                UIRealtimeTip.Popup("存储箱ID格式错误, 应为小数(工厂索引.存储箱ID)".Translate());
            }
        }

        private void AddFavorite(FavStorageItem favStorageItem, bool initializing = false)
        {
            var fav = Instantiate(_favItem, _favItemHolder.transform, false);

            if (favStorageItem.Type == InputItemType.Storage)
            {
                // Save ID to object name for RefreshFavoriteStorageItemIcons()
                fav.name = $"FavItem_{favStorageItem.FactoryIndex}.{favStorageItem.StorageID}";
                fav.GetComponent<Button>().onClick.AddListener(() =>
                {
                    OpenStorage(favStorageItem.FactoryIndex, favStorageItem.StorageID);
                    CloseUI();
                });
                fav.transform.Find("StorageID").GetComponent<Text>().text =
                    $"{favStorageItem.FactoryIndex}.{favStorageItem.StorageID}";

                SetFavStorageItemIcon(favStorageItem.FactoryIndex, favStorageItem.StorageID, fav.transform);
            }
            else
            {
                fav.name = $"FavItem_s{favStorageItem.FactoryIndex}.{favStorageItem.StorageID}";
                fav.GetComponent<Button>().onClick.AddListener(() =>
                {
                    OpenStation(favStorageItem.FactoryIndex, favStorageItem.StorageID);
                    CloseUI();
                });
                fav.transform.Find("StorageID").GetComponent<Text>().text =
                    $"S{favStorageItem.FactoryIndex}.{favStorageItem.StorageID}";
                SetFavStorageItemIcon(favStorageItem.FactoryIndex, favStorageItem.StorageID, fav.transform, true);
            }

            fav.transform.Find("Remark").GetComponent<Text>().text = favStorageItem.Remark;

            fav.transform.Find("Btn_Del").GetComponent<Button>().onClick
                .AddListener(() => { OnFavDelBtnClick(fav.transform, favStorageItem); });

            if (!initializing)
            {
                fav.transform.SetAsFirstSibling();
                The4DPocket.FavStorages.Items.Insert(0, favStorageItem);
                The4DPocket.SaveFavoriteStorages();
            }
        }

        private void OnFavDelBtnClick(Transform parent, FavStorageItem fav)
        {
            The4DPocket.FavStorages.Items.Remove(fav);
            Destroy(parent.gameObject);
            The4DPocket.SaveFavoriteStorages();
        }

        private void SetFavStorageItemIcon(int factoryIndex, int storageId, Transform parent, bool isStation = false)
        {
            if (isStation)
            {
                parent.Find("FavItemIcon").GetComponent<Image>().sprite = LDB.items.Select(2103).iconSprite;
                return;
            }

            if (GameMain.isRunning &&
                !GameMain.instance.isMenuDemo &&
                GameMain.data.factories != null &&
                GameMain.data.factories.Length > factoryIndex &&
                GameMain.data.factories[factoryIndex] != null)
            {
                var factory = GameMain.data.factories[factoryIndex];
                var factoryStorage = factory.factoryStorage;
                if (factoryStorage.storagePool != null &&
                    factoryStorage.storagePool.Length > storageId &&
                    factoryStorage.storagePool[storageId] != null
                )
                {
                    var iconId0 = (int) factory.entitySignPool[factoryStorage.storagePool[storageId].entityId].iconId0;
                    if (iconId0 > 0)
                    {
                        parent.Find("FavItemIcon").GetComponent<Image>().sprite =
                            LDB.items.Select(iconId0).iconSprite;
                        return;
                    }
                }
            }

            parent.Find("FavItemIcon").GetComponent<Image>().sprite = null;
        }

        private void RefreshFavoriteStorageItemIcons()
        {
            foreach (Transform child in _favItemHolder.transform)
            {
                if (TryParseInput(child.name.Replace("FavItem_", ""), out var item))
                {
                    if (item.ItemType == InputItemType.Storage)
                    {
                        SetFavStorageItemIcon(item.FactoryIndex, item.ItemID, child);
                    }
                    else
                    {
                        SetFavStorageItemIcon(item.FactoryIndex, item.ItemID, child, true);
                    }
                }
            }
        }

        private void ToggleUI()
        {
            if (_uiActive)
            {
                CloseUI();
            }
            else
            {
                OpenUI();
            }
        }

        private void OpenUI()
        {
            // Check if in game
            if (GameMain.isRunning && !GameMain.instance.isMenuDemo)
            {
                _placeholderText.text = "输入存储箱ID".Translate();
                _canvasInstance.SetActive(true);
                _inputField.interactable = true;
                _inputField.ActivateInputField();
                _uiActive = true;
                RefreshFavoriteStorageItemIcons();
            }
            else
            {
                _placeholderText.text = "请先开始游戏".Translate();
                _inputField.interactable = false;
                _canvasInstance.SetActive(true);
                _uiActive = true;
            }
        }

        private void CloseUI()
        {
            if (_canvasInstance != null)
                _canvasInstance.SetActive(false);
            _uiActive = false;
        }

        private void OpenStation(int factoryIndex, int stationId)
        {
            if (factoryIndex < 0 || stationId < 0) return;
            if (!GameMain.isRunning || GameMain.instance.isMenuDemo) return;
            _lastFactoryIndex = factoryIndex;
            _lastStationFactoryIndex = stationId;
            if (_uiGame == null || (_uiStation) == null)
            {
                _uiGame = UIRoot.instance.uiGame;
                _uiStation = _uiGame.stationWindow;
            }

            if (!_uiStation.inited) return;

            if (_uiStation.active)
            {
                UIRealtimeTip.Popup("请先关闭目前物流站".Translate());
                return;
            }

            if (GameMain.data.factories != null &&
                GameMain.data.factories.Length > factoryIndex &&
                GameMain.data.factories[factoryIndex] != null)
            {
                try
                {
                    var factory = GameMain.data.factories[factoryIndex];
                    var transport = factory.transport;
                    if (transport.stationPool != null &&
                        transport.stationPool.Length >= stationId &&
                        transport.stationPool[stationId] != null
                    )
                    {
                        _uiStation.stationId = stationId;
                        _uiStation.active = true;
                        if (!_uiStation.gameObject.activeSelf)
                        {
                            _uiStation.gameObject.SetActive(true);
                        }

                        _uiStation.factory = factory;
                        _uiStation.transport = factory.transport;
                        _uiStation.powerSystem = factory.powerSystem;
                        _uiStation.player = GameMain.mainPlayer;
                        _uiStation.OnStationIdChange();

                        _uiStation.nameInput.onValueChanged.AddListener(_uiStation.OnNameInputSubmit);
                        _uiStation.nameInput.onEndEdit.AddListener(_uiStation.OnNameInputSubmit);
                        _uiStation.player.onIntendToTransferItems += _uiStation.OnPlayerIntendToTransferItems;

                        _uiStation.transform.SetAsLastSibling();
                        _uiGame.OpenPlayerInventory();
                        _inspectingStation = true;
                    }
                    else
                    {
                        UIRealtimeTip.Popup("物流站ID不存在".Translate());
                    }
                }
                catch (Exception message)
                {
                    _inspectingStation = false;
                    Debug.Log(message.StackTrace);
                }
            }
            else
            {
                UIRealtimeTip.Popup("工厂不存在".Translate());
            }
        }

        private void OpenStorage(int factoryIndex, int storageId)
        {
            if (factoryIndex < 0 || storageId < 0) return;
            if (!GameMain.isRunning || GameMain.instance.isMenuDemo) return;
            _lastFactoryIndex = factoryIndex;
            _lastStorageId = storageId;
            if (_uiGame == null || _uiStorage == null)
            {
                _uiGame = UIRoot.instance.uiGame;
                _uiStorage = _uiGame.storageWindow;
            }

            if (!_uiStorage.inited) return;

            // Not replacing current opened storage window to avoid potential problems
            if (_uiStorage.active)
            {
                UIRealtimeTip.Popup("请先关闭目前存储箱".Translate());
                return;
            }

            if (GameMain.data.factories != null &&
                GameMain.data.factories.Length > factoryIndex &&
                GameMain.data.factories[factoryIndex] != null)
            {
                try
                {
                    var factory = GameMain.data.factories[factoryIndex];
                    var factoryStorage = factory.factoryStorage;
                    if (factoryStorage.storagePool != null &&
                        factoryStorage.storagePool.Length > storageId &&
                        factoryStorage.storagePool[storageId] != null
                    )
                    {
                        _uiStorage.storageId = storageId;
                        _uiStorage.active = true;
                        if (!_uiStorage.gameObject.activeSelf)
                        {
                            _uiStorage.gameObject.SetActive(true);
                        }

                        _uiStorage.factory = factory;
                        _uiStorage.factoryStorage = factoryStorage;
                        _uiStorage.player = GameMain.mainPlayer;
                        _uiStorage.OnStorageIdChange();
                        _uiStorage.eventLock = true;
                        _uiStorage.transform.SetAsLastSibling();
                        _uiGame.OpenPlayerInventory();
                        _inspectingStorage = true;
                    }
                    else
                    {
                        UIRealtimeTip.Popup("存储箱ID不存在".Translate());
                    }
                }
                catch (Exception message)
                {
                    _inspectingStorage = false;
                    Debug.Log(message.StackTrace);
                }
            }
            else
            {
                UIRealtimeTip.Popup("工厂不存在".Translate());
            }
        }

        private void CloseStorage()
        {
            _uiStorage.storageId = 0;
            _inspectingStorage = false;
        }

        private void CloseStation()
        {
            _uiStation.stationId = 0;
            _inspectingStation = false;
        }

        IEnumerator CheckUpdate()
        {
            var uri = global::Localization.language == Language.zhCN
                ? "https://mod-version.xcpx.workers.dev/bepinex"
                : "https://raw.githubusercontent.com/yyuueexxiinngg/BepInEx-Plugins/master/Meta.json";

            UnityWebRequest uwr = UnityWebRequest.Get(uri);
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.Log($"4DPocket: Error while checking update from {uri}: {uwr.error}");
            }
            else
            {
                try
                {
                    var metaInfo = JSON.Parse(uwr.downloadHandler.text);
                    if (metaInfo != null &&
                        metaInfo.HasKey("mods") &&
                        metaInfo["mods"].HasKey("dyson") &&
                        metaInfo["mods"]["dyson"].HasKey("4DPocket") &&
                        metaInfo["mods"]["dyson"]["4DPocket"].HasKey("version")
                    )
                    {
                        if (float.TryParse(metaInfo["mods"]["dyson"]["4DPocket"]["version"], out var version))
                        {
                            if (version > Version)
                            {
                                var updateText = Instance._canvasInstance.transform.Find("Panel/4DPocket_Update")
                                    .GetComponent<Text>();
                                updateText.text = $"{"检测到更新".Translate()}: {version:0.0}";
                                updateText.gameObject.SetActive(true);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(
                        $"4DPocket: Error while checking update from {uri}: {uwr.downloadHandler.text} Error: {e.Message}");
                }
            }
        }


        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void Start()
        {
            if (!_uiInitialized)
            {
                InitUI();
            }
        }

        private void Update()
        {
            if (_uiInitialized)
            {
                if (_cursorText != null)
                {
                    _cursorTextObj.text = _cursorText;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (_uiActive)
                    {
                        if (_inputText != null)
                        {
                            if (TryParseInput(_inputText.text, out var item))
                            {
                                if (item.ItemType == InputItemType.Storage)
                                {
                                    if (!_inspectingStorage)
                                    {
                                        CloseUI();
                                        OpenStorage(item.FactoryIndex, item.ItemID);
                                    }
                                    else
                                    {
                                        CloseUI();
                                        _uiStorage.storageId = 0;
                                        OpenStorage(item.FactoryIndex, item.ItemID);
                                    }
                                }
                                else
                                {
                                    if (!_inspectingStation)
                                    {
                                        CloseUI();
                                        OpenStation(item.FactoryIndex, item.ItemID);
                                    }
                                    else
                                    {
                                        CloseUI();
                                        _uiStation.stationId = 0;
                                        OpenStation(item.FactoryIndex, item.ItemID);
                                    }
                                }
                            }
                            else
                            {
                                UIRealtimeTip.Popup("存储箱ID格式错误, 应为小数(工厂索引.存储箱ID)".Translate());
                            }
                        }
                    }
                }

                if (Input.GetKeyDown(The4DPocket.HotKey.Value))
                {
                    ToggleUI();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseUI();
                }
            }
        }

        private void OnDestroy()
        {
            _4dAssetBundle.Unload(true);
            Destroy(_canvas);
            Destroy(_favItem);
            Destroy(_canvasInstance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIGame), "OnPlayerInspecteeChange")]
        public static bool OnPlayerInspecteeChange(UIGame __instance, EObjectType objType, int objId)
        {
            if (Instance != null)
            {
                if (Instance._inspectingStorage)
                {
                    if (objType == EObjectType.Entity)
                    {
                        // Trying to inspect another object
                        if (objId != 0)
                        {
                            var factory = GameMain.mainPlayer.factory;
                            var storageId = factory?.entityPool[objId].storageId;
                            var stationId = factory?.entityPool[objId].stationId;
                            // The other object is another storage
                            if (storageId > 0)
                            {
                                Instance.CloseStorage();
                                return true;
                            }

                            if (stationId > 0)
                            {
                                Instance.CloseStation();
                                return true;
                            }
                        }
                        else // PlayerAction_Inspect.InspectNothing()  e.g., in space
                        {
                            // Prevent storage ID set to 0, which closes the window.
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControlGizmo), "SetMouseOverTarget")]
        public static void SetMouseOverTarget(PlayerControlGizmo __instance, EObjectType tarType, int tarId)
        {
            if (Instance != null)
            {
                if (Instance._cursorTextObj == null)
                {
                    Instance._cursorTextObj = GameObject.Find("build-cursor-text")?.GetComponentInChildren<Text>();
                }

                if (tarId != 0 && tarType == EObjectType.Entity)
                {
                    PlanetFactory factory = GameMain.mainPlayer.factory;
                    if (factory != null)
                    {
                        var storageId = factory.entityPool[tarId].storageId;
                        var stationId = factory.entityPool[tarId].stationId;
                        if (storageId > 0)
                        {
                            Instance._cursorText = $"{"存储箱ID".Translate()}: {factory.index}.{storageId}";
                        }
                        else if (stationId > 0)
                        {
                            Instance._cursorText = $"{"物流站ID".Translate()}: S{factory.index}.{stationId}";
                        }
                    }
                }
                else
                {
                    Instance._cursorText = null;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStorageWindow), "_OnClose")]
        public static void _OnStorageWindowClose(UIStorageWindow __instance)
        {
            if (__instance != null &&
                __instance.storageId == Instance._lastStorageId &&
                __instance.factory != null &&
                __instance.factory.index == Instance._lastFactoryIndex)
            {
                Instance._inspectingStorage = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStationWindow), "_OnClose")]
        public static void _OnStationWindowClose(UIStationWindow __instance)
        {
            if (__instance != null &&
                __instance.stationId == Instance._lastStationId &&
                __instance.factory != null &&
                __instance.factory.index == Instance._lastStationFactoryIndex)
            {
                Instance._inspectingStation = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            if (Instance != null)
            {
                Instance.CloseUI();
                Instance._inputText.text = string.Empty;
            }
        }
    }

    // http://gyanendushekhar.com/2019/11/11/move-canvas-ui-mouse-drag-unity-3d-drag-drop-ui/
    public class MouseDragBehaviour : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private Vector2 _lastMousePosition;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _lastMousePosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var currentMousePosition = eventData.position;
            var diff = currentMousePosition - _lastMousePosition;
            var rect = GetComponent<RectTransform>();

            var position = rect.position;
            var newPosition = position + new Vector3(diff.x, diff.y, transform.position.z);
            var oldPos = position;
            position = newPosition;
            rect.position = position;
            if (!IsRectTransformInsideSreen(rect))
            {
                rect.position = oldPos;
            }

            _lastMousePosition = currentMousePosition;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }

        private bool IsRectTransformInsideSreen(RectTransform rectTransform)
        {
            var isInside = false;
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var visibleCorners = 0;
            var rect = new Rect(0, 0, Screen.width, Screen.height);
            foreach (var corner in corners)
            {
                if (rect.Contains(corner))
                {
                    visibleCorners++;
                }
            }

            if (visibleCorners == 4)
            {
                isInside = true;
            }

            return isInside;
        }
    }
}