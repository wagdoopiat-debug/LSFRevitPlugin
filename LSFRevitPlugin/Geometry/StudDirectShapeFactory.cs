using System.Collections.Generic;
using System.Linq;
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
            return CriarElementoMontante(doc, new List<Solid> { solido }, nome);
        }

        /// <summary>
        /// Cria o DirectShape a partir de varios solidos (ex.: a guia de blocking no corpo em "U"
        /// + as orelhas dobradas). Solidos nulos ou vazios sao ignorados.
        /// </summary>
        public static DirectShape CriarElementoMontante(Document doc, IList<Solid> solidos, string nome = "LSF - Montante (teste)")
        {
            if (solidos == null) return null;

            List<GeometryObject> geometria = solidos
                .Where(s => s != null)
                .Cast<GeometryObject>()
                .ToList();
            if (geometria.Count == 0) return null;

            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_StructuralFraming));
            ds.SetShape(geometria);
            ds.Name = nome;
            return ds;
        }
    }
}

