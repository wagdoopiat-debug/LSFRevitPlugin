using System.Globalization;

namespace LSFRevitPlugin.Domain.Models
{
    public enum SistemaPerfil
    {
        Drywall,
        SteelFrame
    }

    /// <summary>
    /// Especificacao do perfil escolhido para a estrutura: sistema (Drywall/Steel Frame),
    /// largura nominal da alma e bitola (espessura da chapa). Aba e labio sao fixos por
    /// sistema, conforme padrao de mercado (NBR 15253 para Steel Frame).
    /// </summary>
    public class PerfilSpec
    {
        public SistemaPerfil Sistema { get; set; }

        /// <summary>Largura nominal da alma, em mm: 48/70/90 (Drywall) ou 90/140/200 (Steel Frame).</summary>
        public double LarguraAlmaMm { get; set; }

        /// <summary>Espessura da chapa (bitola), em mm: 0,50 fixo (Drywall) ou 0,80/0,95/1,25 (Steel Frame).</summary>
        public double EspessuraChapaMm { get; set; }

        /// <summary>Largura da aba, em mm -- fixa por sistema.</summary>
        public double AbaMm => Sistema == SistemaPerfil.Drywall ? 35.0 : 40.0;

        /// <summary>Largura do labio (enrijecedor), em mm -- fixa por sistema.</summary>
        public double LabioMm => Sistema == SistemaPerfil.Drywall ? 7.0 : 12.0;

        public static readonly double[] LargurasDrywallMm = { 48, 70, 90 };
        public static readonly double[] LargurasSteelFrameMm = { 90, 140, 200 };
        public static readonly double[] BitolasSteelFrameMm = { 0.80, 0.95, 1.25 };
        public const double BitolaDrywallMm = 0.50;

        public static PerfilSpec CriarPadrao()
        {
            return new PerfilSpec
            {
                Sistema = SistemaPerfil.SteelFrame,
                LarguraAlmaMm = 90,
                EspessuraChapaMm = 0.95
            };
        }

        public string DescricaoResumo()
        {
            string sistemaTexto = Sistema == SistemaPerfil.Drywall ? "Drywall" : "Steel Frame";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1:F0}mm, chapa {2:F2}mm (aba {3:F0}mm, labio {4:F0}mm)",
                sistemaTexto, LarguraAlmaMm, EspessuraChapaMm, AbaMm, LabioMm);
        }
    }
}