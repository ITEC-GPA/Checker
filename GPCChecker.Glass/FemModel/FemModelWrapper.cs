
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
using GPC.Checkers.Glasses.LoadCases;
using GPC.Checkers.Glasses.Models;
using GPC.Model.FEM.Attributes;
using GPC.Model.FEM.FiniteElements;
using GPC.Model.FEM.Properties;
using GPC.Model.FreedomCases;
using GPC.Model.Restrains;
using GPC.Model.Results;
using GPC.Model.Materials;
using GPC.Model.Combinations;
using GPC.Checkers.Glasses.Results;

namespace GPC.Checkers.Glasses.FemModel
{
    public class FemModelWrapper : Model.FEM.FemModel
    {

        private readonly string _st7ServerIp;

        private string _saveFolderPath;
        private string _st7FilePath;
        private string _st7ResultFilePath;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and St7ComboId in the Linear loadcase combination table  ;
        /// </summary>
        private readonly Dictionary<Combination, int> _st7LSACombinationMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and stageIncrement  ;
        /// </summary>
        private readonly Dictionary<Combination, int> _st7NLACombinationMap;

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


        public FemModelWrapper() : this (string.Empty)
        {

        }

        public FemModelWrapper(string name) 
            : base(name)
        {
            _st7ServerIp = "localhost";
            _st7LSACombinationMap = new Dictionary<Combination, int>(new ModelObjectNameEqualityComparer());
            _st7NLACombinationMap = new Dictionary<Combination, int>(new ModelObjectNameEqualityComparer());
            _st7LoadCaseMap = new Dictionary<string, int>();
            _st7FreedomCaseMap = new Dictionary<string, int>();
            _st7PlatePropertyMap = new Dictionary<PlateProperty, int>(new ModelObjectNameEqualityComparer());
            _st7BrickPropertyMap = new Dictionary<BrickProperty, int>(new ModelObjectNameEqualityComparer());

            _st7NodeMap = new Dictionary<int, int>();
            _st7PlateMap = new Dictionary<int, int>();
            _st7BrickMap = new Dictionary<int, int>();
            _st7StageMap = new Dictionary<int, int>();

        }

        public FemModelWrapper(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            throw new NotImplementedException();
        }


        #region Public methods 

        public void GenerateRigidLinks(IEnumerable<int> node1Ids, IEnumerable<int> node2Ids)
        {

            if (node1Ids.Count() != node2Ids.Count())
                throw new ArgumentException();

            var nodeIdMap = _nodes.GetElementIdMap();


            var nodes1 = new List<GPC.Model.FEM.Node>();
            var nodes2 = new List<GPC.Model.FEM.Node>();


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

        #endregion

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
                    var _ = cmb.GetLoadCaseCoefficients(out List<Model.LoadCases.LoadCaseBase> loadCases);
                    var lcCasted = loadCases.Cast<LoadCase>().ToList();
                    loadDuration = lcCasted.Select(i => i.LoadDuration).Min();
                }
                else
                    throw new NotImplementedException();

