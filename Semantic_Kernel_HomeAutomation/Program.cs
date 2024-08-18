// See https://aka.ms/new-console-template for more information
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Planning.Handlebars;
using plugins;
public class Program
{
    private static Kernel _kernel;
    private static SecretClient keyVaultClient;
    public async static Task Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
                      .AddUserSecrets<Program>()
                      .Build();

        string? appTenant = config["appTenant"];
        string? appId = config["appId"] ?? null;
        string? appPassword = config["appPassword"] ?? null;
        string? keyVaultName = config["KeyVault"] ?? null;

        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        ClientSecretCredential credential = new ClientSecretCredential(appTenant, appId, appPassword);
        keyVaultClient = new SecretClient(keyVaultUri, credential);
        string? apiKey = keyVaultClient.GetSecret("OpenAIapiKey").Value.Value;
        string? orgId = keyVaultClient.GetSecret("OpenAIorgId").Value.Value;

        var _builder = Kernel.CreateBuilder()
           .AddOpenAIChatCompletion("gpt-3.5-turbo", apiKey, orgId, serviceId: "gpt35")
            .AddOpenAIChatCompletion("gpt-4", apiKey, orgId, serviceId: "gpt4");
        _builder.Plugins.AddFromType<HomeAutomation>();
        _kernel = _builder.Build();
        var pluginsDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "plugins", "MovieRecommender");
        _kernel.ImportPluginFromPromptDirectory(pluginsDirectory);
#pragma warning disable SKEXP0060
        var plannerOptions = new HandlebarsPlannerOptions()
        {
            ExecutionSettings = new OpenAIPromptExecutionSettings()
            {
                Temperature = 0.0,
                TopP = 0.1,
                MaxTokens = 4000
            },
            AllowLoops = true
        };
        var planner = new HandlebarsPlanner(plannerOptions);
        FulfillRequest(planner, "Turn on the lights in the kitchen");
        FulfillRequest(planner, "Open the windows of the bedroom, turn the lights off and put on Shawshank Redemption on the TV.");
        FulfillRequest(planner, "Close the garage door and turn off the lights in all rooms.");
        FulfillRequest(planner, "Turn off the lights in all rooms and play a movie in which Tom Cruise is a lawyer in the living room.");

        Console.ReadLine();
    }

    static void  FulfillRequest(HandlebarsPlanner planner, string ask)
    {
        Console.WriteLine($"Fulfilling request: {ask}");
        var plan = planner.CreatePlanAsync(_kernel, ask).Result;
        var result = plan.InvokeAsync(_kernel, []).Result;
        Console.WriteLine("Request complete.");
    }
}
