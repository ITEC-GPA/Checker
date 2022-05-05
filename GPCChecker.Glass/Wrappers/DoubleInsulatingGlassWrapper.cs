
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GPC.Model.Glasses;
using GPC.Model.Fem.Materials;
using GPC.Model.Fem.Properties;
using GPC.Model.Combinations;
using GPC.Model.Restrains;
using GPC.Model.FreedomCases;
using GPC.Model.Results;
using GPC.Geometry.Meshes;
using GPC.Geometry;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Loads;
using GPC.Checkers.Glasses.LoadCases;
using System.Collections.Concurrent;

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


        /// <inheritdoc cref="InsulatedGlassWrapper.GetRedistributionPressures(IEnumerable{IGlassLoad}, Models.Prototype.Standards, bool, double)"/>>
        /// <returns>
        /// An array of loads. Each load correspond to a different loadcase
        /// First index: External slab load. 
        /// Second index: Internal slab load.
        /// </returns>
        internal override List<NormalAreaLoad>[] GetRedistributionPressures(IEnumerable<IGlassLoad> loads, 
                                                                            Models.Prototype.Standards standard,
                                                                            bool compressibleGas, 
                                                                            double cavitySealingPressure = 0.1)
        {

            List<IGlassLoad> loadsToProcess = loads.ToList(); // shallow copy

            List<NormalAreaLoad>[] redistributionPressure = new List<NormalAreaLoad>[2];
            redistributionPressure[0] = new List<NormalAreaLoad>(); // External
            redistributionPressure[1] = new List<NormalAreaLoad>(); // Internal


            if (_glassSurface.IsRectangular())
            {
                if (!compressibleGas)
                {

                    /*
                     *              int
                     *              _______________________
                     *                       ˄
                     *       ˄               | DeltaP positivo per teoria
                     *       |z       
                     *                       | DeltaP Positivo per teoria
                     *              _________˅_____________
                     *              ext
                     */

                    // si cambia segno a lastra esterna in quanto è inversa alla normale

                    List<NormalAreaLoad> normalAreaLoads = loadsToProcess.Where(i => i.GetType() == typeof(NormalAreaLoad)).Select(i => (NormalAreaLoad)i).ToList();

                    if (normalAreaLoads.Count != 0)
                    {
                        if (standard == Models.Prototype.Standards.EN16612)
                        {

                            foreach (KeyValuePair<IGlassLoadCase, double> nal in GetEN16612RedistributionPressure(normalAreaLoads))
                            {
                                redistributionPressure[0].Add(new NormalAreaLoad(-nal.Value, _glassSurface.Shape, nal.Key, GlassPanelWrapper.GlassPanelPositions.External));
                                redistributionPressure[1].Add(new NormalAreaLoad(+nal.Value, _glassSurface.Shape, nal.Key, GlassPanelWrapper.GlassPanelPositions.Internal));
                            }

                            foreach (var item in normalAreaLoads)
                            {
                                loadsToProcess.Remove(item);
                            }
                        }
                        else if (standard == Models.Prototype.Standards.ASTME1300)
                        {

                            foreach (KeyValuePair<IGlassLoadCase, double> nal in GetASTME1300RedistributionPressure(normalAreaLoads))
                            {
                                redistributionPressure[0].Add(new NormalAreaLoad(-nal.Value, _glassSurface.Shape, nal.Key, GlassPanelWrapper.GlassPanelPositions.External));
                                redistributionPressure[1].Add(new NormalAreaLoad(+nal.Value, _glassSurface.Shape, nal.Key, GlassPanelWrapper.GlassPanelPositions.Internal));
                            }

                            foreach (var item in normalAreaLoads)
                            {
                                loadsToProcess.Remove(item);
                            }
                        }
                        else
                        {
                            // passiamo ad altri metodi senza lanciare errori
                        } 
                    }


                }
                else
                {
                    // si usa metodo galuppi da articolo: Pratical expression bam...

                    List<NormalAreaLoad> normalAreaLoads = loadsToProcess.Where(i => i.GetType() == typeof(NormalAreaLoad)).Select(i => (NormalAreaLoad)i).ToList();

                    Dictionary<IGlassLoadCase, double> redistributionPressures = GetBAMLoadSharingPressureRectangularTheoretical(normalAreaLoads,
                                                                                                                                GetMinimumElasticModulus(), 
                                                                                                                                GetMinimumPoissonRatio(),
                                                                                                                                AirThickness * _glassSurface.GetArea(),
                                                                                                                                _glassSurface.GetArea(),
                                                                                                                                cavitySealingPressure);
                    
                    foreach (var nal in redistributionPressures)
                    {
                        redistributionPressure[0].Add(new NormalAreaLoad(-nal.Value, _glassSurface.Shape, nal.Key, GlassPanelWrapper.GlassPanelPositions.External));
                        redistributionPressure[1].Add(new NormalAreaLoad(nal.Value, _glassSurface.Shape, nal.Key, GlassPanelWrapper.GlassPanelPositions.Internal));
                    }

                    foreach (var item in normalAreaLoads)
                    {
                        loadsToProcess.Remove(item);
                    }
                }

            }
            

            // se rimangono carichi non processati si procede con il numerico
            if (loadsToProcess.Count > 0)
            {
                foreach(var (external, _internal) in GetBAMNumericalRedistributionPressures(loadsToProcess, compressibleGas, cavitySealingPressure))
                {
                    redistributionPressure[0].Add(external);
                    redistributionPressure[1].Add(_internal);
                }                
            }

            return redistributionPressure;
        }



        /// <summary>
        /// Load sharing procedure according to EN16612 C1. 
        /// This algorithm is valid only for rectangular shapes
        /// </summary>
        protected Dictionary<IGlassLoadCase, double> GetEN16612RedistributionPressure(List<NormalAreaLoad> loads)
        {

            // h1: ext
            // h2: int

            Dictionary<IGlassLoadCase, double> redistributionPressures = new Dictionary<IGlassLoadCase, double>();


            foreach (NormalAreaLoad load in loads)
            {

                double minPoisson = GetMinimumPoissonRatio();

                double h1Cube = Math.Pow(OuterGlassPanelWrapper.GetDeformationThickness(load.GlassLoadCase.Name), 3.0);
                double h2Cube = Math.Pow(InnerGlassPanelWrapper.GetDeformationThickness(load.GlassLoadCase.Name), 3.0);

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

                if (!redistributionPressures.ContainsKey(load.GlassLoadCase))
                    redistributionPressures[load.GlassLoadCase] = 0;

                if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.External)
                {
                    redistributionPressures[load.GlassLoadCase] += delta2 * (1 - fi) * load.Pressure;
                }
                else if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.Internal)
                {
                    redistributionPressures[load.GlassLoadCase] += - delta1 * (1 - fi) * load.Pressure;
                }
                else
                    throw new NotSupportedException();

            }


            return redistributionPressures;
        }


        /// <summary>
        /// Load sharing procedure according to ASTM E1300 §X3
        /// This algorithm is valid only for rectangular shapes
        /// </summary>
        protected Dictionary<IGlassLoadCase, double> GetASTME1300RedistributionPressure(List<NormalAreaLoad> loads)
        {
            // h1: ext
            // h2: int

            Dictionary<IGlassLoadCase, double> redistributionPressures = new Dictionary<IGlassLoadCase, double>();

            foreach (NormalAreaLoad load in loads)
            {

                double h1Cube = Math.Pow(OuterGlassPanelWrapper.GetDeformationThickness(load.GlassLoadCase.Name), 3.0);
                double h2Cube = Math.Pow(InnerGlassPanelWrapper.GetDeformationThickness(load.GlassLoadCase.Name), 3.0);

                double lsf1 = h1Cube / (h1Cube + h2Cube);
                double lsf2 = h2Cube / (h1Cube + h2Cube);

                if (!redistributionPressures.ContainsKey(load.GlassLoadCase))
                    redistributionPressures[load.GlassLoadCase] = 0;

                if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.External)
                {
                    redistributionPressures[load.GlassLoadCase] += load.Pressure * lsf2;
                }
                else if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.Internal)
                {
                    redistributionPressures[load.GlassLoadCase] += - load.Pressure * lsf1;
                }
                else
                    throw new NotSupportedException();
            }
            

            return redistributionPressures;
        }


        /// <param name="loads"></param>
        /// <param name="compressibleGas"></param>
        /// <param name="cavitySealingPressure"></param>
        /// <returns>The redistribution pressures for each unique loadCase</returns>
        protected virtual (NormalAreaLoad external, NormalAreaLoad _internal)[] GetBAMNumericalRedistributionPressures(List<IGlassLoad> loads,
                                                                                                                       bool compressibleGas,
                                                                                                                       double cavitySealingPressure)
        {

            // h1: ext
            // h2: int
            
            /*
                1) Converto tutti i carichi in pressioni normali
                2) Creo un modello per ogni lastra. In modo da calcolare più facilemente l'integrale per i carichi esterni ed interni
                     Nota. il problema principale è che se due carichi con lo stesso loadcase sono applicati nello stesso plate, il secondo sovrascrive il primo.
                           Però se sono su lastre diverse no.
                3) Ad ogni modello aggiungo una pressione uniforme più tutti i carichi convertiti in pressione uniforme
                4) Risolvo solo la combo con pressione uniform. I risultati degli altri carichi non mi interessano
                5) Calcolo integrale di phi su tutta l'area
                6) Calcolo integrale al numeratore come sommatoria carico * area plate * psi plate
                7) Ritorno la pressione per ridistribzione per ogni loadcase unico.
                   Può esistere il caso in cui i carichi arrivino nello stesso loadcase,
                   in quel caso la pressione di ridistribuzione è unica e fa riferimento alla somma degli effetti
            */

            if (compressibleGas && cavitySealingPressure <= 0)
                throw new ArgumentException("cavitySealingPressure <= 0");

            List<IGlassLoadCase> distinctLoadCase = loads.Select(i => i.GlassLoadCase).Distinct().ToList();

            Dictionary<GlassPanelWrapper.GlassPanelPositions, IGlassLoad[]> loadsGroupedByPostion = 
                                loads.GroupBy(i => i.GlassPanelPosition).ToDictionary(i => i.Key, i => i.ToArray());

            Dictionary<GlassPanelWrapper.GlassPanelPositions, Dictionary<IGlassLoadCase, IGlassLoad[]>> glassPositionLoadCaseLoadMap = 
                                loadsGroupedByPostion.ToDictionary(j => j.Key, j => j.Value.GroupBy(i => i.GlassLoadCase).ToDictionary(k => k.Key, k => k.ToArray()));

            Dictionary<IGlassLoadCase, NormalAreaLoad[]> glassLoadCaseNormalAreaLoadMapExternal = null;
            Dictionary<IGlassLoadCase, NormalAreaLoad[]> glassLoadCaseNormalAreaLoadMapInternal = null;
            if (glassPositionLoadCaseLoadMap.ContainsKey(GlassPanelWrapper.GlassPanelPositions.External))
            {
                glassLoadCaseNormalAreaLoadMapExternal =
                    ConvertLoadToNormalAreaLoads(_glassSurface.Shape.GetPlane(), glassPositionLoadCaseLoadMap[GlassPanelWrapper.GlassPanelPositions.External]);

            }
            if (glassPositionLoadCaseLoadMap.ContainsKey(GlassPanelWrapper.GlassPanelPositions.Internal))
            {
                glassLoadCaseNormalAreaLoadMapInternal =
                    ConvertLoadToNormalAreaLoads(_glassSurface.Shape.GetPlane(), glassPositionLoadCaseLoadMap[GlassPanelWrapper.GlassPanelPositions.Internal]);

            }


            // CREAZIONE MODELLO
            FemModels.FemModelWrapper femModelExternal = new FemModels.FemModelWrapper("BAMNumericalExternal")
            {
                AnalysisType = Model.Fem.FemModel.AnalysisTypes.Linear
            };
            FemModels.FemModelWrapper femModelInternal = new FemModels.FemModelWrapper("BAMNumericalInternal")
            {
                AnalysisType = Model.Fem.FemModel.AnalysisTypes.Linear
            };

            IsotropicFemMaterial material = new IsotropicFemMaterial(GetMinimumElasticModulus(), GetMinimumPoissonRatio(), 0, GetMaximumDensity());
            PlateProperty property = new PlateProperty(material, 1, 1, "p1");
            femModelExternal.AddProperty(property);
            femModelInternal.AddProperty(property);


            // Aggiungo:
            //      carico di pressione uniforme
            //      resto dei carichi tranne quelli di pressione uniforme su tutta l'area del vetro che hanno un loadcase indipendente dagli altri
            LoadCase lcUniformPressure = new LoadCase(Guid.NewGuid().ToString(), 1, 10, Model.LoadCases.LoadCase.LoadCaseTypes.WindPressure);
            List<IGlassLoad> loadToFemExternal = new List<IGlassLoad>
            {
                new NormalAreaLoad(1, _glassSurface.Shape, lcUniformPressure)
            }; 
            List<IGlassLoad> loadToFemInternal = new List<IGlassLoad>
            {
                new NormalAreaLoad(1, _glassSurface.Shape, lcUniformPressure)
            };

            if (glassLoadCaseNormalAreaLoadMapExternal != null)
                loadToFemExternal.AddRange(glassLoadCaseNormalAreaLoadMapExternal.SelectMany(i => i.Value));

            if (glassLoadCaseNormalAreaLoadMapInternal != null)
                loadToFemInternal.AddRange(glassLoadCaseNormalAreaLoadMapInternal.SelectMany(i => i.Value).ToList());


            // dimensione mesh di default 2% del massimo lato della bbox. Alla Straus
            var bbboxSize = _glassSurface.Shape.ToLocal().GetBoundingBox().Size;
            femModelExternal.AddShape(_glassSurface.Shape,
                                      property.Name,
                                      new Mesh.GenerateOptions() { MeshSize = Math.Max(bbboxSize.X, bbboxSize.Y) * 0.02 },
                                      loadToFemExternal.Cast<Model.Loads.Load>().ToList(),
                                      _glassSurface.Shape.Fill.Explode().Select(i =>
                                      (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList(),
                                      out Dictionary<Model.Loads.Load, int[]> loadNodeIdMapExt,
                                      out Dictionary<Model.Loads.Load, int[]> loadPlateIdMapExt,
                                      out Dictionary<GeometryRestrain, int[]> _);

            femModelInternal.AddShape(_glassSurface.Shape,
                                      property.Name,
                                      new Mesh.GenerateOptions() { MeshSize = Math.Max(bbboxSize.X, bbboxSize.Y) * 0.02 },
                                      loadToFemInternal.Cast<Model.Loads.Load>().ToList(),
                                      _glassSurface.Shape.Fill.Explode().Select(i =>
                                      (GeometryRestrain)LineRestrain.GetAllDisplacementFixed(i, new FreedomCase("fc1"), CoordinateSystem.Global)).ToList(),
                                      out Dictionary<Model.Loads.Load, int[]> loadNodeIdMapInt,
                                      out Dictionary<Model.Loads.Load, int[]> loadPlateIdMapInt,
                                      out Dictionary<GeometryRestrain, int[]> _);

            Combination cmb = new Combination("cmb");
            cmb.AddLoadCaseCoefficient(lcUniformPressure, 1);
            femModelExternal.AddCombination(cmb);
            femModelInternal.AddCombination(cmb);


            femModelExternal.ExportToSt7(System.IO.Path.GetTempPath(), femModelExternal.Name);
            femModelExternal.SetSolver(Models.Prototype.Solvers.Straus7);

            femModelInternal.ExportToSt7(System.IO.Path.GetTempPath(), femModelInternal.Name);
            femModelInternal.SetSolver(Models.Prototype.Solvers.Straus7);

            femModelExternal.Solve();
            femModelInternal.Solve();


            Model.Fem.FiniteElements.FiniteElement[] elementsExt = femModelExternal.GetElements();
            Model.Fem.FiniteElements.FiniteElement[] elementsInt = femModelInternal.GetElements();

            // CALCOLO DEL COEFFICIENTE PSI NUMERICO

            Task<(double psiAreaIntegral, Dictionary<string, double> loadPsiIntegral)> taskExt = CalculateBAMIntegral(elementsExt, cmb, lcUniformPressure.Name);
            Task<(double psiAreaIntegral, Dictionary<string, double> loadPsiIntegral)> taskInt = CalculateBAMIntegral(elementsInt, cmb, lcUniformPressure.Name);

            Task.WaitAll(taskExt, taskInt);

            if (Math.Abs(Math.Abs(taskExt.Result.psiAreaIntegral) - Math.Abs(taskInt.Result.psiAreaIntegral)) > Math.Abs(taskExt.Result.psiAreaIntegral) / 10)
            {
                System.Diagnostics.Debug.WriteLine($"EXT:{taskExt.Result.psiAreaIntegral} INT:{taskInt.Result.psiAreaIntegral}");
                throw new ArithmeticException("Internal and External psi internal are different");
            }

            //System.Diagnostics.Debug.WriteLine($"EXT:{taskExt.Result.psiAreaIntegral} " +
            //                                   $"INT:{taskInt.Result.psiAreaIntegral} " +
            //                                   $"{Math.Abs(taskExt.Result.psiAreaIntegral) - Math.Abs(taskInt.Result.psiAreaIntegral)}");

            // CALCOLO DELLA DELTA P

            double num = 0;
            double denExt = 0;
            double denInt = 0;
            int index = 0;
            (NormalAreaLoad external, NormalAreaLoad _internal)[] redistributionPressure 
                = new (NormalAreaLoad external, NormalAreaLoad _internal)[distinctLoadCase.Count()];

            double E = GetMinimumElasticModulus();
            double ni = GetMinimumPoissonRatio();

            double area = 0;
            double factor1FlexuarStiffnessStar = 0;
            double factor2FlexuarStiffnessStar = 0;

            if (compressibleGas)
            {
                area = Area;
                factor1FlexuarStiffnessStar = E / (12.0 * (1.0 - Math.Pow(ni, 2.0)));
                factor2FlexuarStiffnessStar = AirThickness * area / Math.Pow(area, 3.0) / cavitySealingPressure;
            }

            for (int i = 0; i < distinctLoadCase.Count; i++)
            {
                var loadCase = distinctLoadCase[i];

                double deltaP = 0;

                double h1Cube = Math.Pow(OuterGlassPanelWrapper.GetDeformationThickness(loadCase.Name), 3.0);
                double h2Cube = Math.Pow(InnerGlassPanelWrapper.GetDeformationThickness(loadCase.Name), 3.0);

                double flexuarStiffnessStar = 0;
                if (compressibleGas)
                {
                    flexuarStiffnessStar = factor1FlexuarStiffnessStar * h1Cube * h2Cube * factor2FlexuarStiffnessStar;
                }

                denExt = (h1Cube + h2Cube) * taskExt.Result.psiAreaIntegral + flexuarStiffnessStar; // calcoliamo due denominatori perchè numericamente è più preciso, dato che le mesh sono diverse fra i due pannelli
                denInt = (h1Cube + h2Cube) * taskInt.Result.psiAreaIntegral + flexuarStiffnessStar;

                if (denExt == 0 && denInt == 0)
                    throw new ArithmeticException("Bam delta p divide by zero");

                if (taskExt.Result.loadPsiIntegral.ContainsKey(loadCase.Name))
                {
                    num = h2Cube * taskExt.Result.loadPsiIntegral[loadCase.Name] / denExt;
                }
                if (taskInt.Result.loadPsiIntegral.ContainsKey(loadCase.Name))
                {
                    num -= h1Cube * taskInt.Result.loadPsiIntegral[loadCase.Name] / denInt;
                }

                //deltaP = num / denExt;
                deltaP = num;

                /*
                 *              int
                 *              _______________________
                 *                       ˄
                 *       ˄               | DeltaP positivo per teoria
                 *       |z       
                 *                       | DeltaP Positivo per teoria
                 *              _________˅_____________
                 *              ext
                 */

                // si cambia segno a lastra esterna in quanto è inversa alla normale

                redistributionPressure[index].external = new NormalAreaLoad(-deltaP, _glassSurface.Shape, loadCase, GlassPanelWrapper.GlassPanelPositions.External);
                redistributionPressure[index]._internal = new NormalAreaLoad(deltaP, _glassSurface.Shape, loadCase, GlassPanelWrapper.GlassPanelPositions.Internal);

                index++;

            }


            return redistributionPressure;
        }


        protected Dictionary<IGlassLoadCase, NormalAreaLoad[]> ConvertLoadToNormalAreaLoads(Plane referencePlane, Dictionary<IGlassLoadCase, IGlassLoad[]> glassLoadCaseLoadMap)
        {
            Dictionary<IGlassLoadCase, NormalAreaLoad[]> glassLoadCaseNormalAreaLoadMap = new Dictionary<IGlassLoadCase, NormalAreaLoad[]>();

            foreach (var kvp in glassLoadCaseLoadMap)
            {
                IGlassLoadCase glassLoadCase = kvp.Key;

                List<NormalAreaLoad> normalAreaLoads = new List<NormalAreaLoad>();

                for (int i = 0; i < kvp.Value.Count(); i++)
                {
                    if (kvp.Value[i] is NormalAreaLoad nal)
                    {
                        normalAreaLoads.Add(nal);
                    }
                    else if (kvp.Value[i] is AreaLoad al)
                    {
                        normalAreaLoads.Add((NormalAreaLoad)al.ConvertToNormalAreaLoad());
                    }
                    else if (kvp.Value[i] is LineLoad ll)
                    {
                        normalAreaLoads.Add((NormalAreaLoad)ll.ConvertToNormalAreaLoad(referencePlane, _glassSurface.Checker.Options.LineLoadWidthEqThickness));
                    }
                    else if (kvp.Value[i] is PointLoad pl)
                    {
                        normalAreaLoads.Add((NormalAreaLoad)pl.ConvertToNormalAreaLoad(referencePlane, _glassSurface.Checker.Options.LineLoadWidthEqThickness));
                    }
                    else if (kvp.Value[i] is SelfWeightLoad swl)
                    {
                        var vect = swl.GravityVector;
                        normalAreaLoads.Add(new NormalAreaLoad(vect.DotProduct(referencePlane.Normal), _glassSurface.Shape, kvp.Value[i].GlassLoadCase));

                    }
                    else
                        throw new NotImplementedException();
                }
                
                if (normalAreaLoads.Count() > 0)
                    glassLoadCaseNormalAreaLoadMap[glassLoadCase] = normalAreaLoads.ToArray();
            }

            return glassLoadCaseNormalAreaLoadMap;
        }

        protected async Task<(double psiAreaIntegral, Dictionary<string, double> loadPsiIntegral)> CalculateBAMIntegral(Model.Fem.FiniteElements.FiniteElement[] elements, Combination referenceCombination, string uniformPressureLoadCaseName)
        {

            ConcurrentBag<double> psiAreaIntegral = new ConcurrentBag<double>();
            Dictionary<string, double> loadPsiIntegral = new Dictionary<string, double>();

            Action<int> action = new Action<int>((index) =>
            {
                if (elements[index] is Model.Fem.FiniteElements.Plate plate)
                {
                    // Spostamenti del caso di pressione uniforme unitaria. Servono per calcolare la funziona di forma PSI

                    IEnumerable<ResultDisplacement> uniformPressureDisplacements = plate.Nodes.Select(i => i.Results
                                                                                   .FirstOrDefault(j => (Combination)j.Case == referenceCombination).Result)
                                                                                   .Cast<ResultDisplacement>();

                    if (uniformPressureDisplacements is null)
                        throw new ArgumentNullException();

                    // Integrale di psi su tutta l'area
                    double plateIntegral = plate.GetArea() * ResultDisplacement.GetArithmeticMean(uniformPressureDisplacements.ToArray()).D3;
                    
                    psiAreaIntegral.Add(plateIntegral);

                    foreach (var attribute in plate.AttributesLoadCase.Where(i => i.LoadCaseName != uniformPressureLoadCaseName))
                    {
                        // non considero il primo che è la pressione uniforme

                        if (attribute is Model.FEM.Attributes.PlateNormalPressureAttribute pnal)
                        {
                            if (loadPsiIntegral.ContainsKey(pnal.LoadCaseName))
                            {
                                loadPsiIntegral[pnal.LoadCaseName] += pnal.Pressure * plateIntegral;
                            }
                            else
                                loadPsiIntegral[pnal.LoadCaseName] = pnal.Pressure * plateIntegral;
                        }
                    }
                }
            });

            await Task.Run(() => Parallel.ForEach(Enumerable.Range(0, elements.Count()), action));

            return (psiAreaIntegral.Sum(), loadPsiIntegral);
        }

        /// <summary>
        /// Get the PhiA coefficient according to formula 3.10 Pratical expression bam
        /// </summary>
        /// <param name="maxSize"></param>
        /// <param name="minSize"></param>
        /// <returns></returns>
        protected double GetBAMPhiATheoretical(double maxSize, double minSize)
        {
            if (minSize > maxSize)
                throw new ArgumentException();

            double a = 0;
            double lambda2 = Math.Pow(minSize / maxSize, 2);

            for (int m = 0; m < 17; m++)
            {
                if (m % 2 != 0)
                {
                    double m2 = Math.Pow(m, 2);
                    for (int n = 0; n < 17; n++)
                    {
                        if (n % 2 != 0)
                        {
                            a += 1.0 / (m2 * Math.Pow(n, 2) * Math.Pow(m2 + Math.Pow(n, 2) * lambda2, 2));
                        }
                    }
                }
            }

            return a * 64 * lambda2 / Math.Pow(Math.PI, 8);
        }



        /// <summary>
        /// Get the deltaP due to pressure from theoretical approach for rectangular surfaces. Formula 2.3 Pratical expression bam
        /// </summary>
        /// <remarks><see cref="GetBAMLoadSharingPressureRectangularTheoretical(IEnumerable{NormalAreaLoad}, double, double)"/> 
        /// This method does not consider the gas comprimibilty</remarks>
        private Dictionary<IGlassLoadCase, double> GetBAMLoadSharingPressureRectangularTheoretical(IEnumerable<NormalAreaLoad> loads,
                                                                                                    double E, double ni)
        {
             return GetBAMLoadSharingPressureRectangularTheoretical(loads, E, ni, 0, 0, 0);
        }


        /// <summary>
        /// Get the deltaP due to pressure from theoretical approach for rectangular surfaces. Formula 2.3 Pratical expression bam
        /// </summary>
        /// <remarks>This method consider the gas comprimibilty</remarks>
        private Dictionary<IGlassLoadCase, double> GetBAMLoadSharingPressureRectangularTheoretical(IEnumerable<NormalAreaLoad> loads,
                                                                                                    double E, double ni, 
                                                                                                    double v0, double area, 
                                                                                                    double p0)
        {

            /*
                *       int
                *       _______________________
                *                ˄
                *   ˄            | DeltaP positivo per teoria
                *   |z    
                *                | DeltaP Positivo per teoria
                *       _________˅_____________
                *       ext
            */

            if (E <= 0 || ni <= 0 || v0 <= 0 || area <= 0 || p0 <= 0)
                throw new ArgumentException();

            Dictionary<IGlassLoadCase, double> redistributionPressures = new Dictionary<IGlassLoadCase, double>();

            double phiA = GetBAMPhiATheoretical(_glassSurface.Shape.Fill.Explode().Select(i => i.GetLength()).Max(),
                                                _glassSurface.Shape.Fill.Explode().Select(i => i.GetLength()).Min());

            foreach (NormalAreaLoad load in loads)
            {

                double h1 = OuterGlassPanelWrapper.GetDeformationThickness(load.LoadCase.Name);
                double h2 = InnerGlassPanelWrapper.GetDeformationThickness(load.LoadCase.Name);

                double d1 = E * Math.Pow(h1, 3) / (12.0 * (1 - ni * ni));
                double d2 = E * Math.Pow(h2, 3) / (12.0 * (1 - ni * ni));

                double deltaPExt = 0;
                double deltaPInt = 0;

                if (p0 == 0 || area == 0 || v0 == 0)
                {
                    deltaPExt = 1.0 / d1 * phiA / ((1.0 / d1 + 1.0 / d2) * phiA);
                    deltaPInt = 1.0 / d2 * phiA / ((1.0 / d1 + 1.0 / d2) * phiA);
                }
                else
                {
                    deltaPExt = 1.0 / d1 * phiA / ((1.0 / d1 + 1.0 / d2) * phiA + v0 / Math.Pow(area, 3) / p0);
                    deltaPInt = 1.0 / d2 * phiA / ((1.0 / d1 + 1.0 / d2) * phiA + v0 / Math.Pow(area, 3) / p0);
                }


                if (!redistributionPressures.ContainsKey(load.GlassLoadCase))
                    redistributionPressures[load.GlassLoadCase] = 0;

                if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.External)
                {
                    redistributionPressures[load.GlassLoadCase] += +deltaPExt * load.Pressure ;
                }
                else if (load.GlassPanelPosition == GlassPanelWrapper.GlassPanelPositions.Internal)
                {
                    redistributionPressures[load.GlassLoadCase] += -deltaPInt * load.Pressure;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            return redistributionPressures;
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
