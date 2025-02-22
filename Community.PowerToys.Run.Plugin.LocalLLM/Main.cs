using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Wox.Plugin;
using ManagedCommon;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Library;
using Clipboard = System.Windows.Clipboard;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using HtmlAgilityPack;

namespace Community.PowerToys.Run.Plugin.LocalLLM
{
    public class Main : IPlugin, IDelayedExecutionPlugin, ISettingProvider, IContextMenu
    { 
        public static string PluginID => "550A34D0CFA845449989D581149B3D9C";
        public string Name => "LocalLLM";
        public string Description => "Uses Local LLM to output answer";
        private static readonly HttpClient client = new HttpClient();
        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }

        private string Endpoint, Model;
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "LLMEndpoint",
                DisplayLabel = "LLM Endpoint",
                DisplayDescription = "Enter the endpoint of your LLM model. Ex. http://localhost:11434/api/generate",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "http://localhost:11434/api/generate",

            },
            new PluginAdditionalOption()
            {
                Key = "Model",
                DisplayLabel = "Model",
                DisplayDescription = "Enter the Model to be used in Ollama. Ex. llama3.1(default). Make sure to pull model in ollama before using here.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "llama3.1",
            }
        };
        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings != null && settings.AdditionalOptions != null)
            {
                Endpoint = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "LLMEndpoint")?.TextValue ?? "http://localhost:11434/api/generate";
                Model = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "Model")?.TextValue ?? "llama3.1";
            }
        }

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
            var model = Model;
            if (input.StartsWith(">"))
            {
                var segments = input.Split('>');
                if (segments.Length == 3)
                {
                    model = segments[1].Trim();
                    input = segments[2].Trim();
                }
            }


            if (!IsValidModelAsync(model).Result)
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "Error",
                        SubTitle = $"Model '{model}' not found in Ollama's library.",
                        IcoPath = "Images\\icon.png",
                        Action = _ => { return true; }
                    }
                };
            }
            Model = model;
            var response = QueryLLMStreamAsync(input).Result;

            return new List<Result>
            {
                new Result
                {
                    Title = $"LLM Response from model : {Model}",
                    SubTitle = response,
                    IcoPath = "Images\\icon.png",
                    Action = e =>
                    {
                        Context.API.ShowMsg($"LLM Response from model : {Model}", response);
                        return true;
                    },
                    ContextData = new Dictionary<string, string> { { "copy", response } }
                }
            };
        }


        public async Task<string> QueryLLMStreamAsync(string input)
        {
            var endpointUrl = Endpoint;

            var requestBody = new
            {
                model = Model, // Replace with your model
                prompt = input,
                stream = true
            };

            try
            {
                var response = await client.PostAsync(endpointUrl, new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8, "application/json"));

                response.EnsureSuccessStatusCode();

                var responseStream = await response.Content.ReadAsStreamAsync();
                using (var streamReader = new System.IO.StreamReader(responseStream))
                {
                    string? line;
                    string finalResponse = "";

                    while ((line = await streamReader.ReadLineAsync()) != null)
                    {
                        var json = JsonSerializer.Deserialize<JsonElement>(line);
                        var part = json.GetProperty("response").GetString();
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
        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            List<ContextMenuResult> results = [];

            if (selectedResult?.ContextData is Dictionary<string, string> contextData)
            {
                if (contextData.ContainsKey("copy"))
                {
                    results.Add(
                        new ContextMenuResult
                        {
                            PluginName = Name,
                            Title = "Copy (Enter)",
                            FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                            Glyph = "\xE8C8",
                            AcceleratorKey = Key.Enter,
                            Action = _ =>
                            {
                                Clipboard.SetText(contextData["copy"].ToString());
                                return true;
                            }
                        }
                    );
                }
            }
            return results;

        }
        public async Task<bool> IsValidModelAsync(string model)
        {
            var modelList = await FetchModelsFromEndpointAsync();
            return modelList.Contains(model);
        }

        private async Task<List<string>> FetchModelsFromEndpointAsync()
        {
            try
            {
                string apiUrl = Endpoint.Replace("/generate", "/tags");
                var response = await client.GetStringAsync(apiUrl);
                var jsonDocument = JsonDocument.Parse(response);
                var models = jsonDocument.RootElement.GetProperty("models").EnumerateArray();

                List<string> modelNames = new List<string>();
                foreach (var model in models)
                {
                    var modelName = model.GetProperty("name").GetString().Split(':')[0];
                    modelNames.Add(modelName);
                }
                return modelNames;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching models from API: {ex.Message}");
                return new List<string>();
            }
        }


    }

}
