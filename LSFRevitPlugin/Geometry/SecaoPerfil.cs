using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Utilitarios compartilhados pelas secoes em "C" (montantes) e "U" (guias).
    /// </summary>
    public static class SecaoPerfil
    {
        /// <summary>
        /// Espessura minima usada na modelagem 3D, em mm. A chapa do Drywall (0,50 mm) e menor
        /// que a tolerancia de curva curta do Revit (~0,8 mm) e faria Line.CreateBound falhar.
        /// </summary>
        public const double EspessuraMinimaModeladaMm = 1.0;

        /// <summary>
        /// Espessura da chapa a ser desenhada, em unidades internas do Revit.
        /// </summary>
        public static double EspessuraModeladaInterna(double espessuraChapaMm)
        {
            double mm = Math.Max(espessuraChapaMm, EspessuraMinimaModeladaMm);
            return RevitVersionAdapter.MilimetrosParaInterno(mm);
        }

        /// <summary>
        /// Monta um CurveLoop fechado ligando os pontos em sequencia.
        /// </summary>
        public static CurveLoop CriarLoop(IList<XYZ> pontos)
        {
            var loop = new CurveLoop();
            for (int i = 0; i < pontos.Count; i++)
            {
                loop.Append(Line.CreateBound(pontos[i], pontos[(i + 1) % pontos.Count]));
            }
            return loop;
        }
    }
}