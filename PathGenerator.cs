using Autopark.Data;
using Autopark.Models;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Triangulate.Polygon;
using NetTopologySuite.Triangulate.Tri;
using System.Text;
using System.Text.Json;

namespace AutoparkPathsGenerator
{
    public class PathGenerator(ApplicationDbContext _db, string _apiKey)
    {
        private readonly ApplicationDbContext db = _db;
        private readonly string ApiKey = _apiKey;

        private Queue<Coordinate> Coordinates = new();

        public async Task OffsetGenerate(int vehicleId, string cityName, double offset)
        {
            var vehicle = db.Vehicles.Where(v => v.Id == vehicleId).FirstOrDefault();
            if (vehicle == null)
            {
                return;
            }

            string osmId = await GetOsmIdAsync(cityName);
            MultiPolygon polygon = await GetPolygon(osmId);
            List<Tri> triangles = GetTriangles(polygon);

            while (true)
            {
                var delayTask = Task.Delay(10000);

                if (Coordinates.Count == 0)
                {
                    bool succeed;
                    do
                    {
                        succeed = await BuildPath(polygon, triangles);
                    }
                    while (!succeed);

                }

                //Register a point for given vehicle
                var point = new Geopoint()
                {
                    Point = new Point(Coordinates.Dequeue()) { SRID = 4326 },
                    VehicleId = vehicleId,
                    RegisterTime = DateTime.Now.AddDays(offset)
                };
                db.Points.Add(point);
                Console.WriteLine(point.Point + " " + point.RegisterTime);
                db.SaveChanges();
                await delayTask;
            }
        }

        public async Task Generate(int vehicleId, string cityName)
        {
            //Check if vehicle in question exists
            var vehicle = db.Vehicles.Where(v => v.Id == vehicleId).FirstOrDefault();
            if (vehicle == null)
            {
                return;
            }

            string osmId = await GetOsmIdAsync(cityName);
            MultiPolygon polygon = await GetPolygon(osmId);
            List<Tri> triangles = GetTriangles(polygon);

            while (true)
            {
                var delayTask = Task.Delay(10000);

                if (Coordinates.Count == 0)
                {
                    bool succeed;
                    do
                    {
                        succeed = await BuildPath(polygon, triangles);
                    }
                    while (!succeed);

                }

                //Register a point for given vehicle
                var point = new Geopoint()
                {
                    Point = new Point(Coordinates.Dequeue()) { SRID = 4326 },
                    VehicleId = vehicleId,
                    RegisterTime = DateTime.Now
                };
                db.Points.Add(point);
                Console.WriteLine(point.Point + " " + point.RegisterTime);
                db.SaveChanges();
                await delayTask;
            }
        }

        //Get osm id of a city to request its polygon
        private static async Task<string> GetOsmIdAsync(string cityName)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla / 5.0(compatible; AcmeInc / 1.0)");

            string page = @"https://nominatim.openstreetmap.org/search?format=geojson&limit=1&city=" + cityName;
            var response = await client.GetAsync(page);

            if (!response.IsSuccessStatusCode) throw new ArgumentException("Couldn't find given city id");

            using JsonDocument jsonResponse = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            JsonElement root = jsonResponse.RootElement.GetProperty("features")[0].GetProperty("properties");
            return root.GetProperty("osm_id").ToString();
        }

        //Get a city polygon
        private static async Task<MultiPolygon> GetPolygon(string osmId)
        {
            using HttpClient client = new();

            string page = "https://polygons.openstreetmap.fr/get_geojson.py?id=" + osmId;
            var response = await client.GetAsync(page);

            if (!response.IsSuccessStatusCode) throw new ArgumentException("Couldn't locate chosen city polygon");

            GeoJsonReader reader = new();
            return reader.Read<MultiPolygon>(await response.Content.ReadAsStringAsync());
        }

        //Divide given polygon into triangles
        private static List<Tri> GetTriangles(MultiPolygon polygon)
        {
            PolygonTriangulator triangulator = new(polygon);
            List<Tri> triangles = triangulator.GetTriangles();
            return triangles;
        }

        //Get random coordinate in a random triangle
        private static Point CalculateCoordinate(List<Tri> triangles)
        {
            Random random = new();
            float x = random.NextSingle();
            float y = random.NextSingle() * (1 - x);
            float w = 1 - x - y;

            var randomTriangle = triangles[random.Next(triangles.Count - 1)];
            var resultPoint = new Point(new Coordinate(randomTriangle.GetCoordinate(0).X * x
                + randomTriangle.GetCoordinate(1).X * y + randomTriangle.GetCoordinate(2).X * w,
                randomTriangle.GetCoordinate(0).Y * x + randomTriangle.GetCoordinate(1).Y * y
                + randomTriangle.GetCoordinate(2).Y * w));
            return resultPoint;
        }

        //Create a path from one path to another
        private async Task<HttpResponseMessage> GetPathBetweenAsync(Point start, Point finish)
        {
            using HttpClient client = new();

            var json = JsonSerializer.Serialize(new
            {
                coordinates = new float[][]
                {
                    [
                        (float)start.X, (float)start.Y
                    ],
                    [
                        (float)finish.X, (float)finish.Y
                    ]
                }
            });

            var headers = new Dictionary<string, string>
            {
                { "Accept", "application/json, application/geo+json, application/gpx+xml, img/png; charset=utf-8" },
                { "Content-Type", "application/json" },
                { "Authorization", ApiKey }
            };
            StringContent body = new(json, Encoding.UTF8, "application/json");
            string query = "https://api.openrouteservice.org/v2/directions/driving-car/geojson?api_key=" + ApiKey;
            var response = await client.PostAsync(query, body);
            if (response.IsSuccessStatusCode) return response;

            Console.WriteLine(await response.Content.ReadAsStringAsync());
            switch(response.StatusCode)
            {
                case System.Net.HttpStatusCode.BadRequest:
                    throw new HttpRequestException("Couldn't build a road");
                case System.Net.HttpStatusCode.TooManyRequests:
                    var delayTask = Task.Delay(10000);
                    await delayTask;
                    throw new HttpRequestException("Too many requests sent");
                default:
                    throw new Exception("Unknown exception");
            }
        }

        //Build path inside a triangle
        private async Task<bool> BuildPath(MultiPolygon polygon, List<Tri> triangles)
        {
            bool succeed = true;
            FeatureCollection featureCollection = [];
            do
            {
                try
                {
                    Point firstPoint = GeneratePoint(polygon, triangles);
                    Point secondPoint = GeneratePoint(polygon, triangles);
                    GeoJsonReader reader = new();
                    featureCollection = reader.Read<FeatureCollection>(await (await GetPathBetweenAsync(firstPoint, secondPoint))
                        .Content.ReadAsStringAsync());
                }
                catch
                {
                    succeed = false;
                }
            } while (!succeed);

            if (featureCollection == null) return false;

            LineString? path = featureCollection[0].Geometry as LineString;
            if (path == null) return false;

            foreach (var item in path.Coordinates)
            {
                Coordinates.Enqueue(item);
            };
            return true;
        }

        private Point GeneratePoint(MultiPolygon polygon, List<Tri> triangles)
        {
            Point point;
            do
            {
                point = CalculateCoordinate(triangles);
            } while (!polygon.Contains(point));
            return point;
        }
    }
}
