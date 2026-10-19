using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Boards;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Materiais (cores) das placas, um por tipo. Como os materiais da estrutura, cada um tem a
    /// cor no 3D e o preenchimento solido de superficie e de corte com a mesma cor.
    /// Precisa ser chamado com uma Transaction aberta.
    /// </summary>
    public static class PlacaMateriais
    {
        public static ElementId Obter(Document doc, TipoPlaca tipo)
        {
            string nome = "LSF Placa - " + PlacaCatalogo.NomeDoTipo(tipo);

            Material existente = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == nome);

            ElementId id = existente != null ? existente.Id : Material.Create(doc, nome);

            Material material = (Material)doc.GetElement(id);
            Color cor = CorDoTipo(tipo);

            // Reaplicado a cada geracao: corrige tambem os materiais criados na versao anterior,
            // que tinham so a cor simples.
            material.Color = cor;
            material.Transparency = 0;

            ElementId padraoSolido = ObterPadraoSolido(doc);
            if (padraoSolido != ElementId.InvalidElementId)
            {
                material.SurfaceForegroundPatternId = padraoSolido;
                material.SurfaceForegroundPatternColor = cor;
                material.CutForegroundPatternId = padraoSolido;
                material.CutForegroundPatternColor = cor;
            }

            return id;
        }

        public static Color CorDoTipo(TipoPlaca tipo)
        {
            switch (tipo)
            {
                case TipoPlaca.Cimenticia: return new Color(150, 150, 150);
                case TipoPlaca.GlasrocX: return new Color(190, 200, 90);
                case TipoPlaca.Osb: return new Color(205, 165, 105);
                case TipoPlaca.GessoSt: return new Color(235, 235, 225);
                case TipoPlaca.GessoRu: return new Color(110, 180, 120);
                case TipoPlaca.GessoRf: return new Color(230, 120, 130);
                case TipoPlaca.GessoPerforma: return new Color(150, 110, 200);
                case TipoPlaca.PerformaRu: return new Color(60, 170, 170);
                default: return new Color(200, 200, 200);
            }
        }

        private static ElementId ObterPadraoSolido(Document doc)
        {
            FillPatternElement solido = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f =>
                {
                    FillPattern p = f.GetFillPattern();
                    return p != null && p.IsSolidFill;
                });

            return solido != null ? solido.Id : ElementId.InvalidElementId;
        }
    }
}