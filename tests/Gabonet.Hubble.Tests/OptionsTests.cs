namespace Gabonet.Hubble.Tests;

using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Logging;
using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public class OptionsTests
{
    private static IConfigurationSection Section(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection(HubbleOptions.SectionName);

    private static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Validation_reports_every_error_at_once()
    {
        var options = new HubbleOptions
        {
            BasePath = "/",
            RequireAuthentication = true,
            EnableDataPrune = true,
            MaxLogAgeHours = 0,
            HighlightDurationSeconds = -1,
            TimeZoneId = "Not/AZone"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("ConnectionString", ex.Message);
        Assert.Contains("DatabaseName", ex.Message);
        Assert.Contains("BasePath", ex.Message);
        Assert.Contains("RequireAuthentication", ex.Message);
        Assert.Contains("MaxLogAgeHours", ex.Message);
        Assert.Contains("HighlightDurationSeconds", ex.Message);
        Assert.Contains("Not/AZone", ex.Message);
    }

    [Fact]
    public void Valid_options_pass_validation()
    {
        var options = new HubbleOptions
        {
            ConnectionString = TestApp.UnreachableMongo,
            DatabaseName = "db",
            TimeZoneId = "UTC",
            EnableDataPrune = true,
            MaxLogAgeHours = 72
        };

        options.Validate();
    }

    [Fact]
    public void AddHubble_fails_fast_with_invalid_options()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddHubble(o => o.DatabaseName = "db"));
    }

    [Fact]
    public void Binds_every_kind_of_setting_from_configuration()
    {
        var options = HubbleOptionsBinder.Bind(Section(new()
        {
            ["Hubble:ConnectionString"] = "mongodb://db:27017",
            ["Hubble:DatabaseName"] = "Logs",
            ["Hubble:ServiceName"] = "Orders.Api",
            ["Hubble:IgnoreStaticFiles"] = "false",
            ["Hubble:CaptureLoggerMessages"] = "false",
            ["Hubble:MinimumLogLevel"] = "Warning",
            ["Hubble:EnableDataPrune"] = "true",
            ["Hubble:MaxLogAgeHours"] = "72",
            ["Hubble:IgnorePaths:0"] = "/health",
            ["Hubble:Security:AllowedIps:0"] = "10.0.0.0/8",
        }));

        Assert.Equal("mongodb://db:27017", options.ConnectionString);
        Assert.Equal("Logs", options.DatabaseName);
        Assert.Equal("Orders.Api", options.ServiceName);
        Assert.False(options.IgnoreStaticFiles);
        Assert.False(options.CaptureLoggerMessages);
        Assert.Equal(LogLevel.Warning, options.MinimumLogLevel);
        Assert.True(options.EnableDataPrune);
        Assert.Equal(72, options.MaxLogAgeHours);
        Assert.Equal(new[] { "/health" }, options.IgnorePaths);
        Assert.Equal(new[] { "10.0.0.0/8" }, options.Security.AllowedIps);
    }

    [Fact]
    public void Configured_lists_replace_the_defaults()
    {
        var options = HubbleOptionsBinder.Bind(Section(new()
        {
            ["Hubble:Security:MaskHeaders:0"] = "X-Custom-Secret",
            ["Hubble:Security:MaskBodyProperties:0"] = "pin",
        }));

        Assert.Equal(new[] { "X-Custom-Secret" }, options.Security.MaskHeaders);
        Assert.Equal(new[] { "pin" }, options.Security.MaskBodyProperties);
    }

    [Fact]
    public void Lists_missing_from_configuration_keep_their_defaults()
    {
        var options = HubbleOptionsBinder.Bind(Section(new() { ["Hubble:ServiceName"] = "x" }));
        var defaults = new HubbleOptions();

        Assert.Equal(defaults.Security.MaskHeaders, options.Security.MaskHeaders);
        Assert.Equal(defaults.Security.MaskBodyProperties, options.Security.MaskBodyProperties);
    }

    [Fact]
    public void Code_configuration_runs_after_the_section()
    {
        using var provider = Build(services => services.AddHubble(
            Section(new() { ["Hubble:DatabaseName"] = "FromSection", ["Hubble:ServiceName"] = "FromSection" }),
            options =>
            {
                options.ConnectionString = TestApp.UnreachableMongo;
                options.ServiceName = "FromCode";
            }));

        var options = provider.GetRequiredService<HubbleOptions>();
        Assert.Equal("FromSection", options.DatabaseName);
        Assert.Equal("FromCode", options.ServiceName);
    }

    [Fact]
    public void Options_are_also_available_through_IOptions()
    {
        using var provider = Build(services => services.AddHubble(o =>
        {
            o.ConnectionString = TestApp.UnreachableMongo;
            o.DatabaseName = "db";
        }));

        Assert.Same(provider.GetRequiredService<HubbleOptions>(), provider.GetRequiredService<IOptions<HubbleOptions>>().Value);
    }

#pragma warning disable CS0618 // Las sobrecargas obsoletas deben seguir funcionando
    [Fact]
    public void Obsolete_connection_string_overload_registers_the_full_set_of_services()
    {
        using var provider = Build(services => services.AddHubble(TestApp.UnreachableMongo, "db", "Legacy.Api"));

        Assert.Equal("Legacy.Api", provider.GetRequiredService<HubbleOptions>().ServiceName);
        // Antes esta sobrecarga no registraba el servicio de estadísticas y la página /config fallaba
        Assert.NotNull(provider.GetService<IHubbleStatsService>());
    }

    [Fact]
    public void Obsolete_configuration_overload_reads_the_section_and_parameters()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hubble:ServiceName"] = "Legacy.Api",
            ["Hubble:Security:MaskRequestBodyProperties:0"] = "pin"
        }).Build();

        using var provider = Build(services => services.AddHubble(configuration, TestApp.UnreachableMongo, "db"));

        var options = provider.GetRequiredService<HubbleOptions>();
        Assert.Equal("Legacy.Api", options.ServiceName);
        Assert.Equal("db", options.DatabaseName);
        Assert.Equal(new[] { "pin" }, options.Security.MaskRequestBodyProperties);
    }
