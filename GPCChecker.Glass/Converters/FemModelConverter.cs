using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Models;
using GPC.Converters;

using GPC.Model.FEM;
using GPC.Checkers.Glasses.FemModels;
using GPC.Model.FEM.FiniteElements;
using GPC.Model.FEM.Properties;
using St7ApiWrapper;

namespace GPC.Checkers.Glasses.Converters
{

    public class FemModelConverter : GPC.Converters.FemModelConverter
    {

        public enum Straus7SolverTypes
        {
            Linear,
            NonLinear
        }

        private Straus7SolverTypes _st7SolverType;
        private bool _st7NonLinearGeometryActive;

        /// <summary>
        /// Map between <see cref="Stage"/> id and straus7 stage ID
        /// </summary>
        private readonly Dictionary<int, int> _st7StageMap;

        /// <summary>
        /// Map between <see cref="FemModel._combinations"/> id and Stage id - Stage increment id;
        /// </summary>
        private readonly Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)> _st7NLACombinationMap;


        /// <summary>
        /// Map between <see cref="FemModel._combinations"/> id and St7ComboId in the Linear loadcase combination table  ;
        /// </summary>
        private readonly Dictionary<string, int> _st7LSACombinationMap;


        public FemModelConverter() : base()
        {
            _st7StageMap = new Dictionary<int, int>();
            _st7LSACombinationMap = new Dictionary<string, int>();
            _st7NLACombinationMap = new Dictionary<string, (int stageId, int stageIncrementId, int progressiveIncrementId)>();

        }


        protected bool ConvertModelToStraus7(string straus7ModelPath, string fileName, FemModelWrapper femModel, ISt7ApiService aw, int mid)
        {
            
            if (!base.ConvertModelToStraus7(straus7ModelPath, fileName, femModel, aw, mid))
                return false;

            // Estensione  


            // Setup solver
            // 1 Static sub-stepping option; 0, 1, 2 or 3 for None, Load Scaling, Displacement Scaling or Displacement Control(Arc Length), respectively.
            aw.SetSolverDefaultsInteger(mid, St7ApiConst.spStaticAutoStepping, 1);


            if (femModel.GetStages().Length > 0)
            {
                if (!ConvertModelToStraus7SetStages(aw, mid, femModel)) // crea gli stages, senza combo. Non spegne elementi
                    return false;

                if (!ConvertModelToStraus7SetCombination(aw, mid, femModel)) 
                    return false;
                
            }

            switch (ConvertModelToStraus7SetSolverType(femModel, out bool nonLinearGeometry))
            {
                case Straus7SolverTypes.Linear:

                    if (!ConvertModelToStraus7SetLinearSolverSetup(aw, mid, femModel))
                        return false;

                    if (!ConvertModelToStraus7SetSetLinearLoadCaseCombination(aw, mid, femModel))
                        return false;

                    femModel.St7NonLinearGeometryActive = nonLinearGeometry;
                    femModel.St7SolverType = Straus7SolverTypes.Linear;

                    break;

                case Straus7SolverTypes.NonLinear:

                    if (!ConvertModelToStraus7SetNonLinearSolverSetup(aw, mid, false, nonLinearGeometry, false))
                        return false;

                    femModel.St7NonLinearGeometryActive = nonLinearGeometry;
                    femModel.St7SolverType = Straus7SolverTypes.NonLinear;

                    break;

                default:
                    throw new NotSupportedException();
            }

            return true;
        }

