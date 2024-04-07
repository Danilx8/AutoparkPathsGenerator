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
                if (o.RealTime) await generator.RealTimeGenerate(o.VehicleId, o.CityName);
                else if (o.Start != default && o.Finish != default)
                    await generator.GenerateInRange(o.VehicleId, o.CityName, o.Start, o.Finish);
                else Console.WriteLine("Incorrect arguments format");
            });
    }
}

public record Options
{
    [property: Option('v', "vehicleId", Required = true, HelpText = "Set vehicle's id")]
    public int VehicleId { get; set; }
    [property: Option('c', "cityName", Required = true, HelpText = "Set city area to make path in")]
    public string CityName { get; set; }
    [property: Option('r', "realTime", Required = false, HelpText = "Set real-time generation")]
    public bool RealTime { get; set; }
    [property: Option('s', "start", Required = false, HelpText = "Set start time for simultaneous paths generation")]
    public DateTime Start { get; set; }
    [property: Option('f', "finish", Required = false, HelpText = "Set finish time for simultaneous paths generation")]
    public DateTime Finish { get; set; }
}