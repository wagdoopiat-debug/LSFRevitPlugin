using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Criterios de verificacao e de layout da laje com padrao proprio (sobrecarga, flecha,
    /// tolerancia de alinhamento e gravidade). Todos provisorios ou de referencia: o usuario os valida.
    /// </summary>
    public class CriteriosLaje
    {
        public ValorCatalogo SobrecargaUsoKnM2 { get; set; } = new ValorCatalogo(
            "SobrecargaUsoKnM2", 1.5, "kN/m2", OrigemValor.Provisorio,
            "NBR 6120, uso residencial; confirmar a categoria de uso");

        public ValorCatalogo LimiteFlechaDivisor { get; set; } = new ValorCatalogo(
            "LimiteFlechaDivisor", 300.0, "un", OrigemValor.Provisorio,
            "Mesmo limite L/300 usado em Nbr14762Calculator.VerificarFlecha");

        public ValorCatalogo ToleranciaAlinhamentoMm { get; set; } = new ValorCatalogo(
            "ToleranciaAlinhamentoMm", 50.0, "mm", OrigemValor.Provisorio,
            "Padrao provisorio para alinhar a viga ao montante mais proximo");

        public ValorCatalogo GravidadeMs2 { get; set; } = new ValorCatalogo(
            "GravidadeMs2", 9.80665, "m/s2", OrigemValor.Referencia,
            "Gravidade normal padrao");

        public static CriteriosLaje CriarPadrao()
        {
            return new CriteriosLaje();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[] { SobrecargaUsoKnM2, LimiteFlechaDivisor, ToleranciaAlinhamentoMm, GravidadeMs2 };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("CriteriosLaje", Valores());
        }
    }
}
