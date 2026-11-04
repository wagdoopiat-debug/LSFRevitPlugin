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

        // ---------- Conjuntos de travamento dos apoios ----------

        private static ResultadoLayoutVigas LayoutNasBordas(OrientacaoVigas o)
        {
            var layout = new ResultadoLayoutVigas();
            foreach (double v in new[] { o.VMinMm, o.VMaxMm })
            {
                layout.Vigas.Add(new VigaLaje(v, v, false,
                    new[] { new SegmentoViga(o.UMinMm, o.UMaxMm) }));
            }

            return layout;
        }

        [Fact]
        public void Travamento_cria_cinco_pecas_por_interface_em_cada_apoio()
        {
            OrientacaoVigas o = Orientacao();
            ResultadoLayoutVigas layout = LayoutNasBordas(o);
            const double larguraMm = 40.0;
            const double alturaMm = 200.0;

            var conjuntos = TravamentoLaje.CalcularConjuntos(o, layout, alturaMm, larguraMm);

            Assert.Equal(2, conjuntos.Count);
            Assert.Equal(o.UMinMm, conjuntos[0].UApoioMm, 6);
            Assert.Equal(o.UMaxMm, conjuntos[1].UApoioMm, 6);
            Assert.All(conjuntos, c =>
            {
                Assert.Equal(o.VMinMm + larguraMm, c.VInicioLivreMm, 6);
                Assert.Equal(o.VMaxMm - larguraMm, c.VFimLivreMm, 6);
                Assert.Equal(TravamentoLaje.PecasPorConjunto, c.Membros.Count);
                Assert.Equal(2, c.Membros.Count(m =>
                    m.Tipo == MembroTrelicaTipo.BanzoInferior
                    || m.Tipo == MembroTrelicaTipo.BanzoSuperior));
                Assert.Equal(2, c.Membros.Count(m => m.Tipo == MembroTrelicaTipo.Montante));
                Assert.Single(c.Membros, m => m.Tipo == MembroTrelicaTipo.Diagonal);
            });
        }

        [Fact]
        public void Travamento_nao_duplica_a_mesma_interface()
        {
            OrientacaoVigas o = Orientacao();
            var layout = new ResultadoLayoutVigas();
            foreach (double v in new[] { o.VMinMm, (o.VMinMm + o.VMaxMm) / 2.0, o.VMaxMm })
            {
                layout.Vigas.Add(new VigaLaje(v, v, false,
                    new[] { new SegmentoViga(o.UMinMm, o.UMaxMm) }));
            }

            var conjuntos = TravamentoLaje.CalcularConjuntos(o, layout, 200.0, 40.0);

            Assert.Equal(4, conjuntos.Count);
            var interfaces = conjuntos
                .Select(c => (c.UApoioMm, c.VInicioLivreMm, c.VFimLivreMm))
                .ToList();
            Assert.Equal(interfaces.Count, interfaces.Distinct().Count());
        }

        [Fact]
        public void Travamento_com_menos_de_duas_vigas_nao_gera_conjuntos()
        {
            OrientacaoVigas o = Orientacao();
            var layout = new ResultadoLayoutVigas();
            layout.Vigas.Add(new VigaLaje(o.VMinMm, o.VMinMm, false,
                new[] { new SegmentoViga(o.UMinMm, o.UMaxMm) }));

            Assert.Empty(TravamentoLaje.CalcularConjuntos(o, layout, 200.0, 40.0));
        }

        [Fact]
        public void Membros_do_conjunto_formam_quadrado_com_uma_diagonal()
        {
            List<MembroTrelica> membros = TravamentoLaje.MembrosConjunto(100.0, 500.0, 200.0);

            Assert.Equal(5, membros.Count);
            MembroTrelica diagonal = membros.Single(m => m.Tipo == MembroTrelicaTipo.Diagonal);
            Assert.Equal(0.0, diagonal.InicioUMm, 6);
            Assert.Equal(-100.0, diagonal.InicioZMm, 6);
            Assert.Equal(400.0, diagonal.FimUMm, 6);
            Assert.Equal(100.0, diagonal.FimZMm, 6);
            Assert.Equal(System.Math.Sqrt(400.0 * 400.0 + 200.0 * 200.0),
                diagonal.ComprimentoMm, 6);
        }

        [Fact]
        public void Membros_do_conjunto_recuam_os_montantes_para_dentro()
        {
            List<MembroTrelica> membros =
                TravamentoLaje.MembrosConjunto(100.0, 500.0, 200.0, 20.0);

            List<MembroTrelica> montantes = membros
                .Where(m => m.Tipo == MembroTrelicaTipo.Montante)
                .OrderBy(m => m.InicioUMm)
                .ToList();

            Assert.Equal(2, montantes.Count);
            Assert.Equal(20.0, montantes[0].InicioUMm, 6);
            Assert.Equal(380.0, montantes[1].InicioUMm, 6);
            Assert.Equal(TravamentoLaje.PecasPorConjunto, membros.Count);
        }

        [Fact]
        public void Recuo_do_montante_usa_a_dimensao_transversal_real()
        {
            Assert.Equal(20.0,
                TravamentoLaje.RecuoMontanteParaDentroMm(
                    MontagemTrelica.Encaixada, 40.0, 90.0), 6);
            Assert.Equal(45.0,
                TravamentoLaje.RecuoMontanteParaDentroMm(
                    MontagemTrelica.FaceComFace, 40.0, 90.0), 6);
            Assert.Equal(0.0,
                TravamentoLaje.RecuoMontanteParaDentroMm(
                    MontagemTrelica.Encaixada, 0.0, 90.0), 6);
        }

        [Theory]
        [InlineData(MontagemTrelica.Encaixada)]
        [InlineData(MontagemTrelica.FaceComFace)]
        public void Travamento_de_apoio_usa_sempre_montagem_encaixada(MontagemTrelica principal)
        {
            MontagemTrelica travamento = TravamentoLaje.MontagemTravamentoApoio(principal);

            Assert.Equal(MontagemTrelica.Encaixada, travamento);
            Assert.Equal(20.0,
                TravamentoLaje.RecuoMontanteParaDentroMm(travamento, 40.0, 90.0), 6);
        }

        [Fact]
        public void Conjuntos_encaixados_nao_invadem_as_trelicas_adjacentes()
        {
            OrientacaoVigas o = Orientacao();
            var layout = new ResultadoLayoutVigas();
            foreach (double v in new[] { o.VMinMm, o.VMinMm + 2000.0, o.VMaxMm })
            {
                layout.Vigas.Add(new VigaLaje(v, v, false,
                    new[] { new SegmentoViga(o.UMinMm, o.UMaxMm) }));
            }

            const double larguraMm = 90.0;
            const double alturaMm = 200.0;
            const double recuoMm = 20.0;

            var conjuntos = TravamentoLaje.CalcularConjuntos(
                o, layout, alturaMm, larguraMm, recuoMm);

            Assert.Equal(4, conjuntos.Count);
            foreach (var grupo in conjuntos.GroupBy(c => c.UApoioMm))
            {
                var ordenados = grupo.OrderBy(c => c.VInicioLivreMm).ToList();
                for (int i = 0; i + 1 < ordenados.Count; i++)
                {
                    Assert.True(ordenados[i].VFimLivreMm <= ordenados[i + 1].VInicioLivreMm + 1e-9);
                }

                foreach (var conjunto in ordenados)
                {
                    List<MembroTrelica> montantes = conjunto.Membros
                        .Where(m => m.Tipo == MembroTrelicaTipo.Montante)
                        .OrderBy(m => m.InicioUMm)
                        .ToList();

                    Assert.Equal(TravamentoLaje.PecasPorConjunto, conjunto.Membros.Count);
                    Assert.Equal(recuoMm, montantes[0].InicioUMm, 6);
                    Assert.Equal(
                        conjunto.VFimLivreMm - conjunto.VInicioLivreMm - recuoMm,
                        montantes[1].InicioUMm, 6);
                }
            }
        }

        [Fact]
        public void Conjuntos_usam_o_recuo_para_encostar_na_face_da_trelica()
        {
            OrientacaoVigas o = Orientacao();
            ResultadoLayoutVigas layout = LayoutNasBordas(o);
            const double larguraMm = 90.0;
            const double alturaMm = 200.0;
            const double recuoMm = 20.0;

            var conjuntos = TravamentoLaje.CalcularConjuntos(
                o, layout, alturaMm, larguraMm, recuoMm);

            Assert.Equal(2, conjuntos.Count);
            foreach (var conjunto in conjuntos)
            {
                List<MembroTrelica> montantes = conjunto.Membros
                    .Where(m => m.Tipo == MembroTrelicaTipo.Montante)
                    .OrderBy(m => m.InicioUMm)
                    .ToList();

                Assert.Equal(recuoMm, montantes[0].InicioUMm, 6);
                Assert.Equal(
                    conjunto.VFimLivreMm - conjunto.VInicioLivreMm - recuoMm,
                    montantes[1].InicioUMm, 6);
            }
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

        [Theory]
        [InlineData(MontagemTrelica.Encaixada, 40.0)]
        [InlineData(MontagemTrelica.FaceComFace, 90.0)]
        public void Face_inferior_da_fita_acompanha_face_externa_do_banzo(
            MontagemTrelica montagem, double dimensaoVerticalMm)
        {
            double centroMm = 1000.0;
            double alturaTrelicaMm = 250.0;
            double abaMm = 40.0;
            double almaMm = 90.0;

            double faceInferior = FitasTensionadasLaje.FaceInferiorBanzoInferiorMm(
                centroMm, alturaTrelicaMm, montagem, abaMm, almaMm);

            Assert.Equal(
                centroMm - alturaTrelicaMm / 2.0 - dimensaoVerticalMm / 2.0,
                faceInferior, 6);

            double baseFita = FitasTensionadasLaje.BaseNaFaceInferiorMm(faceInferior);

            // A fita ocupa 1 mm abaixo do banzo e seu topo coincide com a face inferior.
            Assert.Equal(faceInferior, baseFita + FitasTensionadasLaje.EspessuraMm, 6);
        }

        [Fact]
        public void Calculo_da_face_inferior_rejeita_dimensoes_invalidas()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                FitasTensionadasLaje.FaceInferiorBanzoInferiorMm(
                    1000.0, 0.0, MontagemTrelica.Encaixada, 40.0, 90.0));
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
        public void Fitas_com_passo_invalido_em_viga_convencional_gera_erro()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(Retangulo),
                TipoViga = TipoVigaLaje.PerfilSimples,
                FitasTensionadas = true,
                EspacamentoFitasTensionadasMm = 0.0
            };

            Assert.Contains(p.Validar(), e => e.Contains("fitas tensionadas"));
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
