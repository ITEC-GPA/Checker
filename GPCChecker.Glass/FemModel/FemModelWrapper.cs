
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using St7ApiWrapper;
using GPC.Geometry;
using GPC.Checker.Glasses.LoadCases;
using GPC.Checker.Glasses.Models;
using GPC.Model.FEM.Attributes;
using GPC.Model.FEM.FiniteElements;
using GPC.Model.FEM.Properties;
using GPC.Model.FreedomCases;
using GPC.Model.Restrains;
using GPC.Model.Results;
using GPC.Model.Materials;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Results;

namespace GPC.Checker.Glasses.FemModel
{
    public class FemModelWrapper : Model.FEM.FemModel
    {

        private string _st7ServerIp;
        private string _saveFolderPath;
        private string _st7FilePath;
        private string _st7LinearResultFilePath;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and St7ComboId  ;
        /// </summary>
        private Dictionary<Combination, int> _st7LSACombinationMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._loadCases"/> St7 loadcase id;
        /// </summary>
        private Dictionary<LoadCase, int> _st7LoadCaseMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._freedomCases"/> St7 fredomcase id;
        /// </summary>
        private Dictionary<FreedomCase, int> _st7FreedomCaseMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._plateProperties"/> St7 platepropertyID;
        /// </summary>
        private Dictionary<PlateProperty, int> _st7PlatePropertyMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.Node"/> id and straus7 node ID
        /// </summary>
        private Dictionary<int, int> _st7NodeMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FiniteElements.Plate"/> id and straus7 node ID
        /// </summary>
        private Dictionary<int, int> _st7PlateMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FiniteElements.Brick"/> id and straus7 node ID
        /// </summary>
        private Dictionary<int, int> _st7BrickMap;


        public FemModelWrapper() : this (string.Empty)
        {

        }

        public FemModelWrapper(string name) 
            : base(name)
        {
            _st7ServerIp = "localhost";
            _st7LSACombinationMap = new Dictionary<Combination, int>(new ModelObjectNameEqualityComparer());
            _st7LoadCaseMap = new Dictionary<LoadCase, int>(new ModelObjectNameEqualityComparer());
            _st7FreedomCaseMap = new Dictionary<FreedomCase, int>(new ModelObjectNameEqualityComparer());
            _st7PlatePropertyMap = new Dictionary<PlateProperty, int>(new ModelObjectNameEqualityComparer());

            _st7NodeMap = new Dictionary<int, int>();
            _st7PlateMap = new Dictionary<int, int>();
            _st7BrickMap = new Dictionary<int, int>();
        }

        public FemModelWrapper(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            throw new NotImplementedException();
        }


        #region RESULTS - public methods


        public GlassResult GetMaxWorkingRatio()
        {
            double ratio = 0;
            double stressResistance = 0;
            double worstStress = 0;
            foreach (var resultPlateStress in ResultPlateStresses)
            {
                double loadDuration = 0;

                if (resultPlateStress.Case is LoadCase lc)
                    loadDuration = lc.LoadDuration;
                else if (resultPlateStress.Case is Combination cmb)
                {
                    var _ = cmb.GetLoadCaseCoefficients(out List<Model.LoadCases.LoadCase> loadCases);
                    var lcCasted = loadCases.Cast<LoadCase>().ToList();
                    loadDuration = lcCasted.Select(i => i.LoadDuration).Min();
                }
                else
                    throw new NotImplementedException();

                if (resultPlateStress.Element is Plate plate)
                {
                    if (plate.Property is MonolithicGlassProperty mgp)
                    {
                        if (mgp.Material is GlassMaterial gm)
                        {
                            double res = gm.GetGlassResistance(false, loadDuration);

                            double r = Math.Abs(resultPlateStress.S11) / res;

                            if (r > ratio)
                            {
                                ratio = r;
                                stressResistance = res;
                                worstStress = resultPlateStress.S11;
                            }
                        }

                    }
                }
            }

            return new GlassResult(worstStress, stressResistance); ;
        }

        public void GetMaxDisplacement()
        {
            throw new NotImplementedException();
        }


        #endregion

        #region STRAUS7


        #region STRAUS7 - PUBLIC METHODS

