namespace Gabonet.Hubble.Tests;

using System.Reflection;
using Gabonet.Hubble.BackgroundServices;
using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

/// <summary>
/// Hubble usa su propio cliente de MongoDB y nunca registra ni reemplaza el IMongoClient de la aplicación.
/// </summary>
public class IsolationTests
{
    private const string HubbleConnection = "mongodb://127.0.0.1:27999";
    private static readonly MongoClient AppClient = new("mongodb://app-db:27017");

    private static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider();
    }

    private static void AddHubble(IServiceCollection services, string connectionString = HubbleConnection) =>
        services.AddHubble(o =>
        {
            o.ConnectionString = connectionString;
            o.DatabaseName = "HubbleIsolation";
        });

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    private static IMongoCollection<GeneralLog> LogsCollection(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return Field<IMongoCollection<GeneralLog>>(scope.ServiceProvider.GetRequiredService<IHubbleService>(), "_logsCollection");
    }

    [Fact]
    public void Hubble_does_not_register_IMongoClient()
    {
        using var provider = Build(s => AddHubble(s));

        Assert.Null(provider.GetService<IMongoClient>());
    }

    [Fact]
    public void Hubble_services_use_their_own_client()
    {
        using var provider = Build(s => AddHubble(s));

        Assert.Equal("127.0.0.1:27999", LogsCollection(provider).Database.Client.Settings.Server.ToString());

        var writer = provider.GetServices<IHostedService>().OfType<HubbleLogWriterService>().Single();
        Assert.Equal("127.0.0.1:27999", Field<IMongoCollection<GeneralLog>>(writer, "_logsCollection").Database.Client.Settings.Server.ToString());

        var stats = provider.GetRequiredService<IHubbleStatsService>();
        Assert.Equal("127.0.0.1:27999", Field<IMongoClient>(stats, "_mongoClient").Settings.Server.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void App_client_is_preserved_regardless_of_registration_order(bool registerBeforeHubble)
    {
        using var provider = Build(s =>
        {
            if (registerBeforeHubble) s.AddSingleton<IMongoClient>(AppClient);
            AddHubble(s);
            if (!registerBeforeHubble) s.AddSingleton<IMongoClient>(AppClient);
        });

        Assert.Same(AppClient, provider.GetRequiredService<IMongoClient>());
    }

    [Fact]
    public void Server_selection_timeout_defaults_to_five_seconds()
    {
        using var provider = Build(s => AddHubble(s));

        Assert.Equal(TimeSpan.FromSeconds(5), LogsCollection(provider).Database.Client.Settings.ServerSelectionTimeout);
    }

    [Fact]
    public void Server_selection_timeout_from_connection_string_is_respected()
    {
        using var provider = Build(s => AddHubble(s, HubbleConnection + "/?serverSelectionTimeoutMS=2000"));

        Assert.Equal(TimeSpan.FromSeconds(2), LogsCollection(provider).Database.Client.Settings.ServerSelectionTimeout);
    }
}
