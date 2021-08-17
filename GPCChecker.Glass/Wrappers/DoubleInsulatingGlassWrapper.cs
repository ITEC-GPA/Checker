
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Loads;
using GPC.Model.Glasses;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Wrappers
{
    internal class DoubleInsulatingGlassWrapper : InsulatedGlassWrapper
    {

        protected GlassPanelWrapper _innerGlassPanelWrapper;
        protected GlassPanelWrapper _outerGlassPanelWrapper;


        internal GlassPanelWrapper InnerGlassPanelWrapper => _innerGlassPanelWrapper;
        internal GlassPanelWrapper OuterGlassPanelWrapper => _outerGlassPanelWrapper;
        internal double AirThickness => ((DoubleInsulatingGlass)Glass).AirChamber.Thickness;


        internal DoubleInsulatingGlassWrapper(GlassSurface glassSurface, DoubleInsulatingGlass glass)
            : base(glassSurface, glass)
        {
            SetUpWrappers();
        }


        internal override List<IGlassLoad>[] GetLoadSharing(IEnumerable<IGlassLoad> loads, Models.Prototype.Standards standard)
        {
            loads = loads.ToList(); // shallow copy

            List<IGlassLoad>[] redistributedLoads = new List<IGlassLoad>[2];
            redistributedLoads[0] = new List<IGlassLoad>(); // External
            redistributedLoads[1] = new List<IGlassLoad>(); // Internal

            List<Task<(IGlassLoad external, IGlassLoad _internal)>> tasks = new List<Task<(IGlassLoad external, IGlassLoad _internal)>>();

            foreach (var load in loads)
            {
                if (_glassSurface.IsRectangular())
                {
                    if (load is NormalAreaLoad rnal)
                    {
                        // Normativa
                        tasks.Add(GetEN16612PressureSharing(rnal));

                        continue;
                    }
                }
                
                if (load is NormalAreaLoad nal)
                {                    
                    // BAM
                    continue;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            Task.WaitAll(tasks.ToArray());

            redistributedLoads[0] = tasks.Select(i => i.Result.external).ToList();
            redistributedLoads[1] = tasks.Select(i => i.Result._internal).ToList();

            return redistributedLoads;
        }

        /// <summary>
        /// Load sharing procedure according to EN16612 C1. 
        /// This algorithm is valid only for rectangular shapes
        /// </summary>
        protected async Task<(IGlassLoad external, IGlassLoad _internal)> GetEN16612PressureSharing(NormalAreaLoad load)
        {
            // h1: ext
            // h2: int

            NormalAreaLoad externalPanelLoad = null;
            NormalAreaLoad internalPanelLoad = null;
            

            await Task.Run(() =>
            {
                double h1Cube = 0;
                double h2Cube = 0;
                double minPoisson = Math.Min(InnerGlassPanelWrapper.GetPoissonRatio(), OuterGlassPanelWrapper.GetPoissonRatio());

                if (OuterGlassPanelWrapper is MonolithicGlassWrapper omgw)
                {
                    h1Cube = Math.Pow(omgw.GetDeformationThickness(), 3.0);
                }
                else if (OuterGlassPanelWrapper is LaminatedGlassWrapper olgw)
                {
                    h1Cube = Math.Pow(olgw.GetDeformationThickness(load), 3.0);
                }

                if (InnerGlassPanelWrapper is MonolithicGlassWrapper imgw)
                {
                    h2Cube = Math.Pow(imgw.GetDeformationThickness(), 3.0);
                }
                else if (InnerGlassPanelWrapper is LaminatedGlassWrapper ilgw)
                {
                    h2Cube = Math.Pow(ilgw.GetDeformationThickness(load), 3.0);
                }

                double delta1 = h1Cube / (h1Cube + h2Cube);
                double delta2 = 1.0 - delta1;

                double minDimension = _glassSurface.Shape.Fill.Explode().Select(i => i.GetLength()).Min();

                double lambda = minDimension / _glassSurface.Shape.Fill.Explode().Select(i => i.GetLength()).Max();

                if (lambda > 1.0001)
                {
                    throw new NotSupportedException("Min dimension is higher than max dimension");
                }

                double k5 = GetEN16612Getk5Coefficient(minPoisson, lambda);

                double aStar = 28.9 * Math.Pow(AirThickness * h1Cube * h2Cube / ((h1Cube + h2Cube) * k5), 0.25);

                double fi = 1.0 / (1.0 + Math.Pow(minDimension / aStar, 4.0));

                if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.External)
                {
                    externalPanelLoad = new NormalAreaLoad(load.Pressure * (delta1 + fi * delta2), _glassSurface.Shape, load.GlassLoadCase, load.Name,
                                                    GlassPanelWrapper.GlassPanelPositions.External, load.LoadRestrainCondition);
                    internalPanelLoad = new NormalAreaLoad((1.0 - fi) * delta2 * load.Pressure, _glassSurface.Shape, load.GlassLoadCase, load.Name,
                                                    GlassPanelWrapper.GlassPanelPositions.Internal, load.LoadRestrainCondition);     

                }
                else if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.Internal)
                {
                    externalPanelLoad = new NormalAreaLoad((1.0 - fi) * delta1 * load.Pressure, _glassSurface.Shape, load.GlassLoadCase, load.Name,
                                                    GlassPanelWrapper.GlassPanelPositions.External, load.LoadRestrainCondition);
                    internalPanelLoad = new NormalAreaLoad(load.Pressure * (delta2 + fi * delta1), _glassSurface.Shape, load.GlassLoadCase, load.Name,
                                                    GlassPanelWrapper.GlassPanelPositions.Internal, load.LoadRestrainCondition);

                }
                else
                    throw new NotSupportedException();
            });
            
            return (externalPanelLoad, internalPanelLoad);
        }
        
        /// <summary>
        /// ref EN 16612 B.3
        /// </summary>
        protected double GetEN16612Getk5Coefficient(double nuPoisson, double lambda)
        {
            double z1 = 192.0 * (1.0 - Math.Pow(nuPoisson, 2)) * Math.Pow(lambda, 2) * (0.00406 + 0.00896 * (1.0 - Math.Exp(-1.123 * Math.Pow(1.0 / lambda - 1.0, 1.097))));

            return z1 / (16.0 * Math.Pow(lambda, 2)) * (0.4198 + 0.22 * Math.Exp(-6.8 * Math.Pow(lambda, 1.33)));
        }


        protected override void SetUpWrappers()
        {
            if (!(Glass is DoubleInsulatingGlass igu))
                throw new ArgumentException();

            if (igu.GlassPanelInner is MonolithicGlass mg)
            {
                _innerGlassPanelWrapper = new MonolithicGlassWrapper(_glassSurface, mg, GlassPanelWrapper.GlassPanelPositions.Internal);

            }
            else if (igu.GlassPanelInner is LaminatedGlass lg)
            {
                _innerGlassPanelWrapper = new LaminatedGlassWrapper(_glassSurface, lg, GlassPanelWrapper.GlassPanelPositions.Internal);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (igu.GlassPanelOuter is MonolithicGlass mgOut)
            {
                _outerGlassPanelWrapper = new MonolithicGlassWrapper(_glassSurface, mgOut, GlassPanelWrapper.GlassPanelPositions.External);

            }
            else if (igu.GlassPanelOuter is LaminatedGlass lgOut)
            {
                _outerGlassPanelWrapper = new LaminatedGlassWrapper(_glassSurface, lgOut, GlassPanelWrapper.GlassPanelPositions.External);
            }
            else
            {
                throw new NotSupportedException();
            }
        }


        public override bool GenerateMesh()
        {
            return false;
        }

    }
}
