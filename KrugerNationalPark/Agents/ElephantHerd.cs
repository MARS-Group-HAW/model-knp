namespace KrugerNationalPark.Agents
{
    /// <summary>
    ///     Represents an elephant herd
    ///     Is not an agent, but just an object to
    ///     store information about elephants in a herd
    /// </summary>
    public class ElephantHerd(int herdId, Elephant leader, List<Elephant> other)
    {
        public readonly List<Elephant> OtherElephants = other;

        private int Id { get; } = herdId;
        public Elephant LeadingElephant { get; } = leader;
    }
}