        /// <summary>
        /// 
        /// </summary>
        /// <param name="saveFolderPath">Folder path where to save the results</param>
        public void SaveToSt7(string saveFolderPath)
        {
            if (string.IsNullOrEmpty(saveFolderPath) || string.IsNullOrWhiteSpace(saveFolderPath))
                throw new DirectoryNotFoundException();

            DirectoryInfo d = Directory.CreateDirectory(saveFolderPath);

            _saveFolderPath = d.FullName;

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                bool status = CreateSt7Model(aw, _saveFolderPath, out int mid, out List<string> warnings, out List<string> errors);

                if (status)
                    status = aw.SaveFile(mid);

                if (status)
                    status = aw.CloseFile(mid);

                if (!status)
                    throw new Exception($"St7 Error: {aw.GetLastErrorString()}");
            }
            else
            {
                throw new Exception($"Unable to connect to Apiservice through ip: {_st7ServerIp}");
            }

            if (channel != null)
                ChannelServices.UnregisterChannel(channel);
        }

        public void RunSt7Solver(Prototype.AnalysisTypes analysisTypes)
        {

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                int mid = 0;

                bool isOpened = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                if (isOpened)
                {
                    switch (analysisTypes)
                    {
                        case Prototype.AnalysisTypes.LinearStaticAnalisys:

                            bool status = St7LinearSolverSetup(aw, mid);
                            if (status)
                                aw.SaveFile(mid);
                            status = St7RunLinearSolver(aw, _st7FilePath);

                            _st7LinearResultFilePath = Path.ChangeExtension(_st7FilePath, "LSA");
                            break;

                        case Prototype.AnalysisTypes.NonLinearStaticAnalysis:
                            throw new NotImplementedException();

                        default:
                            throw new NotSupportedException($"Analysis type {analysisTypes} not supported");
                    }

                    aw.CloseFile(mid);
                }
            }
            else
            {
                throw new Exception($"Unable to connect to Apiservice through ip: {_st7ServerIp}");
            }

