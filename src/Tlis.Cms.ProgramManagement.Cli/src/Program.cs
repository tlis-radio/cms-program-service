﻿using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Tlis.Cms.ProgramManagement.Cli.Commands;
using Tlis.Cms.ProgramManagement.Infrastructure.Persistence;

namespace Tlis.Cms.ProgramManagement.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ServiceProvider serviceProvider = BuildServiceProvider();
        Parser parser = BuildParser(serviceProvider);

        var code = await parser.InvokeAsync(args);
        Log.CloseAndFlush();

        return code;
    }

    private static Parser BuildParser(ServiceProvider serviceProvider)
    {
        var commandLineBuilder = new CommandLineBuilder();

        foreach (Command command in serviceProvider.GetServices<Command>())
        {
            commandLineBuilder.Command.AddCommand(command);
        }

        return commandLineBuilder.UseDefaults().Build();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"appsettings.json")
            .Build();

        Log.Logger = new LoggerConfiguration().ReadFrom
            .Configuration(configuration)
            .CreateLogger();
        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<Command, MigrationCommand>();

        services.AddDbContext<ProgramManagementDbContext>(
            options =>
            {
                options
                    .UseNpgsql(
                        configuration.GetConnectionString("Postgres"),
                        x => x.MigrationsHistoryTable(
                            Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName, 
                            "cms_program_management"))
                    .UseSnakeCaseNamingConvention();
            },
            contextLifetime: ServiceLifetime.Transient,
            optionsLifetime: ServiceLifetime.Singleton
        );

        return services.BuildServiceProvider();
    }
}