                if (resultPlateStress.Element is Plate plate)
                {
                    if (plate.Property is MonolithicGlassProperty mgp)
                    {
                        //if (mgp.Material is GlassMaterial gm)
                        //{
                        //    double res = gm.GetGlassResistance(false, loadDuration);

                        //    double r = Math.Abs(resultPlateStress.S11) / res;

                        //    if (r > ratio)
                        //    {
                        //        ratio = r;
                        //        stressResistance = res;
                        //        worstStress = resultPlateStress.S11;
                        //    }
                        //}

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
        public void SaveFemModelToSt7(string saveFolderPath)
        {
            if (string.IsNullOrEmpty(saveFolderPath) || string.IsNullOrWhiteSpace(saveFolderPath))
                throw new DirectoryNotFoundException();

            DirectoryInfo d = Directory.CreateDirectory(saveFolderPath);

            _saveFolderPath = d.FullName;

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
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

        /// <remarks><see cref="SaveFemModelToSt7(string)"/> must be called before calling this method</remarks>
        public void RunSt7Solver()
        {

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                int mid = 0;

                bool isOpened = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                if (isOpened)
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
        
        public void ReadSt7Result()
        {
            if (File.Exists(_st7ResultFilePath))
            {
                if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
                {
                    int mid = 0;

                    int numPrimary = 0;     // LoadCase o StageIncrement
                    int numSecondary = 0;   // Combinazioni

                    bool fileOpened = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                    bool resultFileOpened = false ;
                    if (fileOpened)
                        resultFileOpened = aw.OpenResultFile(mid, _st7ResultFilePath, string.Empty, Convert.ToByte(true), ref numPrimary, ref numSecondary);
                    
                    if (fileOpened && resultFileOpened)
                    {
                        try
                        {
                            foreach (var combination in _combinations)
                            {
                                int comboId = -1;

                                if (AnalysisType == AnalysisTypes.Linear)
                                {
                                    comboId = _st7LSACombinationMap[combination] + numPrimary;
                                }
                                else if (AnalysisType == AnalysisTypes.NonLinear)
                                {
                                    comboId = _st7NLACombinationMap[combination];
                                }
                                else
                                    throw new NotImplementedException();


                                foreach (var element in _elements)
                                {
                                    if (element is Plate plate)
                                    {
                                        // LETTURA STRESS PLATE
                                        int numPoints = 0; // punti in cui straus da i risultati
                                        int numColumns = 0; // numero di risultati per punto

                                        double[] plateResults = new double[St7ApiConst.kMaxPlateResult];
                                        double[] angles = new double[9];
                                        aw.GetPlateResultArray(mid, St7ApiConst.rtPlateStress, St7ApiConst.stPlateLocal, _st7PlateMap[plate.Id],
                                                               comboId, St7ApiConst.AtGaussPoints, St7ApiConst.psPlateZPlus, 0, ref numPoints, ref numColumns, ref plateResults);

                                        aw.GetPlateAxisSystem(mid, _st7PlateMap[plate.Id], St7ApiConst.btTrue, ref angles);

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
                                    aw.GetNodeResult(mid, St7ApiConst.rtNodeDisp, _st7NodeMap[node.Id], comboId, ref nodeResult);
                                    _resultNodeDisplacements.Add(new ResultNodeDisplacement(node, combination, CoordinateSystem.Global, nodeResult[0], nodeResult[1], nodeResult[2], nodeResult[3], nodeResult[4], nodeResult[5]));
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

        public void ReadSt7LinearCombinationResults()
        {
            if (File.Exists(_st7ResultFilePath))
            {
                if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
                {
                    int mid = 0;

                    int numPrimary = 0;     // LoadCase
                    int numSecondary = 0;   // Combinazioni

                    bool status = aw.OpenFile(_st7FilePath, Path.GetTempPath(), ref mid);

                    if (status)
                        status = aw.OpenResultFile(mid, _st7ResultFilePath, string.Empty, Convert.ToByte(true), ref numPrimary, ref numSecondary);

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
                                        aw.GetPlateResultArray(mid, St7ApiConst.rtPlateStress, St7ApiConst.stPlateLocal, _st7PlateMap[plate.Id],
                                                               comboId + numPrimary, St7ApiConst.AtGaussPoints, St7ApiConst.psPlateZPlus, 0, ref numPoints, ref numColumns, ref plateResults);

                                        aw.GetPlateAxisSystem(mid, _st7PlateMap[plate.Id], St7ApiConst.btTrue, ref angles);

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
                                    aw.GetNodeResult(mid, St7ApiConst.rtNodeDisp, _st7NodeMap[node.Id], comboId + numPrimary, ref nodeResult);
                                    _resultNodeDisplacements.Add(new ResultNodeDisplacement(node, combination, CoordinateSystem.Global, nodeResult[0], nodeResult[1], nodeResult[2], nodeResult[3], nodeResult[4], nodeResult[5]));
                                }


                            }

                        }

                        aw.CloseResultFile(mid);
                        aw.CloseFile(mid);
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
                throw new FileNotFoundException(_st7ResultFilePath);

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


            // Setup Stages
            if (_stages.Count > 0)
            {
                St7SetStages(aw, mid); // crea gli stages, senza combo. Non spegne elementi

                St7SetStagesCombination(aw, mid);

                if (AnalysisType == AnalysisTypes.Linear)
                {
                    // uso il solutore non lineare
                    // non linearità disattivata

                    St7NonLinearSolverSetup(aw, mid, false, false, true);
                }
                else
                {
                    // uso il solutore non lineare
                    // non linearità attivata

                    St7NonLinearSolverSetup(aw, mid, false, true, true);

                }
            }
            else
            {
                if (AnalysisType == AnalysisTypes.Linear) // No stage e analisi lineare
                {
                    bool status = St7LinearSolverSetup(aw, mid);

                    if (status)
                        St7SetLinearLoadCaseCombination(aw, mid);

                    if (status)
                        aw.SaveFile(mid);
                }

                else
                    throw new NotSupportedException(); // Non è possibile avere analisi non lineare senza stages.
            }


            return true;
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

            foreach(Model.FEM.Stage stage in _stages)
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

        private void St7SetStagesCombination(ISt7ApiService aw, int mid)
        {

            foreach (Model.FEM.Stage stage in _stages)
            {
                if (_st7StageMap.ContainsKey(stage.Id))
                {
                    int comboIndex = 1;
                    foreach(var combo in GetStageCombinations(stage.Id))
                    {
                        aw.AddNLAIncrement(mid, _st7StageMap[stage.Id], combo.Name);
                        _st7NLACombinationMap[combo] = _st7StageMap[stage.Id]; // TODO non gestisce il caso di combo splittate

                        foreach (var (loadcase, coefficient) in combo.GetLoadCaseCoefficientsTuple())
                        {
                            aw.SetNLALoadIncrementFactor(mid, _st7StageMap[stage.Id], comboIndex, _st7LoadCaseMap[loadcase.Name], coefficient);
                        }
                        comboIndex++;
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
                    }
                    else if (inp.Material is Model.FEM.Materials.OrthotropicFemMaterial orto)
                    {
                        aw.NewBrickProperty(mid, st7PropId, St7ApiConst.kMaterialTypeOrthotropic, inp.Name);

                        aw.SetBrickOrthotropicMaterial(mid, st7PropId, new[] { orto.E1, orto.E2, orto.E3, orto.G12, orto.G23, orto.G31, orto.Ni12, orto.Ni23, orto.Ni31, 
                                                                               orto.Density, orto.Alpha1, orto.Alpha2, orto.Alpha3, 0, 0, 0, 0, 0, 0 });
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
            foreach(var loadCase in _loadCases)
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

        private bool St7SetStageIncrement(ISt7ApiService aw, int mid)
        {

            int stageId = _st7StageMap.First().Value;

            int stageIncrement = 1;
            foreach (var combo in _combinations)
            {
                if (aw.AddNLAIncrement(mid, stageId, combo.Name))
                {
                    _st7NLACombinationMap[combo] = stageIncrement;
                    foreach (var (loadcase, coefficient) in combo.GetLoadCaseCoefficientsTuple())
                    {
                        aw.SetNLALoadIncrementFactor(mid, stageId, stageIncrement, _st7LoadCaseMap[loadcase.Name], coefficient);
                    }
                    stageIncrement++;
                }
                else
                {
                    throw new Straus7Exception($"St7 Error: {aw.GetLastErrorString()}");
                }
            }

            return true;
        }

        private bool St7SetStageCombinations(ISt7ApiService aw, int mid, int stageId)
        {

            int stageIncrement = 1;
            foreach (var combo in GetStageCombinations(stageId))
            {
                if (aw.AddNLAIncrement(mid, _st7StageMap[stageId], combo.Name))
                {
                    _st7NLACombinationMap[combo] = stageIncrement;

                    foreach (var (loadcase, coefficient) in combo.GetLoadCaseCoefficientsTuple())
                    {
                        aw.SetNLALoadIncrementFactor(mid, _st7StageMap[stageId], stageIncrement, _st7LoadCaseMap[loadcase.Name], coefficient);
                    }
                    stageIncrement++;
                }
                else
                {
                    throw new Straus7Exception($"St7 Error: {aw.GetLastErrorString()}");
                }
            }

            return true;

        }

        #endregion
        
        
        #endregion


    }
}
