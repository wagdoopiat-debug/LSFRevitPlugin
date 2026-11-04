using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Geometria 3D das fitas tensionadas de travamento: placas finas horizontais que correm
    /// transversalmente as vigas (ao longo de v), ASSENTADAS na FACE INFERIOR da estrutura de
    /// vigamento -- face inferior do banzo inferior da trelica ou face inferior da viga
    /// convencional (perfil simples, composta ou caixao). A fita fica logo ABAIXO da face
    /// informada, sem embutir na secao, sem atravessar o perfil e sem subir para o topo. A
    /// largura (ao longo de u) e a espessura sao GEOMETRICAS DE PARTIDA, nao normativas
    /// (ver <see cref="FitasTensionadasLaje"/>).
    /// </summary>
    public static class FitaBuilder
    {
        public static Solid CriarSolidoFita(
            FitaTensionada fita, OrientacaoVigas orientacao,
            double zFaceInferiorEstruturaMm, ElementId materialId = null)
        {
            if (fita == null || orientacao == null) return null;

            double meia = FitasTensionadasLaje.LarguraMm / 2.0;
            var pontos = new List<Ponto2DMm>
            {
                orientacao.ParaMundo(fita.UMm - meia, fita.VInicioMm),
                orientacao.ParaMundo(fita.UMm + meia, fita.VInicioMm),
                orientacao.ParaMundo(fita.UMm + meia, fita.VFimMm),
                orientacao.ParaMundo(fita.UMm - meia, fita.VFimMm)
            };

            double baseFitaMm = FitasTensionadasLaje.BaseNaFaceInferiorMm(zFaceInferiorEstruturaMm);
            return LajeVigaBuilder.CriarSolidoPlaca(
                pontos, baseFitaMm, FitasTensionadasLaje.EspessuraMm, materialId);
        }
    }
}
