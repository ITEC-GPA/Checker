using GPC.Geometry;
using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Wrappers
{
    internal class LaminatedGlassWrapper : GlassPanelWrapper
    {
        #region Variables
        protected readonly MonolithicGlassWrapper[] _monolithicGlassWrappers;

        protected Dictionary<double, double> _thicknessesW;

        protected Dictionary<double, double> _thicknessesStress;

        protected Mesh[] _mesh;

        #endregion

        protected new LaminatedGlass GlassProperty => (LaminatedGlass)_glassSurface.GlassProperty;

        internal List<Mesh> Meshes => _mesh.ToList();


        #region Public constructors
        internal LaminatedGlassWrapper(GlassSurface glassSurface) : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is LaminatedGlass))
                throw new ArgumentException("Glass property should be a Laminated Glass Property");
        }

        #endregion

        #region Public methods - Geometry
        public override double GetDeformationThickness(double loadDuration)
        {
            if (_thicknessesW.ContainsKey(loadDuration))
                return _thicknessesW[loadDuration];

            var keys = _thicknessesW.Keys.ToList();
            if (keys.Max() > loadDuration)
                throw new ArgumentOutOfRangeException($"Load duration of {loadDuration} is higher than maximum value available: {_thicknessesW.Keys.Max()}");
            if (keys.Min() > loadDuration)
                throw new ArgumentOutOfRangeException($"Load duration of {loadDuration} is lower than minimum value available: {_thicknessesW.Keys.Min()}");

            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (keys[i] < loadDuration && keys[i + 1] > loadDuration)
                {
                    return Utilities.Maths.Interpolation.GetLinearInterpolation(keys[i], keys[i + 1], _thicknessesW[keys[i]], _thicknessesW[keys[i + 1]], loadDuration);
                }
            }

            throw new NotSupportedException($"Deformation thickness not found for load duration: {loadDuration}");
        }

        public override double GetStressThickness(double loadDuration)
        {
            if (_thicknessesStress.ContainsKey(loadDuration))
                return _thicknessesStress[loadDuration];

            var keys = _thicknessesStress.Keys.ToList();
            if (keys.Max() > loadDuration)
                throw new ArgumentOutOfRangeException($"Load duration of {loadDuration} is higher than maximum value available: {_thicknessesStress.Keys.Max()}");
            if (keys.Min() > loadDuration)
                throw new ArgumentOutOfRangeException($"Load duration of {loadDuration} is lower than minimum value available: {_thicknessesStress.Keys.Min()}");

            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (keys[i] < loadDuration && keys[i + 1] > loadDuration)
                {
                    return Utilities.Maths.Interpolation.GetLinearInterpolation(keys[i], keys[i + 1], _thicknessesStress[keys[i]], _thicknessesStress[keys[i + 1]], loadDuration);
                }
            }

            throw new NotSupportedException($"Stress thickness not found for load duration: {loadDuration}");
        }

        public override double GetTotalThickness()
        {
            return GlassProperty.MonolithicGlasses.Select(i => i.Thickness).Sum() + GlassProperty.Interlayers.Select(i => i.Thickness).Sum(); ;
        }

        public override double GetElasticModulus()
        {
            return GlassProperty.MonolithicGlasses.Select(i => i.Material.E).Min();
        }

        public override double GetPoissonRatios()
        {
            return GlassProperty.MonolithicGlasses.Select(i => i.Material.Ni).Min();
        }

        public override double GetSelfWeightPerUnitArea()
        {
            return GlassProperty.MonolithicGlasses.Select(i => i.Thickness * i.Material.Density).Sum() + GlassProperty.Interlayers.Select(i => i.Thickness * i.Material.Density).Sum();
        }

        public override double GetSelfWeightTotal()
        {
            return _glassSurface.Shape.GetArea() *
                    (GlassProperty.MonolithicGlasses.Select(i => i.Thickness * i.Material.Density).Sum() + GlassProperty.Interlayers.Select(i => i.Thickness * i.Material.Density).Sum());
        }
        #endregion


        #region Public methods - Analysis

        public override List<FemModel.FemMesh> GeneratePlateMesh()
        {
            var shapes = new List<Shape>();
            shapes.Add(_glassSurface.Shape);

            Mesh.GenerateMeshOptions.Size = 10;

            _mesh = Mesh.Generate(shapes, null, null).ToArray();

            throw new NotSupportedException();
        }

        #endregion
    }
}
