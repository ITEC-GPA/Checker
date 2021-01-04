using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.IO;
using St7ApiWrapper;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.FEM.Attributes;
using GPC.Model.Elements.Glasses;
using GPC.Geometry.Meshes;
using System.Diagnostics;
using System.Reflection;
using GPC.Checker.Glasses.Checkers;
using GPC.Model.Loads;
using GPC.Checker.Glasses.Wrappers;

namespace GPC.Checker.Glasses.FemModel
{
    public class FemModelWrapper
    {
        #region Variables

        /// <summary>
        /// List of mesh for each glass. Each glass can be can be made by one or more meshes
        /// </summary>        
        private List<List<FemMesh>> _singleGlassMeshes;

        private Dictionary<LoadCase, int> _loadCases;

        private Dictionary<IFemGlassProperty, int> _femGlassProperties;

        private string _st7ServerIp;

        private string _name;

        private GlassChecker.CheckParameters.AnalysisType _analysisType;

        #endregion

        public string Name => _name;

        #region Public constructors

        internal FemModelWrapper(string name)
        {
            _st7ServerIp = "localhost";
            _name = name;
            _singleGlassMeshes = new List<List<FemMesh>>();
            _femGlassProperties = new Dictionary<IFemGlassProperty, int>();
            _loadCases = new Dictionary<LoadCase, int>();

            SetGlobalConnectivity();
        }

        #endregion


        #region Public methods

