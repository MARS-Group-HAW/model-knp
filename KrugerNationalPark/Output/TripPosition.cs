using NetTopologySuite.Geometries;

namespace KrugerNationalPark.Output
{
    public class TripPosition(double longitude, double latitude) : Coordinate(longitude, latitude)
    {
        public int UnixTimestamp { get; set; }
    }
}