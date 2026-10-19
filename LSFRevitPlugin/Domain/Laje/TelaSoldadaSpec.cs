using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tela soldada da laje umida: malha POP 20x20 cm, fio 3,4 mm (leve, EQ45), aco CA-60,
    /// painel 2,0 x 3,0 m. O peso e calculado pela geometria da malha.
    /// </summary>
    public class TelaSoldadaSpec
    {
        public string Aco { get; set; } = "CA-60";

        public ValorCatalogo MalhaMm { get; set; } = new ValorCatalogo(
            "MalhaMm", 200.0, "mm", OrigemValor.Conferido, "Decisao do usuario (malha POP 20x20 cm)");

        public ValorCatalogo FioMm { get; set; } = new ValorCatalogo(
            "FioMm", 3.4, "mm", OrigemValor.Conferido, "Decisao do usuario (fio 3,4 mm, leve EQ45)");

        public ValorCatalogo PainelLarguraMm { get; set; } = new ValorCatalogo(
            "PainelLarguraMm", 2000.0, "mm", OrigemValor.Conferido, "Decisao do usuario (painel 2,0 x 3,0 m)");

        public ValorCatalogo PainelComprimentoMm { get; set; } = new ValorCatalogo(
            "PainelComprimentoMm", 3000.0, "mm", OrigemValor.Conferido, "Decisao do usuario (painel 2,0 x 3,0 m)");

        public ValorCatalogo MassaEspecificaAcoKgM3 { get; set; } = new ValorCatalogo(
            "MassaEspecificaAcoKgM3", 7850.0, "kg/m3", OrigemValor.Referencia, "Valor usual para aco");

        public ValorCatalogo SobreposicaoMm { get; set; } = new ValorCatalogo(
            "SobreposicaoMm", 200.0, "mm", OrigemValor.Provisorio,
            "Padrao provisorio de uma malha; a confirmar com o usuario");

        /// <summary>
        /// Peso por m2: fios por metro = 1000 / malha, em duas direcoes; massa = comprimento de
        /// fio por m2 x area do fio x massa especifica. Exige malha maior que zero (ver Validar).
        /// </summary>
        public ValorCatalogo PesoKgM2
        {
            get
            {
                double fiosPorMetro = 1000.0 / MalhaMm.Valor;
                double comprimentoFioPorM2 = 2.0 * fiosPorMetro;
                double diametroM = FioMm.Valor / 1000.0;
                double areaFioM2 = Math.PI * diametroM * diametroM / 4.0;
                double kg = comprimentoFioPorM2 * areaFioM2 * MassaEspecificaAcoKgM3.Valor;

                return new ValorCatalogo(
                    "PesoKgM2", kg, "kg/m2", OrigemValor.Calculado,
                    "Fios nas duas direcoes: (2 x 1000 / malha) x area do fio x massa especifica do aco");
            }
        }

        public static TelaSoldadaSpec CriarPadrao()
        {
            return new TelaSoldadaSpec();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[]
            {
                MalhaMm, FioMm, PainelLarguraMm, PainelComprimentoMm,
                MassaEspecificaAcoKgM3, SobreposicaoMm, PesoKgM2
            };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("TelaSoldadaSpec", Valores());
        }

        public List<string> Validar()
        {
            var erros = new List<string>();

            if (MalhaMm.Valor <= 0) erros.Add("Malha da tela deve ser maior que zero.");
            if (FioMm.Valor <= 0) erros.Add("Fio da tela deve ser maior que zero.");
            if (PainelLarguraMm.Valor <= 0 || PainelComprimentoMm.Valor <= 0)
                erros.Add("Dimensoes do painel de tela devem ser maiores que zero.");

            return erros;
        }
    }
}
