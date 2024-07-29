using KrugerNationalPark.Layers;
using Mars.Interfaces.Environments;

namespace KrugerNationalPark.Agents
{
    public class WaterSources(VectorWaterLayer gisWaterLayer)
    {
        private readonly IList<Position> _waterSources = new List<Position>();

        internal void AddInitialWaterSource(double lat, double lon)
        {
            const double maxMaxDistance = double.MaxValue;
            var closestSource = gisWaterLayer.ExploreClosestFullPotentialField(lat, lon, maxMaxDistance);
            if (closestSource != null) _waterSources.Add(closestSource);
        }

        internal Position GetClosestWaterSource(double lat, double lon)
        {
            var closestInSight = GetClosestWaterSourceInSight(lat, lon);
            if (closestInSight != null) return closestInSight;

            return _waterSources.Any()
                ? _waterSources.OrderBy(source => source.DistanceInKmTo(Position.CreateGeoPosition(lon, lat)))
                    .FirstOrDefault()
                : null;
        }

        internal Position? GetClosestWaterSourceInSight(double lat, double lon)
        {
            // CHECK: Increased sense to water in order to avoid dead ends in the model

            const double agentMaxSightInKm = 25;
            var closestInSight = gisWaterLayer.ExploreClosestFullPotentialField(lat, lon, agentMaxSightInKm);
            if (closestInSight == null)
                //Console.WriteLine("No water available at: " + lat + ", " + lon);
                return null;

            _waterSources.Add(closestInSight);
            return closestInSight;
        }
    }
}