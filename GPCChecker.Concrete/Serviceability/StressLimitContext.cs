using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Serviceability
{
    /// <summary>Resolved normative limits, independent of the solver. Absolute MPa; bar IDs match the response sampling layout.</summary>
    public sealed class StressLimitContext
    {
        public string StandardName { get; }
        public double ConcreteCharacteristic { get; }
        public double ConcreteQuasiPermanent { get; }
        public IReadOnlyDictionary<string, double> SteelCharacteristic { get; }
        public StressLimitContext(string standardName, double concreteCharacteristic, double concreteQuasiPermanent, IDictionary<string, double> steelCharacteristic)
        {
            StandardName = standardName ?? throw new ArgumentNullException(nameof(standardName));
            ConcreteCharacteristic = concreteCharacteristic; ConcreteQuasiPermanent = concreteQuasiPermanent;
            SteelCharacteristic = new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(new Dictionary<string, double>(steelCharacteristic ?? throw new ArgumentNullException(nameof(steelCharacteristic)), StringComparer.Ordinal));
        }
        public static StressLimitContext Resolve(StandardModelCode2010 standard, IConcreteSection section)
        {
            if (standard == null || section == null) throw new ArgumentNullException();
            if (!(section.ConcreteMaterial is ConcreteMaterialEuropeanCommon material)) throw new NotSupportedException("Stress limits require a European concrete material.");
            if (section.Rebars.Any(r => r.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon && r.EpsilonP == 0))
                throw new NotSupportedException("Tendon with zero prestrain: legacy solver identifies tendons through prestrain.");
            return new StressLimitContext(standard.Name, Math.Abs(material.GetConcreteServiceabilityCharacteristicStress(standard)),
                Math.Abs(material.GetConcreteServiceabilityQuasiPermanentStress(standard)), section.Rebars.Select((bar, i) => new {
                    Id = (bar.EpsilonP != 0 ? "P" : "B") + (i + 1), Limit = Math.Abs(bar.EpsilonP != 0
                    ? bar.RebarMaterial.GetServiceabilityCharacteristicStressPrestress(standard) : bar.RebarMaterial.GetServiceabilityCharacteristicStress(standard))
                }).ToDictionary(p => p.Id, p => p.Limit));
        }
    }
}
