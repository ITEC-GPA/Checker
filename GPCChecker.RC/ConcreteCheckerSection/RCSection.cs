using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Utilities;
using GPC.Model.Elements;
using GPC.Model.Materials;
using GPC.Geometry;
using GPC.Model.Sections;

namespace GPC.Checker.ReinforcedConcrete
{
    //public class RCSection
    //{
    //    private readonly Rebars _bars;
    //    private readonly double _elasticModulusE;
    //    private readonly double _fck;
    //    private readonly Shapes _shapes;

    //    public RCSection(double elasticModulusE, double fck, Shapes shapes) :
    //        base()
    //    {
    //        _bars = new Rebars();
    //        _elasticModulusE = elasticModulusE;
    //        _fck = fck;
    //        _shapes = shapes;
    //    }
    //    public void AddBar(double diameter,
    //                       double effectiveArea,
    //                       Point2d position,
    //                       RebarMaterial material)
    //    {
    //        _bars.AddRebar(diameter, effectiveArea, new Point2d(0,0), new Point2d(0, 0), position, material, Guid.Empty);
    //    }
    //    internal Rebars Bars => _bars;
    //    internal double ElasticModulusE => _elasticModulusE;
    //    internal double Fck => _fck;
    //    internal Shapes Shapes => _shapes;
    //}
}
