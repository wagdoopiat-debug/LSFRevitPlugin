using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Argamassa da laje umida: espessura ACIMA da onda da telha e massa especifica.
    /// O enchimento das ondas vem de TelhaTp40.EnchimentoOndasM3PorM2.
    /// </summary>
    public class ArgamassaSpec
    {
        public ValorCatalogo EspessuraAcimaOndaMm { get; set; } = new ValorCatalogo(
            "EspessuraAcimaOndaMm", 50.0, "mm", OrigemValor.Conferido,
            "Decisao do usuario (5 cm acima da onda)");

        public ValorCatalogo MassaEspecificaKgM3 { get; set; } = new ValorCatalogo(
            "MassaEspecificaKgM3", 2100.0, "kg/m3", OrigemValor.Referencia,
            "NBR 6120 (21 kN/m3, argamassa de cimento e areia); conferir no texto da norma");

        public static ArgamassaSpec CriarPadrao()
        {
            return new ArgamassaSpec();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[] { EspessuraAcimaOndaMm, MassaEspecificaKgM3 };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("ArgamassaSpec", Valores());
        }
    }
}
