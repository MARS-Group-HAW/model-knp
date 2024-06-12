using System.Linq;
using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace KrugerNationalPark.Layers
{
    public class VectorWaterLayer : VectorLayer
    {
        public Position ExploreClosestFullPotentialField(double lat, double lon, double maxDistance)
        {
            var centroid = Explore(new[] { lon, lat }, maxDistance).FirstOrDefault().VectorStructured.Geometry.Centroid; 
            return Position.CreateGeoPosition(centroid.X, centroid.Y);
        }
    }
}