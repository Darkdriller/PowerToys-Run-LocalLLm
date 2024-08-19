using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Wox.Plugin;
using ManagedCommon;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin.Logger;
using Clipboard = System.Windows.Clipboard;

namespace Community.PowerToys.Run.Plugin.LocalLLM
{
    public class Main : IPlugin, IDelayedExecutionPlugin
    {

        public static string PluginID => "550A34D0CFA845449989D581149B3D9C";
        public string Name => "LocalLLM";
        public string Description => "Uses Local LLM to output answer";
        private static readonly HttpClient client = new HttpClient();
        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }
        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }
        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);
        private void UpdateIconPath(Theme theme) => IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? Context?.CurrentPluginMetadata.IcoPathLight : Context?.CurrentPluginMetadata.IcoPathDark;
        public List<Result> Query(Query query, bool delayedExecution)
        {
            var input = query.Search;
            var response = QueryLLMStreamAsync(input).Result;

            return new List<Result>
            {
                new Result
                {
                    Title = "LLM Response",
                    SubTitle = response,
                    IcoPath = "Images\\app.png",
                    Action = e =>
                    {
                        Context.API.ShowMsg("LLM Response", response);
                        return true;
                    }
                }
            };
        }

        public async Task<string> QueryLLMStreamAsync(string input)
        {
            var endpointUrl = "http://localhost:11434/api/generate";
            var requestBody = new
            {
                model = "llama3.1", // Replace with your model
                prompt = input,
                stream = true
            };

            try
            {
                var response = await client.PostAsync(endpointUrl, new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(requestBody),
                    System.Text.Encoding.UTF8, "application/json"));

                response.EnsureSuccessStatusCode();

                var responseStream = await response.Content.ReadAsStreamAsync();
                using (var streamReader = new System.IO.StreamReader(responseStream))
                {
                    string? line;
                    string finalResponse = "";

                    while ((line = await streamReader.ReadLineAsync()) != null)
                    {
                        var json = JObject.Parse(line);
                        var part = json["response"]?.ToString();
                        finalResponse += part;
                    }

                    return finalResponse;
                }
            }
            catch (Exception ex)
            {
                return $"Error querying LLM: {ex.Message}";
            }
        }
        public List<Result> Query(Query query)
        {
            List<Result> results = [];
            return results;
        }
    }
}
