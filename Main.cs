using BepInEx;
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
using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]


namespace DSPJapanesePlugin
{
    [BepInPlugin("Appun.DSP.plugin.JapanesePlugin", "DSPJapanesePlugin", "1.2.2")]

    public class Main : BaseUnityPlugin
    {
        public static AssetBundle FontAssetBundle { get; set; }

        public static Font newFont { get; set; }

        //public static StringProtoSet _strings;


        public static ConfigEntry<bool> EnableFixUI;

        //private static ConfigEntry<bool> enableShowUnTranslatedStrings;
        //private static ConfigEntry<bool> enableNewWordExport;
        //private static ConfigEntry<bool> enableNewWordUpload;


        public void Awake()
        {
            LogManager.Logger = Logger;
            //LogManager.Logger.LogInfo("DSPJapanesePlugin awake");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());


            EnableFixUI = Config.Bind("表示の修正：アップデートでエラーが出る場合はfalseにすると解消できる可能性があります。", "EnableFixUI", true, "日本語化に伴い発生する表示の問題を修正するか");

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
            //Localization.language = Language.frFR;


        }


    }



    public class LogManager
    {
        public static ManualLogSource Logger;
    }
}