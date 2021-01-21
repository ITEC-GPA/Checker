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

        internal new LaminatedGlass Glass => (LaminatedGlass)_glassSurface.Glass;


        #region Public constructors

        internal LaminatedGlassWrapper(GlassSurface glassSurface) 
            : base(glassSurface)
        {
            if (!(glassSurface.Glass is LaminatedGlass))
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
            return Glass.MonolithicGlasses.Select(i => i.Thickness).Sum() + Glass.Interlayers.Select(i => i.Thickness).Sum(); ;
        }

        /// <summary>
        /// Get distances of monolithic glass barycenter starting from first slab.
        /// </summary>
        public double[] GetMonolithicBarycenterDistances()
        {
            double[] distances = new double[Glass.MonolithicGlasses.Length];

            for (int i = 0; i < Glass.MonolithicGlasses.Length; i++)
            {
                distances[i] = Glass.MonolithicGlasses[i].Thickness / 2.0;

                if (i != 0)
                {
                    distances[i] += Glass.MonolithicGlasses[i - 1].Thickness / 2.0;
                    distances[i] += distances[i - 1];
                    distances[i] += Glass.Interlayers[i - 1].Thickness;
                }
            }
            return distances;
        }

        /// <summary>
        /// Get distances of interlayer barycenter starting from lower side of first glass slab.
        /// </summary>
        public double[] GetInterlayerBarycenterDistances()
        {
            double[] distances = new double[Glass.Interlayers.Length];

            for (int i = 0; i < Glass.Interlayers.Length; i++)
            {
                distances[i] = Glass.MonolithicGlasses[i].Thickness;
                distances[i] += Glass.Interlayers[i].Thickness / 2.0;

                if (i != 0)
                {
                    distances[i] += distances[i - 1];
                    distances[i] += Glass.Interlayers[i - 1].Thickness / 2.0;
                 }
            }
            return distances;
        }

        public override double GetElasticModulus()
        {
            return Glass.MonolithicGlasses.Select(i => i.Material.E).Min();
        }

        public override double GetPoissonRatios()
        {
            return Glass.MonolithicGlasses.Select(i => i.Material.Ni).Min();
        }

        public override double GetSelfWeightPerUnitArea()
        {
            return Glass.MonolithicGlasses.Select(i => i.Thickness * i.Material.Density).Sum() + Glass.Interlayers.Select(i => i.Thickness * i.Material.Density).Sum();
        }

        public override double GetSelfWeightTotal()
        {
            return _glassSurface.Shape.GetArea() *
                    (Glass.MonolithicGlasses.Select(i => i.Thickness * i.Material.Density).Sum() + Glass.Interlayers.Select(i => i.Thickness * i.Material.Density).Sum());
        }
        
        #endregion


        #region Public methods - Analysis

        

        #endregion
    }
}
