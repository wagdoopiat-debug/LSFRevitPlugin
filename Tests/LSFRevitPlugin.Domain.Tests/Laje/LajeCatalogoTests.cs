using System;
using System.Collections.Generic;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeCatalogoTests
    {
        // ---------- ValorCatalogo ----------

        [Fact]
        public void ValorCatalogo_sem_fonte_lanca_excecao()
        {
            Assert.Throws<ArgumentException>(
                () => new ValorCatalogo("X", 1.0, "mm", OrigemValor.Provisorio, "  "));
        }

        [Fact]
        public void ComValor_troca_valor_origem_e_fonte_e_mantem_nome_e_unidade()
        {
            var original = new ValorCatalogo("LarguraUtilMm", 980.0, "mm", OrigemValor.Referencia, "Outro fabricante");

            ValorCatalogo novo = original.ComValor(1000.0, OrigemValor.Conferido, "Ficha Pinfer");

            Assert.Equal("LarguraUtilMm", novo.Nome);
            Assert.Equal("mm", novo.Unidade);
            Assert.Equal(1000.0, novo.Valor, 6);
            Assert.Equal(OrigemValor.Conferido, novo.Origem);
            Assert.Equal("Ficha Pinfer", novo.Fonte);
        }

        // ---------- TelhaTp40 ----------

        [Fact]
        public void Telha_passo_e_largura_util_dividida_pelas_ondas_altas()
        {
            var telha = TelhaTp40.CriarPadrao();

            Assert.Equal(245.0, telha.PassoOndaMm.Valor, 6);
            Assert.Equal(OrigemValor.Calculado, telha.PassoOndaMm.Origem);
        }

        [Fact]
        public void Telha_ondas_baixas_igual_ondas_altas()
        {
            Assert.Equal(4.0, TelhaTp40.CriarPadrao().OndasBaixasPorTelha.Valor, 6);
        }

        [Fact]
        public void Telha_enchimento_padrao_e_0_028_m3_por_m2()
        {
            Assert.Equal(0.028, TelhaTp40.CriarPadrao().EnchimentoOndasM3PorM2.Valor, 6);
        }

        [Fact]
        public void Telha_enchimento_muda_com_a_altura_da_onda()
        {
            var telha = TelhaTp40.CriarPadrao();

            telha.AlturaOndaMm = telha.AlturaOndaMm.ComValor(50.0, OrigemValor.Conferido, "Ficha Pinfer");

            Assert.Equal(0.035, telha.EnchimentoOndasM3PorM2.Valor, 6);
        }

        [Fact]
        public void Telha_peso_por_espessura()
        {
            var telha = TelhaTp40.CriarPadrao();

            Assert.Equal(3.94, telha.PesoKgM2(0.43).Valor, 6);
            Assert.Equal(4.59, telha.PesoKgM2(0.50).Valor, 6);
        }

        [Fact]
        public void Telha_peso_de_espessura_sem_cadastro_lanca_excecao()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TelhaTp40.CriarPadrao().PesoKgM2(0.65));
        }

        [Fact]
        public void Telha_padrao_e_coerente()
        {
            Assert.Empty(TelhaTp40.CriarPadrao().Validar());
        }

        [Fact]
        public void Telha_validar_detecta_geometria_incoerente()
        {
            var crista = TelhaTp40.CriarPadrao();
            crista.CristaOndaMm = crista.CristaOndaMm.ComValor(150.0, OrigemValor.Provisorio, "Teste");
            Assert.Contains(crista.Validar(), e => e.Contains("Crista"));

            var semOndas = TelhaTp40.CriarPadrao();
            semOndas.OndasAltasPorTelha = semOndas.OndasAltasPorTelha.ComValor(0.0, OrigemValor.Provisorio, "Teste");
            Assert.Contains(semOndas.Validar(), e => e.Contains("onda alta"));
        }

        // ---------- Argamassa, tela, piso seco, criterios ----------

        [Fact]
        public void Argamassa_padrao_tem_5_cm_acima_da_onda_decididos_pelo_usuario()
        {
            var argamassa = ArgamassaSpec.CriarPadrao();

            Assert.Equal(50.0, argamassa.EspessuraAcimaOndaMm.Valor, 6);
            Assert.Equal(OrigemValor.Conferido, argamassa.EspessuraAcimaOndaMm.Origem);
            Assert.Equal(2100.0, argamassa.MassaEspecificaKgM3.Valor, 6);
        }

        [Fact]
        public void Tela_peso_calculado_e_cerca_de_0_713_kg_por_m2()
        {
            Assert.Equal(0.713, TelaSoldadaSpec.CriarPadrao().PesoKgM2.Valor, 3);
        }

        [Fact]
        public void Tela_valores_decididos_pelo_usuario_sao_conferidos()
        {
            var tela = TelaSoldadaSpec.CriarPadrao();

            Assert.Equal("CA-60", tela.Aco);
            Assert.Equal(200.0, tela.MalhaMm.Valor, 6);
            Assert.Equal(3.4, tela.FioMm.Valor, 6);
            Assert.Equal(2000.0, tela.PainelLarguraMm.Valor, 6);
            Assert.Equal(3000.0, tela.PainelComprimentoMm.Valor, 6);
            Assert.Equal(OrigemValor.Conferido, tela.MalhaMm.Origem);
            Assert.Equal(OrigemValor.Conferido, tela.FioMm.Origem);
            Assert.Equal(OrigemValor.Provisorio, tela.SobreposicaoMm.Origem);
        }

        [Fact]
        public void PisoSeco_peso_calculado_e_11_7_kg_por_m2()
        {
            Assert.Equal(11.7, PisoSecoSpec.CriarPadrao().PesoKgM2.Valor, 6);
        }

        [Fact]
        public void Criterios_padrao()
        {
            var c = CriteriosLaje.CriarPadrao();

            Assert.Equal(1.5, c.SobrecargaUsoKnM2.Valor, 6);
            Assert.Equal(300.0, c.LimiteFlechaDivisor.Valor, 6);
            Assert.Equal(50.0, c.ToleranciaAlinhamentoMm.Valor, 6);
            Assert.Equal(9.80665, c.GravidadeMs2.Valor, 6);
        }

        // ---------- CatalogoLaje ----------

        [Fact]
        public void Catalogo_padrao_e_coerente()
        {
            Assert.Empty(CatalogoLaje.CriarPadrao().Validar());
        }

        [Fact]
        public void Pendencias_nao_incluem_valores_conferidos()
        {
            List<PendenciaCatalogo> pendencias = CatalogoLaje.CriarPadrao().PendenciasDoCatalogo();

            Assert.NotEmpty(pendencias);
            Assert.DoesNotContain(pendencias, p => p.Valor.Origem == OrigemValor.Conferido);
            Assert.DoesNotContain(pendencias, p => p.Valor.Nome == "EspessuraAcimaOndaMm");
            Assert.DoesNotContain(pendencias, p => p.Valor.Nome == "MalhaMm");
        }

        [Fact]
        public void Pendencias_incluem_referencia_provisorio_e_calculado()
        {
            List<PendenciaCatalogo> pendencias = CatalogoLaje.CriarPadrao().PendenciasDoCatalogo();

            Assert.Contains(pendencias, p =>
                p.Grupo == "TelhaTp40" && p.Valor.Nome == "LarguraUtilMm" && p.Valor.Origem == OrigemValor.Referencia);
            Assert.Contains(pendencias, p =>
                p.Valor.Nome == "SobreposicaoMm" && p.Valor.Origem == OrigemValor.Provisorio);
            Assert.Contains(pendencias, p =>
                p.Valor.Nome == "PassoOndaMm" && p.Valor.Origem == OrigemValor.Calculado);
        }

        [Fact]
        public void Todos_os_valores_tem_nome_unidade_e_fonte()
        {
            int total = 0;

            foreach (ValorCatalogo v in CatalogoLaje.CriarPadrao().TodosOsValores())
            {
                Assert.False(string.IsNullOrWhiteSpace(v.Nome));
                Assert.False(string.IsNullOrWhiteSpace(v.Unidade));
                Assert.False(string.IsNullOrWhiteSpace(v.Fonte));
                total++;
            }

            Assert.True(total > 0);
        }
    }
}
