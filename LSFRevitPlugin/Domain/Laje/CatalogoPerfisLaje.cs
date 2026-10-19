using System.Collections.Generic;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Catalogo de perfis especifico para vigas de laje.
    /// Filtra exclusivamente perfis Steel Frame, com larguras 90/140/200 mm e bitolas 0.95/1.25 mm.
    /// Exclui Drywall e chapa 0,80 mm conforme requisito do refinamento.
    /// </summary>
    public static class CatalogoPerfisLaje
    {
        private const double AbaMmSteelFrame = 40.0;
        private const double LabioMmSteelFrame = 12.0;

        public static readonly double[] LargurasSteelFrameMm = { 90.0, 140.0, 200.0 };
        public static readonly double[] BitolasSteelFrameMm = { 0.95, 1.25 };

        public static IEnumerable<PerfilSpec> ObterTodos()
        {
            foreach (double largura in LargurasSteelFrameMm)
            {
                foreach (double bitola in BitolasSteelFrameMm)
                {
                    yield return new PerfilSpec
                    {
                        Sistema = SistemaPerfil.SteelFrame,
                        LarguraAlmaMm = largura,
                        EspessuraChapaMm = bitola
                    };
                }
            }
        }

        public static PerfilSpec CriarPadrao()
        {
            return new PerfilSpec
            {
                Sistema = SistemaPerfil.SteelFrame,
                LarguraAlmaMm = 90.0,
                EspessuraChapaMm = 0.95
            };
        }

        /// <summary>
        /// true se o perfil pode ser usado numa viga de laje: Steel Frame, alma 90/140/200 mm
        /// e chapa 0,95/1,25 mm. Exclui Drywall e chapa 0,80 mm.
        /// </summary>
        public static bool EhPerfilDeLaje(PerfilSpec perfil)
        {
            if (perfil == null) return false;
            if (perfil.Sistema != SistemaPerfil.SteelFrame) return false;
            if (!Contem(LargurasSteelFrameMm, perfil.LarguraAlmaMm)) return false;
            if (!Contem(BitolasSteelFrameMm, perfil.EspessuraChapaMm)) return false;
            return true;
        }

        private static bool Contem(double[] valores, double alvo)
        {
            foreach (double v in valores)
            {
                if (System.Math.Abs(v - alvo) < 1e-6) return true;
            }

            return false;
        }
    }
}
