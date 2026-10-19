using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes do travamento da laje (Lote C): membro de borda (trelica de travamento x guia "U"),
    /// linhas de apoio, fitas tensionadas (disposicao geometrica de partida, NAO normativa)
    /// e sua validacao/persistencia.
    /// </summary>
    public class LajeTravamentoTests
    {
        private static readonly Ponto2DMm[] Retangulo =
        {
            new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
            new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
        };

        private static OrientacaoVigas Orientacao()
        {
            return OrientacaoVigas.Calcular(new ContornoLaje(Retangulo), null);
        }

        private static ResultadoLayoutVigas LayoutComVigas(int quantidade)
        {
            var layout = new ResultadoLayoutVigas();
            for (int i = 0; i < quantidade; i++)
            {
                layout.Vigas.Add(new VigaLaje(i * 400.0, i * 400.0, false,
                    new[] { new SegmentoViga(0.0, 6000.0) }));
            }

            return layout;
        }

        // ---------- Membro de borda ----------

        [Fact]
        public void Vigas_de_trelica_usam_trelica_de_travamento_na_borda()
        {
            Assert.Equal(TipoMembroBorda.TrelicaTravamento,
                TravamentoLaje.MembroDeBorda(TipoVigaLaje.Trelica));
        }

        [Theory]
        [InlineData(TipoVigaLaje.PerfilSimples)]
        [InlineData(TipoVigaLaje.Caixao)]
        [InlineData(TipoVigaLaje.BackToBack)]
        public void Vigas_convencionais_usam_guia_u_na_borda(TipoVigaLaje tipo)
        {
            Assert.Equal(TipoMembroBorda.GuiaBorda, TravamentoLaje.MembroDeBorda(tipo));
        }

        // ---------- Linhas de apoio ----------

        [Fact]
        public void Apoios_sao_as_duas_bordas_do_vao()
        {
            OrientacaoVigas o = Orientacao();

            List<double> apoios = TravamentoLaje.Apoios(o);

            Assert.Equal(2, apoios.Count);
            Assert.Equal(o.UMinMm, apoios[0], 6);
            Assert.Equal(o.UMaxMm, apoios[1], 6);
        }

        [Fact]
        public void Sem_orientacao_nao_ha_apoios()
        {
            Assert.Empty(TravamentoLaje.Apoios(null));
        }

        // ---------- Fitas tensionadas ----------

        [Fact]
        public void Fitas_seguem_o_passo_no_interior_do_vao()
        {
            OrientacaoVigas o = Orientacao();

            List<FitaTensionada> fitas = FitasTensionadasLaje.Calcular(o, LayoutComVigas(2), 1500.0);

            Assert.Equal(2, fitas.Count);
            Assert.Equal(o.UMinMm + 1500.0, fitas[0].UMm, 6);
            Assert.Equal(o.UMinMm + 3000.0, fitas[1].UMm, 6);
            Assert.All(fitas, f =>
            {
                Assert.Equal(o.VMinMm, f.VInicioMm, 6);
                Assert.Equal(o.VMaxMm, f.VFimMm, 6);
            });
        }

        [Fact]
        public void Fitas_nao_ficam_sobre_as_bordas()
        {
            OrientacaoVigas o = Orientacao();

            List<FitaTensionada> fitas = FitasTensionadasLaje.Calcular(o, LayoutComVigas(2), 1500.0);

            Assert.DoesNotContain(fitas, f => System.Math.Abs(f.UMm - o.UMinMm) < 1e-6);
            Assert.DoesNotContain(fitas, f => System.Math.Abs(f.UMm - o.UMaxMm) < 1e-6);
        }

        [Fact]
        public void Uma_unica_viga_nao_recebe_fita()
        {
            OrientacaoVigas o = Orientacao();

            Assert.Empty(FitasTensionadasLaje.Calcular(o, LayoutComVigas(1), 1500.0));
        }

        [Fact]
        public void Passo_invalido_usa_o_padrao()
        {
            OrientacaoVigas o = Orientacao();

            List<FitaTensionada> fitas = FitasTensionadasLaje.Calcular(o, LayoutComVigas(2), 0.0);

            Assert.Equal(2, fitas.Count);
            Assert.Equal(o.UMinMm + FitasTensionadasLaje.PassoPadraoMm, fitas[0].UMm, 6);
        }

        [Fact]
        public void Sem_orientacao_ou_layout_nao_ha_fitas()
        {
            Assert.Empty(FitasTensionadasLaje.Calcular(null, LayoutComVigas(2), 1500.0));
            Assert.Empty(FitasTensionadasLaje.Calcular(Orientacao(), null, 1500.0));
        }

        [Fact]
        public void Fita_com_fim_menor_que_inicio_e_rejeitada()
        {
            Assert.Throws<System.ArgumentException>(() => new FitaTensionada(0.0, 4000.0, 1000.0));
        }

        // ---------- Validacao ----------

        [Fact]
        public void Fitas_com_passo_invalido_em_trelica_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(Retangulo),
                TipoViga = TipoVigaLaje.Trelica,
                FitasTensionadas = true,
                EspacamentoFitasTensionadasMm = 0.0
            };

            Assert.Contains(p.Validar(), e => e.Contains("fitas tensionadas"));
        }

        [Fact]
        public void Fitas_com_passo_invalido_em_viga_convencional_nao_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(Retangulo),
                TipoViga = TipoVigaLaje.PerfilSimples,
                FitasTensionadas = true,
                EspacamentoFitasTensionadasMm = 0.0
            };

            Assert.DoesNotContain(p.Validar(), e => e.Contains("fitas tensionadas"));
        }

        // ---------- Persistencia ----------

        [Fact]
        public void Persistencia_preserva_fitas_tensionadas()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(Retangulo),
                TipoViga = TipoVigaLaje.Trelica,
                FitasTensionadas = true,
                EspacamentoFitasTensionadasMm = 1200.0
            };

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(
                ConfiguracaoLajeTexto.Serializar(p), out lido));

            Assert.True(lido.FitasTensionadas);
            Assert.Equal(1200.0, lido.EspacamentoFitasTensionadasMm, 6);
        }

        [Fact]
        public void Texto_antigo_sem_fitas_usa_padrao()
        {
            string texto = "versao=1\ntipo=Seca\ncontorno=0,0;6000,0;6000,4000;0,4000";

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));

            Assert.False(lido.FitasTensionadas);
            Assert.Equal(FitasTensionadasLaje.PassoPadraoMm, lido.EspacamentoFitasTensionadasMm, 6);
        }
    }
}