        protected bool ConvertModelToStraus7SetBrickProperties(ISt7ApiService aw, int mid, FemModelWrapper femModel)
        {

            int _bufferId = 0;
            int st7PropId = 0;
            foreach (var propertyName in femModel.GetBrickPropertyNames())
            {
                st7PropId++;

                BrickProperty property = femModel.GetBrickProperty(propertyName);

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

                        if (!ModelAnalysisOptions.Instance.Straus7BrickBubbleFunction)
                            aw.SetBrickAddBubbleFunction(mid, st7PropId, false);
                    }
                    else if (inp.Material is Model.FEM.Materials.OrthotropicFemMaterial orto)
                    {
                        aw.NewBrickProperty(mid, st7PropId, St7ApiConst.kMaterialTypeOrthotropic, inp.Name);

                        aw.SetBrickOrthotropicMaterial(mid, st7PropId, new[] { orto.E1, orto.E2, orto.E3, orto.G12, orto.G23, orto.G31, orto.Ni12, orto.Ni23, orto.Ni31,
                                                                               orto.Density, orto.Alpha1, orto.Alpha2, orto.Alpha3, 0, 0, 0, 0, 0, 0 });

                        if (!ModelAnalysisOptions.Instance.Straus7BrickBubbleFunction)
                            aw.SetBrickAddBubbleFunction(mid, st7PropId, false);
                    }
                    else
                        throw new NotImplementedException();
                }
                else
                {
                    throw new NotSupportedException($"Property type: {property} not supported");
                }

