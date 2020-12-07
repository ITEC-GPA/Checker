using GPC.Geometry;
using GPC.Model.Elements.Glasses;
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;

namespace GPC.Checker.Glasses.Wrappers
{
    public class LaminatedGlassWrapper : GlassPanelWrapper
    {
        #region Variables

        /// <summary>
        /// Thickness associates to displacement. Key = load duration; Value = thickness
        /// </summary>
        protected Dictionary<double, double> _thicknessesW;

        /// <summary>
        /// Thickness associates to stress. Key = load duration; Value = thickness
        /// </summary>
        protected Dictionary<double, double> _thicknessesStress;

        #endregion

        internal new LaminatedGlass GlassProperty => (LaminatedGlass)_glassSurface.GlassProperty;


        #region Public constructors

        internal LaminatedGlassWrapper(GlassSurface glassSurface) 
            : base(glassSurface)
        {
            if (!(glassSurface.GlassProperty is LaminatedGlass))
                throw new ArgumentException("Glass property should be a Laminated Glass Property");
                       
            _thicknessesW = new Dictionary<double, double>();
            _thicknessesStress = new Dictionary<double, double>();

        }

        #endregion

        #region Public methods - Geometry

        public override double GetDeformationThickness(double loadDuration)
        {
            if (_thicknessesStress.Keys.Count > 0)
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
            else
            {
                throw new ArgumentException($"Stress thickness table is empty");
            }
        }

        public override double GetStressThickness(double loadDuration)
        {
            if (_thicknessesStress.Keys.Count > 0)
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
                
                throw new ArgumentException($"Stress thickness not found for load duration: {loadDuration}");
            }
            else
            {
                throw new ArgumentException($"Stress thickness table is empty");
            }
        }

        public override double GetTotalThickness()
        {
            return GlassProperty.MonolithicGlasses.Select(i => i.Thickness).Sum() + GlassProperty.Interlayers.Select(i => i.Thickness).Sum(); ;
        }

        /// <summary>
        /// Get distances of monolithic glass barycenter starting from first slab.
        /// </summary>
        public double[] GetMonolithicBarycenterDistances()
        {
            double[] distances = new double[GlassProperty.MonolithicGlasses.Length];

            for (int i = 0; i < GlassProperty.MonolithicGlasses.Length; i++)
            {
                distances[i] = GlassProperty.MonolithicGlasses[i].Thickness / 2.0;

                if (i != 0)
                {
                    distances[i] += GlassProperty.MonolithicGlasses[i - 1].Thickness;
                    distances[i] += distances[i - 1];
                }
            }
            return distances;
        }

        /// <summary>
        /// Get distances of interlayer barycenter starting from lower side of first glass slab.
        /// </summary>
        public double[] GetInterlayerBarycenterDistances()
        {
            double[] distances = new double[GlassProperty.Interlayers.Length];

            for (int i = 0; i < GlassProperty.Interlayers.Length; i++)
            {
                distances[i] = GlassProperty.MonolithicGlasses[i].Thickness;
                distances[i] += GlassProperty.Interlayers[i].Thickness / 2.0;

                if (i != 0)
                {
                    distances[i] += GlassProperty.Interlayers[i - 1].Thickness;
                    distances[i] += distances[i - 1];
                }
            }
            return distances;
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

        

        #endregion
    }
}
