using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Envolve um Solid num DirectShape, o elemento leve do Revit usado para representar
    /// geometria gerada por programa sem o custo de FamilyInstance (ADR-004 do plano).
    /// </summary>
    public static class StudDirectShapeFactory
    {
        /// <summary>
        /// Cria o elemento DirectShape. Se um nome for informado, ele e gravado no elemento --
        /// usado para marcar reforcos de canto e evitar duplicacao quando paredes diferentes
        /// sao processadas em execucoes separadas.
        /// </summary>
        public static DirectShape CriarElementoMontante(Document doc, Solid solido, string nome = "LSF - Montante (teste)")
        {
            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_StructuralFraming));
            ds.SetShape(new List<GeometryObject> { solido });
            ds.Name = nome;
            return ds;
        }
    }
}
