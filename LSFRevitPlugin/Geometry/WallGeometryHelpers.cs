using System.Linq;
using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>Funcoes compartilhadas pelos 3 calculadores de encontro (L, T, +).</summary>
    public static class WallGeometryHelpers
    {
        public const double ToleranciaPes = 0.05;

        /// <summary>Espessura do NUCLEO estrutural da parede (exclui placas de acabamento).</summary>
        public static double ObterEspessuraNucleo(Wall parede)
        {
            CompoundStructure estrutura = parede.WallType?.GetCompoundStructure();
            if (estrutura == null)
                return parede.Width;

            double espessuraNucleo = estrutura.GetLayers()
                .Where(camada => camada.Function == MaterialFunctionAssignment.Structure)
                .Sum(camada => camada.Width);

            return espessuraNucleo > 0 ? espessuraNucleo : parede.Width;
        }
    }
}
