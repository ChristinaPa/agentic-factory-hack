using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairPlanner;
using RepairPlanner.Models;
using RepairPlanner.Services;

// ---------------------------------------------------------------------------
// 1. Read environment variables
// ---------------------------------------------------------------------------
var projectEndpoint     = RequireEnv("AZURE_AI_PROJECT_ENDPOINT");
var modelDeploymentName = RequireEnv("MODEL_DEPLOYMENT_NAME");
var cosmosEndpoint      = RequireEnv("COSMOS_ENDPOINT");
var cosmosKey           = RequireEnv("COSMOS_KEY");
var cosmosDatabaseName  = RequireEnv("COSMOS_DATABASE_NAME");

// ---------------------------------------------------------------------------
// 2. Build DI container
// ---------------------------------------------------------------------------
var services = new ServiceCollection();

services.AddLogging(b => b
    .AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    })
    .SetMinimumLevel(LogLevel.Information));

// Foundry project client (uses DefaultAzureCredential -> az login).
services.AddSingleton(_ => new AIProjectClient(
    new Uri(projectEndpoint),
    new DefaultAzureCredential()));

// Cosmos DB.
services.AddSingleton(_ => new CosmosDbOptions
{
    Endpoint = cosmosEndpoint,
    Key = cosmosKey,
    DatabaseName = cosmosDatabaseName,
});
services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<CosmosDbOptions>();
    var clientOptions = new CosmosClientOptions
    {
        // Use Newtonsoft serializer so [JsonProperty] attributes on the models work.
        Serializer = new CosmosNewtonsoftJsonSerializer(),
    };
    // Cosmos local key auth is disabled by policy on this account -> use AAD.
    return new CosmosClient(opts.Endpoint, new DefaultAzureCredential(), clientOptions);
});
services.AddSingleton<CosmosDbService>();
services.AddSingleton<IFaultMappingService, FaultMappingService>();

// The agent itself - constructor takes the model name as a string parameter.
services.AddSingleton(sp => new RepairPlannerAgent(
    sp.GetRequiredService<AIProjectClient>(),
    sp.GetRequiredService<CosmosDbService>(),
    sp.GetRequiredService<IFaultMappingService>(),
    modelDeploymentName,
    sp.GetRequiredService<ILogger<RepairPlannerAgent>>()));

// await using == Python's "async with"
await using var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();

// ---------------------------------------------------------------------------
// 3. Register the Foundry agent (idempotent)
// ---------------------------------------------------------------------------
var agent = provider.GetRequiredService<RepairPlannerAgent>();
await agent.EnsureAgentVersionAsync();

// ---------------------------------------------------------------------------
// 4. Sample fault (mimics output of Challenge 1's Fault Diagnosis Agent)
// ---------------------------------------------------------------------------
var sampleFault = new DiagnosedFault
{
    MachineId   = "machine-001",
    MachineType = "tire_curing_press",
    FaultType   = "curing_temperature_excessive",
    RootCause   = "Heating element malfunction",
    Severity    = "High",
    DetectedAt  = DateTime.UtcNow,
    Description = "Curing temperature reading 179.2C exceeds threshold of 178C.",
    Metadata = new Dictionary<string, object>
    {
        ["observedTemperature"] = 179.2,
        ["thresholdTemperature"] = 178,
    },
};

logger.LogInformation("Sample fault: {Fault}",
    JsonSerializer.Serialize(sampleFault, new JsonSerializerOptions { WriteIndented = false }));

// ---------------------------------------------------------------------------
// 5. Run the planner
// ---------------------------------------------------------------------------
try
{
    var workOrder = await agent.PlanAndCreateWorkOrderAsync(sampleFault);

    Console.WriteLine();
    Console.WriteLine("==================== WORK ORDER ====================");
    Console.WriteLine(JsonSerializer.Serialize(workOrder, new JsonSerializerOptions
    {
        WriteIndented = true,
    }));
    Console.WriteLine("====================================================");
}
catch (Exception ex)
{
    logger.LogError(ex, "Repair planning failed.");
    Environment.ExitCode = 1;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static string RequireEnv(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        Console.Error.WriteLine($"Missing required environment variable: {name}");
        Environment.Exit(2);
    }
    return value!;
}

// Cosmos serializer that defers to Newtonsoft so [JsonProperty] attributes apply.
file sealed class CosmosNewtonsoftJsonSerializer : CosmosSerializer
{
    private static readonly Newtonsoft.Json.JsonSerializer Serializer =
        Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
        {
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
        });

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }
            using var sr = new StreamReader(stream);
            using var jr = new Newtonsoft.Json.JsonTextReader(sr);
            return Serializer.Deserialize<T>(jr)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var ms = new MemoryStream();
        using (var sw = new StreamWriter(ms, leaveOpen: true))
        using (var jw = new Newtonsoft.Json.JsonTextWriter(sw))
        {
            Serializer.Serialize(jw, input);
            jw.Flush();
        }
        ms.Position = 0;
        return ms;
    }
}

