
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
using System.Threading.Tasks;
using GPC.Checker.Glasses.LoadCases;
using GPC.Checker.Glasses.Models;
using GPC.Model.FEM.Attributes;
using GPC.Model.FEM.FiniteElements;
using GPC.Model.FEM.Properties;
using GPC.Model.Restrains;

namespace GPC.Checker.Glasses.FemModel
{
    public class FemModelWrapper : Model.FEM.FemModel
    {

        private string _st7ServerIp;


        public FemModelWrapper() : this (string.Empty)
        {

        }

        public FemModelWrapper(string name) 
            : base(name)
        {
            _st7ServerIp = "localhost";
        }

        public FemModelWrapper(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            throw new NotImplementedException();
        }

        #region STRAUS7

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
        /// <param name="saveFolderPath">Folder path where to save the results</param>
        public void SaveToSt7(string saveFolderPath)
        {

            Directory.CreateDirectory(saveFolderPath);


            if (ConnectService(_st7ServerIp, out ISt7ApiService aw, out TcpChannel channel))
            {
                bool status = CreateSt7Model(aw, saveFolderPath, out int mid, out List<string> warnings, out List<string> errors);

                if (status)
                    status = aw.SaveFile(mid);

                //switch (_analysisType)
                //{
                //    case Prototype.AnalysisType.LinearStaticAnalisys:

                //        if (status)
                //            St7NonLinearSolverSetupForLinearAnalysis(aw, mid);

                //        if (status)
                //            status = aw.SaveFile(mid);

                //        break;

                //    case Prototype.AnalysisType.NonLinearStaticAnalysis:
                //        throw new NotImplementedException();

                //    default:
                //        throw new NotSupportedException($"Analysis type {_analysisType} not supported");
                //}

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
            string filePath = Path.ChangeExtension(Path.Combine(saveFolderPath, Name), "St7");

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
                int st7NodeIndex = node.Id; //St7GetElementIndex(node.NodeIndex);

                aw.SetNodeXYZ(mId, st7NodeIndex, node.Position.X, node.Position.Y, node.Position.Z);


                foreach (var attribute in node.AttributesFreedomCase)
                {
                    if (attribute is NodeRestrainAttribute nra)
                    {
                        St7SetNodeRestrain(aw, mId, node.Id, 1, 1, nra.Restrains);
                    }
                    else if (attribute is NodeStiffnessAttribute nsa)
                    {
                        St7SetNodeRestrain(aw, mId, node.Id, 1, 1, nsa.Stiffnesses);
                    }
                }

                // Carichi
                foreach (var attribute in node.AttributesLoadCase)
                {
                    if (attribute is NodeForceAttribute pgfa)
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

            foreach(var element in _elements)
            {
                if (element is Plate plate)
                {
                    int faceIndex = plate.Id; //St7GetElementIndex(face.Index);

                    int[] st7ConnectivityArray;

                    if (plate.IsQuad)
                    {
                        var st7FaceConnection = plate.GetNodesID();
                        st7ConnectivityArray = new int[5];
                        st7ConnectivityArray[0] = 4;
                        st7ConnectivityArray[1] = st7FaceConnection[0];
                        st7ConnectivityArray[2] = st7FaceConnection[1];
                        st7ConnectivityArray[3] = st7FaceConnection[2];
                        st7ConnectivityArray[4] = st7FaceConnection[3];
                    }
                    else
                    {
                        var st7FaceConnection = plate.GetNodesID();
                        st7ConnectivityArray = new int[4];
                        st7ConnectivityArray[0] = 3;
                        st7ConnectivityArray[1] = st7FaceConnection[0];
                        st7ConnectivityArray[2] = st7FaceConnection[1];
                        st7ConnectivityArray[3] = st7FaceConnection[2];
                    }

                    
                    int propNum = _plateProperties[plate.Property];

                    aw.SetElementConnection(mId, St7ApiConst.tyPLATE, faceIndex, propNum, st7ConnectivityArray);
                    aw.SetEntityGroup(mId, St7ApiConst.tyPLATE, faceIndex, glassGroupId);

                    //foreach (var attribute in .Attributes)
                    //{
                    //    if (attribute is PlatePressureAttribute pgpa)
                    //    {
                    //        int lcNum = _loadCases[(LoadCase)pgpa.LoadCase];
                    //        St7SetPlateGlobalPressure(aw, mId, faceIndex, lcNum, pgpa);
                    //    }

                    //    else
                    //        throw new NotSupportedException("Point attribute not supported");
                    //}
                }
            }
                       
            

            return true;
        }

        private void St7SetStages(ISt7ApiService aw, int mid)
        {
            // Creo stage per ogni loadcase

            foreach (var lc in base._loadCases.OrderBy(x => x.Value))
            {
                aw.AddStage(mid, lc.Key.Name, new int[] { St7ApiConst.btFalse, St7ApiConst.btFalse, St7ApiConst.btFalse });
            }
        }

        private void St7SetPlateProperties(ISt7ApiService aw, int mid)
        {
            if (_plateProperties.Values.Min() != 1)
                throw new NotSupportedException("Minimum plate properties id different than 1");

            int _bufferId = 0;
            foreach (var gpkvp in _plateProperties.OrderBy(k => k.Value))
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


        #endregion



    }
}
