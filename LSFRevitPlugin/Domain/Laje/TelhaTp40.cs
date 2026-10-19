using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Catalogo da telha metalica trapezoidal TP40. A ficha da Pinfer nao foi localizada: os
    /// valores usam referencias de outros fabricantes (ver Origem e Fonte de cada um). Para usar
    /// a ficha oficial, troque so os valores aqui (ComValor), sem mexer em quem consome o catalogo.
    /// Os valores calculados (passo, ondas baixas, enchimento) dependem de catalogo valido:
    /// chame Validar() antes de le-los.
    /// </summary>
    public class TelhaTp40
    {
        public const double Espessura043Mm = 0.43;
        public const double Espessura050Mm = 0.50;
        public const double Espessura063Mm = 0.63;

        /// <summary>Espessuras de chapa disponiveis (ficha Pinfer: chapa 28 = 0,43 mm e chapa 26 = 0,50 mm; 0,63 mm provisorio).</summary>
        public static readonly double[] EspessurasDisponiveisMm = { Espessura043Mm, Espessura050Mm, Espessura063Mm };

        public ValorCatalogo LarguraUtilMm { get; set; } = new ValorCatalogo(
            "LarguraUtilMm", 980.0, "mm", OrigemValor.Referencia,
            "Padrao de mercado do TP40/980 (Kingspan Isoeste e outros); ficha Pinfer nao localizada");

        public ValorCatalogo LarguraTotalMm { get; set; } = new ValorCatalogo(
            "LarguraTotalMm", 1032.0, "mm", OrigemValor.Referencia,
            "Kingspan Isoeste (TP40)");

        public ValorCatalogo AlturaOndaMm { get; set; } = new ValorCatalogo(
            "AlturaOndaMm", 40.0, "mm", OrigemValor.Referencia,
            "Nome do perfil (TP40); projeto publico com altura 40 mm");

        public ValorCatalogo OndasAltasPorTelha { get; set; } = new ValorCatalogo(
            "OndasAltasPorTelha", 4.0, "un", OrigemValor.Provisorio,
            "Padrao geometrico usual do TP40/980 (4 ondas); nao encontrado em ficha");

        public ValorCatalogo CristaOndaMm { get; set; } = new ValorCatalogo(
            "CristaOndaMm", 35.0, "mm", OrigemValor.Provisorio,
            "Aproximacao do desenho de perfil TP40 de outro fabricante; usado so em geometria 3D e enchimento");

        public ValorCatalogo BaseOndaAltaMm { get; set; } = new ValorCatalogo(
            "BaseOndaAltaMm", 112.0, "mm", OrigemValor.Provisorio,
            "Aproximacao do desenho de perfil TP40 de outro fabricante; usado so em geometria 3D e enchimento");

        public ValorCatalogo PesoKgM2Chapa043 { get; set; } = new ValorCatalogo(
            "PesoKgM2Chapa043", 3.94, "kg/m2", OrigemValor.Referencia,
            "Hidroaco RT 40/980; a Pinfer informa 3,87 kg/m para 0,43 mm (compativel)");

        public ValorCatalogo PesoKgM2Chapa050 { get; set; } = new ValorCatalogo(
            "PesoKgM2Chapa050", 4.59, "kg/m2", OrigemValor.Referencia,
            "Hidroaco RT 40/980; peso da Pinfer para 0,50 mm nao localizado");

        public ValorCatalogo PesoKgM2Chapa063 { get; set; } = new ValorCatalogo(
            "PesoKgM2Chapa063", 5.80, "kg/m2", OrigemValor.Provisorio,
            "Peso provisorio para chapa 0,63 mm (conferir com ficha Pinfer)");

        public ValorCatalogo ComprimentoMaximoMm { get; set; } = new ValorCatalogo(
            "ComprimentoMaximoMm", 12000.0, "mm", OrigemValor.Referencia,
            "Catalogo Isoeste (conferir; Pinfer nao localizado)");

        public ValorCatalogo RecobrimentoLateralOndas { get; set; } = new ValorCatalogo(
            "RecobrimentoLateralOndas", 2.0, "ondas", OrigemValor.Provisorio,
            "Decisao do usuario (duas ondas); na v1 so define a costura de parafusos e nao reduz a largura util");

        public ValorCatalogo EspacamentoParafusoCosturaMm { get; set; } = new ValorCatalogo(
            "EspacamentoParafusoCosturaMm", 300.0, "mm", OrigemValor.Provisorio,
            "Padrao provisorio; a confirmar com o usuario");

        /// <summary>Passo da onda = largura util / numero de ondas altas.</summary>
        public ValorCatalogo PassoOndaMm => new ValorCatalogo(
            "PassoOndaMm", LarguraUtilMm.Valor / OndasAltasPorTelha.Valor, "mm", OrigemValor.Calculado,
            "LarguraUtilMm / OndasAltasPorTelha");

        /// <summary>Uma onda baixa por passo, igual ao numero de ondas altas.</summary>
        public ValorCatalogo OndasBaixasPorTelha => new ValorCatalogo(
            "OndasBaixasPorTelha", Math.Round(OndasAltasPorTelha.Valor), "un", OrigemValor.Calculado,
            "Igual ao numero de ondas altas");

        /// <summary>
        /// Argamassa que preenche o espaco entre as ondas altas, em m3 por m2 de laje: area
        /// do retangulo (passo x altura) menos a area do trapezio da onda alta (base e crista),
        /// dividida pelo passo e convertida de mm para m.
        /// </summary>
        public ValorCatalogo EnchimentoOndasM3PorM2
        {
            get
            {
                double passo = LarguraUtilMm.Valor / OndasAltasPorTelha.Valor;
                double altura = AlturaOndaMm.Valor;
                double areaOndaAlta = (BaseOndaAltaMm.Valor + CristaOndaMm.Valor) / 2.0 * altura;
                double espessuraEquivalenteMm = (passo * altura - areaOndaAlta) / passo;

                return new ValorCatalogo(
                    "EnchimentoOndasM3PorM2", espessuraEquivalenteMm / 1000.0, "m3/m2", OrigemValor.Calculado,
                    "((passo x altura) - area do trapezio da onda alta) / passo");
            }
        }

        public static TelhaTp40 CriarPadrao()
        {
            return new TelhaTp40();
        }

        /// <summary>true se a espessura (mm) esta entre as disponiveis.</summary>
        public static bool EspessuraDisponivel(double espessuraMm)
        {
            foreach (double e in EspessurasDisponiveisMm)
            {
                if (Math.Abs(espessuraMm - e) < 1e-6) return true;
            }

            return false;
        }

        /// <summary>Peso da telha (kg/m2) para a espessura informada; lanca excecao se nao houver cadastro.</summary>
        public ValorCatalogo PesoKgM2(double espessuraMm)
        {
            if (Math.Abs(espessuraMm - Espessura043Mm) < 1e-6) return PesoKgM2Chapa043;
            if (Math.Abs(espessuraMm - Espessura050Mm) < 1e-6) return PesoKgM2Chapa050;
            if (Math.Abs(espessuraMm - Espessura063Mm) < 1e-6) return PesoKgM2Chapa063;

            throw new ArgumentOutOfRangeException(
                nameof(espessuraMm), "Espessura de telha sem peso cadastrado (disponiveis: 0,43, 0,50 e 0,63 mm).");
        }

        /// <summary>Todos os valores, inclusive os calculados. Exige catalogo valido (Validar() sem erros).</summary>
        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[]
            {
                LarguraUtilMm, LarguraTotalMm, AlturaOndaMm, OndasAltasPorTelha, CristaOndaMm, BaseOndaAltaMm,
                PesoKgM2Chapa043, PesoKgM2Chapa050, PesoKgM2Chapa063, ComprimentoMaximoMm, RecobrimentoLateralOndas,
                EspacamentoParafusoCosturaMm, PassoOndaMm, OndasBaixasPorTelha, EnchimentoOndasM3PorM2
            };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("TelhaTp40", Valores());
        }

        /// <summary>Erros de coerencia da geometria da telha (lista vazia = coerente).</summary>
        public List<string> Validar()
        {
            var erros = new List<string>();

            if (LarguraUtilMm.Valor <= 0) erros.Add("Largura util da telha deve ser maior que zero.");
            if (AlturaOndaMm.Valor <= 0) erros.Add("Altura da onda deve ser maior que zero.");

            if (OndasAltasPorTelha.Valor < 1)
            {
                erros.Add("Telha precisa de pelo menos 1 onda alta.");
                return erros;
            }

            double passo = LarguraUtilMm.Valor / OndasAltasPorTelha.Valor;

            if (CristaOndaMm.Valor <= 0 || CristaOndaMm.Valor > BaseOndaAltaMm.Valor)
                erros.Add("Crista da onda deve ser positiva e nao maior que a base da onda alta.");

            if (BaseOndaAltaMm.Valor > passo)
                erros.Add("Base da onda alta nao pode ser maior que o passo da onda.");

            return erros;
        }
    }
}
