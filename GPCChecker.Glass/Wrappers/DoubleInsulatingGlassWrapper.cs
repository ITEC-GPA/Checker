
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Loads;
using GPC.Model.Glasses;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using GPC.Model.FEM.Materials;
using GPC.Model.FEM.Properties;
using GPC.Geometry.Meshes;
using GPC.Model.Combinations;
using GPC.Model.Restrains;
using GPC.Model.FreedomCases;
using GPC.Geometry;
using GPC.Model.Results;

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


        internal override List<NormalAreaLoad>[] GetLoadSharing(IEnumerable<IGlassLoad> loads, Models.Prototype.Standards standard)
        {
            List<IGlassLoad> loadsToProcess = loads.ToList(); // shallow copy

            List<NormalAreaLoad>[] redistributionPressure = new List<NormalAreaLoad>[2];
            redistributionPressure[0] = new List<NormalAreaLoad>(); // External
            redistributionPressure[1] = new List<NormalAreaLoad>(); // Internal


            if (_glassSurface.IsRectangular())
            {

                List<NormalAreaLoad> normalAreaLoads = loadsToProcess.Where(i => i.GetType() == typeof(NormalAreaLoad)).Select(i => (NormalAreaLoad)i).ToList();

                foreach (var nal in normalAreaLoads)
                {
                    // Normativa
                    if (standard == Models.Prototype.Standards.EN16612)
                    {
                        var bufferLoads = GetEN16612PressureSharing(nal);

                        redistributionPressure[0].Add(bufferLoads.external);
                        redistributionPressure[1].Add(bufferLoads._internal);

                        loadsToProcess.Remove(nal);
                        continue;
                    }
                    else if (standard == Models.Prototype.Standards.ASTME1300)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        // passiamo agli altri metodi senza lanciare errori
                    }
                }
            }


            if (loadsToProcess.Count > 0)
            {
                foreach(var rediLoads in GetBAMNumericalLoadSharing(loadsToProcess, true, 1))
                {
                    redistributionPressure[0].Add(rediLoads.external);
                    redistributionPressure[1].Add(rediLoads._internal);
                }                
            }



            return redistributionPressure;
        }


        /// <summary>
        /// Load sharing procedure according to EN16612 C1. 
        /// This algorithm is valid only for rectangular shapes
        /// </summary>
        protected (NormalAreaLoad external, NormalAreaLoad _internal) GetEN16612PressureSharing(NormalAreaLoad load)
        {
            // h1: ext
            // h2: int

            NormalAreaLoad externalPanelLoad = null;
            NormalAreaLoad internalPanelLoad = null;

            double h1Cube = 0;
            double h2Cube = 0;
            double minPoisson = GetMinimumPoissonRatio();

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

            return (externalPanelLoad, internalPanelLoad);
        }
        

        protected (NormalAreaLoad external, NormalAreaLoad _internal)[] GetBAMNumericalLoadSharing(List<IGlassLoad> loads, bool compressibleGas, double cavitySealingPressure)
        {

            // h1: ext
            // h2: int

            if (compressibleGas && cavitySealingPressure <= 0)
                throw new ArgumentException("cavitySealingPressure <= 0");

            (NormalAreaLoad external, NormalAreaLoad _internal)[] redistributionPressure = new (NormalAreaLoad external, NormalAreaLoad _internal)[loads.Count];
            List<Model.Loads.NormalAreaLoad> normalAreaLoads = new List<Model.Loads.NormalAreaLoad>();


            var surfacePlane = _glassSurface.Shape.GetPlane();

            // Converto i carichi in pressioni normali
            for (int i = 0; i < loads.Count; i++)
            {
                if (loads[i] is NormalAreaLoad)
                {
                    continue;
                }
                else if (loads[i] is AreaLoad al)
                    normalAreaLoads.Add(al.ConvertToNormalAreaLoad());
                else if (loads[i] is LineLoad ll)
                    normalAreaLoads.Add(ll.ConvertToNormalAreaLoad(surfacePlane, _glassSurface.Checker.Options.LineLoadWidthEqThickness));
                else if (loads[i] is PointLoad pl)
                    normalAreaLoads.Add(pl.ConvertToNormalAreaLoad(surfacePlane, _glassSurface.Checker.Options.LineLoadWidthEqThickness));
                else if (loads[i] is SelfWeightLoad swl)
                {
                    var vect = swl.GravityVector * swl.Acceleration; // TODO: cambiare prop swl

                    normalAreaLoads.Add(new NormalAreaLoad(vect.DotProduct(surfacePlane.Normal), _glassSurface.Shape, loads[i].GlassLoadCase));

                }
                else
                    throw new NotImplementedException();
            }
            

            // CREAZIONE MODELLO
            FemModels.FemModelWrapper femModel = new FemModels.FemModelWrapper("BAMNumerical")
            {
                AnalysisType = Model.FEM.FemModel.AnalysisTypes.Linear
            };

            IsotropicFemMaterial material = new IsotropicFemMaterial(GetMinimumElasticModulus(), GetMinimumPoissonRatio(), 0, GetMaximumDensity());

            PlateProperty property = new PlateProperty(material, 1, 1, "p1");

            femModel.AddProperty(property);

            LoadCases.LoadCase lcUniformPressure = new LoadCases.LoadCase("UniformPressure", 1, 10, Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);

            // dimensione mesh di default 2% del massimo lato della bbox. Alla Straus
            var bbboxSize = _glassSurface.Shape.ToLocal().GetBoundingBox().Size;

            var loadToFem = new List<Model.Loads.Load>();
            loadToFem.Add(new NormalAreaLoad(1, _glassSurface.Shape, lcUniformPressure));
            loadToFem.AddRange(normalAreaLoads);

            femModel.AddShape(_glassSurface.Shape,
                              property.Name,
                              new Mesh.GenerateOptions() { MeshSize = Math.Max(bbboxSize.X, bbboxSize.Y) * 0.02 },
                              loadToFem,
                              _glassSurface.Shape.Fill.Explode().Select(i =>
                              (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList());

            var combinations = new List<Combination>
                {
                    new Combination($"Load {0}")
                };

            combinations[0].AddLoadCaseCoefficient(lcUniformPressure, 1);

            femModel.AddCombinations(combinations);

            femModel.ExportToSt7(System.IO.Path.GetTempPath(), femModel.Name);
            femModel.SetSolver(Models.Prototype.Solvers.Straus7);
            femModel.Solve();

            Model.FEM.FiniteElements.FiniteElement[] elements = femModel.GetElements();

            double h1Cube = Math.Pow(OuterGlassPanelWrapper.GetDeformationThickness((IGlassLoad)loadToFem[0]), 3.0); // TODO: sistemare load
            double h2Cube = Math.Pow(InnerGlassPanelWrapper.GetDeformationThickness((IGlassLoad)loadToFem[0]), 3.0);
            
            // CALCOLO DEL COEFFICIENTE PSI NUMERICO
            Dictionary<Model.FEM.FiniteElements.Plate, double> psiCoefficient = new Dictionary<Model.FEM.FiniteElements.Plate, double>();

            for (int e = 0; e < elements.Length; e++)
            {
                if (elements[e] is Model.FEM.FiniteElements.Plate plate)
                {
                    IEnumerable<ResultDisplacement> resultDisplacement = plate.Nodes.Select(i => i.Results.FirstOrDefault(j => j == combinations[0]).Result)
                                                    .Cast<ResultDisplacement>();

                    if (resultDisplacement is null)
                        throw new ArgumentNullException();

                    psiCoefficient[plate] = ResultDisplacement.GetArithmeticMean(resultDisplacement.ToArray()).D3;
                }
            }

            // CALCOLO DELLA DELTA P
            double meanPsiIntegral = psiCoefficient.Select(j => j.Key.GetArea() * j.Value).Sum();
            double[] num = new double[loads.Count];
            double[] den = new double[loads.Count];
            double doubleFlexuarStiffness = 0;
            double[] deltaP = new double[loads.Count];

            if (compressibleGas)
            {
                double area = Area;
                double cavityVolume = AirThickness * area;
                doubleFlexuarStiffness = GetMinimumElasticModulus() / (12.0 * (1.0 - Math.Pow(GetMinimumPoissonRatio(), 2.0))) 
                                                   * h1Cube * h2Cube * cavityVolume / Math.Pow(area, 3.0) / cavitySealingPressure;
            }

            for (int i = 0; i < loads.Count; i++)
            {
                // basta calcolare l'integrale del coefficinete psi
                if (loads[i] is NormalAreaLoad nal)
                {                   
                    if (nal.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.External)
                    {
                        // carico è f2
                        num[i] = nal.Pressure * h2Cube * meanPsiIntegral;
                    }
                    else
                    {
                        // carico è f1
                        num[i] = nal.Pressure * h1Cube;
                    }

                    den[i] = (h1Cube + h2Cube) * meanPsiIntegral + doubleFlexuarStiffness;
                }
                // se carico 


                if (den[i] == 0)
                    throw new ArithmeticException();

                deltaP[i] = num[i] / den[i];

                redistributionPressure[i].external = new NormalAreaLoad(deltaP[i], _glassSurface.Shape, loads[i].GlassLoadCase, GlassPanelWrapper.GlassPanelPositions.External);
                redistributionPressure[i]._internal = new NormalAreaLoad(deltaP[i], _glassSurface.Shape, loads[i].GlassLoadCase, GlassPanelWrapper.GlassPanelPositions.Internal);
            }


            return redistributionPressure;
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


        public override double GetMinimumElasticModulus()
        {
            return Math.Min(OuterGlassPanelWrapper.GetElasticModulus(), InnerGlassPanelWrapper.GetElasticModulus());
        }

        public override double GetMinimumPoissonRatio()
        {
            return Math.Min(OuterGlassPanelWrapper.GetPoissonRatio(), InnerGlassPanelWrapper.GetPoissonRatio());
        }

        public override double GetSelfWeightPerUnitArea()
        {
            return OuterGlassPanelWrapper.GetSelfWeightPerUnitArea() + InnerGlassPanelWrapper.GetSelfWeightPerUnitArea();
        }

        public override double GetSelfWeightTotal()
        {
            return OuterGlassPanelWrapper.GetSelfWeightTotal() + InnerGlassPanelWrapper.GetSelfWeightTotal();
        }

        public override double GetMaximumDensity()
        {
            return Math.Max(OuterGlassPanelWrapper.GetDensity(), InnerGlassPanelWrapper.GetDensity());
        }
    }
}
