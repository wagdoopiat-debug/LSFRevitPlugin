using System;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Papel de um elemento gerado (define a cor dele).
    /// </summary>
    public enum PapelElemento
    {
        Montante,
        Guia,
        ReforcoVao
    }

    /// <summary>
    /// Ids dos tres materiais de um sistema, criados uma unica vez por geracao.
    /// </summary>
    public sealed class MateriaisLsf
    {
        public ElementId Montante { get; set; }
        public ElementId Guia { get; set; }
        public ElementId ReforcoVao { get; set; }
    }

    /// <summary>
    /// Cria (ou reaproveita) os materiais coloridos do plugin. Cada material tem cor no 3D
    /// e preenchimento solido na planta (corte), usando a mesma cor.
    /// IMPORTANTE: precisa ser chamado com uma Transaction aberta (cria Material no projeto).
    /// Chamar uma vez por geracao e guardar os ids -- nao chamar por elemento.
    /// </summary>
    public static class LsfMateriais
    {
        public static MateriaisLsf ObterConjunto(Document doc, SistemaPerfil sistema)
        {
            return new MateriaisLsf
            {
                Montante = ObterOuCriar(doc, sistema, PapelElemento.Montante),
                Guia = ObterOuCriar(doc, sistema, PapelElemento.Guia),
                ReforcoVao = ObterOuCriar(doc, sistema, PapelElemento.ReforcoVao),
            };
        }

        public static ElementId ObterOuCriar(Document doc, SistemaPerfil sistema, PapelElemento papel)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            string nome = MontarNome(sistema, papel);

            Material existente = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == nome);

            if (existente != null) return existente.Id;

            ElementId novoId = Material.Create(doc, nome);
            Material material = (Material)doc.GetElement(novoId);

            Color cor = CorPara(sistema, papel);
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

            return novoId;
        }

        private static string MontarNome(SistemaPerfil sistema, PapelElemento papel)
        {
            string nomeSistema = sistema == SistemaPerfil.Drywall ? "Drywall" : "SteelFrame";
            return "LSF_" + nomeSistema + "_" + papel;
        }

        private static Color CorPara(SistemaPerfil sistema, PapelElemento papel)
        {
            if (sistema == SistemaPerfil.Drywall)
            {
                switch (papel)
                {
                    case PapelElemento.Montante: return new Color(46, 134, 222);   // azul
                    case PapelElemento.Guia: return new Color(27, 79, 140);        // azul escuro
                    default: return new Color(0, 184, 212);                        // ciano
                }
            }

            switch (papel)
            {
                case PapelElemento.Montante: return new Color(242, 140, 40);       // laranja
                case PapelElemento.Guia: return new Color(181, 69, 27);            // tijolo
                default: return new Color(242, 197, 0);                            // amarelo
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