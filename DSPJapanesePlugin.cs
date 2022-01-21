﻿using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Net;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Networking;
using TranslationCommon.SimpleJSON;
using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]


namespace DSPJapanesePlugin
{
    [BepInPlugin("Appun.DSP.plugin.JapanesePlugin", "DSPJapanesePlugin", "1.1.3")]

    public class DSPJapaneseMod : BaseUnityPlugin
    {
        public static AssetBundle FontAssetBundle { get; set; }

        public static Font newFont { get; set; }
        public static Dictionary<string, string> JPDictionary { get; set; }

        public static StringProtoSet _strings;

        public static bool BeltCheckSignUpdated = false;


        private static ConfigEntry<bool> EnableFixUI;
        private static ConfigEntry<bool> EnableAutoUpdate;
        private static ConfigEntry<bool> ImportSheet;
        private static ConfigEntry<bool> exportNewStrings;
        private static ConfigEntry<string> DictionaryGAS;
        private static ConfigEntry<string> SsheetGAS;

        //private static ConfigEntry<bool> enableShowUnTranslatedStrings;
        //private static ConfigEntry<bool> enableNewWordExport;
        //private static ConfigEntry<bool> enableNewWordUpload;

        public static string PluginPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        public static string jsonFilePath = Path.Combine(PluginPath, "translation_DysonSphereProgram.json");
        public static string newStringsFilePath = Path.Combine(PluginPath, "newStrings.tsv");


        public void Awake()
        {
            LogManager.Logger = Logger;
            //LogManager.Logger.LogInfo("DSPJapanesePlugin awake");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            EnableFixUI = Config.Bind("表示の修正：アップデートでエラーが出る場合はfalseにすると解消できる可能性があります。", "EnableFixUI", true, "日本語化に伴い発生する表示の問題を修正するか");
            EnableAutoUpdate = Config.Bind("辞書自動アップデート：起動時に日本語辞書ファイルを自動でダウンロードすることができます。", "EnableAutoUpdate", true, "起動時に日本語辞書ファイルを自動でアップデートするかどうか");
            ImportSheet = Config.Bind("翻訳者、開発者向けの設定：基本的に変更しないでください。", "ImportSheet", false, "翻訳作業所のシートのデータを取り込んで辞書ファイルを作るかどうか");
            DictionaryGAS = Config.Bind("翻訳者、開発者向けの設定：基本的に変更しないでください。", "DictionaryGAS", "https://script.google.com/macros/s/AKfycbwRjiRA6PUeh02MOQ6ccWfbhkQ3wW_qxM6MEl_UXcltGHnU59GLhIOcNNoM35NS7N7_/exec", "日本語辞書ファイル取得のスクリプトアドレス");
            SsheetGAS = Config.Bind("翻訳者、開発者向けの設定：基本的に変更しないでください。", "SsheetGAS", "https://script.google.com/macros/s/AKfycbxOATSa3MHENWQfWc8Ti6XLK-yx-HjzvoLMnO7S2u2nKuZYrRrD3Luh2NLA6jehgf1RUQ/exec", "翻訳作業所のシート取得のスクリプトアドレス");
            exportNewStrings = Config.Bind("翻訳者、開発者向けの設定：基本的に変更しないでください。", "exportNewStrings", false, "バージョンアップ時に新規文字列を翻訳作業所用に書き出すかどうか。");

            //辞書ファイルのダウンロード
            if (EnableAutoUpdate.Value)
            {
                if (!ImportSheet.Value) //Jsonを直接ダウンロード
                {
                    LogManager.Logger.LogInfo("完成済みの辞書をダウンロードします");
                    IEnumerator coroutine = CheckAndDownload(DictionaryGAS.Value, jsonFilePath);
                    ////IEnumerator coroutine = DownloadAndSave(DictionaryGAS.Value, jsonFilePath);
                    coroutine.MoveNext();
                }
                else //スプレッドシートからjson作成
                {
                    LogManager.Logger.LogInfo("辞書を作業所スプレッドシートから作成します");
                    IEnumerator coroutine = MakeFromSheet(SsheetGAS.Value, jsonFilePath);
                    coroutine.MoveNext();
                }
            }
            else
            {
                LogManager.Logger.LogInfo("辞書を既存のファイルから読み込みます");
                //LogManager.Logger.LogInfo("target path " + jsonFilePath);
                if (!File.Exists(jsonFilePath))
                {
                    LogManager.Logger.LogInfo("File not found" + jsonFilePath);
                }
                JPDictionary = JSON.FromJson<Dictionary<string, string>>(File.ReadAllText(jsonFilePath));



            }

            //フォントの読み込み
            try
            {
                var assetBundle = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("DSPJapanesePlugin.newjpfont"));
                if (assetBundle == null)
                {
                    LogManager.Logger.LogInfo("Asset Bundle not loaded.");
                }
                else
                {
                    FontAssetBundle = assetBundle;
                    newFont = FontAssetBundle.LoadAsset<Font>("MPMK85");
                    LogManager.Logger.LogInfo("フォントを読み込みました : " + newFont);
                }
            }
            catch (Exception e)
            {
                LogManager.Logger.LogInfo("e.Message " + e.Message);
                LogManager.Logger.LogInfo("e.StackTrace " + e.StackTrace);
            }
            //言語の設定
            Localization.language = Language.frFR;

