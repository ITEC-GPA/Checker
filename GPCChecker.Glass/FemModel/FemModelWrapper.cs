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
        private List<GlassFemNode> _nodes;
        private List<GlassFemPlate> _plates;
        private string _ip;

        //public FemModelWrapper(List<Mesh> meshes)
        //{
        //    _ = meshes ?? throw new ArgumentNullException(nameof(meshes));
        //}

        internal FemModelWrapper()
        {
            _ip = "localhost";

            _nodes = new List<GlassFemNode>();
            _plates = new List<GlassFemPlate>();
        }

        internal void AddMeshes(List<Mesh> meshes)
        {
            foreach(var mesh in meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    _nodes.Add(new GlassFemNode(vertex));
                }
                foreach (var face in mesh.Faces)
                {
                    _plates.Add(new GlassFemPlate(face));
                }
            }
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

        internal void ToSt7(string filePath)
        {
            if (ConnectService(_ip, out St7ApiWrapper.ISt7ApiService aw, out TcpChannel channel))
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
                throw new Exception($"Unable to connect to Apiservice through ip: {_ip}");
            }

            if (channel != null)
                ChannelServices.UnregisterChannel(channel);
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

            foreach(var nodes in _nodes)
            {
                aw.SetNodeXYZ(mId, nodes.Id, nodes.Point.X, nodes.Point.Y, nodes.Point.Z);
            }

            foreach (var plate in _plates)
            {
                aw.SetElementConnection(mId, aw.Const("tyPLATE"), plate.Id, 1, plate.GetConnection());
            }

            return true;
        }

        internal void ToFeM()
        {
            throw new NotSupportedException();
        }


        #region Nested classes

        private sealed class GlassFemNode : MeshVertex
        {
            public GlassFemNode(MeshVertex vertex)
                : base(vertex)
            {

            }
        }

        private sealed class GlassFemPlate : MeshFace
        {


            public GlassFemPlate(MeshFace face)
                : base(face)
            {

            }

            public GlassFemPlate(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }

            public GlassFemPlate(int id, int a, int b, int c)
                : base(id, a, b, c)
            {

            }

            public GlassFemPlate(int id, int a, int b, int c, int d)
                : base(id, a, b, c, d)
            {

            }
        } 
        
        #endregion
    }
}