            if (channel != null)
                ChannelServices.UnregisterChannel(channel);

        }
        
        public void ReadSt7LinearCombinationResults()
        {
            if (File.Exists(_st7LinearResultFilePath))
            {
                if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
                {
                    int mid = 0;

                    int numPrimary = 0;     // LoadCase
                    int numSecondary = 0;   // Combinazioni

                    bool status = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                    if (status)
                        status = aw.OpenResultFile(mid, _st7LinearResultFilePath, string.Empty, Convert.ToByte(true), ref numPrimary, ref numSecondary);

                    if (status)
                    {

                        foreach (var combination in _combinations)
                        {
                            if (_st7LSACombinationMap.ContainsKey(combination))
                            {
                                int comboId = _st7LSACombinationMap[combination];

                                foreach (var element in _elements)
                                {
                                    if (element is Plate plate)
                                    {

                                        // LETTURA STRESS PLATE
                                        int numPoints = 0; // punti in cui straus da i risultati
                                        int numColumns = 0; // numero di risultati per punto

                                        double[] plateResults = new double[St7ApiConst.kMaxPlateResult];
                                        double[] angles = new double[9];
                                        aw.GetPlateResultArray(mid, St7ApiConst.rtPlateStress, St7ApiConst.stPlateLocal, plate.Id,
                                                               comboId + numPrimary, St7ApiConst.AtGaussPoints, St7ApiConst.psPlateZPlus, 0, ref numPoints, ref numColumns, ref plateResults);

                                        aw.GetPlateAxisSystem(mid, plate.Id, St7ApiConst.btTrue, ref angles);

                                        for (int np = 0; np < numPoints; np++)
                                        {
                                            for (int nc = 0; nc < numColumns; nc++)
                                            {
                                                _resultPlateStress.Add(
                                                    new ResultPlateStress(plate, combination,
                                                    new ResultStressPoint(np),
                                                    new CoordinateSystem(new Vector3d(angles[0], angles[1], angles[2]), new Vector3d(angles[3], angles[4], angles[5]), new Vector3d(angles[6], angles[7], angles[8])),
                                                    plateResults[nc * numColumns + 0], plateResults[nc * numColumns + 1], plateResults[nc * numColumns + 3], plateResults[nc * numColumns + 4], plateResults[nc * numColumns + 5]));

                                                _resultPlateStress.Last().GetPrincipalStress(out _, out _); // uso metodo approssimato
                                            }
                                        }
                                    }
                                }

                                foreach (var node in _nodes)
                                {
                                    double[] nodeResult = new double[6];
                                    aw.GetNodeResult(mid, St7ApiConst.rtNodeDisp, node.Id, comboId + numPrimary, ref nodeResult);
                                    _resultNodeDisplacements.Add(new ResultNodeDisplacement(node, combination, CoordinateSystem.Global, nodeResult[0], nodeResult[1], nodeResult[2], nodeResult[3], nodeResult[4], nodeResult[5]));
                                }


                            }

                        }

                        aw.CloseResultFile(mid);
                    }
                    else
                        aw.CloseFile(mid);


                    if (!status)
                        throw new Straus7Exception(aw.GetLastErrorString());
                }
                else
                {
                    throw new Exception($"Unable to connect to Apiservice through ip: {_st7ServerIp}");
                }

                if (channel != null)
                    ChannelServices.UnregisterChannel(channel);
            }
            else
                throw new FileNotFoundException(_st7LinearResultFilePath);

        }
        

        #endregion

        #region STRAUS7 - PRIVATE METHODS

        private static bool ConnectService(string ip, out ISt7ApiService ro, out TcpChannel channel)
        {
            ro = null;
            channel = null;
            try
            {
                channel = new TcpChannel();
                ChannelServices.RegisterChannel(channel, false);

                System.Threading.Thread.Sleep(1000);
                ro = (ISt7ApiService)Activator.GetObject(typeof(ISt7ApiService), string.Format("tcp://{0}:8085/St7ApiService", ip));
                return true;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="saveFolderPath">Folder where to save the St7 model</param>
        /// <param name="mId"></param>
        /// <param name="warnings"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        private bool CreateSt7Model(ISt7ApiService aw, string saveFolderPath, out int mId, out List<string> warnings, out List<string> errors)
        {
            warnings = new List<string>();
            errors = new List<string>();
            mId = 0;

            string scratchPath = Path.GetTempPath();
            _st7FilePath = Path.ChangeExtension(Path.Combine(saveFolderPath, Name), "St7");

            // Create a new model
            if (!aw.NewFile(_st7FilePath, scratchPath, ref mId))
                throw new Exception("Failed to create new model");

            // Units
            int[] st7Units = new int[St7ApiConst.kLastUnit];
            st7Units[St7ApiConst.ipLENGTHU] = St7ApiConst.luMILLIMETRE;
            st7Units[St7ApiConst.ipFORCEU] = St7ApiConst.fuNEWTON;
            st7Units[St7ApiConst.ipSTRESSU] = St7ApiConst.suMEGAPASCAL;
            st7Units[St7ApiConst.ipMASSU] = St7ApiConst.muTONNE;
            st7Units[St7ApiConst.ipTEMPERU] = St7ApiConst.tuCELSIUS;
            st7Units[St7ApiConst.ipENERGYU] = St7ApiConst.euJOULE;

            if (!aw.SetUnits(mId, st7Units))
                throw new Exception("Failed to set the units");

            // Setup Stages
            St7SetStages(aw, mId);

            // Setup loadcases
            St7SetLoadCase(aw, mId);

            St7SetLinearLoadCaseCombination(aw, mId);

            // Nodes
            int st7NodeIndex = 0;
            foreach (var node in _nodes)
            {
                st7NodeIndex++;
                _st7NodeMap.Add(node.Id, st7NodeIndex);

                aw.SetNodeXYZ(mId, st7NodeIndex, node.Position.X, node.Position.Y, node.Position.Z);


                foreach (var attribute in node.AttributesFreedomCase)
                {
                    if (attribute is NodeRestrainAttribute nra)
                    {
                        St7SetNodeRestrain(aw, mId, st7NodeIndex, 1, 1, nra.Restrains);
                    }
                    else if (attribute is NodeStiffnessAttribute nsa)
                    {
                        St7SetNodeRestrain(aw, mId, st7NodeIndex, 1, 1, nsa.Stiffnesses);
                    }
                }

                // Carichi
                foreach (var attribute in node.AttributesLoadCase)
                {
                    if (attribute is NodeForceAttribute pgfa)
                    {
                        var lc = _loadCases.GetElementByName(pgfa.LoadCase.Name);

                        St7SetNodeGlobalLoad(aw, mId, st7NodeIndex, _st7LoadCaseMap[(LoadCase)pgfa.LoadCase], pgfa);
                    }
                    else
                        throw new NotSupportedException("Point attribute not supported");
                }
            }

            St7SetPlateProperties(aw, mId);

            // Plate
            int glassGroupId = 0;
            aw.NewChildGroup(mId, 1, "Glass " + "1", ref glassGroupId);

            int st7PlateIndex = 0;
            foreach (var element in _elements)
            {
                if (element is Plate plate)
                {
                    st7PlateIndex++;
                    _st7PlateMap.Add(plate.Id, st7PlateIndex);

                    int[] st7ConnectivityArray;
                    if (plate.IsQuad)
                    {
                        var st7FaceConnection = plate.GetNodesID();
                        st7ConnectivityArray = new int[5];
                        st7ConnectivityArray[0] = 4;
                        st7ConnectivityArray[1] = _st7NodeMap[st7FaceConnection[0]];
                        st7ConnectivityArray[2] = _st7NodeMap[st7FaceConnection[1]];
                        st7ConnectivityArray[3] = _st7NodeMap[st7FaceConnection[2]];
                        st7ConnectivityArray[4] = _st7NodeMap[st7FaceConnection[3]];
                    }
                    else
                    {
                        var st7FaceConnection = plate.GetNodesID();
                        st7ConnectivityArray = new int[4];
                        st7ConnectivityArray[0] = 3;
                        st7ConnectivityArray[1] = _st7NodeMap[st7FaceConnection[0]];
                        st7ConnectivityArray[2] = _st7NodeMap[st7FaceConnection[1]];
                        st7ConnectivityArray[3] = _st7NodeMap[st7FaceConnection[2]];
                    }

                    var property = _plateProperties.GetElementByName(plate.Property.Name);

                    int propNum = _st7PlatePropertyMap[property];

                    aw.SetElementConnection(mId, St7ApiConst.tyPLATE, st7PlateIndex, propNum, st7ConnectivityArray);
                    aw.SetEntityGroup(mId, St7ApiConst.tyPLATE, st7PlateIndex, glassGroupId);

                    foreach (var attribute in plate.AttributesLoadCase)
                    {
                        if (attribute is PlatePressureAttribute pgpa)
                        {
                            St7SetPlateGlobalPressure(aw, mId, st7PlateIndex, _st7LoadCaseMap[(LoadCase)pgpa.LoadCase], pgpa);
                        }

                        else
                            throw new NotSupportedException("Point attribute not supported");
                    }
                }
            }

            return true;
        }


        private void St7SetStages(ISt7ApiService aw, int mid)
        {
            //// Creo stage per ogni loadcase

            //foreach (var lc in base._loadCases.OrderBy(x => x.Value))
            //{
            //    aw.AddStage(mid, lc.Key.Name, new int[] { St7ApiConst.btFalse, St7ApiConst.btFalse, St7ApiConst.btFalse });
            //}
        }

        private void St7SetPlateProperties(ISt7ApiService aw, int mid)
        {

            int _bufferId = 0;
            int st7PropId = 0;
            foreach (var property in _plateProperties)
            {
                st7PropId++;

                if ((st7PropId - _bufferId) != 1)
                    throw new NotSupportedException("Plate properties not in order");
                _bufferId = st7PropId;

                if (property is MonolithicGlassProperty mgp)
                {
                    aw.NewPlateProperty(mid, st7PropId, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, $"MonolithicGlass{st7PropId}");

                    aw.SetPlateThickness(mid, st7PropId, new double[] { mgp.MembraneThickness, mgp.BendingThickness });

                    aw.SetPlateIsotropicMaterial(mid, st7PropId, mgp.GetE(), mgp.GetNi(), mgp.GetDensity(), 0, 0, 0, 0, 0);
                }
                else if (property is InterlayerProperty inp)
                {
                    aw.NewPlateProperty(mid, st7PropId, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, $"Interlayer{st7PropId}");

                    aw.SetPlateThickness(mid, st7PropId, new double[] { inp.MembraneThickness, inp.BendingThickness });

                    aw.SetPlateIsotropicMaterial(mid, st7PropId, inp.GetE(), inp.GetNi(), inp.GetDensity(), 0, 0, 0, 0, 0);
                }
                else
                {
                    throw new NotSupportedException("Glass property not supported");
                }

                _st7PlatePropertyMap.Add(property, st7PropId);
            }

        }

        private void St7SetLoadCase(ISt7ApiService aw, int mid)
        {
            if (!(_loadCases.Count > 0))
                return;

            int st7LcId = 0;
            foreach(var loadCase in _loadCases)
            {
                st7LcId++;
                if (st7LcId != 1)
                {
                    if (!aw.NewLoadCase(mid, loadCase.Name))
                        throw new Straus7Exception($"Unable lo add loadCase {loadCase.Name}");
                }
                else
                {
                    aw.SetLoadCaseName(mid, st7LcId, loadCase.Name);
                }


                if (loadCase.GetLoadCaseType() == LoadCase.LoadCaseType.SelfWeight)
                {
                    aw.SetLoadCaseType(mid, st7LcId, St7ApiConst.kGravity);
                    aw.SetLoadCaseGravityDir(mid, st7LcId, 3);
                    var doubles = new double[13];
                    doubles[4] = 0;
                    doubles[5] = 0;
                    doubles[6] = -9806.65; //mm/s2
                    aw.SetLoadCaseDefaults(mid, st7LcId, doubles);
                }
                else
                    aw.SetLoadCaseType(mid, st7LcId, St7ApiConst.kNoInertia);

                _st7LoadCaseMap.Add((LoadCase)loadCase, st7LcId);
            }

        }

        private void St7SetLinearLoadCaseCombination(ISt7ApiService aw, int mid)
        {
            int st7CId = 0;

            foreach (var combo in _combinations)
            {
                List<double> coefficients = combo.GetLoadCaseCoefficients(out List<GPC.Model.LoadCases.LoadCase> loadCasesBuffer);

                var loadCases = loadCasesBuffer.Cast<LoadCase>().ToList();
                                
                if (aw.AddLSACombination(mid, combo.Name))
                {
                    st7CId++;

                    bool added = false;
                    foreach (var loadCase in loadCases)
                    {
                        if (_loadCases.Contains(loadCase))
                        {
                            if (aw.SetLSACombinationFactor(mid, St7ApiConst.ltLoadCase, st7CId, _st7LoadCaseMap[loadCase], 1, combo[loadCase]))
                            {
                                added = true;
                            }
                        }
                    }

                    if (added)
                    {
                        _st7LSACombinationMap.Add(combo, st7CId);
                    }
                    else
                    {
                        aw.DeleteLSACombination(mid, st7CId);
                        st7CId--;
                    }
                }
            }
        }

        /// <summary>
        /// Set a node external restrain or imposed displacement
        /// </summary>
        /// <returns></returns>
        private bool St7SetNodeRestrain(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, int ucsId, List<DofRestrain> restrains)
        {
            int[] status = new int[6];

            double[] imposedDisplacement = new double[6];

            foreach (var restrain in restrains)
            {
                if (restrain.Dof == Model.FEM.LinearSolver.DOF.DX)
                {
                    status[0] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[0] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.LinearSolver.DOF.DY)
                {
                    status[1] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[1] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.LinearSolver.DOF.DZ)
                {
                    status[2] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[2] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.LinearSolver.DOF.RX)
                {
                    status[3] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[3] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.LinearSolver.DOF.RY)
                {
                    status[4] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[4] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.LinearSolver.DOF.RZ)
                {
                    status[5] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[5] = restrain.ImposedDisplacement;
                }
                else
                    throw new NotSupportedException();
            }

            return aw.SetNodeRestraint(mid, nodeNumber, caseNumber, ucsId, status, imposedDisplacement);
        }

        private bool St7SetNodeGlobalLoad(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, NodeForceAttribute pgfa)
        {
            return aw.SetNodeForce(mid, nodeNumber, caseNumber, pgfa.F1, pgfa.F2, pgfa.F3) && aw.SetNodeMoment(mid, nodeNumber, caseNumber, pgfa.M1, pgfa.M2, pgfa.M3);
        }

        private bool St7SetNodeLocalLoad(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, NodeForceAttribute gpl)
        {
            throw new NotImplementedException();
        }

        private bool St7SetPlateGlobalPressure(ISt7ApiService aw, int mid, int plateNumber, int caseNumber, PlatePressureAttribute gpl)
        {
            return aw.SetPlateGlobalPressure(mid, plateNumber, St7ApiConst.btFalse, caseNumber, gpl.P1, gpl.P2, gpl.P3);
        }

        private bool St7LinearSolverSetup(ISt7ApiService aw, int mid)
        {
            foreach (var lc in _loadCases)
            {
                aw.EnableLSALoadCase(mid, _st7LoadCaseMap[(LoadCase)lc], 1);
            }
            return true;
        }

        private void St7NonLinearSolverSetupForLinearAnalysis(ISt7ApiService aw, int mid)
        {
            aw.SetSolverNonlinearMaterial(mid, false);
            aw.SetSolverNonlinearGeometry(mid, false);

            aw.SetNLAStagedAnalysis(mid, true);


            foreach (var lc in _loadCases)
            {
                aw.AddNLAIncrement(mid, _st7LoadCaseMap[(LoadCase)lc], lc.Name);
                aw.SetNLALoadIncrementFactor(mid, _st7LoadCaseMap[(LoadCase)lc], 1, 1, 1);
            }

        }

        private bool St7RunLinearSolver(ISt7ApiService aw, string filePath)
        {
            //var c = Assembly.GetExecutingAssembly().GetName().Name;
            //var b = AppDomain.CurrentDomain.GetAssemblies();

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith(Assembly.GetExecutingAssembly().GetName().Name)).First();
            string directory = Path.GetDirectoryName(assembly.Location);

            //string resultExtension = "lsa";
            ProcessStartInfo pInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(directory, "St7Solver.exe"),
                Arguments = $"{filePath} 0",
            };

            if (!File.Exists(pInfo.FileName))
                throw new FileNotFoundException($"File {pInfo.FileName} not found");
            else
            {
                Process p = Process.Start(pInfo);
                
                p.WaitForExit(); // Wait for the process to end.

                if (p.ExitCode == 0) // Analysis terminated with success
                {
                    //string resultPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "." + resultExtension);

                    return true;
                    //bool status = St7ReadResults(aw, mid, resultPath);
                    //return status;
                }
                else
                {
                    string err = "";
                    if (p.ExitCode < 1000)
                        err = aw.GetAPIErrorString(p.ExitCode);
                    else
                        err = aw.GetSolverErrorString(p.ExitCode);
                    throw new Straus7Exception($"St7 solver error {err}");
                }
            }
        }

        private bool St7RunNonLinearStagedSolver(ISt7ApiService aw, int mid, string filePath)
        {
            //var c = Assembly.GetExecutingAssembly().GetName().Name;
            //var b = AppDomain.CurrentDomain.GetAssemblies();

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith(Assembly.GetExecutingAssembly().GetName().Name)).First();
            string directory = Path.GetDirectoryName(assembly.Location);

            //string resultExtension = "nla";
            ProcessStartInfo pInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(directory, "St7Solver.exe"),
                Arguments = $"{filePath} 2",
            };

            if (!File.Exists(pInfo.FileName))
                throw new FileNotFoundException($"File {pInfo.FileName} not found");
            else
            {
                try
                {
                    Process p = Process.Start(pInfo);

                    p.WaitForExit(); // Wait for the process to end.

                    if (p.ExitCode == 0) // Analysis terminated with success
                    {
                        //string resultPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "." + resultExtension);

                        return true;
                        //bool status = St7ReadResults(aw, mid, resultPath);
                        //return status;
                    }
                    else
                    {
                        string err = "";
                        if (p.ExitCode < 1000)
                            err = aw.GetAPIErrorString(p.ExitCode);
                        else if (p.ExitCode == 2000)
                            err = "Number of arguments lower than two";
                        else
                            err = aw.GetSolverErrorString(p.ExitCode);

                        throw new Exception($"St7 solver error {err}");
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }


            }
        }

        #endregion
        
        
        #endregion


    }
}
