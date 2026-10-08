using System;
using System.Globalization;
using System.Linq;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Identidade de uma placa gerada: a parede a que pertence, o painel dentro
    /// dessa parede, a face, o tipo da placa e a orientacao.
    ///
    /// A identidade do painel e o par (ParedeId, NumeroPainel). Nao existe um id proprio de
    /// painel: o painel e uma divisao da parede, e o numero e a sequencia dentro dela.
    ///
    /// Sem dependencia do Revit: o codec do texto legado fica aqui porque e uma regra de
    /// dominio (formato da etiqueta) e nao uma chamada de API.
    /// Formato legado: LSF-PLACA | Parede <id> | Painel <n> | Face <1|2> | <Tipo> | <Codigo> | <L>x<A> mm | ...
    /// </summary>
    public sealed class IdentidadePlaca
    {
        public int ParedeId { get; set; }
        public int NumeroPainel { get; set; }
        public int Face { get; set; }
        public string TipoPlaca { get; set; }
        public string Orientacao { get; set; }
    }

    /// <summary>
    /// Utilitario para codec do formato legado de placas.
    /// </summary>
    public static class IdentidadePlacaCodec
    {
        public const string PrefixoEtiquetaPlaca = "LSF-PLACA | ";
        private const string Separador = " | ";

        /// <summary>
        /// Tenta extrair a identidade minima de uma placa a partir do texto de Comentarios legado.
        /// Devolve false quando o texto nao e uma etiqueta de placa do plugin.
        /// </summary>
        public static bool TentarExtrair(string comentario, out IdentidadePlaca identidade)
        {
            identidade = null;
            if (string.IsNullOrEmpty(comentario)) return false;
            if (!comentario.StartsWith(PrefixoEtiquetaPlaca, StringComparison.Ordinal)) return false;

            string[] seg = comentario.Split(new[] { Separador }, StringSplitOptions.None);
            if (seg.Length < 7) return false;

            int paredeId;
            int numeroPainel;
            int face;

            if (!int.TryParse(
                    seg[1].Replace("Parede ", ""),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out paredeId))
            {
                return false;
            }

            if (!int.TryParse(
                    seg[2].Replace("Painel ", ""),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out numeroPainel))
            {
                return false;
            }

            if (!int.TryParse(
                    seg[3].Replace("Face ", ""),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out face))
            {
                return false;
            }

            string tipoPlaca = seg[4] ?? string.Empty;
            string orientacao = string.Empty;

            for (int i = 6; i < seg.Length; i++)
            {
                string s = seg[i];
                if (s.StartsWith("orientacao ", StringComparison.Ordinal))
                {
                    orientacao = s.Substring("orientacao ".Length);
                    break;
                }
            }

            identidade = new IdentidadePlaca
            {
                ParedeId = paredeId,
                NumeroPainel = numeroPainel,
                Face = face,
                TipoPlaca = tipoPlaca,
                Orientacao = orientacao
            };

            return true;
        }
    }
}
