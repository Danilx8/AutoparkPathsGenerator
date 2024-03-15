using Autopark.Data;
using AutoparkPathsGenerator;
using CommandLine;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private async static Task Main(string[] args)
    {
        var contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(
                    "Server=(localdb)\\mssqllocaldb;Database=helloappdb;Trusted_Connection=True;TrustServerCertificate=True",
                    x => x.UseNetTopologySuite())
                .Options;
        using var _db = new ApplicationDbContext(contextOptions);


        await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async o =>
            {
                PathGenerator generator = new(_db, "5b3ce3597851110001cf6248ba643297a7154f94952c7f2968f1f56d");
                await generator.Generate(o.VehicleId, o.CityName);
            });
    }
}

public record Options
{
    [property: Option('v', "vehicleId", Required = true, HelpText = "Set vehicle's id")]
    public int VehicleId { get; set; }
    [property: Option('c', "cityName", Required = true, HelpText = "Set city area to make path in")]
    public string CityName { get; set; }
}