                _st7BrickPropertyMap.Add(propertyName, st7PropId);
            }

            return true;
        }


        /// <summary>
        /// Add each stage in <see cref="FemModel._stages"/> to st7 and update <see cref="_st7StageMap"/>
        /// </summary>
        /// <remarks>This method does not turn off elements (groups for straus) at certain stage</remarks>
        protected virtual bool ConvertModelToStraus7SetStages(ISt7ApiService aw, int mid, FemModelWrapper femModel)
        {
            int st7StageId = _st7StageMap.Values.DefaultIfEmpty(0).Max();

            foreach (Model.FEM.Stage stage in femModel.GetStages())
            {
                aw.AddStage(mid, stage.Name, new int[] { stage.Morph ? St7ApiConst.btTrue : St7ApiConst.btFalse, St7ApiConst.btFalse, St7ApiConst.btFalse });
                _st7StageMap.Add(stage.Id, ++st7StageId);

                using (var stagePropertyEnum = femModel.GetStagePropertyEnumerator(stage.Id))
                {
                    while (stagePropertyEnum.MoveNext())
                    {
                        var current = stagePropertyEnum.Current;

                        if (current.Key is Brick brick)
                        {
                            BrickProperty propertyOverload = femModel.GetBrickProperty(current.Value.PropertyName);

                            if (brick.Property.Name != propertyOverload.Name)
                            {
                                aw.St7SetElementPropertySwitch(mid, St7ApiConst.tyBRICK, _st7BrickMap[current.Key.Id], _st7BrickPropertyMap[propertyOverload.Name], _st7StageMap[stage.Id]);
                            }
                        }
                        else if (current.Key is Plate plate)
                        {
                            PlateProperty propertyOverload = femModel.GetPlateProperty(current.Value.PropertyName);

                            if (plate.Property.Name != propertyOverload.Name)
                            {
                                aw.St7SetElementPropertySwitch(mid, St7ApiConst.tyPLATE, _st7PlateMap[current.Key.Id], _st7PlatePropertyMap[propertyOverload.Name], _st7StageMap[stage.Id]);
                            }
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }

            }

            return true;
        }


        /// <summary>
        /// This method set also the map <see cref="_st7NLACombinationMap"/>
        /// </summary>
        protected virtual bool ConvertModelToStraus7SetCombination(ISt7ApiService aw, int mid, FemModelWrapper femModel)
        {

            int progressiveID = 1;
            foreach (Model.FEM.Stage stage in femModel.GetStages().OrderBy(i => _st7StageMap[i.Id]))
            {
                int comboIndex = 1;

                if (_st7StageMap.ContainsKey(stage.Id))
                {
                    foreach (var combo in femModel.GetStageCombinations(stage.Id))
                    {
                        aw.AddNLAIncrement(mid, _st7StageMap[stage.Id], combo.Name);


                        foreach (var (loadcase, coefficient) in combo.GetLoadCaseCoefficientsTuple())
                            aw.SetNLALoadIncrementFactor(mid, _st7StageMap[stage.Id], comboIndex, _st7LoadCaseMap[loadcase.Name], coefficient);


                        _st7NLACombinationMap[combo.Name] = (_st7StageMap[stage.Id], comboIndex, progressiveID);


                        comboIndex++;
                        progressiveID++;
                    }
                }
            }

            return true;
        }


        private Straus7SolverTypes ConvertModelToStraus7SetSolverType(FemModelWrapper femModel, out bool nonLinearGeometry)
        {
            switch (femModel.AnalysisType)
            {
                case FemModel.AnalysisTypes.Linear:
                    if (femModel.GetStages().Length > 0)
                    {
                        nonLinearGeometry = false;
                        return Straus7SolverTypes.NonLinear;
                    }
                    else
                    {
                        nonLinearGeometry = false;
                        return Straus7SolverTypes.Linear;
                    }

                case FemModel.AnalysisTypes.NonLinear:
                    nonLinearGeometry = true;
                    return Straus7SolverTypes.NonLinear;

                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        /// Set up the linear solver, activating each loadcase in the <see cref="FemModel._loadCases"/> list
        /// </summary>
        private bool ConvertModelToStraus7SetLinearSolverSetup(ISt7ApiService aw, int mid, FemModelWrapper femModel)
        {
            foreach (var lcName in femModel.GetLoadCaseNames())
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
        private bool ConvertModelToStraus7SetNonLinearSolverSetup(ISt7ApiService aw, int mid, bool nonLinearMaterial, 
                                                                    bool nonLinearGeometry, bool stagedAnalysis)
        {
            if (stagedAnalysis)
            {
                foreach (var stageId in _st7StageMap)
                {
                    aw.EnableNLAStage(mid, stageId.Value);
                }
            }

            return aw.SetSolverNonlinearMaterial(mid, nonLinearMaterial) 
                   && aw.SetSolverNonlinearGeometry(mid, nonLinearGeometry) 
                   && aw.SetNLAStagedAnalysis(mid, stagedAnalysis);
        }

        /// <summary>
        /// Set up the linear load case combination table
        /// </summary>
        private bool ConvertModelToStraus7SetSetLinearLoadCaseCombination(ISt7ApiService aw, int mid, FemModelWrapper femModel)
        {
            int st7CId = 0;

            foreach (var combo in femModel.GetCombinations())
            {
                var lcTuples = combo.GetLoadCaseCoefficientsTuple();

                if (aw.AddLSACombination(mid, combo.Name))
                {
                    st7CId++;
                    bool added = false;

                    foreach (var (loadcase, coefficient) in lcTuples)
                    {
                        if (femModel.LoadCaseExist(loadcase.Name))
                        {
                            if (aw.SetLSACombinationFactor(mid, St7ApiConst.ltLoadCase, st7CId, _st7LoadCaseMap[loadcase.Name], 1, combo[loadcase]))
                            {
                                added = true;
                            }
                        }
                    }

                    if (added)
                    {
                        _st7LSACombinationMap.Add(combo.Name, st7CId);
                    }
                    else
                    {
                        aw.DeleteLSACombination(mid, st7CId);
                        st7CId--;
                    }
                }

            }

            return true;
        }


        /// <summary>
        /// This method set the non linear solver, turning off the nonlinearity and activating one loadcase for each stage.
        /// </summary>
        /// <param name="aw"></param>
        /// <param name="mid"></param>
        /// <remarks>Lenght of <see cref="_st7StageMap"/> must be the same of _st7LoadCaseMap lenght </remarks>
        /// <exception cref="ArgumentException">If lenght of <see cref="_st7StageMap"/> is different than _st7LoadCaseMap</exception>
        private void ConvertModelToStraus7SetNonLinearSolverSetupForLinearAnalysis(ISt7ApiService aw, int mid)
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
    }
}
