using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Geometria 3D das fitas tensionadas de travamento: placas finas horizontais que correm
    /// transversalmente as trelicas (ao longo de v), na cota do banzo superior. A largura (ao
    /// longo de u) e a espessura sao GEOMETRICAS DE PARTIDA, nao normativas.
    /// </summary>
    public static class FitaBuilder
    {
        /// <summary>Largura da fita ao longo do vao (mm) -- geometrica de partida.</summary>
        public const double LarguraFitaMm = 30.0;

        /// <summary>Espessura da fita (mm) -- geometrica de partida.</summary>
        public const double EspessuraFitaMm = 1.0;

        public static Solid CriarSolidoFita(
            FitaTensionada fita, OrientacaoVigas orientacao, double zBaseMm, ElementId materialId = null)
        {
            if (fita == null || orientacao == null) return null;

            double meia = LarguraFitaMm / 2.0;
            var pontos = new List<Ponto2DMm>
            {
                orientacao.ParaMundo(fita.UMm - meia, fita.VInicioMm),
                orientacao.ParaMundo(fita.UMm + meia, fita.VInicioMm),
                orientacao.ParaMundo(fita.UMm + meia, fita.VFimMm),
                orientacao.ParaMundo(fita.UMm - meia, fita.VFimMm)
            };

            return LajeVigaBuilder.CriarSolidoPlaca(pontos, zBaseMm, EspessuraFitaMm, materialId);
        }
    }
}
