using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using Unity.Burst.CompilerServices;
using Oculus.Platform;
using System.Reflection;
using UnityEngine.UI;

namespace YoutubeSubCount
{
	[BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
	public class Plugin : BaseUnityPlugin
	{
		ConfigEntry<string> URL;

        string ChannelName, SubCount;

        static Action BackgroundStuff;

        Sprite PFP;

        GameObject Board;

        void Start()
		{
			URL = Config.Bind("Setup", "Channel", "");
            GorillaTagger.OnPlayerSpawned(delegate
            {
                if (URL.Value != "")
                {
                    BackgroundStuff += actualRefreshSubCount;
                    BackgroundStuff += LoadBundle;
                    BackgroundStuff += GetImage;
                    RefreshSubCount();
                }
            });
		}

        private void LoadBundle()
        {
            using (Stream str = Assembly.GetExecutingAssembly().GetManifestResourceStream("YoutubeSubCount.subboard"))
            {
                var wawa = AssetBundle.LoadFromStreamAsync(str);
                var bundle = wawa.assetBundle;
                if (bundle != null)
                {
                    Board = Instantiate(bundle.LoadAsset<GameObject>("board"));
                    Board.name = "SubBoard";
                    Board.transform.GetChild(1).AddComponent<RefreshButton>();
                    Board.transform.position = new Vector3(-68.999f, 12.3594f, - 84.1853f);
                    Board.transform.localScale = new Vector3(0.2964f, 0.4365f, 0.2495f);
                    Board.transform.localRotation = Quaternion.Euler(270f, 241.5015f, 0);
                    BackgroundStuff -= LoadBundle;
                }
            }
        }

        async void GetImage()
        {
            var CurrentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var texture in Directory.GetFiles(CurrentPath))
            {
                if (texture.ToUpper().Contains("PFP") && texture.ToUpper().Contains(".PNG"))
                {
                    Texture2D tex = new Texture2D(2, 2);
                    var imgdata = await File.ReadAllBytesAsync(texture);
                    tex.LoadImage(imgdata);
                    string name = Path.GetFileNameWithoutExtension(texture);
                    tex.name = name;
                    tex.filterMode = FilterMode.Point;
                    PFP = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
                    if (Board)
                    {
                        Board.transform.GetChild(2).GetChild(2).GetComponent<Image>().sprite = PFP;
                    }
                }
            }
        }

        public static void RefreshSubCount()
        {
            ThreadingHelper.Instance.StartAsyncInvoke(delegate { return BackgroundStuff; });
        }

        void actualRefreshSubCount()
        {
            StartCoroutine(GetSubscriberCount());
        }
        IEnumerator GetSubscriberCount()
		{
            using (UnityWebRequest request = UnityWebRequest.Get(Path.Combine(URL.Value, "about")))
            {
                request.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
                request.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
                request.SetRequestHeader("Accept", "text/html");
                string cookieString = "GPS=1; PREF=f6=40000000&tz=Europe.London; " +
                                      "SOCS=CAISNQgDEitib3FfaWRlbnRpdHlmcm9udGVuZHVpc2VydmVyXzIwMjUwMzAzLjA1X3AwGgJlbiACGgYIgKievgY; " +
                                      "VISITOR_INFO1_LIVE=4EVKZruckm4";
                request.SetRequestHeader("Cookie", cookieString);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError("Getting Page Failed: " + request.error);
                }
                else
                {
                    string html = request.downloadHandler.text;
                    ExtractSubscriberCount(html);
                    ExtractName(html);
                }
            }
        }

        void ExtractName(string html)
        {
            string namePattern = @"<title>(.*?) - YouTube</title>";
            Match nameMatch = Regex.Match(html, namePattern);
            string channelName = nameMatch.Success ? nameMatch.Groups[1].Value : "Not Found";

            ChannelName = channelName;
            if (Board)
            {
                Board.transform.GetChild(2).GetChild(0).GetComponent<TextMeshProUGUI>().text = ChannelName.ToUpper();
            }
        }
        void ExtractSubscriberCount(string html)
        {
            string pattern = @"(\d+)\s+subscribers";
            Match match = Regex.Match(html, pattern);

            if (match.Success)
            {
                string countStr = match.Groups[1].Value.Replace(",", "");
                SubCount = countStr + " SUBS";
                if (Board)
                {
                    Board.transform.GetChild(2).GetChild(1).GetComponent<TextMeshProUGUI>().text = SubCount;
                }
            }
        }


        class RefreshButton : GorillaPressableButton
        {

            public override void Start()
            {
                base.Start();
                gameObject.layer = LayerMask.NameToLayer("GorillaInteractable");
            }
            public override void ButtonActivation()
            {
                RefreshSubCount();
            }
        }
    }
}
