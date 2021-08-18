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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization;

namespace GPC.Checkers.Glasses.FemModels
{
    [Serializable]
    public class FemModelWrapper : Model.FEM.FemModel, ISerializable
    {

        private readonly string _st7ServerIp;

        private string _st7FilePath;
        private string _st7ResultFilePath;
        private Prototype.Solvers _solver;


        /// <summary>
        /// Map used to identify how a combination is splitted into different stage combinations.
        /// </summary>
        protected Dictionary<string, (List<int> stageIds, List<string> stageCombinationsNames)> _stageCombinationsSplittedMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and St7ComboId in the Linear loadcase combination table  ;
        /// </summary>
        private Dictionary<string, int> _st7LSACombinationMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.FemModel._combinations"/> id and Stage id - Stage increment id;
        /// </summary>
        private Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)> _st7NLACombinationMap;

        ///// <summary>
        ///// Map between <see cref="Model.FEM.FemModel._loadCases"/> St7 loadcase id;
        ///// </summary>
        //private readonly Dictionary<string, int> _st7LoadCaseMap;

        ///// <summary>
        ///// Map between <see cref="Model.FEM.FemModel._freedomCases"/> St7 fredomcase id;
        ///// </summary>
        //private readonly Dictionary<string, int> _st7FreedomCaseMap;

        ///// <summary>
        ///// Map between <see cref="Model.FEM.FemModel._plateProperties"/> St7 platepropertyID;
        ///// </summary>
        //private readonly Dictionary<PlateProperty, int> _st7PlatePropertyMap;

        ///// <summary>
        ///// Map between <see cref="Model.FEM.FemModel._brickProperties"/> St7 brickpropertyID;
        ///// </summary>
        //private readonly Dictionary<BrickProperty, int> _st7BrickPropertyMap;

        /// <summary>
        /// Map between <see cref="Model.FEM.Node"/> id and straus7 node ID
        /// </summary>
        private Dictionary<int, int> _st7NodeMap;

        /// <summary>
        /// Map between <see cref="Plate"/> id and straus7 plate ID
        /// </summary>
        private Dictionary<int, int> _st7PlateMap;

        /// <summary>
        /// Map between <see cref="Brick"/> id and straus7 brick ID
        /// </summary>
        private Dictionary<int, int> _st7BrickMap;

        ///// <summary>
        ///// Map between <see cref="Model.FEM.Stage"/> id and straus7 stage ID
        ///// </summary>
        //private readonly Dictionary<int, int> _st7StageMap;


        private Converters.FemModelConverter.Straus7SolverTypes _st7SolverType;
        private bool _st7NonLinearGeometryActive;


        internal Converters.FemModelConverter.Straus7SolverTypes St7SolverType { get { return _st7SolverType; } set { _st7SolverType = value; } }
        internal bool St7NonLinearGeometryActive { get { return _st7NonLinearGeometryActive; } set { _st7NonLinearGeometryActive = value; } }


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

            //_st7LSACombinationMap = new Dictionary<string, int>();
            //_st7NLACombinationMap = new Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)>();
            //_st7LoadCaseMap = new Dictionary<string, int>();
            //_st7FreedomCaseMap = new Dictionary<string, int>();
            //_st7PlatePropertyMap = new Dictionary<PlateProperty, int>(new ModelObjectNameEqualityComparer());
            //_st7BrickPropertyMap = new Dictionary<BrickProperty, int>(new ModelObjectNameEqualityComparer());

            //_st7NodeMap = new Dictionary<int, int>();
            //_st7PlateMap = new Dictionary<int, int>();
            //_st7BrickMap = new Dictionary<int, int>();
            //_st7StageMap = new Dictionary<int, int>();

            // valori di default nel caso non vengano settati

            _solver = Prototype.Solvers.GPCSolver;