            //UIの修正
            //fixUI();
        }

        //辞書ファイルの更新チェック＆ダウンロードコルーチン
        public IEnumerator CheckAndDownload(String Url, String dstPath)
        {
            string LastUpdate;
            if (!File.Exists(dstPath))
            {
                LastUpdate = "0";
            }
            else
            {
                LastUpdate = System.IO.File.GetLastWriteTime(dstPath).ToString("yyyyMMddHHmmss");
            }
            LogManager.Logger.LogInfo("URL : " + $"{Url}?date={LastUpdate}");
            UnityWebRequest request = UnityWebRequest.Get($"{Url}?date={LastUpdate}");
            request.timeout = 10;
            AsyncOperation checkAsync = request.SendWebRequest();
            while (!checkAsync.isDone) ;
            if (request.isNetworkError || request.isHttpError)
            {
                LogManager.Logger.LogInfo("辞書チェックエラー : " + request.error);
            }
            else
            {
                if (request.downloadHandler.text == "match")
                {
                    LogManager.Logger.LogInfo("辞書は最新です");
                    JPDictionary = JSON.FromJson<Dictionary<string, string>>(File.ReadAllText(dstPath));
                }
                else if (request.downloadHandler.data.Length < 2000)
                {
                    LogManager.Logger.LogInfo("辞書のダウンロードに失敗しました　:　" + Regex.Match(request.downloadHandler.text, @"TypeError.*）"));
                    if (!File.Exists(dstPath))
                    {
                        LogManager.Logger.LogInfo("File not found" + dstPath);
                    }
                    JPDictionary = JSON.FromJson<Dictionary<string, string>>(File.ReadAllText(dstPath));
                }
                else
                {

                    LogManager.Logger.LogInfo("辞書をダウンロードしました");
                    JPDictionary = JSON.FromJson<Dictionary<string, string>>(request.downloadHandler.text);
                    File.WriteAllText(dstPath, request.downloadHandler.text);
                }
            }
            yield return null;
        }

        //辞書ファイルをスプレッドシートから取得コルーチン
        public IEnumerator MakeFromSheet(String Url, String dstPath)
        {
            LogManager.Logger.LogInfo("URL : " + Url);

            UnityWebRequest request = UnityWebRequest.Get($"{Url}");
            request.timeout = 10;
            AsyncOperation checkAsync = request.SendWebRequest();
            while (!checkAsync.isDone) ;

            if (request.isNetworkError || request.isHttpError)
            {
                LogManager.Logger.LogInfo("辞書チェックエラー : " + request.error);
            }
            else if (request.downloadHandler.data.Length < 2000)
            {
                LogManager.Logger.LogInfo("辞書のダウンロードに失敗しました　:　" + Regex.Match(request.downloadHandler.text, @"TypeError.*）"));

            }
            else
            {
                LogManager.Logger.LogInfo("辞書をダウンロードしました");
                var strings = request.downloadHandler.text;
                JPDictionary = JSON.FromJson<Dictionary<string, string>>(strings.Replace("[LF]", @"\n").Replace("[CRLF]", @"\r\n").Replace("\n", "\\\n"));
                File.WriteAllText(dstPath, strings);

            }
            yield return null;
        }

