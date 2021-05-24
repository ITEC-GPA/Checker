
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Checkers.Glasses.Results;
using GPC.Geometry;
using GPC.Model.Combinations;
using GPC.Model.FEM.Attributes;
using GPC.Model.FEM.FiniteElements;
using GPC.Model.FEM.Properties;
using GPC.Model.Restrains;
using GPC.Model.Results;
using St7ApiWrapper;
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

namespace GPC.Checkers.Glasses.FemModel
{
    public class FemModelWrapper : Model.FEM.FemModel
    {

        public enum Straus7SolverTypes
        {
            Linear,
            NonLinear
        }


        private readonly string _st7ServerIp;

        private string _saveFolderPath;
        private string _st7FilePath;
        private string _st7ResultFilePath;

        private Prototype.SolverTypes _solverType;


        /// <summary>
        /// Map used to identify how a combination is splitted into different stage combinations.
        /// </summary>
        protected Dictionary<string, (List<int> stageIds, List<string> stageCombinationsNames)> _stageCombinationsSplittedMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and St7ComboId in the Linear loadcase combination table  ;
        /// </summary>
        private readonly Dictionary<string, int> _st7LSACombinationMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and Stage id - Stage increment id;
        /// </summary>
        private readonly Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)> _st7NLACombinationMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._loadCases"/> St7 loadcase id;
        /// </summary>
        private readonly Dictionary<string, int> _st7LoadCaseMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._freedomCases"/> St7 fredomcase id;
        /// </summary>
        private readonly Dictionary<string, int> _st7FreedomCaseMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._plateProperties"/> St7 platepropertyID;
        /// </summary>
        private readonly Dictionary<PlateProperty, int> _st7PlatePropertyMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._brickProperties"/> St7 brickpropertyID;
        /// </summary>
        private readonly Dictionary<BrickProperty, int> _st7BrickPropertyMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.Node"/> id and straus7 node ID
        /// </summary>
        private readonly Dictionary<int, int> _st7NodeMap;

        /// <summary>
        /// Map between <see cref="Plate"/> id and straus7 plate ID
        /// </summary>
        private readonly Dictionary<int, int> _st7PlateMap;

        /// <summary>
        /// Map between <see cref="Brick"/> id and straus7 brick ID
        /// </summary>
        private readonly Dictionary<int, int> _st7BrickMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.Stage"/> id and straus7 stage ID
        /// </summary>
        private readonly Dictionary<int, int> _st7StageMap;


        private Straus7SolverTypes _st7SolverType;
        private bool _st7NonLinearGeometryActive;


        public override AnalysisTypes AnalysisType { get => base.AnalysisType; set => base.AnalysisType = value; }


        public FemModelWrapper() 
            : this(string.Empty)
        {

        }

        public FemModelWrapper(string name)
            : base(name)
        {
            _st7ServerIp = "localhost";

            _stageCombinationsSplittedMap = new Dictionary<string, (List<int> stageIds, List<string> stageCombinationsNames)>();

            _st7LSACombinationMap = new Dictionary<string, int>();
            _st7NLACombinationMap = new Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)>();
            _st7LoadCaseMap = new Dictionary<string, int>();
            _st7FreedomCaseMap = new Dictionary<string, int>();
            _st7PlatePropertyMap = new Dictionary<PlateProperty, int>(new ModelObjectNameEqualityComparer());
            _st7BrickPropertyMap = new Dictionary<BrickProperty, int>(new ModelObjectNameEqualityComparer());

            _st7NodeMap = new Dictionary<int, int>();
            _st7PlateMap = new Dictionary<int, int>();
            _st7BrickMap = new Dictionary<int, int>();
            _st7StageMap = new Dictionary<int, int>();

            _solverType = Prototype.SolverTypes.GPCSolver;

