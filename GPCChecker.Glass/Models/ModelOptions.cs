using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;
using GPC.Geometry.Meshes;

namespace GPC.Checkers.Glasses.Models
{
    public class ModelOptions
    {
        private readonly Mesh.GenerateOptions _meshOptions;

        /// <summary>
        /// Rapresent width of the line load strip used in the eq thickness analysis
        /// </summary>
        public double LineLoadWidthEqThickness { get; set; }


        /// <summary>
        /// Rapresent lenght of the point load square side used in the eq thickness analysis
        /// </summary>
        public double PointLoadWidthEqThickness { get; set; }


        public Mesh.GenerateOptions MeshOptions => _meshOptions;


        public ModelOptions()
        {

            LineLoadWidthEqThickness = 20;
            PointLoadWidthEqThickness = 20;

            _meshOptions = new Mesh.GenerateOptions();
        }


    }
}
