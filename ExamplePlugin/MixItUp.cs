using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Lum_MixItUp {
    public class MixItUp : MonoBehaviour, VNyanInterface.IButtonClickedHandler, VNyanInterface.ITriggerHandler {
        public GameObject windowPrefab;
        private GameObject window;

        // Settings
        private string miuURL = "http://localhost:8911/api/v2/";
        private string platform = "Twitch";
        // private float someValue2 = 5.0f;
        private static HttpClient client = new HttpClient();
        Dictionary<String, String> miuCommands = new Dictionary<string, string>();

        public void Awake() {
            // Register button to plugins window
            VNyanInterface.VNyanInterface.VNyanUI.registerPluginButton("LumKitty's MixItUp Plugin", this);
            VNyanInterface.VNyanInterface.VNyanTrigger.registerTriggerListener(this);

            // Create a window that will show when the button in plugins window is clicked
            window = (GameObject)VNyanInterface.VNyanInterface.VNyanUI.instantiateUIPrefab(windowPrefab);

            // Load settings
            loadPluginSettings();

            // Hide the window by default
            if (window != null) {
                window.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);  
                window.SetActive(false);

                // Set ui component callbacks and loaded values
                //window.GetComponentInChildren<Slider>()?.onValueChanged.AddListener((v) => { someValue2 = v; });
                //window.GetComponentInChildren<Slider>()?.SetValueWithoutNotify(someValue2);

                window.GetComponentInChildren<InputField>()?.onValueChanged.AddListener((v) => { miuURL = v; });
                window.GetComponentInChildren<InputField>()?.SetTextWithoutNotify(miuURL);

            }

        }

        /// <summary>
        /// Load plugin settings
        /// </summary>
        private void loadPluginSettings() {
            // Get settings in dictionary
            Dictionary<string, string> settings = VNyanInterface.VNyanInterface.VNyanSettings.loadSettings("Lum-MixItUp.cfg");
            if (settings != null)
            {
                // Read string value
                settings.TryGetValue("MixItUpURL", out miuURL);
                settings.TryGetValue("DefaultPlatform", out platform);

                // Convert second value to decimal
                //if (settings.TryGetValue("SomeValue2", out string s))
                //{
                //    float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out someValue2);
                //}

            }
        }

        /// <summary>
        /// Called when VNyan is shutting down
        /// </summary>
        private void OnApplicationQuit() {
            // Save settings
            savePluginSettings();
        }

        private void savePluginSettings() {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            settings["MixItUpURL"] = miuURL;
            settings["DefaultPlatform"] = platform;
            // settings["SomeValue2"] = someValue2.ToString(CultureInfo.InvariantCulture); // Make sure to use InvariantCulture to avoid decimal delimeter errors

            VNyanInterface.VNyanInterface.VNyanSettings.saveSettings("Lum-MixItUp.cfg", settings);
        }

        public void pluginButtonClicked() {
            // Flip the visibility of the window when plugin window button is clicked
            if (window != null)
            {
                window.SetActive(!window.activeSelf);
                if(window.activeSelf)
                    window.transform.SetAsLastSibling();
            }
                
        }

        private static string EscapeJSON(string value)
        {
            const char BACK_SLASH = '\\';
            const char SLASH = '/';
            const char DBL_QUOTE = '"';

            var output = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case SLASH:
                        output.AppendFormat("{0}{1}", BACK_SLASH, SLASH);
                        break;

                    case BACK_SLASH:
                        output.AppendFormat("{0}{0}", BACK_SLASH);
                        break;

                    case DBL_QUOTE:
                        output.AppendFormat("{0}{1}", BACK_SLASH, DBL_QUOTE);
                        break;

                    default:
                        output.Append(c);
                        break;
                }
            }

            return output.ToString();
        }
        async Task updateMiuCommands()
        {
            try
            {
                miuCommands.Clear();
                int skip = 0;
                int count = 0;
                do
                {
                    var GetResult = await client.GetAsync(miuURL + "commands?pagesize=10&skip=" + skip.ToString());
                    string Response = GetResult.Content.ReadAsStringAsync().Result;

                    dynamic Results = JsonConvert.DeserializeObject<dynamic>(Response);
                    count = Results.Commands.Count;
                    // Console.WriteLine("Count: " + count.ToString());
                    foreach (dynamic Result in Results.Commands)
                    {
                        // Console.WriteLine(Result.ToString());
                        // Console.WriteLine(Result.Name + " : " + Result.ID);
                        miuCommands.Add(Result.Name.ToString().ToLower(), Result.ID.ToString());
                        // Console.WriteLine("Added");
                    }
                    GetResult.Dispose();
                    skip += 10;
                } while (count >= 10);
            }
            catch //(Exception e)
            {
                //Console.WriteLine(e.Message);
            }
        }
        async Task httpRequest(string Method, string URL, string Content)
        {
            var jsonData = new System.Net.Http.StringContent(Content, Encoding.UTF8, "application/json");
            string Response = "";
            //Console.WriteLine(Method + ": " + URL + " : " + Content);
            switch (Method)
            {
                case "POST":
                    var PostResult = await client.PostAsync(URL, jsonData);
                    Response = PostResult.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(PostResult.ToString());
                    PostResult.Dispose();
                    break;
                case "PUT":
                    var PutResult = await client.PutAsync(URL, jsonData);
                    Response = PutResult.Content.ReadAsStringAsync().Result;
                    PutResult.Dispose();
                    break;
                case "GET":
                    var GetResult = await client.GetAsync(URL);
                    Response = GetResult.Content.ReadAsStringAsync().Result;
                    GetResult.Dispose();
                    break;
                case "PATCH":
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), URL);
                    var PatchResult = await client.SendAsync(request);
                    Response = PatchResult.Content.ReadAsStringAsync().Result;
                    PatchResult.Dispose();
                    break;
            }
            //Console.WriteLine(Response.ToString());
        }

        async Task runMiuCommand(string command, string args) {
            if (!miuCommands.ContainsKey(command))
            {
                await updateMiuCommands();
                if (!miuCommands.ContainsKey(command)) { Console.WriteLine("command not found"); return; }
            }

            string Content = "{ \"Platform\": \"" + platform.ToString() + "\", \"Arguments\": \"" + args + "\" }";
            httpRequest("POST", miuURL + "commands/" + miuCommands[command], Content);
        }

        public void triggerCalled(string name, int val1, int val2, int val3, string val4, string val5, string val6) {
            if (val6.Length == 0) { val6 = platform.ToString(); }

            string URL = "";
            string Method = "";
            string Content = "";
            switch (name)
            {
                case "_lum_miu_chat":
                    URL = miuURL + "chat/message";
                    Method = "POST";
                    Content = "{ \"Message\": \"" + EscapeJSON(val4) + "\", \"Platform\": \"" + platform.ToString() + "\", \"SendAsStreamer\": ";
                    if (val1 > 0)
                    {
                        Content += "true }";
                    }
                    else
                    {
                        Content += "false }";
                    }
                    httpRequest(Method, URL, Content);
                    break;

                case "_lum_miu_command":
                    runMiuCommand(val4.ToLower(), val5);
                    break;
                case "_lum_miu_getcommands":
                //    await updateMiuCommands();

                    break;
            }
        }
        public void Start() { }
    }
}