        private FemMesh GenerateFemMesh(int surfaceId, Mesh geometryMesh, IFemGlassProperty glassProperty, Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex, 
                                    List<IGeometryRestrain> restrains, List<Load> uniformPressureLoads, List<Load> nonUniformPressureLoads)
        {
            var pointRestrainVertexIndex = new Dictionary<int, Restrain>();
            Dictionary<INodeFemAttribute, int[]> nodeAttributeVertexIndex = new Dictionary<INodeFemAttribute, int[]>();
            Dictionary<IPlateFemAttribute, int[]> plateAttributeFaceIndex = new Dictionary<IPlateFemAttribute, int[]>();
            List<IFemGlassProperty> plateProperties = new List<IFemGlassProperty>();

            // proprietà
            foreach (var face in geometryMesh.Faces)
            {
                plateProperties.Add(glassProperty);
            }

            if (!_femGlassProperties.ContainsKey(glassProperty))
            {
                _femGlassProperties[glassProperty] = _femGlassProperties.Values.DefaultIfEmpty().Max() + 1; ;
            }

            // Uniform loads
            foreach (var load in uniformPressureLoads)
            {
                if (load is NormalAreaLoad nal)
                {
                    throw new NotImplementedException();
                }
                else if (load is GlobalAreaLoad gal)
                {
                    PlateGlobalPressureAttribute pgpa = new PlateGlobalPressureAttribute(gal.LoadCase, gal.Px, gal.Py, gal.Pz);
                    plateAttributeFaceIndex[pgpa] = geometryMesh.Faces.Select(I => I.Id).ToArray();

                }
                else
                    throw new NotSupportedException("Load type not supported");

                if (!_loadCases.ContainsKey(load.LoadCase))
                {
                    _loadCases[load.LoadCase] = _loadCases.Values.DefaultIfEmpty().Max() + 1; ;
                }
            }

            // geometria embedded
            foreach (var kvp in embeddedGeometriesMapVertex)
            {
                GeometryBase geometry = kvp.Key;
                int[] vertexIndexes = kvp.Value;

                if (geometry is Line3d || geometry is Line2d)
                {
                    Line3d line;
                    if (geometry is Line3d l)
                        line = l;
                    else
                        line = new Line3d((Line2d)geometry);

                    var restrain = restrains.Where(i => i.GetType() == typeof(LineRestrain)).Where(i => (i as LineRestrain).Line == line)
                                             .Select(i => (i as LineRestrain).Restrain).FirstOrDefault();

                    if (restrain != null)
                        foreach (int v in vertexIndexes)
                            pointRestrainVertexIndex[v] = restrain;
                }
                else if (geometry is Point3d || geometry is Point2d)
                {
                    Point3d point;
                    if (geometry is Point3d p)
                        point = p;
                    else
                        point = new Point3d((Point2d)geometry);

                    var restrain = restrains.Where(i => i.GetType() == typeof(PointRestrain)).Where(i => (i as PointRestrain).Point == point)
                                             .Select(i => (i as PointRestrain).Restrain).FirstOrDefault();

                    if (restrain != null)
                        foreach (int v in vertexIndexes)
                            pointRestrainVertexIndex[v] = restrain;

                    var loads = nonUniformPressureLoads.Where(i => i.GetGeometry().GetType() == typeof(Point3d)).Where(i => (Point3d)i.GetGeometry() == point);

                    //var loads = _glassSurface.Loads.Where(i => i.GetGeometry().GetType() == typeof(Point3d)).Where(i => (Point3d)i.GetGeometry() == point);

                    foreach (var load in loads)
                    {
                        if (load is GlobalPointLoad gpl)
                        {
                            NodeGlobalForceAttribute pgfa = new NodeGlobalForceAttribute(gpl.LoadCase, gpl.Fx, gpl.Fy, gpl.Fz, gpl.Mx, gpl.My, gpl.Mz);
                            nodeAttributeVertexIndex[pgfa] = vertexIndexes;
                        }
                        else
                        {
                            throw new NotSupportedException("Load type not supported");
                        }

                        if (!_loadCases.ContainsKey(load.LoadCase))
                        {
                            _loadCases[load.LoadCase] = _loadCases.Values.DefaultIfEmpty().Max() + 1; ;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"Geometry of type {geometry.GetType()} is not supported.");
                }
            }

            var femMesh = new FemMesh(surfaceId, geometryMesh.Vertices, geometryMesh.Faces, plateProperties, pointRestrainVertexIndex, nodeAttributeVertexIndex, plateAttributeFaceIndex);


            return femMesh;
        }


        /// <summary>
        /// Set up a single monolithic glass
        /// </summary>
        /// <param name="geometryMesh">geometry mesh composing a single monolithic glass</param>
        public void SetUpMonolithic(int surfaceId, Mesh geometryMesh, Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex, List<IGeometryRestrain> restrains, 
                                    IFemGlassProperty property, List<Load> uniformPressureLoads, List<Load> notUniformPressureLoads)
        {
            FemMesh meshes = GenerateFemMesh(surfaceId, geometryMesh, property, embeddedGeometriesMapVertex, restrains, uniformPressureLoads, notUniformPressureLoads);

            _singleGlassMeshes.Add(new List<FemMesh>() { meshes });

            SetGlobalConnectivity();
        }


        public void SetUpLaminated(int surfaceId, Mesh geometryMesh, Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex, List<IGeometryRestrain> restrains,
                                    LaminatedGlassWrapper laminatedGlassWrapper, List<Load> uniformPressureLoads, List<Load> notUniformPressureLoads, 
                                    GlassChecker.CheckParameters.LaminatedAnalysisType laminatedAnalysisType)
        {
            switch (laminatedAnalysisType)
            {
                case GlassChecker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer:

                    var normal = laminatedGlassWrapper.GetNormalVector();
                    var glassDistances = laminatedGlassWrapper.GetMonolithicBarycenterDistances();
                    var interlayerDistances = laminatedGlassWrapper.GetInterlayerBarycenterDistances();

                    List<FemMesh> femMeshes = new List<FemMesh>();
                   
                    for (int i = 0; i < glassDistances.Length; i++)
                    {
                        Mesh movedMesh = (geometryMesh.Clone() as Mesh);
                        movedMesh.Pan(normal * glassDistances[i]);

                        femMeshes.Add(GenerateFemMesh(surfaceId, movedMesh, laminatedGlassWrapper.GlassProperty.MonolithicGlasses[i], embeddedGeometriesMapVertex, restrains,
                                                        uniformPressureLoads, notUniformPressureLoads));
                    }

                    for (int i = 0; i < interlayerDistances.Length; i++)
                    {
                        Mesh movedMesh = (geometryMesh.Clone() as Mesh);
                        movedMesh.Pan(normal * interlayerDistances[i]);

                        femMeshes.Add(GenerateFemMesh(surfaceId, movedMesh, laminatedGlassWrapper.GlassProperty.Interlayers[0], embeddedGeometriesMapVertex, restrains,
                                                        uniformPressureLoads, notUniformPressureLoads));
                    }

                    _singleGlassMeshes.Add(femMeshes);
                    SetGlobalConnectivity();
                    break;

                default:
                    throw new NotSupportedException($"LaminatedAnalysisType: {laminatedAnalysisType} not implemented");

            }
        }


        public void SetAnalysisType(GlassChecker.CheckParameters.AnalysisType analysisType)
        {
            _analysisType = analysisType;
        }

        #endregion


        #region Private methods


        private void SetGlobalConnectivity()
        {
            int nodeId = 1;
            int faceId = 1;

            foreach(var sgm in _singleGlassMeshes)
            {
                Dictionary<Point3d, int> nodeGlobalId = new Dictionary<Point3d, int>();
                foreach(FemMesh femMesh in sgm)
                {
                    femMesh.SetGlobalIds(ref nodeId, ref faceId);

                    foreach (var node in femMesh.Nodes)
                    {
                        if (nodeGlobalId.ContainsKey(node.Position))
                        {
                            node.GlobalId = nodeGlobalId[node.Position];
                        }
                        else
                        {
                            nodeGlobalId[node.Position] = node.GlobalId;
                        }
                    }
                }
            }
        }

        #endregion


        #region STRAUS7

        internal void SaveToSt7(string filePath)
        {
            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                bool status = CreateSt7Model(aw, filePath, out int mid, out List<string> warnings, out List<string> errors);

                if (status)
                    status = aw.SaveFile(mid);

                switch (_analysisType)
                {
                    case GlassChecker.CheckParameters.AnalysisType.LinearStaticAnalisys:

                        if (status)
                            St7NonLinearSolverSetupForLinearAnalysis(aw, mid);

                        if (status)
                            status = aw.SaveFile(mid);

                        break;

                    case GlassChecker.CheckParameters.AnalysisType.NonLinearStaticAnalysis:
                        throw new NotImplementedException();

                    default:
                        throw new NotSupportedException($"Analysis type {_analysisType} not supported");
                }

                if (status)
                    status = aw.CloseFile(mid);

                if(!status)
                    throw new Exception($"St7 Error: {aw.GetLastErrorString()}");
            }
            else
            {
                throw new Exception($"Unable to connect to Apiservice through ip: {_st7ServerIp}");
            }

            if (channel != null)
                ChannelServices.UnregisterChannel(channel);
        }