        //辞書ファイルのダウンロードコルーチン
        public IEnumerator DownloadAndSave(String Url, String dstPath)
        {
            UnityWebRequest request = UnityWebRequest.Get(Url);

            AsyncOperation checkAsync = request.SendWebRequest();

            while (!checkAsync.isDone) ;

            if (request.isNetworkError || request.isHttpError)
            {
                LogManager.Logger.LogInfo("Dictionary download error : " + request.error);
            }
            else
            {
                LogManager.Logger.LogInfo("Dictionary downloaded");
                File.WriteAllText(dstPath, request.downloadHandler.text);
                LogManager.Logger.LogInfo("Dictionary saved ");
            }
            yield return null;
        }

        //コンボボックスへ「日本語」を追加
        [HarmonyPatch(typeof(UIOptionWindow), "TempOptionToUI")]
        public static class UIOptionWindow_TempOptionToUI_Harmony
        {
            [HarmonyPrefix]
            public static void Prefix(UIOptionWindow __instance)
            {
                if (!__instance.languageComp.Items.Contains("日本語"))
                {
                    __instance.languageComp.Items.Add("日本語");
                    __instance.languageComp.itemIndex = 2;
                }
            }
        }

        //翻訳メイン
        [HarmonyPatch(typeof(StringTranslate), "Translate", typeof(string))]
        public static class StringTranslate_Translate_Prefix
        {
            [HarmonyPrefix]
            public static bool Prefix(ref string __result, string s)
            {
                if (Localization.language == Language.frFR)
                {
                    if (s == null)
                    {
                        return true;
                    }

                    if (JPDictionary.ContainsKey(s))
                    {
                        __result = JPDictionary[s];
                        return false;
                    }
                }
                return true;
            }
        }

        //リソース全体のTextのフォントを変更   //新規文字列のチェック
        [HarmonyPatch(typeof(VFPreload), "PreloadThread")]
        public static class VFPreload_PreloadThread_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                LogManager.Logger.LogInfo("フォントを変更しました");
                var texts = Resources.FindObjectsOfTypeAll(typeof(Text)) as Text[];
                foreach (var text in texts)
                {
                    text.font = newFont;
                    //HyphenationJpn HyphenationText = text.gameObject.AddComponent<HyphenationJpn>();

                    if (JPDictionary.ContainsKey(text.text))
                    {
                        text.text = JPDictionary[text.text];
                    }
                    //HyphenationText.text = text.text;


                }