            _st7NonLinearGeometryActive = false;
            _st7SolverType = Converters.FemModelConverter.Straus7SolverTypes.Linear;
        }


        public FemModelWrapper(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            _st7ServerIp = (string)info.GetValue("ServerIp", typeof(string));
            //_stageCombinationsSplittedMap = (Dictionary<string, (List<int> stageIds, List<string> stageCombinationsNames)>)info.GetValue("StageCombinationsSplittedMap", typeof(Dictionary<string, (List<int> stageIds, List<string> stageCombinationsNames)>));
            //_st7LSACombinationMap = (Dictionary<string, int>)info.GetValue("LSACombinationMap", typeof(Dictionary<string, int>));
            //_st7NLACombinationMap = (Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)>)info.GetValue("NLACombinationMap", 
            //                        typeof(Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)>));
            //_st7LoadCaseMap = (Dictionary<string, int>)info.GetValue("LoadCaseMap", typeof(Dictionary<string, int>));
            //_st7FreedomCaseMap = (Dictionary<string, int>)info.GetValue("FreedomCaseMap", typeof(Dictionary<string, int>));
            //_st7PlatePropertyMap = (Dictionary<PlateProperty, int>)info.GetValue("PlatePropertyMap", typeof(Dictionary<PlateProperty, int>));
            //_st7BrickPropertyMap = (Dictionary<BrickProperty, int>)info.GetValue("BrickPropertyMap", typeof(Dictionary<BrickProperty, int>));

            //_st7NodeMap = (Dictionary<int, int>)info.GetValue("NodeMap", typeof(Dictionary<int, int>));
            //_st7PlateMap = (Dictionary<int, int>)info.GetValue("PlateMap", typeof(Dictionary<int, int>));
            //_st7BrickMap = (Dictionary<int, int>)info.GetValue("BrickMap", typeof(Dictionary<int, int>));
            //_st7StageMap = (Dictionary<int, int>)info.GetValue("StageMap", typeof(Dictionary<int, int>));

            _solver = (Prototype.Solvers)info.GetValue("SolverType", typeof(Prototype.Solvers));
            _st7NonLinearGeometryActive = (bool)info.GetValue("NonLinearGeometryActive", typeof(bool));
        }


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ServerIp", _st7ServerIp);
            //info.AddValue("StageCombinationsSplittedMap", _stageCombinationsSplittedMap);
            //info.AddValue("LSACombinationMap", _st7LSACombinationMap);
            //info.AddValue("NLACombinationMap", _st7NLACombinationMap);
            //info.AddValue("LoadCaseMap", _st7LoadCaseMap);
            //info.AddValue("FreedomCaseMap", _st7FreedomCaseMap);
            //info.AddValue("PlatePropertyMap", _st7PlatePropertyMap);
            //info.AddValue("BrickPropertyMap", _st7BrickPropertyMap);
            //info.AddValue("NodeMap", _st7NodeMap);
            //info.AddValue("PlateMap", _st7PlateMap);
            //info.AddValue("BrickMap", _st7BrickMap);
            //info.AddValue("StageMap", _st7StageMap);
            //info.AddValue("SolverType", _solverType);
            info.AddValue("NonLinearGeometryActive", _st7NonLinearGeometryActive);
        }


        #region Public methods 

        #region Combinations

        /// <summary>
        /// Map used to identify how a combination is splitted into different stage combinations.
        /// </summary>
        /// <returns> 
        /// <para><see langword="False"/> if <paramref name="combinationName"/> is not contained in the <see cref="Model.FEM.FemModel._combinations"/> collection</para>
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

        public void GenerateRigidLinks(IEnumerable<int> node1Ids, IEnumerable<int> node2Ids)
        {
            int count = node1Ids.Count();
            if (count != node2Ids.Count())
                throw new ArgumentException();

            //var nodeIdMap = _nodes.GetElementIdMap();
            /* 
             * Giorgio: Ottimizzato in un unico ciclo. in questo caso il guadagno di prestazioni irrisorio ma seguiamo uno standard 
             *          che, se sempre rispettato, porta ad un generale aumento di prestazioeni
             *          
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
            */

            IEnumerator<int> en1 = node1Ids.GetEnumerator();
            IEnumerator<int> en2 = node2Ids.GetEnumerator();
            for (int i = 0; i < count; i++)
            {
                en1.MoveNext();
                var id1 = en1.Current;
                en2.MoveNext();
                var id2 = en2.Current;

                Model.FEM.Node node1 = _nodes[id1];//.GetByIndex(nodeIdMap[id1]);
                Model.FEM.Node node2 = _nodes[id2];//.GetByIndex(nodeIdMap[id2]);

                if (node1!= null && node2 != null)
                    AddCostrain(new Model.FEM.Costrains.RigidLink(node1, node2));
            }
        }

        /// <summary>
        /// Run the solver and read the results in case of straus7 solver
        /// </summary>
        public override void Solve()
        {
            if (_solver == Prototype.Solvers.Straus7)
            {
                var status = RunSt7Solver();

                if (status)
                    ReadSt7Result();
            }
            else
                base.Solve();

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

        #region Internal methods


        internal void SetSolver(Prototype.Solvers solver)
        {
            _solver = solver;
        }

        #endregion


        #region STRAUS7

        public bool ExportToSt7(string folderPath, string fileName)
        {
            Converters.FemModelConverter converter = new Converters.FemModelConverter();

            if (!converter.FromModelToStraus7(this, folderPath, fileName))
            {
                return false;
            }

            _st7FilePath = converter.OutputFilePath;

            _st7NodeMap = converter.NodeMap;
            _st7PlateMap = converter.PlateMap;
            _st7BrickMap = converter.BrickMap;

            _st7LSACombinationMap = converter.LSACombinationMap;
            _st7NLACombinationMap = converter.NSACombinationMap;


            return true;
        }


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


        /// <remarks><see cref="Converters.FemModelConverter"/> 
        /// must be called before calling this method</remarks>
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

                    if (resultFileOpened)
                    {
                        try
                        {       
                            foreach (Combination combination in _combinations)
                            {
                                if (_st7SolverType == Converters.FemModelConverter.Straus7SolverTypes.Linear)
                                {
                                    int comboId = _st7LSACombinationMap[combination.Name] + numPrimary;

                                    St7ReadElementResults(aw, mid, new List<int> { comboId }, combination);
                                    St7ReadNodeResults(aw, mid, new List<int> { comboId }, combination);
                                }
                                else if (_st7SolverType == Converters.FemModelConverter.Straus7SolverTypes.NonLinear)
                                {                                   
                                    // combination è stata splittata in questi stageID

                                    //List<int> comboIdSplitted = _stageCombinationsSplittedMap[combination.Name].stageIds;

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
                                                                                     plateResults[np * numColumns + 5],
                                                                                     _st7PlateMap[plate.Id].ToString());

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

                        throw new Exception($"St7 solver error: {err}");
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

        //internal bool ExportSt7PlateUserDefinedCustomResultFile(string folderPath, string fileName, Combination combination)
        //{
        //    if (!Directory.Exists(folderPath))
        //        return false;

        //    string fileZminus = Path.ChangeExtension(Path.Combine(folderPath, fileName + "_Lower"), "txt");
        //    string fileZmid = Path.ChangeExtension(Path.Combine(folderPath, fileName + "_Mid"), "txt");
        //    string fileZplus = Path.ChangeExtension(Path.Combine(folderPath, fileName + "_Upper"), "txt");

        //    using (StreamWriter swMinus = File.CreateText(fileZminus))
        //    using (StreamWriter swMid = File.CreateText(fileZmid))
        //    using (StreamWriter swPlus = File.CreateText(fileZplus))
        //    {
        //        swMinus.WriteLine($"{combination.Name} My User Generated Gauss Point File");
        //        swMid.WriteLine($"{combination.Name} My User Generated Gauss Point File");
        //        swPlus.WriteLine($"{combination.Name} My User Generated Gauss Point File");

        //        IEnumerator<FiniteElement> enumerator = GetElementsEnumerator();

        //        NumberFormatInfo nfi = CultureInfo.CurrentCulture.NumberFormat;

        //        using (enumerator)
        //        {
        //            while (enumerator.MoveNext())
        //            {
        //                var element = enumerator.Current;

        //                if (element is Plate plate)
        //                {
        //                    PlateResult plateResult = (PlateResult)element.Results.Where(i => i.Case.Equals(combination)).SingleOrDefault();

        //                    swMinus.Write($"{_st7PlateMap[element.Id]} ");
        //                    swMid.Write($"{_st7PlateMap[element.Id]} ");
        //                    swPlus.Write($"{_st7PlateMap[element.Id]} ");


        //                    (ResultType[] lowerFace, ResultType[] midFace, ResultType[] upperFace) faceResults = plateResult.GetFaceResults();

        //                    foreach (ResultType result in faceResults.lowerFace)
        //                    {
        //                        if (result is ResultStress rs)
        //                        {
        //                            swMinus.Write($"{rs.S11.ToString("N5", nfi)} ");
        //                        }
        //                    }

        //                    foreach (ResultType result in faceResults.midFace)
        //                    {
        //                        if (result is ResultStress rs)
        //                        {
        //                            swMid.Write($"{rs.S11.ToString("N5", nfi)} ");
        //                        }
        //                    }

        //                    foreach (ResultType result in faceResults.upperFace)
        //                    {
        //                        if (result is ResultStress rs)
        //                        {
        //                            swPlus.Write($"{rs.S11.ToString("N5", nfi)} ");
        //                        }
        //                    }

        //                    swMinus.Write("\n");
        //                    swMid.Write("\n");
        //                    swPlus.Write("\n");
        //                }
        //            }
        //        }
        //    }

        //    return true;
        //}


        //internal void ExportSt7NodeUserDefinedCustomResultFile(string filePath, Combination combination)
        //{

        //    using (StreamWriter sw = File.CreateText(filePath))
        //    {
        //        sw.WriteLine($"{combination.Name} My User Generated Node Contour File");

        //        IEnumerator<Model.FEM.Node> enumerator = GetNodesEnumerator();

        //        //NumberFormatInfo nfi = new NumberFormatInfo();
        //        //nfi.NumberDecimalSeparator = ",";
        //        NumberFormatInfo nfi = CultureInfo.CurrentCulture.NumberFormat;

        //        using (enumerator)
        //        {
        //            while (enumerator.MoveNext())
        //            {
        //                var node = enumerator.Current;

        //                if (node.Results.Where(i => i.Case.Equals(combination)).SingleOrDefault().Result is ResultDisplacement rd)
        //                {
        //                    sw.WriteLine($"{_st7NodeMap[node.Id]} {rd.D3.ToString("N5", nfi)}");
        //                }
        //            }
        //        }
        //    }
        //}

        #endregion  
#endif


        #endregion


    }
}
