using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Checker.Glasses.Wrappers;
using System.Runtime.Serialization;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.IO;
using St7ApiWrapper;
using GPC.Model.Elements;
using GPC.Model.Loads;
using GPC.Model.FEM.Attributes;
using System.Diagnostics;
using GPC.Geometry.Meshes;

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemModelWrapper
    {
        #region Variables

        /// <summary>
        /// List of mesh for each glass. Each glass can be can be made by one or more meshes
        /// </summary>
        private List<List<FemMesh>> _singleGlassMeshes;

        private string _st7ServerIp;

        #endregion


        #region Public constructors

        internal FemModelWrapper()
            : this(null)
        {

        }

        internal FemModelWrapper(List<List<FemMesh>> meshes)
        {
            _st7ServerIp = "localhost";

            _singleGlassMeshes = meshes ?? new List<List<FemMesh>>();

            SetGlobalConnectivity();
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
                foreach(var femMesh in sgm)
                {
                    femMesh.SetGlobalIds(ref nodeId, ref faceId);

                    foreach (var node in femMesh.Vertices)
                    {
                        if (nodeGlobalId.ContainsKey(node.Point))
                        {
                            (node as FemNode).GlobalId = nodeGlobalId[node.Point];
                        }
                        else
                        {
                            nodeGlobalId[node.Point] = (node as FemNode).GlobalId;
                        }
                    }
                }
            }
        }

        #endregion


        #region STRAUS7

        internal void ToSt7(string filePath)
        {
            if (ConnectService(_st7ServerIp, out St7ApiWrapper.ISt7ApiService aw, out TcpChannel channel))
            {
                bool status = CreateSt7Model(aw, filePath, out int mId, out List<string> warnings, out List<string> errors);

                if (status)
                {
                    aw.SaveFile(mId);
                    aw.CloseFile(mId);
                }
                else
                {
                    throw new Exception("Error");
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
                        
            // Nodes
            foreach (var sgm in _singleGlassMeshes)
            {
                foreach (var femMesh in sgm)
                {
                    foreach (var node in femMesh.Vertices)
                    {
                        aw.SetNodeXYZ(mId, (node as FemNode).GlobalId, node.Point.X, node.Point.Y, node.Point.Z);

                        if ((node as FemNode).Restrain != null)
                            St7SetNodeRestrain(aw, mId, (node as FemNode).GlobalId, 1, 1, (node as FemNode).Restrain);

                        foreach (var attribute in (node as FemNode).Attributes)
                        {
                            if (attribute is NodeGlobalForceAttribute pgfa)
                            {
                                St7SetNodeGlobalLoad(aw, mId, (node as FemNode).GlobalId, 1, pgfa);
                            }
                            else
                                throw new NotSupportedException("Point attribute not supported");
                        }
                    }
                }
            }

            // Plate
            foreach (var sgm in _singleGlassMeshes)
            {
                foreach (var femMesh in sgm)
                {
                    foreach (var face in femMesh.Faces)
                    {
                        int[] globalConnectivity;

                        if (face.IsQuad)
                        {
                            globalConnectivity = new int[5];
                            globalConnectivity[0] = 4;
                            globalConnectivity[1] = (femMesh.Vertices.Where(i => i.Id == face.A).FirstOrDefault() as FemNode).GlobalId;
                            globalConnectivity[2] = (femMesh.Vertices.Where(i => i.Id == face.B).FirstOrDefault() as FemNode).GlobalId;
                            globalConnectivity[3] = (femMesh.Vertices.Where(i => i.Id == face.C).FirstOrDefault() as FemNode).GlobalId;
                            globalConnectivity[4] = (femMesh.Vertices.Where(i => i.Id == face.D).FirstOrDefault() as FemNode).GlobalId;
                        }
                        else
                        {
                            globalConnectivity = new int[4];
                            globalConnectivity[0] = 3;
                            globalConnectivity[1] = (femMesh.Vertices.Where(i => i.Id == face.A).FirstOrDefault() as FemNode).GlobalId;
                            globalConnectivity[2] = (femMesh.Vertices.Where(i => i.Id == face.B).FirstOrDefault() as FemNode).GlobalId;
                            globalConnectivity[3] = (femMesh.Vertices.Where(i => i.Id == face.C).FirstOrDefault() as FemNode).GlobalId;
                        }
                        aw.SetElementConnection(mId, St7ApiConst.tyPLATE, (face as FemPlate).GlobalId, 1, globalConnectivity);

                        foreach (var attribute in (face as FemPlate).Attributes)
                        {
                            if (attribute is PlateGlobalPressureAttribute pgpa)
                            {
                                St7SetPlateGlobalPressure(aw, mId, (face as FemPlate).GlobalId, 1, pgpa);
                            }
                            else
                                throw new NotSupportedException("Point attribute not supported");
                        }
                    }
                }
            }

            return true;
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

        private bool St7RunLinearSolver(ISt7ApiService aw, int mid, string filePath)
        {
            string resultExtension = "lsa";
            ProcessStartInfo pInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(filePath, "St7Solver.exe"),
                Arguments = string.Format("\"{0}\" {1}", filePath, 1)
            };

            if (!File.Exists(pInfo.FileName))
                throw new FileNotFoundException($"File {pInfo.FileName} not found");
            else
            {
                Process p = Process.Start(pInfo);
                
                p.WaitForExit(); // Wait for the process to end.

                if (p.ExitCode == 0) // Analysis terminated with success
                {
                    string resultPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "." + resultExtension);
                    int numPrimary = 0, numSecondary = 0;

                    if (aw.OpenResultFile(mid, resultPath, null, (byte)St7ApiConst.btTrue, ref numPrimary, ref numSecondary))
                    {
                        aw.CloseResultFile(mid);
                    }
                    return true;
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


        #endregion

        #region FeM

        internal void ToFeM()
        {
            throw new NotSupportedException();
        } 
        
        #endregion

    }
}
