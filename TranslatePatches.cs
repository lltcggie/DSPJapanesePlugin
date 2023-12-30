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

namespace DSPJapanesePlugin
{
    [HarmonyPatch]
    internal class TranslatePatches
    {
        ////リソース全体のTextのフォントを変更   //新規文字列のチェック
        [HarmonyPostfix, HarmonyPatch(typeof(VFPreload), "PreloadThread")]
        public static void VFPreload_PreloadThread_Patch()
        {
            var texts = Resources.FindObjectsOfTypeAll(typeof(Text)) as Text[];
            foreach (var text in texts)
            {
                //フォント
                if (text.font != null && text.font.name != "DIN")
                {
                    text.font = Main.newFont;
                }
            }
            LogManager.Logger.LogInfo("フォントを変更しました");
        }




    }
}