                //新規文字列のチェック
                if (exportNewStrings.Value)
                {
                    LogManager.Logger.LogInfo("新規文字列をチェックします");
                    string path = LDB.protoResDir + typeof(StringProtoSet).Name;
                    StringProtoSet strings = (Resources.Load(path) as StringProtoSet);
                    StringProtoSet stringProtoSet = Localization.strings;
                    var tsvText = new StringBuilder();

                    for (int i = 0; i < strings.Length; i++)
                    {

                        if (!JPDictionary.ContainsKey(strings[i].Name))
                        {
                            StringProto stringProto = strings[strings[i].Name];
                            string enUS = stringProto.ENUS.Replace("\n", "[LF]").Replace("\r\n", "[CRLF]");
                            string zhCN = stringProto.ZHCN.Replace("\n", "[LF]").Replace("\r\n", "[CRLF]");
                            string frFR = stringProto.FRFR.Replace("\n", "[LF]").Replace("\r\n", "[CRLF]");

                            tsvText.Append($"\t\t{strings[i].Name}\t=googletranslate(\"{enUS}\",\"en\",\"ja\")\tnew\t\t{enUS}\t{zhCN}\t{frFR}\r\n");
                            LogManager.Logger.LogInfo($"新規文字列 {i} : {strings[i].Name} : {enUS}");
                        }
                    }
                    if (tsvText.Length == 0)
                    {
                        LogManager.Logger.LogInfo("新規文字列はありません");
                    }
                    else
                    {
                        LogManager.Logger.LogInfo($"新規文字列がありましたので、{newStringsFilePath}に書き出しました。");
                    }

                    File.WriteAllText(newStringsFilePath, tsvText.ToString());
                }
            }
        }

        //未翻訳のMODアイテム名と説明分、MOD技術名と説明文の翻訳  新規文字列チェック
        [HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]
        public static class VFPreload_InvokeOnLoadWorkEnded_Patch
        {
            [HarmonyPostfix]
            [HarmonyPriority(1)]
            public static void Postfix()
            {
                //未翻訳のMODアイテム名と説明分、MOD技術名と説明文の翻訳
                if (Localization.language == Language.frFR)
                {
                    foreach (var item in LDB.items.dataArray)
                    {
                        if (item == null || item.name == null || item.description == null)
                            continue;

                        if (JPDictionary.ContainsKey(item.name))
                        {
                            item.name = JPDictionary[item.name];
                        }
                        if (JPDictionary.ContainsKey(item.description))
                        {
                            item.description = JPDictionary[item.description];
                        }
                    }

                    foreach (var tech in LDB.techs.dataArray)
                    {
                        if (tech == null || tech.name == null || tech.description == null)
                            continue;

                        if (JPDictionary.ContainsKey(tech.name))
                        {
                            tech.name = JPDictionary[tech.name];
                        }
                        if (JPDictionary.ContainsKey(tech.description))
                        {
                            tech.description = JPDictionary[tech.description];
                        }
                    }
                    LogManager.Logger.LogInfo("MODを翻訳しました");








                }
            }
        }

        //modのUIの翻訳テスト
        //[HarmonyPatch(typeof(VFPreload), "PreloadThread")]
        public static class VFPreload_PreloadThread_Patch2
        {
            [HarmonyPostfix]
            [HarmonyPriority(1)]

            public static void Postfix()
            {
                LogManager.Logger.LogInfo("call VFPreload PreloadThread.");

                var texts = Resources.FindObjectsOfTypeAll(typeof(Text)) as Text[];
                foreach (var text in texts)
                {
                    text.font = newFont;
                    if (JPDictionary.ContainsKey(text.text))
                    {
                        text.text = JPDictionary[text.text];
                    }

                }
            }

        }


        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////表示の修正//////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////



        //メカエディタ表示の調整
        [HarmonyPatch(typeof(UIMechaEditor), "_OnInit")]
        public static class UIMechaEditorl_OnInit_PostPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (EnableFixUI.Value)
                {
                    GameObject Text1 = GameObject.Find("UI Root/Overlay Canvas/Mecha Editor UI/Left Panel/scroll-view/Viewport/Left Panel Content/part-group/disable-all-button/Text");
                    Text1.transform.localScale = new Vector3(0.7f, 1f, 1f);
                    GameObject Text2 = GameObject.Find("UI Root/Overlay Canvas/Mecha Editor UI/Left Panel/scroll-view/Viewport/Left Panel Content/part-group/enable-all-button/Text");
                    Text2.transform.localScale = new Vector3(0.7f, 1f, 1f);
                    GameObject Text3 = GameObject.Find("UI Root/Overlay Canvas/Mecha Editor UI/Left Panel/scroll-view/Viewport/Left Panel Content/bone-group/disable-all-button/Text");
                    Text3.transform.localScale = new Vector3(0.7f, 1f, 1f);
                    GameObject Text4 = GameObject.Find("UI Root/Overlay Canvas/Mecha Editor UI/Left Panel/scroll-view/Viewport/Left Panel Content/bone-group/enable-all-button/Text");
                    Text4.transform.localScale = new Vector3(0.7f, 1f, 1f);
                }
            }
        }



        //ダイソンスフィアエディタ表示の調整
        [HarmonyPatch(typeof(UIDESwarmPanel), "_OnInit")]
        public static class UIDESwarmPanel_OnInit_PostPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (EnableFixUI.Value)
                {
                    GameObject inEditorText1 = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/swarm/display-group/display-toggle-1/checkbox-editor/in-editor-text");
                    inEditorText1.transform.localPosition = new Vector3(35f, 0f, 0f);
                    inEditorText1.transform.localScale = new Vector3(0.7f, 1f, 1f);
                    GameObject inGameText1 = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/swarm/display-group/display-toggle-1/checkbox-game/in-game-text");
                    inGameText1.transform.localPosition = new Vector3(33f, 0f, 0f);
                    inGameText1.transform.localScale = new Vector3(0.8f, 1f, 1f);
                    GameObject displayText = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/swarm/display-group/display-toggle-2/display-text");
                    displayText.transform.localScale = new Vector3(0.66f, 1f, 1f);
                    GameObject inEditorText2 = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/swarm/display-group/display-toggle-2/checkbox-editor/in-editor-text");
                    inEditorText2.transform.localPosition = new Vector3(35f, 0f, 0f);
                    inEditorText2.transform.localScale = new Vector3(0.7f, 1f, 1f);
                    GameObject inGameText2 = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/swarm/display-group/display-toggle-2/checkbox-game/in-game-text");
                    inGameText2.transform.localPosition = new Vector3(33f, 0f, 0f);
                    inGameText2.transform.localScale = new Vector3(0.8f, 1f, 1f);
                    GameObject inEditorText3 = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/layers/display-group/display-toggle-1/checkbox-editor/in-editor-text");
                    inEditorText3.transform.localScale = new Vector3(0.7f, 1f, 1f);
                    GameObject inEditorText4 = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/layers/display-group/display-toggle-2/checkbox-editor/in-editor-text");
                    inEditorText4.transform.localScale = new Vector3(0.7f, 1f, 1f);
                }
            }
        }


        //組み立て機等のアラームボタンの調整
        [HarmonyPatch]
        public static class alarmSwitchButton_Patch
        {
            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(UIAssemblerWindow), "_OnInit");
                yield return AccessTools.Method(typeof(UIMinerWindow), "_OnInit");
                yield return AccessTools.Method(typeof(UIPowerGeneratorWindow), "_OnInit");
                yield return AccessTools.Method(typeof(UISiloWindow), "_OnInit");
                yield return AccessTools.Method(typeof(UILabWindow), "_OnInit");
                yield return AccessTools.Method(typeof(UIVeinCollectorPanel), "_OnInit");
            }
            [HarmonyPostfix]
            public static void Postfix(ref UIButton ___alarmSwitchButton)
            {
                if (EnableFixUI.Value)
                {
                    ___alarmSwitchButton.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 22);
                    ___alarmSwitchButton.transform.Find("alarm-state-text").transform.localPosition = new Vector3(44, 9, 0);
                }
            }
        }

        //マイルストーンの説明文で、惑星名のスペースで改行されてしまう問題の解消
        [HarmonyPatch(typeof(CommonUtils), "ToNonBreakingString")]
        public static class CommonUtils_ToNonBreakingString_PrePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref string __result, string str)
            {
                if (str.Contains(" "))
                {
                    __result = str.Replace(" ", "\u00a0");
                    return false;
                }
                if (str.Contains("_"))
                {
                    __result = str.Replace("_", " ");
                    return false;
                }
                return true;
            }
        }

        //SailIndicatorの日本語化
        [HarmonyPatch(typeof(UISailIndicator), "_OnInit")]
        public static class UISailIndicator_OnInit_PostPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UISailIndicator __instance)
            {
                if (EnableFixUI.Value)
                {
                    GameObject SailIndicator = GameObject.Find("UI Root/Auxes/Sail Indicator/group");
                    SailIndicator.transform.Find("labels").GetComponent<TextMesh>().text = "\n\n\n\n\n到着まで\n偏角\n方位角                                   仰俯角";
                    SailIndicator.transform.Find("labels").GetComponent<TextMesh>().transform.localScale = new Vector3(0.7f, 1, 1);
                    SailIndicator.transform.Find("dist").transform.position = new Vector3(0.71f, -0.565f, 0);
                    SailIndicator.transform.Find("eta").transform.position = new Vector3(1.1f, -1.723f, 0);
                    SailIndicator.transform.Find("bias").transform.position = new Vector3(1.1f, -2.067f, 0);
                    SailIndicator.transform.Find("yaw").transform.position = new Vector3(1.1f, -2.411f, 0);
                    SailIndicator.transform.Find("yaw-sign").transform.position = new Vector3(1f, -2.44f, 0);
                    SailIndicator.transform.Find("pitch").transform.position = new Vector3(3.5f, -2.411f, 0);
                    SailIndicator.transform.Find("pitch-sign").transform.position = new Vector3(3.4f, -2.44f, 0);
                }
            }
        }
        //ブループリント保存画面のUI修正
        [HarmonyPatch(typeof(UIBlueprintBrowser), "_OnOpen")]
        public static class UIBlueprintInspector__OnOpen_Harmony
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (EnableFixUI.Value)
                {
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Blueprint Browser/inspector-group/delete-button").GetComponent<RectTransform>().sizeDelta = new Vector2(170, 30);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Blueprint Browser/inspector-group/group-1/thumbnail-image/layout-combo/label").GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Blueprint Browser/folder-info-group/delete-button").GetComponent<RectTransform>().sizeDelta = new Vector2(170, 30);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Blueprint Copy Mode/Blueprint Copy Inspector/group-1/thumbnail-image/layout-combo/label").GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Blueprint Copy Mode/Blueprint Copy Inspector/group-1/save-state-text").transform.localPosition = new Vector3(80, -30, 0);
                }
            }
        }

        //トラフィックモニタの表示調整
        [HarmonyPatch(typeof(UIMonitorWindow), "_OnInit")]
        public static class UIMonitorWindow_OnInit_PostPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (EnableFixUI.Value)
                {
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/speaker-panel/volume/label").transform.localScale = new Vector3(0.8f, 1, 1);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/system-mode/system-label").transform.localScale = new Vector3(0.7f, 1, 1);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/system-mode/system-label").GetComponent<RectTransform>().sizeDelta = new Vector2(120, 30);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/system-mode/system-mode-box/Main Button").GetComponent<RectTransform>().sizeDelta = new Vector2(10, 0);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/speaker-mode/speaker-label").transform.localScale = new Vector3(0.7f, 1, 1);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/speaker-mode/speaker-label").GetComponent<RectTransform>().sizeDelta = new Vector2(120, 30);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/speaker-mode/speaker-mode-box/Main Button").GetComponent<RectTransform>().sizeDelta = new Vector2(10, 0);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/signal/icon-tag-label").transform.localScale = new Vector3(0.7f, 1, 1);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/alarm-settings/signal/icon-tag-label").GetComponent<RectTransform>().sizeDelta = new Vector2(120, 24);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/monitor-settings/cargo-filter/icon-tag-label").transform.localScale = new Vector3(0.7f, 1, 1);
                    GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Monitor Window/monitor-settings/cargo-filter/icon-tag-label").GetComponent<RectTransform>().sizeDelta = new Vector2(120, 24);
                }
            }
        }

        //建築メニューの[☑コンベアベルト]の位置調整
        [HarmonyPatch(typeof(UIBuildMenu), "UpdateUXPanel")]
        public static class UIBuildMenu_UpdateUXPanels_PrePatch
        {
            [HarmonyPrefix]

            public static void Prefix(Image ___uxBeltCheckSign, UIButton ___uxBeltCheck)
            {
                if (!BeltCheckSignUpdated)
                {
                    ___uxBeltCheck.transform.Translate(-0.4f, 0, 0);
                    BeltCheckSignUpdated = true;
                }

            }

        }

        //セーブ＆ロード確認MessageBoxのフォント変更
        //新しく作られるのでフォントの変更
        [HarmonyPatch(typeof(UIDialog), "CreateDialog")]
        public static class UIMessageBox_Show_Patch
        {
            [HarmonyPostfix]
            public static void Postfix() //UIDialog __result)
            {
                if (EnableFixUI.Value)
                {
                    var texts = GameObject.Find("UI Root/Overlay Canvas/DialogGroup/MessageBox VE(Clone)/Window/Body").GetComponentsInChildren<Text>();
                    //var texts = Resources.FindObjectsOfTypeAll(typeof(Text)) as Text[];
                    foreach (var text in texts)
                    {
                        text.font = newFont;
                    }
                }
            }
        }

        //UIRandomTipのフック：バルーンチップのサイズ調整
        [HarmonyPatch(typeof(UIRandomTip), "_OnOpen")]
        static class UIRandomTip_OnOpen_Postfix
        {
            [HarmonyPostfix]
            static void Postfix(RectTransform ___balloonTrans)
            {
                if (EnableFixUI.Value)
                {
                    ___balloonTrans.sizeDelta = new Vector2(290.0f, ___balloonTrans.sizeDelta.y - 18f);
                    //___balloonTrans.gameObject.GetComponentInParent<Text>().text = HyphenationJpn.GetFormatedText(___balloonTrans.gameObject.GetComponentInParent<Text>(), ___balloonTrans.gameObject.GetComponentInParent<Text>().text);
                }
            }
        }

        // UITechNodeのフック：テックツリーの技術名の位置調整 by aki9284
        [HarmonyPatch(typeof(UITechNode), "UpdateLayoutDynamic")]
        static class UITechNodePatch
        {
            [HarmonyPostfix]
            static void Postfix(Text ___titleText2, Text ___techDescText)
            {
                if (EnableFixUI.Value)
                {
                    ___titleText2.rectTransform.anchoredPosition = new Vector2(0, 10.0f);
                }
            }
        }

        //新規開始画面の恒星タイプ名の文字位置調整
        [HarmonyPatch(typeof(UIGalaxySelect), "_OnOpen")]
        static class UpdateUIDisplayPatch
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (EnableFixUI.Value)
                {
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/m-star").GetComponent<Text>().text = "　　　　　　　" + "M型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/k-star").GetComponent<Text>().text = "　　　　　　　" + "K型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/g-star").GetComponent<Text>().text = "　　　　　　　" + "G型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/f-star").GetComponent<Text>().text = "　　　　　　　" + "A型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/a-star").GetComponent<Text>().text = "　　　　　　　" + "B型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/b-star").GetComponent<Text>().text = "　　　　　　　" + "O型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/o-star").GetComponent<Text>().text = "　　　　　　　" + "M型恒星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/n-star").GetComponent<Text>().text = "　　　　　　　" + "空格中子星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/wd-star").GetComponent<Text>().text = "　　　　　　　" + "空格白矮星".Translate();
                    GameObject.Find("UI Root/Overlay Canvas/Galaxy Select/right-group/bh-star").GetComponent<Text>().text = "　　　　　　　" + "空格黑洞".Translate();
                }
            }
        }


        //UIAssemblerWindowのフック：コピー＆ペーストボタンのサイズ拡大
        [HarmonyPatch(typeof(UIAssemblerWindow), "_OnOpen")]
        static class UIAssemblerWindowPatch
        {
            [HarmonyPostfix]
            static void Postfix(UIButton ___resetButton, UIButton ___copyButton, UIButton ___pasteButton)
            {
                if (EnableFixUI.Value)
                {

                    //LogManager.Logger.LogInfo("copyButton");
                    Text copyText = ___copyButton.GetComponent<Text>();
                    if (copyText != null)
                    {
                        float width = copyText.preferredWidth;
                        float height = copyText.preferredHeight;

                        RectTransform trs = (RectTransform)___copyButton.button.transform;

                        trs.offsetMin = new Vector2(-35, trs.offsetMin.y);
                        trs.offsetMax = new Vector2(35, trs.offsetMax.y);
                    }
                    // LogManager.Logger.LogInfo("pasteButton");
                    Text pasteText = ___pasteButton.GetComponent<Text>();
                    if (pasteText != null)
                    {
                        RectTransform trs = (RectTransform)___pasteButton.button.transform;
                        trs.offsetMin = new Vector2(10, trs.offsetMin.y);
                        trs.offsetMax = new Vector2(80, trs.offsetMax.y);
                    }
                }
            }
        }


    }



    public class LogManager
    {
        public static ManualLogSource Logger;
    }
}