        internal void RunSt7Solver(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File {filePath}, not found");
            }

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                switch (_analysisType)
                {
                    case GlassChecker.CheckParameters.AnalysisType.LinearStaticAnalisys:

                        int mid = 0;
                        bool status = aw.OpenFile(filePath, Path.GetTempPath(), ref mid);

                        //if (status)
                        //    status = St7LinearSolverSetup(aw, mid);
                        //if (status)
                        //    status = St7RunLinearSolver(aw, mid, filePath);

                        if (status)
                            status = St7RunNonLinearStagedSolver(aw, mid, filePath);

                        break;

                    case GlassChecker.CheckParameters.AnalysisType.NonLinearStaticAnalysis:
                        throw new NotImplementedException();

                    default:
                        throw new NotSupportedException($"Analysis type {_analysisType} not supported");
                }
            }
            else
            {
                throw new Exception($"Unable to connect to Apiservice through ip: {_st7ServerIp}");
            }

            if (channel != null)
                ChannelServices.UnregisterChannel(channel);


            
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

        private bool CreateSt7Model(ISt7ApiService aw, string filePath, out int mId, out List<string> warnings, out List<string> errors)
        {
            warnings = new List<string>();
            errors = new List<string>();
            mId = 0;

            string scratchPath = Path.GetTempPath();

            // Create a new model
            if (!aw.NewFile(filePath, scratchPath, ref mId))
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

            // Nodes
            foreach (var sgm in _singleGlassMeshes)
            {
                foreach (var femMesh in sgm)
                {
                    foreach (var node in femMesh.Nodes)
                    {
                        aw.SetNodeXYZ(mId, node.GlobalId, node.Position.X, node.Position.Y, node.Position.Z);

                        if (node.GetRestrain() != null)
                            St7SetNodeRestrain(aw, mId, node.GlobalId, 1, 1, node.GetRestrain());
                        

                        foreach (var attribute in node.Attributes)
                        {
                            if (attribute is NodeGlobalForceAttribute pgfa)
                            {
                                int lcNum = _loadCases[pgfa.LoadCase];
                                St7SetNodeGlobalLoad(aw, mId, node.GlobalId, lcNum, pgfa);
                            }
                            else
                                throw new NotSupportedException("Point attribute not supported");
                        }
                    }
                }
            }

            St7SetPlateProperties(aw, mId);

            // Plate
            foreach (var sgm in _singleGlassMeshes)
            {
                foreach (var femMesh in sgm)
                {
                    int glassGroupId = 0;
                    aw.NewChildGroup(mId, 1, "Glass " + femMesh.SurfaceId, ref glassGroupId);
                    foreach (var face in femMesh.Plates)
                    {
                        int[] globalConnectivity;
                        
                        if (face.IsQuad)
                        {
                            globalConnectivity = new int[5];
                            globalConnectivity[0] = 4;
                            globalConnectivity[1] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[0]).FirstOrDefault().GlobalId;
                            globalConnectivity[2] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[1]).FirstOrDefault().GlobalId;
                            globalConnectivity[3] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[2]).FirstOrDefault().GlobalId;
                            globalConnectivity[4] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[3]).FirstOrDefault().GlobalId;
                        }
                        else
                        {
                            globalConnectivity = new int[4];
                            globalConnectivity[0] = 3;
                            globalConnectivity[1] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[0]).FirstOrDefault().GlobalId;
                            globalConnectivity[2] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[1]).FirstOrDefault().GlobalId;
                            globalConnectivity[3] = femMesh.Nodes.Where(i => i.NodeIndex == face.GetConnection()[2]).FirstOrDefault().GlobalId;
                        }

                        int propNum = _femGlassProperties[(IFemGlassProperty)face.Property];

                        aw.SetElementConnection(mId, St7ApiConst.tyPLATE, face.GlobalId, propNum, globalConnectivity);
                        aw.SetEntityGroup(mId, St7ApiConst.tyPLATE, face.GlobalId, glassGroupId);

                        foreach (var attribute in face.Attributes)
                        {
                            if (attribute is PlateGlobalPressureAttribute pgpa)
                            {
                                int lcNum = _loadCases[pgpa.LoadCase];
                                St7SetPlateGlobalPressure(aw, mId, face.GlobalId, lcNum, pgpa);
                            }                           
                            
                            else
                                throw new NotSupportedException("Point attribute not supported");
                        }
                    }
                }
            }

            return true;
        }

        private void St7SetStages(ISt7ApiService aw, int mid)
        {
            // Creo stage per ogni loadcase

            foreach (var lc in _loadCases.OrderBy(x => x.Value))
            {
                aw.AddStage(mid, lc.Key.Name, new int[] { St7ApiConst.btFalse, St7ApiConst.btFalse, St7ApiConst.btFalse });
            }
        }

        private void St7SetPlateProperties(ISt7ApiService aw, int mid)
        {
            if (_femGlassProperties.Values.Min() != 1)
                throw new NotSupportedException("Minimum plate properties id different than 1");

            int _bufferId = 0;
            foreach (var gpkvp in _femGlassProperties.OrderBy(k => k.Value))
            {
                var property = gpkvp.Key;
                int propNum = gpkvp.Value;
                if ((propNum - _bufferId) != 1)
                    throw new NotSupportedException("Plate properties not in order");
                _bufferId = propNum;

                if (property is MonolithicGlass mg)
                {
                    aw.NewPlateProperty(mid, propNum, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, mg.Name);

                    aw.SetPlateThickness(mid, propNum, new double[] { mg.Thickness, mg.Thickness });

                    aw.SetPlateIsotropicMaterial(mid, propNum, mg.Material.E, mg.Material.Ni, mg.Material.Density, mg.Material.AlfaThermalExpansion, 0, 0, 0, 0);
                }
                else if (property is Interlayer itr)
                {
                    aw.NewPlateProperty(mid, propNum, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, itr.Name);

                    aw.SetPlateThickness(mid, propNum, new double[] { itr.Thickness, itr.Thickness });

                    //aw.SetPlateIsotropicMaterial(mid, propNum, mg.Material.E, mg.Material.Ni, mg.Material.Density, mg.Material.AlfaThermalExpansion, 0, 0, 0, 0);

                }
                else
                {
                    throw new NotSupportedException("Glass property not supported");
                }
            }
        }

        private void St7SetLoadCase(ISt7ApiService aw, int mid)
        {
            if (_loadCases.Values.Min() != 1)
                throw new NotSupportedException("Minimum load case id different than 1");

            int _bufferId = 0;
            foreach (var lckvp in _loadCases.OrderBy(k => k.Value))
            {
                var loadCase = lckvp.Key;
                int lcNum = lckvp.Value;

                if ((lcNum - _bufferId) != 1)
                    throw new NotSupportedException("Load case not in order");

                _bufferId = lcNum;

                if (lcNum == 1)
                {
                    aw.SetLoadCaseName(mid, lcNum, loadCase.Name);

                    if (loadCase.GetLoadCaseType() == LoadCase.LoadCaseType.SelfWeight)
                    {
                        aw.SetLoadCaseType(mid, lcNum, St7ApiConst.kGravity);
                        aw.SetLoadCaseGravityDir(mid, lcNum, 3);
                        var doubles = new double[13];
                        doubles[4] = 0;
                        doubles[5] = 0;
                        doubles[6] = -9806.65; //mm/s2
                        aw.SetLoadCaseDefaults(mid, lcNum, doubles);
                    }
                    else
                        aw.SetLoadCaseType(mid, lcNum, St7ApiConst.kNoInertia);

                    _loadCases[loadCase] = lcNum;
                }
                else if (lcNum > 1)
                {
                    if (aw.NewLoadCase(mid, loadCase.Name))
                    {
                        if (loadCase.GetLoadCaseType() == LoadCase.LoadCaseType.SelfWeight)
                        {
                            aw.SetLoadCaseType(mid, lcNum, St7ApiConst.kGravity);
                            aw.SetLoadCaseGravityDir(mid, lcNum, 3);
                            var doubles = new double[13];
                            doubles[4] = 0;
                            doubles[5] = 0;
                            doubles[6] = -9806.65; //mm/s2
                            aw.SetLoadCaseDefaults(mid, lcNum, doubles);
                        }
                        else
                            aw.SetLoadCaseType(mid, lcNum, St7ApiConst.kNoInertia);

                        _loadCases[loadCase] = lcNum;
                    }
                    else
                        throw new Exception($"Unable lo add loadCase {loadCase.Name}");
                }
                else
                {
                    throw new ArgumentException("Loadcase index lower than 1");
                }
            }
        }

        private bool St7SetNodeRestrain(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, int ucsId, Restrain restrain)
        {
            int[] status = new int[6];
            status[0] = restrain.D1 == true ? St7ApiConst.btTrue : St7ApiConst.btFalse;
            status[1] = restrain.D2 == true ? St7ApiConst.btTrue : St7ApiConst.btFalse;
            status[2] = restrain.D3 == true ? St7ApiConst.btTrue : St7ApiConst.btFalse;
            status[3] = restrain.R1 == true ? St7ApiConst.btTrue : St7ApiConst.btFalse;
            status[4] = restrain.R2 == true ? St7ApiConst.btTrue : St7ApiConst.btFalse;
            status[5] = restrain.R3 == true ? St7ApiConst.btTrue : St7ApiConst.btFalse;

            double[] doubles = new double[6] { 0, 0, 0, 0, 0, 0 };

            return aw.SetNodeRestraint(mid, nodeNumber, caseNumber, ucsId, status, doubles);
        }

        private bool St7SetNodeGlobalLoad(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, NodeGlobalForceAttribute pgfa)
        {            
            return aw.SetNodeForce(mid, nodeNumber, caseNumber, pgfa.Fx, pgfa.Fy, pgfa.Fz) && aw.SetNodeMoment(mid, nodeNumber, caseNumber, pgfa.Mx, pgfa.My, pgfa.Mz);
        }

        private bool St7SetNodeLocalLoad(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, NodeGlobalForceAttribute gpl)
        {
            throw new NotImplementedException();
        }

        private bool St7SetPlateGlobalPressure(ISt7ApiService aw, int mid, int plateNumber, int caseNumber, PlateGlobalPressureAttribute gpl)
        {
            return aw.SetPlateGlobalPressure(mid, plateNumber, St7ApiConst.btFalse, caseNumber, gpl.Px, gpl.Py, gpl.Pz);
        }

        private bool St7LinearSolverSetup(ISt7ApiService aw, int mid)
        {
            foreach(var lc in _loadCases)
            {
                aw.EnableLSALoadCase(mid, lc.Value, 1);
            }
            return true;
        }

        private void St7NonLinearSolverSetupForLinearAnalysis(ISt7ApiService aw, int mid)
        {
            aw.SetSolverNonlinearMaterial(mid, false);
            aw.SetSolverNonlinearGeometry(mid, false);

            aw.SetNLAStagedAnalysis(mid, true);


            foreach(var lcKvp in _loadCases)
            {
                aw.AddNLAIncrement(mid, lcKvp.Value, lcKvp.Key.Name);
                aw.SetNLALoadIncrementFactor(mid, lcKvp.Value, 1, lcKvp.Value, 1);
            }
            
        }

        private bool St7RunLinearSolver(ISt7ApiService aw, int mid, string filePath)
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
                    throw new Exception($"St7 solver error {err}");
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

        //private bool St7ReadResults(ISt7ApiService aw, int mid, string resultFilePath)
        //{
        //    int numPrimary = 0;
        //    int numSecondary = 0;
        //    aw.OpenResultFile(mid, resultFilePath, string.Empty, Convert.ToByte(true), ref numPrimary, ref numSecondary);

        //    foreach(var singleGlassMesh in _singleGlassMeshes)
        //    {
        //        foreach (var mesh in singleGlassMesh)
        //        {
        //            foreach (var plate in mesh.Plates)
        //            {
        //                plate.AddStressResultsGaussPoint()
        //            }
        //        }
        //    }

        //    aw.CloseResultFile(mid);
        //    return true;
        //}

        #endregion

        #region FeM

        internal void ToFeM()
        {
            throw new NotSupportedException();
        } 
        
        #endregion

    }
}
