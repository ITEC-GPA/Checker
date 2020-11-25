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
                            SetSt7NodeRestrain(aw, mId, (node as FemNode).GlobalId, 1, 1, (node as FemNode).Restrain);
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
                    }
                }
            }

            return true;
        }

        private bool SetSt7NodeRestrain(ISt7ApiService aw, int mid, int nodeNumber, int caseNumber, int ucsId, Restrain restrain)
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

        #endregion

        #region FeM
        
        internal void ToFeM()
        {
            throw new NotSupportedException();
        } 
        
        #endregion

        [System.Diagnostics.Conditional("Debug")]
        internal void ExportMeshMSHFormat(string filePath, List<Mesh> meshes)
        {
            GPC.Utilities.Meshes.MeshExport.ExportToMshFormatv2(filePath, meshes);
        }

    }
}
