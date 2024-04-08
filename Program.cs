using Autopark.Data;
using AutoparkPathsGenerator;
using CommandLine;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(
                    "Server=localhost;Database=AutoparkDb;User Id=SA;Password=AVeryComplex123Password;MultipleActiveResultSets=true;TrustServerCertificate=True",
                    x => x.UseNetTopologySuite())
                .Options;
        await using var _db = new ApplicationDbContext(contextOptions);


        await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async o =>
            {
                PathGenerator generator = new(_db, "5b3ce3597851110001cf6248ba643297a7154f94952c7f2968f1f56d");
                if (o.Offset == default)
                {
                    await generator.Generate(o.VehicleId, o.CityName, o.RideAmount);
                }
                else
                {
                    await generator.OffsetGenerate(o.VehicleId, o.CityName, o.Offset * -1, o.RideAmount);
                }
            });
    }
}

public record Options
{
    [property: Option('v', "vehicleId", Required = true, HelpText = "Set vehicle's id")]
    public int VehicleId { get; set; }
    [property: Option('c', "cityName", Required = true, HelpText = "Set city area to make path in")]
    public string CityName { get; set; }
    [property: Option('o', "daysOffset", Required = false, HelpText = "Set offset of all rides in days")]
    public int Offset { get; set; }
    [property: Option('a', "rideAmount", Required = true, HelpText = "Set amount of rides you would like to generate")]
    public int RideAmount { get; set; }
}