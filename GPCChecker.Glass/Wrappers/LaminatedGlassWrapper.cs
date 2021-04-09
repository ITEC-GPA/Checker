
using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;

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


        #region Public constructors

        internal LaminatedGlassWrapper(GlassSurface glassSurface, LaminatedGlass glass) 
            : base(glassSurface, glass)
        {                       
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
            return (Glass as LaminatedGlass).MonolithicGlasses.Select(i => i.Thickness).Sum() + (Glass as LaminatedGlass).Interlayers.Select(i => i.Thickness).Sum(); ;
        }

        /// <returns>The incremental distances of the glass layers center of mass starting from the first one</returns>
        public double[] GetMonolithicBarycenterDistances()
        {
            double[] distances = new double[(Glass as LaminatedGlass).MonolithicGlasses.Length];

            for (int i = 0; i < (Glass as LaminatedGlass).MonolithicGlasses.Length; i++)
            {
                if (i != 0)
                {
                    distances[i] += distances[i - 1];
                    distances[i] += (Glass as LaminatedGlass).MonolithicGlasses[i - 1].Thickness / 2.0;
                    distances[i] += (Glass as LaminatedGlass).Interlayers[i - 1].Thickness;
                    distances[i] += (Glass as LaminatedGlass).MonolithicGlasses[i].Thickness / 2.0;
                }
                else
                {
                    distances[i] = 0;
                }
            }
            return distances;
        }

        /// <returns>The incremental distances of the interlayer center of mass starting from the center of mass of the first glass layer</returns>
        public double[] GetInterlayerBarycenterDistances()
        {
            double[] distances = new double[(Glass as LaminatedGlass).Interlayers.Length];

            for (int i = 0; i < (Glass as LaminatedGlass).Interlayers.Length; i++)
            {
                if (i != 0)
                {
                    distances[i] += distances[i - 1];
                    distances[i] += (Glass as LaminatedGlass).Interlayers[i - 1].Thickness / 2.0;
                    distances[i] += (Glass as LaminatedGlass).MonolithicGlasses[i].Thickness;
                    distances[i] += (Glass as LaminatedGlass).Interlayers[i].Thickness / 2.0;
                }
                else
                {
                    distances[i] = (Glass as LaminatedGlass).MonolithicGlasses[0].Thickness / 2.0;
                    distances[i] += (Glass as LaminatedGlass).Interlayers[0].Thickness / 2.0;
                }
            }
            return distances;
        }

        public override double GetElasticModulus()
        {
            return (Glass as LaminatedGlass).MonolithicGlasses.Select(i => i.Material.E).Min();
        }

        public override double GetPoissonRatios()
        {
            return (Glass as LaminatedGlass).MonolithicGlasses.Select(i => i.Material.Ni).Min();
        }

        public override double GetSelfWeightPerUnitArea()
        {
            return (Glass as LaminatedGlass).MonolithicGlasses.Select(i => i.Thickness * i.Material.Density).Sum() + (Glass as LaminatedGlass).Interlayers.Select(i => i.Thickness * i.Material.Density).Sum();
        }

        public override double GetSelfWeightTotal()
        {
            return _glassSurface.Shape.GetArea() * ((Glass as LaminatedGlass).MonolithicGlasses.Select(i => i.Thickness * i.Material.Density).Sum() + (Glass as LaminatedGlass).Interlayers.Select(i => i.Thickness * i.Material.Density).Sum());
        }

        #endregion


    }
}
