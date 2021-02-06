using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using UnityEngine.Networking;

namespace Dyson4DPocket
{
    public static class ModStringTranslate
    {
        private static JSONNode _translations;

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
                    var currentLocale = Localization.language.ToString();
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
                }
            }
            catch (Exception e)
            {
                return s;
            }

            return s;
        }
    }

    [BepInPlugin("com.github.yyuueexxiinngg.plugin.dyson.4dpocket", "4D Pocket", "1.0")]
    public class The4DPocket : BaseUnityPlugin
    {
        public static ConfigEntry<KeyCode> HotKey;

        void Start()
        {
            var pocket = new GameObject(typeof(Pocket).FullName).AddComponent<Pocket>();
            Pocket.Instance = pocket;

            Harmony.CreateAndPatchAll(typeof(Pocket));

            HotKey = Config.Bind("config", "HotKey", KeyCode.F5, "插件按键");

            var translationsPath = $"{Paths.GameRootPath}/BepInEx/data/4DPocket/Strings.json";
            if (File.Exists(translationsPath))
            {
                ModStringTranslate.LoadTranslations(File.ReadAllText(translationsPath));
            }
            else
            {
                new FileInfo(translationsPath).Directory?.Create();
                File.WriteAllText(translationsPath,
                    "{\"Version\":1,\"HelpText\":{\"zhCN\":\"${Key}开关此窗口， 游戏启动后输入存储箱ID后按回车开启存储箱\",\"enUS\":\"${Key} to open this window. Enter storage ID and hit Enter to open storage after game loaded.\",\"frFR\":null,\"Other\":null},\"四次元口袋\":{\"enUS\":\"4D Pocket\"},\"检测到更新\":{\"enUS\":\"Update available\"},\"输入存储箱ID\":{\"enUS\":\"Enter storage ID\"},\"请先开始游戏\":{\"enUS\":\"Please start game first\"},\"存储箱ID\":{\"enUS\":\"StorageID\"},\"存储箱ID格式错误, 应为小数(工厂索引.存储箱ID)\":{\"enUS\":\"StorageID format error, should be decimal (FactoryIndex.StorageID)\"},\"存储箱ID不存在\":{\"enUS\":\"StorageID does not exist\"},\"工厂不存在\":{\"enUS\":\"Factory does not exist\"}}");
                ModStringTranslate.LoadTranslations(File.ReadAllText(translationsPath));
            }
        }
    }


    public class Pocket : MonoBehaviour
    {
        public static Pocket Instance;

        private const float Version = 1.0f;

        private bool _uiInitialized;
        private static Harmony _har;

        // Keep reference to unload for Script Loader 
        private AssetBundle _4dAssetBundle;

        private UIGame _uiGame;
        private UIStorageWindow _uiStorage;
        private Text _cursorTextObj;
        private string _cursorText;
        private GameObject _canvas;
        private GameObject _canvasInstance;
        private InputField _inputField;
        private Text _inputText;
        private Text _placeholderText;

        private bool _inspectingStorage;
        private int _lastFactoryIndex = -1;
        private int _lastStorageId = -1;

        private bool _uiActive;

        void InitUI()
        {
            _4dAssetBundle = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Dyson4DPocket.Assets.4dpocket.ab"));

            _canvas = _4dAssetBundle.LoadAsset<GameObject>("4DPocket_Canvas");
            _canvas.SetActive(false);

            _canvasInstance = Instantiate(_canvas);

            DontDestroyOnLoad(_canvas);
            DontDestroyOnLoad(_canvasInstance);

            _inputField = _canvasInstance.transform.Find("4DPocket_Input").GetComponent<InputField>();

            _inputText = _inputField.transform
                .Find("4DPocket_InputText")
                .GetComponent<Text>();

            _placeholderText = _inputField.transform
                .Find("4DPocket_Placeholder")
                .GetComponent<Text>();

            _inputField.transform
                .Find("Panel/4DPocket_Title")
                .GetComponent<Text>().text = "四次元口袋".Translate() + Version.ToString("0.0");

            _inputField.transform
                    .Find("Panel/4DPocket_Help")
                    .GetComponent<Text>().text =
                "HelpText".Translate().Replace("${Key}", The4DPocket.HotKey.Value.ToString());

            _uiInitialized = true;

            StartCoroutine(CheckUpdate());
        }

        void ToggleUI()
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

        void OpenUI()
        {
            // Check if in game
            if (GameMain.isRunning && !GameMain.instance.isMenuDemo)
            {
                _placeholderText.text = "输入存储箱ID".Translate();
                _canvasInstance.SetActive(true);
                _inputField.interactable = true;
                _inputField.ActivateInputField();
                _uiActive = true;
            }
            else
            {
                _placeholderText.text = "请先开始游戏".Translate();
                _inputField.interactable = false;
                _canvasInstance.SetActive(true);
                _uiActive = true;
            }
        }

        void CloseUI()
        {
            if (_canvasInstance != null)
                _canvasInstance.SetActive(false);
            _uiActive = false;
        }

        void OpenStorage(int factoryIndex, int storageId)
        {
            if (factoryIndex >= 0 && storageId >= 0)
            {
                if (GameMain.isRunning && !GameMain.instance.isMenuDemo)
                {
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
                        GameMain.data.factories.Length >= factoryIndex &&
                        GameMain.data.factories[factoryIndex] != null)
                    {
                        try
                        {
                            var factory = GameMain.data.factories[factoryIndex];
                            var factoryStorage = factory.factoryStorage;
                            if (factoryStorage.storagePool != null &&
                                factoryStorage.storagePool.Length >= storageId &&
                                factoryStorage.storagePool[storageId] != null
                            )
                            {
                                _uiStorage.storageId = storageId;
                                Traverse.Create(_uiStorage).Property("active").SetValue(true);
                                if (!_uiStorage.gameObject.activeSelf)
                                {
                                    _uiStorage.gameObject.SetActive(true);
                                }

                                _uiStorage.factory = factory;
                                _uiStorage.factoryStorage = factoryStorage;
                                _uiStorage.player = GameMain.mainPlayer;
                                Traverse.Create(_uiStorage).Method("OnStorageIdChange").GetValue();
                                Traverse.Create(_uiStorage).Field("eventLock").SetValue(true);
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
            }
        }

        void CloseStorage()
        {
            _uiStorage.storageId = 0;
            _inspectingStorage = false;
        }

        IEnumerator CheckUpdate()
        {
            var uri = Localization.language == Language.zhCN
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
                if (float.TryParse(uwr.downloadHandler.text, out var version))
                {
                    if (version > Version)
                    {
                        var updateText = Instance._inputField.transform.Find("Panel/4DPocket_Update")
                            .GetComponent<Text>();
                        updateText.text = $"{"检测到更新".Translate()}: {version}";
                        updateText.gameObject.SetActive(true);
                    }
                }
            }
        }

        public static void Main()
        {
            Debug.Log("4D Pocket loading from ScriptLoader");
            _har = Harmony.CreateAndPatchAll(typeof(Pocket));
            Instance = new GameObject(typeof(Pocket).FullName).AddComponent<Pocket>();
            DontDestroyOnLoad(Instance);
        }

        void Awake()
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

        void Update()
        {
            if (_uiInitialized)
            {
                if (_cursorText != null)
                {
                    _cursorTextObj.text = _cursorText;
                }

                if (Input.GetKeyDown(KeyCode.Return))
                {
                    if (_uiActive)
                    {
                        if (_inputText != null)
                        {
                            var split = _inputText.text.Split('.');
                            if (split.Length == 2 && int.TryParse(split[0], out _lastFactoryIndex) &&
                                int.TryParse(split[1], out _lastStorageId))
                            {
                                if (!_inspectingStorage)
                                {
                                    CloseUI();
                                    OpenStorage(_lastFactoryIndex, _lastStorageId);
                                }
                                else
                                {
                                    CloseUI();
                                    _uiStorage.storageId = 0;
                                    OpenStorage(_lastFactoryIndex, _lastStorageId);
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
                        if (objId != 0)
                        {
                            var factory = GameMain.mainPlayer.factory;
                            var storageId = factory?.entityPool[objId].storageId;
                            if (storageId > 0)
                            {
                                Instance.CloseStorage();
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
                        if (storageId > 0)
                        {
                            Instance._cursorText = $"{"存储箱ID".Translate()}: {factory.index}.{storageId}";
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
        public static void _OnClose(UIStorageWindow __instance)
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
        [HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            if (Instance != null)
            {
                Instance.CloseUI();
                Instance._inputText.text = string.Empty;
            }
        }

        public static void Unload()
        {
            Debug.Log("Unloading 4D Pocket for ScriptLoader");
            _har?.UnpatchAll();
            _har = null;
            if (Instance != null)
            {
                Destroy(Instance);
                Instance = null;
            }

            Instance._uiGame = null;
            Instance._uiStorage = null;
            Instance._cursorTextObj = null;
            if (Instance._canvas != null)
            {
                Destroy(Instance._canvas);
                Instance._canvas = null;
            }

            if (Instance._canvasInstance != null)
            {
                Destroy(Instance._canvasInstance);
                Instance._canvasInstance = null;
            }

            if (Instance._4dAssetBundle != null)
            {
                Instance._4dAssetBundle.Unload(true);
                Instance._4dAssetBundle = null;
            }
        }
    }
}