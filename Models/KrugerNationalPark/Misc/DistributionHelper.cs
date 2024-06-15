namespace KrugerNationalPark.Misc;

public class NormalDistributionGenerator(double meanValue, double maximumDeviation)
{
    private readonly Random _rand = new Random();
    private readonly double _standardDeviation = maximumDeviation / 3;

    //Since the normal distribution is theoretically infinite, you can't have a hard cap on your range.
    //So we cut every number that is not in the 99.73% (three standard deviations). Therefore the maximum deviation is devided by 3 to get the standard deviation.
    //Hopefully this description is formally correct ;-) Otherwise look a the example in the class documentation.

    public double GetNext()
    {
        //code partly from http://stackoverflow.com/questions/218060/random-gaussian-variables
        var u1 = _rand.NextDouble();
        var u2 = _rand.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                            Math.Sin(2.0 * Math.PI * u2);
        var random = meanValue + _standardDeviation * randStdNormal;

        if (random < meanValue - maximumDeviation) return meanValue - maximumDeviation;

        if (random > meanValue + maximumDeviation) return meanValue + maximumDeviation;

        return random;
    }
}