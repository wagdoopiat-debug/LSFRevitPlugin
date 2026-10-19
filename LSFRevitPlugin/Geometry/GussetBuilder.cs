using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Cria solidos de chapas gusset nas juntas da trelica. O gusset e uma placa plana situada
    /// NO PLANO VERTICAL da trelica (plano u,z). A extrusao ocorre ao longo da direcao normal ao
    /// plano da trelica (<paramref name="direcaoNormal"/>), centralizada (metade da espessura
    /// para cada lado) para minimizar sobreposicoes com os perfis "C".
    /// </summary>
    public static class GussetBuilder
    {
        public static List<Solid> CriarSolidosGussets(
            List<PosicaoGusset> posicoes,
            XYZ origemEixo,
            XYZ direcaoU,
            XYZ direcaoNormal,
            ElementId materialId = null)
        {
            var solidos = new List<Solid>();
            if (posicoes == null || origemEixo == null || direcaoU == null) return solidos;

            XYZ uDir = direcaoU.Normalize();
            XYZ nDir = direcaoNormal;
            if (nDir == null || nDir.GetLength() < 1e-9)
            {
                nDir = uDir.CrossProduct(XYZ.BasisZ);
            }
            nDir = nDir.Normalize();

            foreach (var g in posicoes)
            {
                XYZ centro = origemEixo
                    + uDir.Multiply(RevitVersionAdapter.MilimetrosParaInterno(g.UMm))
                    + XYZ.BasisZ.Multiply(RevitVersionAdapter.MilimetrosParaInterno(g.ZCentroMm));

                Solid s = CaixaPlana(centro, g.ComprimentoUMm, g.AlturaZMm, g.EspessuraMm, uDir, nDir, materialId);
                if (s != null) solidos.Add(s);
            }
            return solidos;
        }

        private static Solid CaixaPlana(
            XYZ centro,
            double compUMm,
            double alturaZMm,
            double espMm,
            XYZ uDir,
            XYZ nDir,
            ElementId materialId)
        {
            double comp = RevitVersionAdapter.MilimetrosParaInterno(compUMm);
            double altura = RevitVersionAdapter.MilimetrosParaInterno(alturaZMm);
            double esp = RevitVersionAdapter.MilimetrosParaInterno(espMm);
            if (comp <= 1e-9 || altura <= 1e-9 || esp <= 1e-9) return null;

            double c2 = comp / 2.0;
            double a2 = altura / 2.0;
            double e2 = esp / 2.0;

            // Offset pela metade da espessura ao longo da direcao normal, para centralizar.
            XYZ centroExtrusao = centro - nDir.Multiply(e2);

            var pontos = new List<XYZ>
            {
                centroExtrusao - uDir.Multiply(c2) - XYZ.BasisZ.Multiply(a2),
                centroExtrusao + uDir.Multiply(c2) - XYZ.BasisZ.Multiply(a2),
                centroExtrusao + uDir.Multiply(c2) + XYZ.BasisZ.Multiply(a2),
                centroExtrusao - uDir.Multiply(c2) + XYZ.BasisZ.Multiply(a2)
            };

            var loops = new List<CurveLoop> { SecaoPerfil.CriarLoop(pontos) };
            var opcoes = new SolidOptions(materialId ?? ElementId.InvalidElementId, ElementId.InvalidElementId);
            try
            {
                return GeometryCreationUtilities.CreateExtrusionGeometry(loops, nDir, esp, opcoes);
            }
            catch
            {
                return null;
            }
        }
    }
}
