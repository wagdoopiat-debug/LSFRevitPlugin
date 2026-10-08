using System;
using System.Globalization;
using System.Linq;

namespace LSFRevitPlugin.Domain.Models
{
    /// <summary>
    /// Identidade de um elemento de estrutura gerado: a parede a que pertence, o painel dentro
    /// dessa parede, o tipo do elemento e o resumo do perfil usado.
    ///
    /// A identidade do painel e o par (ParedeId, NumeroPainel). Nao existe um id proprio de
    /// painel: o painel e uma divisao da parede, e o numero e a sequencia dentro dela.
    ///
    /// Sem dependencia do Revit: o codec do texto legado fica aqui porque e uma regra de
    /// dominio (formato da etiqueta) e nao uma chamada de API.
    /// </summary>
    public sealed class IdentidadeEstrutural
    {
        public int ParedeId { get; set; }
        public int NumeroPainel { get; set; }
        public string Tipo { get; set; }
        public string Perfil { get; set; }

        /// <summary>
        /// Abre o texto que a geracao grava no campo Comentarios do elemento, no formato
        /// "LSF | Parede &lt;id&gt; | Painel &lt;n&gt; | &lt;tipo&gt; | &lt;perfil&gt;".
        /// O separador " | " apos o id evita confundir a parede 1234 com a 12345.
        /// </summary>
        public static string MontarTexto(int paredeId, int numeroPainel, string tipo, string perfil)
        {
            return "LSF | Parede " + paredeId.ToString(CultureInfo.InvariantCulture)
                + " | Painel " + numeroPainel.ToString(CultureInfo.InvariantCulture)
                + " | " + tipo
                + " | " + perfil;
        }

        /// <summary>
        /// Extrai a identidade do texto legado de Comentarios. Devolve false quando o texto nao
        /// e uma etiqueta do plugin ou quando o id da parede nao pode ser lido -- nesses casos o
        /// elemento nao pertence a nenhuma parede gerada por aqui.
        ///
        /// Se o numero do painel nao estiver no texto, vale 0 (o painel nao e conhecido), que e
        /// o que a leitura fazia antes do Extensible Storage existir.
        /// </summary>
        public static bool TentarExtrair(string comentario, out IdentidadeEstrutural identidade)
        {
            identidade = null;
            if (string.IsNullOrEmpty(comentario)) return false;
            if (!comentario.StartsWith(EstruturaTexto.Prefixo, StringComparison.Ordinal)) return false;

            string[] seg = comentario.Split(new[] { EstruturaTexto.Separador }, StringSplitOptions.None);

            int paredeId;
            if (seg.Length < 4
                || !int.TryParse(
                    seg[1].Replace("Parede ", ""),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out paredeId))
            {
                return false;
            }

            int numeroPainel;
            if (!int.TryParse(
                seg[2].Replace("Painel ", ""),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out numeroPainel))
            {
                numeroPainel = 0;
            }

            identidade = new IdentidadeEstrutural
            {
                ParedeId = paredeId,
                NumeroPainel = numeroPainel,
                Tipo = seg[3],
                Perfil = seg.Length > 4 ? string.Join(EstruturaTexto.Separador, seg.Skip(4)) : ""
            };
            return true;
        }
    }

    /// <summary>Partes do texto de etiqueta dos elementos de estrutura.</summary>
    public static class EstruturaTexto
    {
        public const string Prefixo = "LSF | Parede ";
        public const string Separador = " | ";
    }
}