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

namespace GPC.Checker.Glasses.FemModel
{
    internal class FemModelWrapper
    {
        #region Variables
        /// <summary>
        /// List of mesh for each glass. Each glass can be rapresneted by a list of mesh
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

        #region

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

        private static bool ConnectService(string ip, out St7ApiWrapper.ISt7ApiService ro, out TcpChannel channel)
        {
            ro = null;
            channel = null;
            try
            {
                channel = new TcpChannel();
                ChannelServices.RegisterChannel(channel, false);

                System.Threading.Thread.Sleep(1000);
                ro = (St7ApiWrapper.ISt7ApiService)Activator.GetObject(typeof(St7ApiWrapper.ISt7ApiService), string.Format("tcp://{0}:8085/St7ApiService", ip));
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

        private bool CreateSt7Model(St7ApiWrapper.ISt7ApiService aw, string filePath, out int mId, out List<string> warnings, out List<string> errors)
        {
            warnings = new List<string>();
            errors = new List<string>();
            mId = 0;

            string scratchPath = Path.GetTempPath();

            // Create a new model
            if (!aw.NewFile(filePath, scratchPath, ref mId))
                throw new Exception("Failed to create new model");

            int[] st7Units = new int[aw.Const("kLastUnit")];
            st7Units[aw.Const("ipLENGTHU")] = aw.Const("luMILLIMETRE");
            st7Units[aw.Const("ipFORCEU")] = aw.Const("fuNEWTON");
            st7Units[aw.Const("ipSTRESSU")] = aw.Const("suMEGAPASCAL");
            st7Units[aw.Const("ipMASSU")] = aw.Const("muTONNE");
            st7Units[aw.Const("ipTEMPERU")] = aw.Const("tuCELSIUS");
            st7Units[aw.Const("ipENERGYU")] = aw.Const("euJOULE");

            if (!aw.SetUnits(mId, st7Units))
                throw new Exception("Failed to set the units");

            foreach (var sgm in _singleGlassMeshes)
            {
                foreach (var femMesh in sgm)
                {
                    foreach (var node in femMesh.Vertices)
                    {
                        aw.SetNodeXYZ(mId, (node as FemNode).GlobalId, node.Point.X, node.Point.Y, node.Point.Z);  
                    }
                }
            }

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
                        aw.SetElementConnection(mId, St7ApiWrapper.St7ApiConst.tyPLATE, (face as FemPlate).GlobalId, 1, globalConnectivity);
                    }
                }
            }

            return true;
        } 
        
        #endregion

        internal void ToFeM()
        {
            throw new NotSupportedException();
        }

        internal void ExportMeshMSHFormat(string filePath, List<Mesh> meshes)
        {
            GPC.Utilities.Meshes.MeshExport.ExportToMshFormatv2(filePath, meshes);
        }

    }
}