#pragma warning restore CS0618

    [Theory]
    [InlineData(null, LogLevel.Warning, LogLevel.Warning)]
    [InlineData(LogLevel.Error, LogLevel.Warning, LogLevel.Error)]
    public void AddHubbleLogging_uses_the_explicit_level_or_the_options(LogLevel? explicitLevel, LogLevel optionsLevel, LogLevel expected)
    {
        using var provider = Build(services =>
        {
            services.AddHubble(o =>
            {
                o.ConnectionString = TestApp.UnreachableMongo;
                o.DatabaseName = "db";
                o.MinimumLogLevel = optionsLevel;
            });
            services.AddLogging(builder => builder.AddHubbleLogging(explicitLevel));
        });

        var logger = provider.GetServices<ILoggerProvider>().OfType<HubbleLoggerProvider>().Single().CreateLogger("Orders");

        Assert.True(logger.IsEnabled(expected));
        Assert.False(logger.IsEnabled(expected - 1));
    }

    [Fact]
    public void CaptureLoggerMessages_false_disables_the_logger()
    {
        using var provider = Build(services =>
        {
            services.AddHubble(o =>
            {
                o.ConnectionString = TestApp.UnreachableMongo;
                o.DatabaseName = "db";
                o.CaptureLoggerMessages = false;
            });
            services.AddLogging(builder => builder.AddHubbleLogging());
        });

        var logger = provider.GetServices<ILoggerProvider>().OfType<HubbleLoggerProvider>().Single().CreateLogger("Orders");

        Assert.False(logger.IsEnabled(LogLevel.Critical));
    }
}
