using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;

using GPC.Checker.Glasses.Checkers;
using GPC.Checker.Glasses.LoadCases;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.Models ;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Elements;
using GPC.Model.Elements.Glasses;
using GPC.Model.FEM.Attributes;
using GPC.Model.Loads;
using GPC.Model.FEM;
using GPC.Model.FEM.FiniteElements;
using St7ApiWrapper;


namespace GPC.Checker.Glasses.FemModel
{
    public class FemModelWrapper
    {
        private List<Node> _nodes;

        private List<Plate> _plates;

        private List<Line3d> _links;
        
        private Dictionary<LoadCase, int> _loadCases;

        /// <summary>
        /// List of property used by this fem model. with the property number associated.
        /// </summary>
        private Dictionary<IGlassProperty, int> _femGlassProperties;

        private string _name;

        private string _st7ServerIp;

        private Prototype.AnalysisType _analysisType;

        public string Name => _name;


        public FemModelWrapper(string name, Prototype.AnalysisType analysisType)
        {
            _nodes = new List<Node>();
            _plates = new List<Plate>();
            _loadCases = new Dictionary<LoadCase, int>();
            _femGlassProperties = new Dictionary<IGlassProperty, int>();
            _links = new List<Line3d>();

            _analysisType = analysisType;
            _st7ServerIp = "localhost";
            _name = name;
        }

        private void GetElementAttributes(Mesh geometryMesh, Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex, 
                                          List<IGeometryRestrain> restrains, List<Load> uniformPressureLoads, List<Load> nonUniformPressureLoads,
                                          out Dictionary<int, Restrain> pointRestrainVertexIndex, out Dictionary<INodeLoadCaseAttribute, int[]> nodeAttributeVertexIndex, 
                                          out Dictionary<IPlateLoadCaseAttribute, int[]> plateAttributeFaceIndex)
        {
            pointRestrainVertexIndex = new Dictionary<int, Restrain>();
            nodeAttributeVertexIndex = new Dictionary<INodeLoadCaseAttribute, int[]>();
            plateAttributeFaceIndex = new Dictionary<IPlateLoadCaseAttribute, int[]>();

            // Uniform loads
            // Crea dizionario di platePropertyAttribute per quanto riguarda i carichi uniformi
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

                if (!_loadCases.ContainsKey((LoadCase)load.LoadCase))
                {
                    _loadCases[(LoadCase)load.LoadCase] = _loadCases.Values.DefaultIfEmpty().Max() + 1; ;
                }
            }

            // Geometria embedded
            // Va a vedere se nella embedded geometry ci sono carichi o restrain e compila i relativi dizionari
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

                    var restrain = restrains.Where(i => i.GetType() == typeof(LineRestrain)).Where(i => (i as LineRestrain).Line == line).Select(i => (i as LineRestrain).Restrain).FirstOrDefault();

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

                    var restrain = restrains.Where(i => i.GetType() == typeof(PointRestrain)).Where(i => (i as PointRestrain).Point == point).Select(i => (i as PointRestrain).Restrain).FirstOrDefault();

                    if (restrain != null)
                        foreach (int v in vertexIndexes)
                            pointRestrainVertexIndex[v] = restrain;

                    var loads = nonUniformPressureLoads.Where(i => i.GetGeometry().GetType() == typeof(Point3d)).Where(i => (Point3d)i.GetGeometry() == point);