            _st7NonLinearGeometryActive = false;
        }

        public FemModelWrapper(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotImplementedException();
        }


        #region Public methods 

        #region Combinations

        /// <summary>
        /// Map used to identify how a combination is splitted into different stage combinations.
        /// </summary>
        /// <returns> 
        /// <para><see langword="False"/> if <paramref name="combinationName"/> is not contained in the <see cref="Model.FEM.FemModel.Combinations"/> collection</para>
        /// <para><see langword="False"/> if <paramref name="stageIds"/> lenght is differenet to <paramref name="stageCombinationsNames"/> lenght</para>
        /// <para><see langword="False"/> if <paramref name="stageIds"/> or <paramref name="stageCombinationsNames"/> are not contained the in the collections</para>
        /// </returns>
        /// <remarks>If the <paramref name="combinationName"/> already exist, the <paramref name="stageIds"/> and <paramref name="stageCombinationsNames"/> will be merged </remarks>
        public bool AddStageCombinationSplittedMap(string combinationName, IEnumerable<int> stageIds, IEnumerable<string> stageCombinationsNames)
        {
            if (stageIds.Count() != stageCombinationsNames.Count() )
                return false;

            if (_stages.ContainsRange(stageIds))
            {
                var stageIdsList = stageIds.ToList();
                var stageCombinationsNamesList = stageCombinationsNames.ToList();

                // controllo che tutti i nomi stageCombinationsNames siano effetivamente delle combo negli stage
                for (int i = 0; i < stageIdsList.Count(); i++)
                {
                    if (!_stageCombinationsMap[stageIdsList[i]].Contains(stageCombinationsNamesList[i])) 
                        return false;
                }


                if (_stageCombinationsSplittedMap.ContainsKey(combinationName))
                {
                    _stageCombinationsSplittedMap[combinationName].stageIds.AddRange(stageIds);
                    _stageCombinationsSplittedMap[combinationName].stageCombinationsNames.AddRange(stageCombinationsNames);
                }
                else
                {
                    _stageCombinationsSplittedMap[combinationName] = (stageIds.ToList(), stageCombinationsNames.ToList());
                }

                return true;
            }
            return false;
        }

        public bool RemoveStageCombinationSplittedMap(string combinationName)
        {
            return _stageCombinationsSplittedMap.Remove(combinationName);
        }

        public (List<int> stageIds, List<string> stageCombinationsNames) GetStageCombinationSplittedMap(string combinationName)
        {
            if (!_stageCombinationsSplittedMap.ContainsKey(combinationName))
                throw new KeyNotFoundException(combinationName);

            return _stageCombinationsSplittedMap[combinationName];
        }


        #endregion



        public override void Solve()
        {
            if (_solverType == Prototype.SolverTypes.Straus7)
            {
                var status = RunSt7Solver();

                if (status)
                    ReadSt7Result();
            }
            else
                base.Solve();

        }


        public void GenerateRigidLinks(IEnumerable<int> node1Ids, IEnumerable<int> node2Ids)
        {

            if (node1Ids.Count() != node2Ids.Count())
                throw new ArgumentException();

            var nodeIdMap = _nodes.GetElementIdMap();

            var nodes1 = new List<Model.FEM.Node>();
            var nodes2 = new List<Model.FEM.Node>();


            foreach (var id in node1Ids)
            {
                nodes1.Add(_nodes.GetElementByIndex(nodeIdMap[id]));
            }

            foreach (var id in node2Ids)
            {
                nodes2.Add(_nodes.GetElementByIndex(nodeIdMap[id]));
            }


            for (int i = 0; i < nodes1.Count; i++)
            {
                Model.FEM.Node node1 = nodes1[i];
                Model.FEM.Node node2 = nodes2[i];

                AddCostrain(new Model.FEM.Costrains.RigidLink(node1, node2));
            }

        }


        #region RESULTS - public methods


        public GlassResult GetMaxWorkingRatio()
        {
            //double ratio = 0;
            //double stressResistance = 0;
            //double worstStress = 0;
            //foreach (var resultPlateStress in ResultPlateStresses)
            //{
            //    double loadDuration = 0;

            //    if (resultPlateStress.Case is LoadCase lc)
            //        loadDuration = lc.LoadDuration;
            //    else if (resultPlateStress.Case is Combination cmb)
            //    {
            //        var _ = cmb.GetLoadCaseCoefficients(out List<Model.LoadCases.LoadCaseBase> loadCases);
            //        var lcCasted = loadCases.Cast<LoadCase>().ToList();
            //        loadDuration = lcCasted.Select(i => i.LoadDuration).Min();
            //    }
            //    else
            //        throw new NotImplementedException();

            //    if (resultPlateStress.Element is Plate plate)
            //    {
            //        if (plate.Property is MonolithicGlassProperty mgp)
            //        {
            //            //if (mgp.Material is GlassMaterial gm)
            //            //{
            //            //    double res = gm.GetGlassResistance(false, loadDuration);

            //            //    double r = Math.Abs(resultPlateStress.S11) / res;

            //            //    if (r > ratio)
            //            //    {
            //            //        ratio = r;
            //            //        stressResistance = res;
            //            //        worstStress = resultPlateStress.S11;
            //            //    }
            //            //}

            //        }
            //    }
            //}

            return new GlassResult(0, 0); ;
        }

        public void GetMaxDisplacement()
        {
            throw new NotImplementedException();
        }


        #endregion



        #endregion


        #region STRAUS7


        #region STRAUS7 - PUBLIC METHODS


        /// <param name="saveFolderPath">Folder path where to save the results</param>
        public void SaveFemModelToSt7(string saveFolderPath)
        {
            if (string.IsNullOrEmpty(saveFolderPath) || string.IsNullOrWhiteSpace(saveFolderPath))
                throw new DirectoryNotFoundException();

            DirectoryInfo d = Directory.CreateDirectory(saveFolderPath);

            _saveFolderPath = d.FullName;

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                _solverType = Prototype.SolverTypes.Straus7; // setto il solutore in Straus7

                int mid = 0;
                bool closed = false;
                try
                {
                    bool status = CreateSt7Model(aw, _saveFolderPath, out mid, out List<string> warnings, out List<string> errors);

                    if (status)
                        status = aw.SaveFile(mid);

                    if (status)
                    {
                        status = aw.CloseFile(mid);
                        closed = true;
                    }

                    if (!status)
                        throw new Exception($"St7 Error: {aw.GetLastErrorString()}");
                }
                finally
                {
                    if (!closed)
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

        #endregion


        #region STRAUS7 - PRIVATE METHODS

        private Straus7SolverTypes GetSt7SolverType(out bool nonLinearGeometry)
        {
            switch (AnalysisType)
            {
                case AnalysisTypes.Linear:
                    if (_stages.Count() > 0)
                    {
                        nonLinearGeometry = false;
                        return Straus7SolverTypes.NonLinear;
                    }
                    else
                    {
                        nonLinearGeometry = false;
                        return Straus7SolverTypes.Linear;
                    }
                case AnalysisTypes.NonLinear:
                    nonLinearGeometry = true;
                    return Straus7SolverTypes.NonLinear;

                default:
                    throw new NotImplementedException();
            }
        }

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
        /// Export this FemModel to a new St7 File
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="saveFolderPath">Folder where to save the St7 model</param>
        /// <param name="mid"></param>
        /// <param name="warnings"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        private bool CreateSt7Model(ISt7ApiService aw, string saveFolderPath, out int mid, out List<string> warnings, out List<string> errors)
        {
            warnings = new List<string>();
            errors = new List<string>();
            mid = 0;

            string scratchPath = Path.GetTempPath();
            _st7FilePath = Path.ChangeExtension(Path.Combine(saveFolderPath, Name), "St7");

            // Create a new model
            if (!aw.NewFile(_st7FilePath, scratchPath, ref mid))
                throw new Exception("Failed to create new model");

            // Units
            int[] st7Units = new int[St7ApiConst.kLastUnit];
            st7Units[St7ApiConst.ipLENGTHU] = St7ApiConst.luMILLIMETRE;
            st7Units[St7ApiConst.ipFORCEU] = St7ApiConst.fuNEWTON;
            st7Units[St7ApiConst.ipSTRESSU] = St7ApiConst.suMEGAPASCAL;
            st7Units[St7ApiConst.ipMASSU] = St7ApiConst.muTONNE;
            st7Units[St7ApiConst.ipTEMPERU] = St7ApiConst.tuCELSIUS;
            st7Units[St7ApiConst.ipENERGYU] = St7ApiConst.euJOULE;

            if (!aw.SetUnits(mid, st7Units))
                throw new Exception("Failed to set the units");


            // Setup loadcases
            St7SetLoadCase(aw, mid);


            #region Geometry

            // Nodes
            int st7NodeIndex = 0;
            foreach (var node in _nodes)
            {
                st7NodeIndex++;
                _st7NodeMap.Add(node.Id, st7NodeIndex);

                aw.SetNodeXYZ(mid, st7NodeIndex, node.Position.X, node.Position.Y, node.Position.Z);


                foreach (var attribute in node.AttributesFreedomCase)
                {
                    if (attribute is NodeRestrainAttribute nra)
                    {
                        St7SetNodeRestrain(aw, mid, st7NodeIndex, 1, 1, nra.Restrains);
                    }
                    else if (attribute is NodeStiffnessAttribute nsa)
                    {
                        St7SetNodeRestrain(aw, mid, st7NodeIndex, 1, 1, nsa.Stiffnesses);
                    }
                }

                // Carichi
                foreach (var attribute in node.AttributesLoadCase)
                {
                    if (attribute is NodeForceAttribute pgfa)
                    {
                        var lc = _loadCases.GetElementByName(pgfa.LoadCaseName);

                        St7SetNodeGlobalLoad(aw, mid, st7NodeIndex, _st7LoadCaseMap[pgfa.LoadCaseName], pgfa);
                    }
                    else
                        throw new NotSupportedException("Point attribute not supported");
                }
            }

            St7SetPlateProperties(aw, mid);
            St7SetBrickProperties(aw, mid);

            int glassGroupId = 0;
            aw.NewChildGroup(mid, 1, "Glass " + "1", ref glassGroupId);

            // Plate
            // Brick
            int st7PlateIndex = 0;
            int st7BrickIndex = 0;
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

                    aw.SetElementConnection(mid, St7ApiConst.tyPLATE, st7PlateIndex, propNum, st7ConnectivityArray);
                    aw.SetEntityGroup(mid, St7ApiConst.tyPLATE, st7PlateIndex, glassGroupId);

                    foreach (var attribute in plate.AttributesLoadCase)
                    {
                        if (attribute is PlatePressureAttribute pgpa)
                        {
                            St7SetPlateGlobalPressure(aw, mid, st7PlateIndex, _st7LoadCaseMap[pgpa.LoadCaseName], pgpa);
                        }
                        else if (attribute is PlateNormalPressureAttribute pnpa)
                        {
                            St7SetPlateNormalPressure(aw, mid, st7PlateIndex, _st7LoadCaseMap[pnpa.LoadCaseName], pnpa);
                        }
                        else
                            throw new NotSupportedException("Point attribute not supported");
                    }
                }
                else if (element is Brick brick)
                {

                    st7BrickIndex++;
                    _st7BrickMap.Add(brick.Id, st7BrickIndex);

                    int[] st7ConnectivityArray;
                    if (brick.IsQuadrangular)
                    {
                        var st7FaceConnection = brick.GetNodesID();
                        st7ConnectivityArray = new int[9];
                        st7ConnectivityArray[0] = 8;
                        st7ConnectivityArray[1] = _st7NodeMap[st7FaceConnection[0]];
                        st7ConnectivityArray[2] = _st7NodeMap[st7FaceConnection[1]];
                        st7ConnectivityArray[3] = _st7NodeMap[st7FaceConnection[2]];
                        st7ConnectivityArray[4] = _st7NodeMap[st7FaceConnection[3]];
                        st7ConnectivityArray[5] = _st7NodeMap[st7FaceConnection[4]];
                        st7ConnectivityArray[6] = _st7NodeMap[st7FaceConnection[5]];
                        st7ConnectivityArray[7] = _st7NodeMap[st7FaceConnection[6]];
                        st7ConnectivityArray[8] = _st7NodeMap[st7FaceConnection[7]];
                    }
                    else
                    {
                        var st7FaceConnection = brick.GetNodesID();
                        st7ConnectivityArray = new int[7];
                        st7ConnectivityArray[0] = 6;
                        st7ConnectivityArray[1] = _st7NodeMap[st7FaceConnection[0]];
                        st7ConnectivityArray[2] = _st7NodeMap[st7FaceConnection[1]];
                        st7ConnectivityArray[3] = _st7NodeMap[st7FaceConnection[2]];
                        st7ConnectivityArray[4] = _st7NodeMap[st7FaceConnection[3]];
                        st7ConnectivityArray[5] = _st7NodeMap[st7FaceConnection[4]];
                        st7ConnectivityArray[6] = _st7NodeMap[st7FaceConnection[5]];
                    }

                    var property = _brickProperties.GetElementByName(brick.Property.Name);

                    int propNum = _st7BrickPropertyMap[property];

                    aw.SetElementConnection(mid, St7ApiConst.tyBRICK, st7BrickIndex, propNum, st7ConnectivityArray);
                    aw.SetEntityGroup(mid, St7ApiConst.tyBRICK, st7BrickIndex, glassGroupId);
                }

            }

            // Links
            int st7LinkIndex = 0;
            foreach (var link in _costrains)
            {
                if (link is Model.FEM.Costrains.RigidLink rl)
                {
                    st7LinkIndex++;
                    aw.SetRigidLink(mid, st7LinkIndex, 1, St7ApiConst.rgPlaneXYZ, new int[] { 2, rl.StartNode.Id, rl.EndNode.Id });
                }
                else
                    throw new NotImplementedException();
            }


            #endregion


            // Setup solver
            aw.SetSolverDefaultsInteger(mid, St7ApiConst.spStaticAutoStepping, 1); // 1 Static sub-stepping option; 0, 1, 2 or 3 for None, Load Scaling, Displacement Scaling or Displacement Control(Arc Length), respectively.

            // TODO: gestire eccezioni e salvare file 


            if (_stages.Count > 0)
            {
                St7SetStages(aw, mid); // crea gli stages, senza combo. Non spegne elementi

                St7SetStagesCombination(aw, mid);
            }

            switch (GetSt7SolverType(out bool nonLinearGeometry))
            {
                case Straus7SolverTypes.Linear:
                    St7LinearSolverSetup(aw, mid);
                    St7SetLinearLoadCaseCombination(aw, mid);

                    _st7NonLinearGeometryActive = nonLinearGeometry;
                    _st7SolverType = Straus7SolverTypes.Linear;
                    break;
                    
                case Straus7SolverTypes.NonLinear:
                    St7NonLinearSolverSetup(aw, mid, false, nonLinearGeometry, false);
                    _st7NonLinearGeometryActive = nonLinearGeometry;
                    _st7SolverType = Straus7SolverTypes.NonLinear;
                    
                    break;

                default:
                    throw new NotSupportedException();
            }

            return true;
        }


        /// <remarks><see cref="SaveFemModelToSt7(string)"/> must be called before calling this method</remarks>
        private bool RunSt7Solver()
        {

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                int mid = 0;

                bool isOpened = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                if (isOpened)
                {
                    if (_stages.Count > 0)
                    {
                        switch (AnalysisType)
                        {
                            case AnalysisTypes.Linear:
                            case AnalysisTypes.NonLinear:

                                bool status = St7RunNonLinearStagedSolver(aw, mid, _st7FilePath);

                                if (!status)
                                    throw new Exception($"St7 Error: {aw.GetLastErrorString()}");

                                _st7ResultFilePath = Path.ChangeExtension(_st7FilePath, "NLA");

                                break;

                            default:
                                throw new NotSupportedException($"Analysis type {AnalysisType} not supported");
                        }
                    }
                    else
                    {
                        switch (AnalysisType)
                        {
                            case AnalysisTypes.Linear:

                                bool status = St7RunLinearSolver(aw, _st7FilePath);

                                if (!status)
                                    throw new Exception($"St7 Error: {aw.GetLastErrorString()}");

                                _st7ResultFilePath = Path.ChangeExtension(_st7FilePath, "LSA");

                                break;

                            case AnalysisTypes.NonLinear:

                                status = St7RunNonLinearStagedSolver(aw, mid, _st7FilePath);

                                if (!status)
                                    throw new Exception($"St7 Error: {aw.GetLastErrorString()}");

                                _st7ResultFilePath = Path.ChangeExtension(_st7FilePath, "NLA");

                                break;

                            default:
                                throw new NotSupportedException($"Analysis type {AnalysisType} not supported");
                        }
                    }

                    aw.CloseFile(mid);
                }
            }
            else
            {
                throw new Exception($"Unable to connect to Apiservice through ip: {_st7ServerIp}");
            }

            if (channel != null)
            {
                ChannelServices.UnregisterChannel(channel);
                return true;
            }

            return false;
        }

        private void ReadSt7Result()
        {
            if (File.Exists(_st7ResultFilePath))
            {
                if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
                {
                    int mid = 0;

                    int numPrimary = 0;     // LoadCase o StageIncrement
                    int numSecondary = 0;   // Combinazioni

                    bool fileOpened = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                    bool resultFileOpened = false;
                    if (fileOpened)
                        resultFileOpened = aw.OpenResultFile(mid, _st7ResultFilePath, string.Empty, Convert.ToByte(true), ref numPrimary, ref numSecondary);

                    if (fileOpened && resultFileOpened)
                    {
                        try
                        {       
                            foreach (Combination combination in _combinations)
                            {
                                if (_st7SolverType == Straus7SolverTypes.Linear)
                                {
                                    int comboId = _st7LSACombinationMap[combination.Name] + numPrimary;

                                    St7ReadElementResults(aw, mid, new List<int> { comboId }, combination);
                                    St7ReadNodeResults(aw, mid, new List<int> { comboId }, combination);
                                }
                                else if (_st7SolverType == Straus7SolverTypes.NonLinear)
                                {                                   
                                    // combination è stata splittata in questi stageID

                                    List<int> comboIdSplitted = _stageCombinationsSplittedMap[combination.Name].stageIds;

                                    List<string> comboFictitiousName = _stageCombinationsSplittedMap[combination.Name].stageCombinationsNames;

                                    // lista degli indici degli incrementi dove leggere le forze
                                    List<int> incrementIds = comboFictitiousName.Select(i => _st7NLACombinationMap[i].progressiveIncrementId).ToList();

                                    St7ReadElementResults(aw, mid, incrementIds, combination);
                                    St7ReadNodeResults(aw, mid, incrementIds, combination);

                                }

                            }
                        }
                        finally
                        {
                            if (resultFileOpened)
                                aw.CloseResultFile(mid);
                            if (fileOpened)
                                aw.CloseFile(mid);
                        }
                    }


                    if (!fileOpened || !resultFileOpened)
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
                throw new FileNotFoundException(_st7ResultFilePath);
        }

        private void St7ReadElementResults(ISt7ApiService aw, int mid, List<int> incrementIds, Combination combination)
        {

            int numPoints = 0; // punti in cui straus da i risultati
            int numColumns = 0; // numero di risultati per punto

            double[] angles = new double[9];

            int[] straus7PlatePosition = new[] { St7ApiConst.psPlateZMinus, St7ApiConst.psPlateMidPlane, St7ApiConst.psPlateZPlus };

            double[] plateResults = new double[St7ApiConst.kMaxPlateResult];

            foreach (var element in _elements)
            {

                // LETTURA STRESS PLATE di una certa combo in straus
                if (element is Plate plate)
                {
                    aw.GetPlateAxisSystem(mid, _st7PlateMap[plate.Id], St7ApiConst.btTrue, ref angles);
                    var coordinateSystem = new CoordinateSystem(new Vector3d(angles[0], angles[1], angles[2]), new Vector3d(angles[3], angles[4], angles[5]), new Vector3d(angles[6], angles[7], angles[8]));

                    List<ResultLocationId> resultLocationPoints = new List<ResultLocationId>();
                    List<ResultStress> resultStresses = new List<ResultStress>();


                    for (int k = 0; k < incrementIds.Count(); k++)
                    {
                        int index = 0;
                        for (int j = 0; j < straus7PlatePosition.Length; j++)
                        {
                            // in teoria:
                            // numpoints dipende dal tipo di punto, gp, nodo etc.
                            // numColumns dipende da tipo di risultato es. rtPlateStress e tipo di asse es. stPlateLocal

                            aw.GetPlateResultArray(mid, St7ApiConst.rtPlateStress, St7ApiConst.stPlateLocal, _st7PlateMap[plate.Id],
                                                   incrementIds[k], St7ApiConst.AtGaussPoints, straus7PlatePosition[j], 0, ref numPoints, ref numColumns, ref plateResults);


                            for (int np = 0; np < numPoints; np++)
                            {

                                ResultStress rs = new ResultStress(coordinateSystem, plateResults[np * numColumns + 0],
                                                                                     plateResults[np * numColumns + 1],
                                                                                     plateResults[np * numColumns + 2],
                                                                                     plateResults[np * numColumns + 3],
                                                                                     plateResults[np * numColumns + 4],
                                                                                     plateResults[np * numColumns + 5]);
                                rs.CalculatePrincipalStressFullMethod();

                                if (k == 0)
                                {
                                    resultStresses.Add(rs);
                                    resultLocationPoints.Add(new ResultLocationId(np));
                                }
                                else
                                {
                                    resultStresses[index] += rs;
                                    index++;
                                }
                            }
                        }
                    }

                    plate.AddResult(new PlateResult(combination, coordinateSystem, resultStresses.ToArray(), resultLocationPoints.ToArray()));
                }
            }

        }


        private void St7ReadNodeResults(ISt7ApiService aw, int mid, List<int> incrementIds, Combination combination)
        {

            double[] nodeResult = new double[6];

            foreach (var node in _nodes)
            {
                List<ResultDisplacement> resultDisplacements = new List<ResultDisplacement>();

                ResultDisplacement rd = null;

                for (int i = 0; i < incrementIds.Count; i++)
                {

                    aw.GetNodeResult(mid, St7ApiConst.rtNodeDisp, _st7NodeMap[node.Id], incrementIds[i], ref nodeResult);

                    if (rd != null)
                        rd += new ResultDisplacement(nodeResult[0], nodeResult[1], nodeResult[2], nodeResult[3], nodeResult[4], nodeResult[5]);
                    else
                        rd = new ResultDisplacement(nodeResult[0], nodeResult[1], nodeResult[2], nodeResult[3], nodeResult[4], nodeResult[5]);
                }

                node.AddResult(new NodeResult(combination, CoordinateSystem.Global, rd)); 
            }

        }



        /// <summary>
        /// Add each stage in <see cref="Model.FEM.FemModel._stages"/> to st7 and update <see cref="_st7StageMap"/>
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="mid"></param>
        /// <remarks>This method does not turn off elements (groups for straus) at certain stage</remarks>
        private void St7SetStages(ISt7ApiService aw, int mid)
        {
            int st7StageId = _st7StageMap.Values.DefaultIfEmpty(0).Max();

            foreach (Model.FEM.Stage stage in _stages)
            {
                aw.AddStage(mid, stage.Name, new int[] { stage.Morph ? St7ApiConst.btTrue : St7ApiConst.btFalse, St7ApiConst.btFalse, St7ApiConst.btFalse });
                _st7StageMap.Add(stage.Id, ++st7StageId);

                using (var stagePropertyEnum = GetStagePropertyEnumerator(stage.Id))
                {
                    while (stagePropertyEnum.MoveNext())
                    {
                        var current = stagePropertyEnum.Current;

                        if (current.Key is Brick brick)
                        {
                            BrickProperty propertyOverload = GetBrickProperty(current.Value.PropertyName);

                            if (brick.Property.Name != propertyOverload.Name)
                            {
                                aw.St7SetElementPropertySwitch(mid, St7ApiConst.tyBRICK, _st7BrickMap[current.Key.Id], _st7BrickPropertyMap[propertyOverload], _st7StageMap[stage.Id]);
                            }
                        }
                        else if (current.Key is Plate plate)
                        {
                            PlateProperty propertyOverload = GetPlateProperty(current.Value.PropertyName);

                            if (plate.Property.Name != propertyOverload.Name)
                            {
                                aw.St7SetElementPropertySwitch(mid, St7ApiConst.tyPLATE, _st7PlateMap[current.Key.Id], _st7PlatePropertyMap[propertyOverload], _st7StageMap[stage.Id]);
                            }
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }

            }

        }


        /// <summary>
        /// This method set also the map <see cref="_st7NLACombinationMap"/>
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="mid"></param>
        private void St7SetStagesCombination(ISt7ApiService aw, int mid)
        {

            int progressiveID = 1;
            foreach (Model.FEM.Stage stage in _stages.OrderBy(i => _st7StageMap[i.Id]))
            {
                int comboIndex = 1;

                if (_st7StageMap.ContainsKey(stage.Id))
                {
                    foreach (var combo in GetStageCombinations(stage.Id))
                    {
                        aw.AddNLAIncrement(mid, _st7StageMap[stage.Id], combo.Name);
                        

                        foreach (var (loadcase, coefficient) in combo.GetLoadCaseCoefficientsTuple())
                            aw.SetNLALoadIncrementFactor(mid, _st7StageMap[stage.Id], comboIndex, _st7LoadCaseMap[loadcase.Name], coefficient);


                        _st7NLACombinationMap[combo.Name] = (_st7StageMap[stage.Id], comboIndex, progressiveID); 


                        comboIndex++;
                        progressiveID++;
                    }
                }
            }

        }

        private void St7StageAnalysisSetup(ISt7ApiService aw, int mid, int stageIndex, bool morph, bool moveFixedNodes, bool rotateCluster)
        {
            aw.SetStageData(mid, stageIndex, new[] { morph ? St7ApiConst.btTrue : St7ApiConst.btFalse,
                                                     moveFixedNodes ? St7ApiConst.btTrue : St7ApiConst.btFalse,
                                                     rotateCluster ? St7ApiConst.btTrue : St7ApiConst.btFalse });
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
                    if (mgp.Material is Model.FEM.Materials.IsotropicFemMaterial iso)
                    {
                        aw.NewPlateProperty(mid, st7PropId, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, mgp.Name);
                        aw.SetPlateIsotropicMaterial(mid, st7PropId, iso.E, iso.Ni, iso.Density, 0, 0, 0, 0, 0);
                    }
                    else if (mgp.Material is Model.FEM.Materials.OrthotropicFemMaterial orto)
                    {
                        aw.NewPlateProperty(mid, st7PropId, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeOrthotropic, mgp.Name);
                        aw.SetPlateOrthotropicMaterial(mid, st7PropId, new[] { orto.E1, orto.E2, orto.E3, orto.G12, orto.G23, orto.G31, orto.Ni12, orto.Ni23, orto.Ni31, orto.Density, orto.Alpha1, orto.Alpha2, orto.Alpha3 });
                    }
                    else
                        throw new NotImplementedException();

                    aw.SetPlateThickness(mid, st7PropId, new double[] { mgp.MembraneThickness, mgp.BendingThickness });
                }
                else
                {
                    throw new NotSupportedException($"Property type: {property} not supported");
                }

                _st7PlatePropertyMap.Add(property, st7PropId);
            }

        }

        private void St7SetBrickProperties(ISt7ApiService aw, int mid)
        {

            int _bufferId = 0;
            int st7PropId = 0;
            foreach (var property in _brickProperties)
            {
                st7PropId++;

                if ((st7PropId - _bufferId) != 1)
                    throw new NotSupportedException("Brick properties not in order");
                _bufferId = st7PropId;

                if (property is InterlayerBrickProperty inp)
                {

                    if (inp.Material is Model.FEM.Materials.IsotropicFemMaterial iso)
                    {
                        aw.NewBrickProperty(mid, st7PropId, St7ApiConst.kMaterialTypeIsotropic, inp.Name);
                        double[] doubles = new double[8];
                        doubles[0] = iso.E;
                        doubles[1] = iso.Ni;
                        doubles[2] = iso.Density;
                        doubles[3] = iso.Alpha;
                        doubles[4] = 0;
                        doubles[5] = 0;
                        doubles[6] = 0;
                        doubles[7] = 0;

                        aw.SetBrickIsotropicMaterial(mid, st7PropId, doubles);

                        if (!ModelAnalysisOptions.Instance.Straus7BrickBubbleFunction)
                            aw.SetBrickAddBubbleFunction(mid, st7PropId, false);
                    }
                    else if (inp.Material is Model.FEM.Materials.OrthotropicFemMaterial orto)
                    {
                        aw.NewBrickProperty(mid, st7PropId, St7ApiConst.kMaterialTypeOrthotropic, inp.Name);

                        aw.SetBrickOrthotropicMaterial(mid, st7PropId, new[] { orto.E1, orto.E2, orto.E3, orto.G12, orto.G23, orto.G31, orto.Ni12, orto.Ni23, orto.Ni31,
                                                                               orto.Density, orto.Alpha1, orto.Alpha2, orto.Alpha3, 0, 0, 0, 0, 0, 0 });

                        if (!ModelAnalysisOptions.Instance.Straus7BrickBubbleFunction)
                            aw.SetBrickAddBubbleFunction(mid, st7PropId, false);
                    }
                    else
                        throw new NotImplementedException();


                }
                else
                {
                    throw new NotSupportedException($"Property type: {property} not supported");
                }

                _st7BrickPropertyMap.Add(property, st7PropId);
            }

        }


        /// <summary>
        /// This method creates the loadCases from the LoadCase list
        /// </summary>
        private void St7SetLoadCase(ISt7ApiService aw, int mid)
        {
            if (!(_loadCases.Count > 0))
                return;

            int st7LoadCaseId = 0;
            foreach (var loadCase in _loadCases)
            {
                st7LoadCaseId++;
                if (st7LoadCaseId != 1)
                {
                    if (!aw.NewLoadCase(mid, loadCase.Name))
                        throw new Straus7Exception($"Unable lo add loadCase {loadCase.Name}");
                }
                else
                {
                    aw.SetLoadCaseName(mid, st7LoadCaseId, loadCase.Name);
                }


                if ((loadCase is LoadCase) && (loadCase as LoadCase).LoadCaseType == LoadCase.LoadCaseTypes.SelfWeight)
                {
                    aw.SetLoadCaseType(mid, st7LoadCaseId, St7ApiConst.kGravity);
                    aw.SetLoadCaseGravityDir(mid, st7LoadCaseId, 3);
                    var doubles = new double[13];
                    doubles[4] = 0;
                    doubles[5] = 0;
                    doubles[6] = -9806.65; //mm/s2
                    aw.SetLoadCaseDefaults(mid, st7LoadCaseId, doubles);
                    aw.SetLoadCaseMassOption(mid, st7LoadCaseId, true, false);
                }
                else
                    aw.SetLoadCaseType(mid, st7LoadCaseId, St7ApiConst.kNoInertia);

                _st7LoadCaseMap.Add(loadCase.Name, st7LoadCaseId);
            }

        }


        /// <summary>
        /// Set up the linear load case combination table
        /// </summary>
        private void St7SetLinearLoadCaseCombination(ISt7ApiService aw, int mid)
        {
            int st7CId = 0;

            foreach (var combo in _combinations)
            {
                var lcTuples = combo.GetLoadCaseCoefficientsTuple();

                if (aw.AddLSACombination(mid, combo.Name))
                {
                    st7CId++;
                    bool added = false;

                    foreach (var (loadcase, coefficient) in lcTuples)
                    {
                        if (_loadCases.Contains(loadcase.Name))
                        {
                            if (aw.SetLSACombinationFactor(mid, St7ApiConst.ltLoadCase, st7CId, _st7LoadCaseMap[loadcase.Name], 1, combo[loadcase]))
                            {
                                added = true;
                            }
                        }
                    }

                    if (added)
                    {
                        _st7LSACombinationMap.Add(combo.Name, st7CId);
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
                if (restrain.Dof == Model.FEM.Solver.DOF.DX)
                {
                    status[0] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[0] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.Solver.DOF.DY)
                {
                    status[1] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[1] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.Solver.DOF.DZ)
                {
                    status[2] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[2] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.Solver.DOF.RX)
                {
                    status[3] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[3] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.Solver.DOF.RY)
                {
                    status[4] = restrain.Restrained == true || restrain.ImposedDisplacement != 0 ? St7ApiConst.btTrue : St7ApiConst.btFalse;
                    imposedDisplacement[4] = restrain.ImposedDisplacement;
                }
                else if (restrain.Dof == Model.FEM.Solver.DOF.RZ)
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

        private bool St7SetPlateNormalPressure(ISt7ApiService aw, int mid, int plateNumber, int caseNumber, PlateNormalPressureAttribute pnp)
        {
            return aw.SetPlateNormalPressure(mid, plateNumber, caseNumber, pnp.Pressure);
        }


        /// <summary>
        /// Set up the linear solver, activating each loadcase in the <see cref="GPC.Model.FEM.FemModel._loadCases"/> list
        /// </summary>
        private bool St7LinearSolverSetup(ISt7ApiService aw, int mid)
        {
            foreach (var lcName in _loadCases.GetNames())
            {
                aw.EnableLSALoadCase(mid, _st7LoadCaseMap[lcName], 1);
            }

            return true;
        }

        /// <summary>
        /// Set the non linear options and if, <paramref name="stagedAnalysis"/> is <see langword="True"/>, enable each stage in the <see cref="_st7StageMap"/>.
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="mid"></param>
        /// <param name="nonLinearMaterial"></param>
        /// <param name="nonLinearGeometry"></param>
        /// <param name="stagedAnalysis"></param>
        /// <returns></returns>
        private bool St7NonLinearSolverSetup(ISt7ApiService aw, int mid, bool nonLinearMaterial, bool nonLinearGeometry, bool stagedAnalysis)
        {
            if (stagedAnalysis)
            {
                foreach (var stageId in _st7StageMap)
                {
                    aw.EnableNLAStage(mid, stageId.Value);
                }
            }

            return aw.SetSolverNonlinearMaterial(mid, nonLinearMaterial) && aw.SetSolverNonlinearGeometry(mid, nonLinearGeometry) && aw.SetNLAStagedAnalysis(mid, stagedAnalysis);
        }

        /// <summary>
        /// This method set the non linear solver, turning off the nonlinearity and activating one loadcase for each stage.
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="mid"></param>
        /// <remarks>Lenght of <see cref="_st7StageMap"/> must be the same of <see cref="_st7LoadCaseMap"/> lenght </remarks>
        /// <exception cref="ArgumentException">If lenght of <see cref="_st7StageMap"/> is different than <see cref="_st7LoadCaseMap"/></exception>
        private void St7NonLinearSolverSetupForLinearAnalysis(ISt7ApiService aw, int mid)
        {
            aw.SetSolverNonlinearMaterial(mid, false);
            aw.SetSolverNonlinearGeometry(mid, false);

            aw.SetNLAStagedAnalysis(mid, true);

            if (_st7StageMap.Keys.Count != _st7LoadCaseMap.Keys.Count)
                throw new ArgumentException();

            var femStageIds = _st7StageMap.Keys.ToArray();
            var loadCases = _st7LoadCaseMap.Keys.ToArray();

            for (int i = 0; i < _st7LoadCaseMap.Count; i++)
            {
                aw.AddNLAIncrement(mid, _st7StageMap[femStageIds[i]], loadCases[i]);
                aw.SetNLALoadIncrementFactor(mid, _st7StageMap[femStageIds[i]], 1, _st7LoadCaseMap[loadCases[i]], 1);
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

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith(Assembly.GetExecutingAssembly().GetName().Name)).First();
            string directory = Path.GetDirectoryName(assembly.Location);

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
                        return true;
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


#if DEBUG
        #region STRAUS7 - DEBUG INTERNAL METHODS

        internal void ExportSt7PlateUserDefinedCustomResultFile(string filePath, Combination combination)
        {

            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.WriteLine($"{combination.Name} My User Generated Gauss Point File");

                IEnumerator<FiniteElement> enumerator = GetElementsEnumerator();

                using (enumerator)
                {
                    while (enumerator.MoveNext())
                    {
                        var element = enumerator.Current;

                        if (element is Plate plate)
                        {
                            sw.Write($"{_st7PlateMap[element.Id]} ");

                            foreach (var result in element.Results.Where(i => i.Case.Equals(combination)).SingleOrDefault().Results)
                            {
                                if (result is ResultStress rs)
                                {
                                    sw.Write($"{rs.S11} ");
                                }
                            }
                            sw.Write("\n");
                        }
                    }
                }
            }
        }


        internal void ExportSt7NodeUserDefinedCustomResultFile(string filePath, Combination combination)
        {

            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.WriteLine($"{combination.Name} My User Generated Node Contour File");

                IEnumerator<Model.FEM.Node> enumerator = GetNodesEnumerator();

                using (enumerator)
                {
                    while (enumerator.MoveNext())
                    {
                        var node = enumerator.Current;

                        if (node.Results.Where(i => i.Case.Equals(combination)).SingleOrDefault().Result is ResultDisplacement rd)
                        {
                            sw.WriteLine($"{_st7NodeMap[node.Id]} {rd.D3}");
                        }
                    }
                }
            }
        }

        #endregion  
#endif


        #endregion


    }
}
