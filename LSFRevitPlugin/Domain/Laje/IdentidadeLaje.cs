using System;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Papel de um elemento gerado da laje. E a peca da identidade que diz o que o elemento e
    /// (viga ou guia de borda), independente de cor. A T09 (identidade completa) acrescenta os
    /// papeis restantes (telha, argamassa, piso, parafusos) sem mudar o formato.
    /// </summary>
    public enum PapelElementoLaje
    {
        Viga,
        GuiaBorda,
        CamadaOsb,
        CamadaCimenticia,
        Argamassa,
        Telha,
        Parafuso,
        Enchimento,
        Gusset,

        /// <summary>Trelica de travamento de borda (travamento das trelicas nos dois apoios).</summary>
        Travamento,

        /// <summary>Fita tensionada de travamento entre trelicas.</summary>
        Fita
    }

    /// <summary>
    /// Identidade de um elemento gerado da laje: a laje a que pertence, o tipo, o papel e um
    /// numero sequencial dentro do papel. E o par (LajeId, Papel, Numero) que permite regerar
    /// uma laje sem duplicar elementos. Sem dependencia do Revit: so o codec do texto.
    /// </summary>
    public sealed class IdentidadeLaje
    {
        public int LajeId { get; set; }

        public TipoLaje Tipo { get; set; }

        public PapelElementoLaje Papel { get; set; }

        public int Numero { get; set; }
    }

    /// <summary>Partes do texto de etiqueta dos elementos da laje.</summary>
    public static class LajeTexto
    {
        public const string Prefixo = "LSF-LAJE | ";
        public const string Separador = " | ";
    }

    /// <summary>
    /// Codec do texto de identidade da laje, gravado no campo Comentarios como canal legado /
    /// de leitura humana. O formato e
    /// "LSF-LAJE | Laje &lt;id&gt; | &lt;tipo&gt; | &lt;papel&gt; | &lt;numero&gt;".
    /// O prefixo "LSF-LAJE" e distinto do prefixo da estrutura ("LSF | Parede "), para que uma
    /// leitura nunca confunda um elemento da laje com um elemento de parede.
    /// </summary>
    public static class IdentidadeLajeCodec
    {
        public static string MontarTexto(int lajeId, TipoLaje tipo, PapelElementoLaje papel, int numero)
        {
            return LajeTexto.Prefixo
                + "Laje " + lajeId.ToString(CultureInfo.InvariantCulture)
                + LajeTexto.Separador + tipo
                + LajeTexto.Separador + papel
                + LajeTexto.Separador + numero.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Extrai a identidade do texto de Comentarios. Devolve false quando o texto nao e uma
        /// etiqueta da laje ou quando o id, o tipo ou o papel nao podem ser lidos.
        /// </summary>
        public static bool TentarExtrair(string comentario, out IdentidadeLaje identidade)
        {
            identidade = null;
            if (string.IsNullOrEmpty(comentario)) return false;
            if (!comentario.StartsWith(LajeTexto.Prefixo, StringComparison.Ordinal)) return false;

            string[] seg = comentario.Split(new[] { LajeTexto.Separador }, StringSplitOptions.None);
            if (seg.Length < 5) return false;

            int lajeId;
            if (!int.TryParse(
                seg[1].Replace("Laje ", ""),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out lajeId))
            {
                return false;
            }

            TipoLaje tipo;
            if (!Enum.TryParse(seg[2], out tipo)) return false;

            PapelElementoLaje papel;
            if (!Enum.TryParse(seg[3], out papel)) return false;

            int numero;
            if (!int.TryParse(seg[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out numero))
            {
                numero = 0;
            }

            identidade = new IdentidadeLaje
            {
                LajeId = lajeId,
                Tipo = tipo,
                Papel = papel,
                Numero = numero
            };
            return true;
        }
    }
}