                    foreach (var load in loads)
                    {
                        if (load is PointLoad gpl)
                        {
                            NodeGlobalForceAttribute pgfa = new NodeGlobalForceAttribute(gpl.LoadCase, gpl.Fx, gpl.Fy, gpl.Fz, gpl.Mx, gpl.My, gpl.Mz);
                            nodeAttributeVertexIndex[pgfa] = vertexIndexes;
                        }
                        else
                        {
                            throw new NotSupportedException("Load type not supported");
                        }

                        if (!_loadCases.ContainsKey((LoadCase)load.LoadCase))
                        {
                            _loadCases[(LoadCase)load.LoadCase] = _loadCases.Values.DefaultIfEmpty().Max() + 1; ;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"Geometry of type {geometry.GetType()} is not supported.");
                }
            }

        }


        private void SetUpFemElements(Mesh geometryMesh, IGlassProperty glassProperty, Dictionary<int, Restrain> pointRestrainVertexIndex, Dictionary<INodeFemAttribute, int[]> nodeAttributeIndex, 
                                        Dictionary<IPlateFemAttribute, int[]> plateAttributeIndex)
        {
            List<IGlassProperty> plateProperties = new List<IGlassProperty>();

            // proprietà
            foreach (var face in geometryMesh.Faces)
            {
                plateProperties.Add(glassProperty);
            }

            if (!_femGlassProperties.ContainsKey(glassProperty))
            {
                _femGlassProperties[glassProperty] = _femGlassProperties.Values.DefaultIfEmpty().Max() + 1; ;
            }

            // Creo i nodi da vertici
            foreach (var vertex in geometryMesh.Vertices)
            {
                if (pointRestrainVertexIndex.ContainsKey(vertex.Id))
                {
                    this._nodes.Add(new Node(vertex.Point, vertex.Id, pointRestrainVertexIndex[vertex.Id]));
                }
                else
                {
                    this._nodes.Add(new Node(vertex.Point, vertex.Id, null));
                }
            }

            // Creazione plate
            for (int i = 0; i < geometryMesh.Faces.Count; i++)
            {
                if (geometryMesh.Faces[i].IsQuad)
                    this._plates.Add(new Plate(plateProperties[i], geometryMesh.Faces[i].Id, _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].A).First(), _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].B).First(),
                                                     _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].C).First(), _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].D).First()));
                else
                    this._plates.Add(new Plate(plateProperties[i], geometryMesh.Faces[i].Id, _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].A).First(), _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].B).First(),
                                                     _nodes.Where(j => j.NodeIndex == geometryMesh.Faces[i].C).First()));
            }

            // Aggiunta attributi
            foreach (IPlateFemAttribute att in plateAttributeIndex.Keys)
            {
                foreach (int id in plateAttributeIndex[att])
                {
                    this._plates.Where(i => i.Index == id).FirstOrDefault().AddAttribute(att);
                }
            }

            foreach (INodeFemAttribute att in nodeAttributeIndex.Keys)
            {
                foreach (int id in nodeAttributeIndex[att])
                {
                    this._nodes.Where(i => i.NodeIndex == id).FirstOrDefault().AddAttribute(att);
                }
            }
        }


        public void SetUpMonolithic(Mesh geometryMesh, Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex, List<IGeometryRestrain> restrains,
                                    MonolithicGlass monolithicGlass, List<Load> uniformPressureLoads, List<Load> nonUniformPressureLoads)
        {
            MonolithicGlassProperty monolithicGlassProperty = new MonolithicGlassProperty(monolithicGlass);

            GetElementAttributes(geometryMesh, embeddedGeometriesMapVertex, restrains, uniformPressureLoads, nonUniformPressureLoads, out Dictionary<int, Restrain> pointRestrainIndex,
                                   out Dictionary<INodeFemAttribute, int[]> nodeAttributesIndex, out Dictionary<IPlateFemAttribute, int[]> plateAttributeIndex);


            SetUpFemElements(geometryMesh, monolithicGlassProperty, pointRestrainIndex, nodeAttributesIndex, plateAttributeIndex);
        }

        public void SetUpLaminated(Mesh geometryMesh, Dictionary<GeometryBase, int[]> embeddedGeometriesMapVertex, List<IGeometryRestrain> restrains,
                                    LaminatedGlassWrapper laminatedGlassWrapper, List<Load> uniformPressureLoads, List<Load> nonUniformPressureLoads,
                                    Checkers.Checker.CheckParameters.LaminatedAnalysisType laminatedAnalysisType)
        {

            switch (laminatedAnalysisType)
            {
                case Checkers.Checker.CheckParameters.LaminatedAnalysisType.MultiElementBrickIntelayer:


                    break;

                    //    case GlassChecker.CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer:
                    //        var normal = laminatedGlassWrapper.GetNormalVector();
                    //        var glassDistances = laminatedGlassWrapper.GetMonolithicBarycenterDistances();
                    //        var interlayerDistances = laminatedGlassWrapper.GetInterlayerBarycenterDistances();

                    //        List<double> distances = new List<double>();
                    //        distances.AddRange(glassDistances);
                    //        distances.AddRange(interlayerDistances);
                    //        distances.Sort();

                    //        List<Mesh> glassMovedMesh = new List<Mesh>();
                    //        List<Mesh> interlayerMovedMesh = new List<Mesh>();
                    //        List<Line3d> line3d = new List<Line3d>();

                    //        // Clona e sposta la mesh
                    //        Mesh movedMesh = geometryMesh;
                    //        for (int i = 0; i < distances.Count; i++)
                    //        {                        
                    //            if (i == 0)
                    //                movedMesh.Pan(normal * distances[i]);
                    //            else
                    //            {
                    //                movedMesh = (Mesh)movedMesh.Clone(true);
                    //                movedMesh.Pan(normal * (distances[i] - distances[i - 1]));
                    //            }

                    //            if (glassDistances.Contains(distances[i]))
                    //            {
                    //                glassMovedMesh.Add(movedMesh);
                    //            }
                    //            else
                    //            {
                    //                interlayerMovedMesh.Add(movedMesh);
                    //            }
                    //        }

                    //        // Vetri
                    //        for (int i = 0; i < glassDistances.Length; i++)
                    //        {
                    //            movedMesh = glassMovedMesh[i];

                    //            var monolithicGlassProperty = new MonolithicGlassProperty(laminatedGlassWrapper.Glass.MonolithicGlasses[i]);

                    //            GetElementAttributes(movedMesh, embeddedGeometriesMapVertex, restrains, uniformPressureLoads, nonUniformPressureLoads, out Dictionary<int, Restrain> pointRestrainIndex,
                    //                                   out Dictionary<INodeFemAttribute, int[]> nodeAttributesIndex, out Dictionary<IPlateFemAttribute, int[]> plateAttributeIndex);

                    //            SetUpFemElements(movedMesh, monolithicGlassProperty, pointRestrainIndex, nodeAttributesIndex, plateAttributeIndex);

                    //        }

                    //        // Interlayer
                    //        for (int i = 0; i < interlayerDistances.Length; i++)
                    //        {
                    //            movedMesh = interlayerMovedMesh[i];

                    //            InterlayerProperty interlayerProperty;
                    //            if (nonUniformPressureLoads != null && uniformPressureLoads != null && nonUniformPressureLoads.Count > 0 && uniformPressureLoads.Count > 0)
                    //            {
                    //                var lc = (LoadCase)nonUniformPressureLoads.First().LoadCase;

                    //                interlayerProperty = new InterlayerProperty(laminatedGlassWrapper.Glass.Interlayers[i], lc.LoadDuration, lc.Temperature);
                    //            }
                    //            else
                    //            {
                    //                var interlayer = laminatedGlassWrapper.Glass.Interlayers[i];
                    //                interlayerProperty = new InterlayerProperty(interlayer, interlayer.Material.GetLoadDurations()[0], interlayer.Material.GetTemperatures()[0]);
                    //            }

                    //            GetElementAttributes(movedMesh, embeddedGeometriesMapVertex, restrains, uniformPressureLoads, nonUniformPressureLoads, out Dictionary<int, Restrain> pointRestrainIndex,
                    //                                   out Dictionary<INodeFemAttribute, int[]> nodeAttributesIndex, out Dictionary<IPlateFemAttribute, int[]> plateAttributeIndex);

                    //            SetUpFemElements(movedMesh, interlayerProperty, pointRestrainIndex, nodeAttributesIndex, plateAttributeIndex);
                    //        }

                    //        // Crea links
                    //        // la mesh di interlayer e la copia di quella del vetro quindi ho assocazione punto punto con l'indice

                    //        for (int i = 0; i < interlayerMovedMesh.Count; i++)
                    //        {
                    //            interlayerMovedMesh[i]
                    //        }


                    //        for (int i = 0; i < glassMovedMesh.Count; i++)
                    //        {
                    //            foreach(var vertex in glassMovedMesh[i].Vertices)
                    //            {
                    //                if (i == 0) // fra glass 0 e interlayer 0
                    //                {
                    //                    Line3d link = new Line3d(vertex.Point, interlayerMovedMesh[0].Vertices[i].Point);
                    //                }
                    //                else // fra glass i e interlayer i-1 e fra glass i e interlayer i
                    //                {
                    //                    Line3d link1 = new Line3d(vertex.Point, interlayerMovedMesh[i-1].Vertices[i].Point); // link fra glass 1 e interlayer 0

                    //                    if (interlayerMovedMesh.Count > i)  // link fra glass 1 e interlayer 1
                    //                    {
                    //                        Line3d link2 = new Line3d(vertex.Point, interlayerMovedMesh[i + 1].Vertices[i].Point); 
                    //                    }
                    //                }

                    //            }                        
                    //        }

                    //        break;

                    //    default:
                    //        throw new NotSupportedException($"LaminatedAnalysisType: {laminatedAnalysisType} not implemented");

                    //}
            }

        }
        #region STRAUS7

        public void SaveToSt7(string filePath)
        {
            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                bool status = CreateSt7Model(aw, filePath, out int mid, out List<string> warnings, out List<string> errors);

                if (status)
                    status = aw.SaveFile(mid);

                switch (_analysisType)
                {
                    case Prototype.AnalysisType.LinearStaticAnalisys:

                        if (status)
                            St7NonLinearSolverSetupForLinearAnalysis(aw, mid);

                        if (status)
                            status = aw.SaveFile(mid);

                        break;

                    case Prototype.AnalysisType.NonLinearStaticAnalysis:
                        throw new NotImplementedException();

                    default:
                        throw new NotSupportedException($"Analysis type {_analysisType} not supported");
                }

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

        public void RunSt7Solver(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File {filePath}, not found");
            }

            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                int mid = 0;
                bool isOpened = aw.OpenFile(filePath, Path.GetTempPath(), ref mid);

                if (isOpened)
                {
                    switch (_analysisType)
                    {
                        case Checkers.Checker.CheckParameters.AnalysisType.LinearStaticAnalisys:

                            //if (status)
                            //    status = St7LinearSolverSetup(aw, mid);
                            //if (status)
                            //    status = St7RunLinearSolver(aw, mid, filePath);

                            bool status = St7RunNonLinearStagedSolver(aw, mid, filePath);

                            break;

                        case Checkers.Checker.CheckParameters.AnalysisType.NonLinearStaticAnalysis:
                            throw new NotImplementedException();

                        default:
                            throw new NotSupportedException($"Analysis type {_analysisType} not supported");
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
            foreach (var node in _nodes)
            {
                int st7NodeIndex = node.NodeIndex; //St7GetElementIndex(node.NodeIndex);
                
                aw.SetNodeXYZ(mId, st7NodeIndex, node.Position.X, node.Position.Y, node.Position.Z);

                if (node.GetRestrain() != null)
                    St7SetNodeRestrain(aw, mId, st7NodeIndex, 1, 1, node.GetRestrain());

                foreach (var attribute in node.Attributes)
                {
                    if (attribute is NodeGlobalForceAttribute pgfa)
                    {
                        int lcNum = _loadCases[(LoadCase)pgfa.LoadCase];
                        St7SetNodeGlobalLoad(aw, mId, st7NodeIndex, lcNum, pgfa);
                    }
                    else
                        throw new NotSupportedException("Point attribute not supported");
                }
            }
            
            St7SetPlateProperties(aw, mId);

            // Plate
            int glassGroupId = 0;
            aw.NewChildGroup(mId, 1, "Glass " + "1", ref glassGroupId);
            foreach (var face in _plates)
            {
                int faceIndex = face.Index; //St7GetElementIndex(face.Index);
                
                var buffer = face.GetConnection();

                int[] st7ConnectivityArray;

                if (face.IsQuad)
                {
                    var st7FaceConnection = face.GetConnection().Select(i => i).ToArray();
                    st7ConnectivityArray = new int[5];
                    st7ConnectivityArray[0] = 4;
                    st7ConnectivityArray[1] = st7FaceConnection[0];
                    st7ConnectivityArray[2] = st7FaceConnection[1];
                    st7ConnectivityArray[3] = st7FaceConnection[2];
                    st7ConnectivityArray[4] = st7FaceConnection[3];
                }
                else
                {
                    var st7FaceConnection = face.GetConnection().Select(i => i).ToArray();
                    st7ConnectivityArray = new int[4];
                    st7ConnectivityArray[0] = 3;
                    st7ConnectivityArray[1] = st7FaceConnection[0];
                    st7ConnectivityArray[2] = st7FaceConnection[1];
                    st7ConnectivityArray[3] = st7FaceConnection[2];
                }

                int propNum = _femGlassProperties[(IGlassProperty)face.Property];

                aw.SetElementConnection(mId, St7ApiConst.tyPLATE, faceIndex, propNum, st7ConnectivityArray);
                aw.SetEntityGroup(mId, St7ApiConst.tyPLATE, faceIndex, glassGroupId);

                foreach (var attribute in face.Attributes)
                {
                    if (attribute is PlateGlobalPressureAttribute pgpa)
                    {
                        int lcNum = _loadCases[(LoadCase)pgpa.LoadCase];
                        St7SetPlateGlobalPressure(aw, mId, faceIndex, lcNum, pgpa);
                    }

                    else
                        throw new NotSupportedException("Point attribute not supported");
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

                if (property is MonolithicGlassProperty mgp)
                {
                    aw.NewPlateProperty(mid, propNum, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, $"MonolithicGlass{propNum}");

                    aw.SetPlateThickness(mid, propNum, new double[] { mgp.MembraneThickness, mgp.BendingThickness });

                    aw.SetPlateIsotropicMaterial(mid, propNum, mgp.GetE(), mgp.GetNi(), mgp.GetDensity(), 0, 0, 0, 0, 0);
                }
                else if (property is InterlayerProperty inp)
                {
                    aw.NewPlateProperty(mid, propNum, St7ApiConst.kPlateTypePlateShell, St7ApiConst.kMaterialTypeIsotropic, $"Interlayer{propNum}");

                    aw.SetPlateThickness(mid, propNum, new double[] { inp.MembraneThickness, inp.BendingThickness });

                    aw.SetPlateIsotropicMaterial(mid, propNum, inp.GetE(), inp.GetNi(), inp.GetDensity(), 0, 0, 0, 0, 0);
                }
                else
                {
                    throw new NotSupportedException("Glass property not supported");
                }
            }
        }

        private void St7SetLoadCase(ISt7ApiService aw, int mid)
        {
            if (!(_loadCases.Count > 0))
                return;

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
            foreach (var lc in _loadCases)
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


            foreach (var lcKvp in _loadCases)
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

        
        private class StageAnalysis
        {
            /// <summary>
            /// ElementID - glassproperty association
            /// </summary>
            private Dictionary<int, IGlassProperty> _elementPropertiesAssociation;


            public void AddPlateProperty(IGlassProperty property, List<Plate> plates)
            {
                foreach (var plate in plates)
                {
                    _elementPropertiesAssociation[plate.GlobalId] = property;
                }
            }
        }


    }
}
