using System.Collections.Concurrent;
using KrugerNationalPark.Agents;
using KrugerNationalPark.Misc;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;

namespace KrugerNationalPark.Layers;

public class ElephantLayer(
    RasterVegetationLayer vegetationLayerDgvm,
    VectorWaterLayer waterPotentialLayer,
    RasterTempLayer temperatureLayer,
    RasterFenceLayer rasterFenceLayer,
    RasterShadeLayer shadeLayer)
    : AbstractActiveLayer
{
    private readonly NormalDistributionGenerator _normalDistributionGenerator = new(35, 30);
    private readonly IDictionary<int, ElephantHerd> _herdMap = new ConcurrentDictionary<int, ElephantHerd>();
    
    public ConcurrentDictionary<Guid, Elephant> Entities { get; set; } = new();
    
    private RegisterAgent? _registerAgent;
    private UnregisterAgent? _unregisterAgent;
    private GeoHashEnvironment<Elephant>? _environment;


    public override bool InitLayer(
        LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null, 
        UnregisterAgent? unregisterAgentHandle = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);
            
        ArgumentNullException.ThrowIfNull(registerAgentHandle);
        ArgumentNullException.ThrowIfNull(unregisterAgentHandle);
            
        //params needed for calf spawn
        _registerAgent = registerAgentHandle;
        _unregisterAgent = unregisterAgentHandle;
            
        var baseExtent = new Envelope(vegetationLayerDgvm.Extent.ToEnvelope());
        baseExtent.ExpandedBy(waterPotentialLayer.Extent.ToEnvelope());
        baseExtent.ExpandedBy(temperatureLayer.Extent.ToEnvelope());
        baseExtent.ExpandedBy(rasterFenceLayer.Extent.ToEnvelope());
        baseExtent.ExpandedBy(shadeLayer.Extent.ToEnvelope());
            
        _environment = GeoHashEnvironment<Elephant>.BuildByBBox(new BoundingBox(baseExtent), 1000);

        var agentInitConfig =
            layerInitData.AgentInitConfigs.FirstOrDefault(mapping => mapping.Type.MetaType == typeof(Elephant));

        if (agentInitConfig != null)
        {
            // Spawn all elephant agents
            Entities = AgentManager.GetAgentsByAgentInitConfig<Elephant>
            (agentInitConfig, registerAgentHandle, unregisterAgentHandle,
                [
                    this, waterPotentialLayer, temperatureLayer, shadeLayer, vegetationLayerDgvm,
                    rasterFenceLayer
                ],
                _environment);
            Console.WriteLine("[ElephantLayer]: Created " + Entities.Count + " Agents");

            // create herd objects
            var listOfHerds =
                Entities.Values.AsParallel().GroupBy(elephant => elephant.HerdId).Select(grp => grp.ToList())
                    .ToList();
            Console.WriteLine("[ElephantLayer]: Created " + listOfHerds.Count + " Herds");

            foreach (var h in listOfHerds)
            {
                var leader = h.FirstOrDefault(e => e.Leading);
                if (leader == null)
                {
                    leader = h.FirstOrDefault();
                    if (leader == null)
                        throw new Exception("There is a herd without elephants, which is impossible!");

                    leader.Leading = true;
                }

                var other = h.Where(e => !e.Leading).ToList();
                _herdMap.Add(leader.HerdId, new ElephantHerd(leader.HerdId, leader, other));
            }

            Console.WriteLine("[ElephantLayer]: Filled Herds");
            return true;
        }

        return false;
    }

    public override void PostTick()
    {
        // CHECK: this must be harmonized to real elephant numbers

//            // culling elephants (goes on to including 1994)
//            // a year is calculated with 8766 hours to include leap years
//            // the culling quotas used for this come from the book
//            // "Elephant Management" - Scholes 2009
//            // every 3 days a herd is killed (if neccessary)
//            if (_currentTick % 72 != 0) return;
//            // 1989
//            if (_currentTick < 8766 && ElephantMap.Count > 7468)
//            {
//                KillElephantHerd();
//            }
//            // 1990
//            else if (_currentTick < 17532 && ElephantMap.Count > 7287)
//            {
//                KillElephantHerd();
//            }
//            // 1991
//            else if (_currentTick < 26298 && ElephantMap.Count > 7470)
//            {
//                KillElephantHerd();
//            }
//            // 1992
//            else if (_currentTick < 35064 && ElephantMap.Count > 7632)
//            {
//                KillElephantHerd();
//            }
//            // 1993
//            else if (_currentTick < 43830 && ElephantMap.Count > 7834)
//            {
//                KillElephantHerd();
//            }
//            // 1994
//            else if (_currentTick < 52596 && ElephantMap.Count > 7806)
//            {
//                KillElephantHerd();
//            }
    }

    public Elephant? GetLeadingElephantByHerd(int herdId)
    {
        _herdMap.TryGetValue(herdId, out var herd);
        return herd?.LeadingElephant;
    }

    public void SpawnCalf(ElephantLayer elephantLayer, double latitude, double longitude, int herdId,
        double biomassCellDifference = 1.0, double satietyMultiplier = 1.0, int tickSearchForFood = 1,
        int biomassNeighbourSearchLvl = 1,
        double minDehydration = 100)
    {
        ArgumentNullException.ThrowIfNull(_registerAgent);
        ArgumentNullException.ThrowIfNull(_unregisterAgent);
        ArgumentNullException.ThrowIfNull(_environment);
        
        var newElephant = new Elephant(elephantLayer,
            _registerAgent, _unregisterAgent, _environment,
            waterPotentialLayer, vegetationLayerDgvm, rasterFenceLayer, temperatureLayer, shadeLayer,
            Guid.NewGuid(), latitude, longitude, herdId, "ELEPHANT_NEWBORN",
            false, biomassCellDifference, satietyMultiplier, tickSearchForFood,
            biomassNeighbourSearchLvl, minDehydration);

        Entities.TryAdd(newElephant.ID, newElephant);
    }

    private void KillElephantHerd()
    {
        var herdId = _herdMap.Keys.FirstOrDefault();
        _herdMap.TryGetValue(herdId, out var myHerd);
        if (myHerd != null)
        {
            var leadingCow = myHerd.LeadingElephant;
            var otherElephants = myHerd.OtherElephants;
            leadingCow.Die(MattersOfDeath.Culling);
            Entities.TryRemove(leadingCow.ID, out _);
            foreach (var el in otherElephants)
            {
                el.Die(MattersOfDeath.Culling);
                Entities.TryRemove(el.ID, out _);
            }

            _herdMap.Remove(herdId);
        }
        else
        {
            Console.WriteLine("[ElephantLayer] error killing a herd");
        }
    }

    public int GetNextNormalDistribution()
    {
        return (int) _normalDistributionGenerator.GetNext();
    }
}