using System;
using System.Collections.Generic;
using System.Linq;

namespace LSFRevitPlugin.Geometry
{
    /// <summary>
    /// Contorno transversal da secao de montante em "C" (alma + duas abas + labios de
    /// enrijecedor), em funcao pura. Existe para contraste com a secao de guia ("U"):
    /// o montante tem labios (12 vertices quando ha labio valido) e a guia nao (8 vertices).
    ///
    /// Coordenadas locais em unidades internas do Revit: U ao longo da direcao do elemento,
    /// V perpendicular a parede; a alma corre ao longo de V e as abas ao longo de U.
    /// </summary>
    public static class SecaoMontante
    {
        /// <summary>
        /// Contorno do "C" no plano da planta. Sem labio valido vira um "U" simples (8 vertices).
        /// <paramref name="abertoParaFrente"/> = true: abertura voltada para +U.
        /// </summary>
        public static List<(double U, double V)> ContornoC(
            double meiaLarguraInterna, double meiaProfundidadeInterna, double espessuraInterna,
            double labioInterno, bool abertoParaFrente)
        {
            double a = meiaLarguraInterna;
            double w = meiaProfundidadeInterna;
            double t = espessuraInterna;
            double labio = labioInterno;

            var local = new List<(double U, double V)>();

            bool temLabio = labio > t * 1.5 && labio < w - t;
            if (temLabio)
            {
                local.Add((-a, -w));
                local.Add((a, -w));
                local.Add((a, -w + labio));
                local.Add((a - t, -w + labio));
                local.Add((a - t, -w + t));
                local.Add((-a + t, -w + t));
                local.Add((-a + t, w - t));
                local.Add((a - t, w - t));
                local.Add((a - t, w - labio));
                local.Add((a, w - labio));
                local.Add((a, w));
                local.Add((-a, w));
            }
            else
            {
                local.Add((-a, -w));
                local.Add((a, -w));
                local.Add((a, -w + t));
                local.Add((-a + t, -w + t));
                local.Add((-a + t, w - t));
                local.Add((a, w - t));
                local.Add((a, w));
                local.Add((-a, w));
            }

            if (!abertoParaFrente)
            {
                // Espelha em U e inverte a ordem, para manter o mesmo sentido de percurso.
                local = local.Select(p => (-p.U, p.V)).Reverse().ToList();
            }

            return local;
        }
    }
}
