using System;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeLayoutVigasTests
    {
        private static Ponto2DMm P(double x, double y)
        {
            return new Ponto2DMm(x, y);
        }

        private static Ponto2DMm[] Retangulo(double larguraMm, double alturaMm)
        {
            return new[] { P(0, 0), P(larguraMm, 0), P(larguraMm, alturaMm), P(0, alturaMm) };
        }

        private static Ponto2DMm[] FormaEmL()
        {
            return new[] { P(0, 0), P(6000, 0), P(6000, 2000), P(2000, 2000), P(2000, 4000), P(0, 4000) };
        }

        private static Ponto2DMm[] FormaEmU()
        {
            return new[]
            {
                P(0, 0), P(6000, 0), P(6000, 4000), P(4000, 4000),
                P(4000, 1000), P(2000, 1000), P(2000, 4000), P(0, 4000)
            };
        }

        private static ResultadoLayoutVigas Calcular(
            Ponto2DMm[] pontos, double? direcao, double espacamentoMm,
            double[]? apoios = null, double tolerancia = 0.0, double larguraViga = 0.0)
        {
            var contorno = new ContornoLaje(pontos);
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, direcao);
            return LayoutVigas.Calcular(contorno, o, espacamentoMm, apoios, tolerancia, larguraViga);
        }

        // ---------- Testes originais do Lote 2 (mantidos) ----------

        [Fact]
        public void Retangulo_4x6_com_400_gera_16_vigas_com_vao_de_4_m()
        {
            ResultadoLayoutVigas r = Calcular(Retangulo(6000, 4000), null, 400.0);

            Assert.True(r.EhValido);
            Assert.Equal(16, r.Vigas.Count);
            Assert.Equal(6000.0, r.Vigas[15].PosicaoTransversalMm - r.Vigas[0].PosicaoTransversalMm, 6);
            Assert.Equal(400.0, r.MaiorVaoEntreVigasMm, 6);
            Assert.Empty(r.Avisos);

            for (int i = 0; i + 1 < r.Vigas.Count; i++)
            {
                Assert.Equal(400.0, r.Vigas[i + 1].PosicaoTransversalMm - r.Vigas[i].PosicaoTransversalMm, 6);
            }

            foreach (VigaLaje viga in r.Vigas)
            {
                Assert.Single(viga.Segmentos);
                Assert.Equal(4000.0, viga.Segmentos[0].ComprimentoMm, 6);
            }
        }

        [Fact]
        public void Retangulo_4x6_com_600_gera_11_vigas()
        {
            ResultadoLayoutVigas r = Calcular(Retangulo(6000, 4000), null, 600.0);

            Assert.Equal(11, r.Vigas.Count);
            Assert.Equal(600.0, r.MaiorVaoEntreVigasMm, 6);
        }

        [Fact]
        public void Largura_nao_multipla_do_espacamento_fecha_com_vao_menor_na_borda()
        {
            ResultadoLayoutVigas r = Calcular(Retangulo(6000, 4000), null, 700.0);

            Assert.Equal(10, r.Vigas.Count);
            double ultimoVao = r.Vigas[9].PosicaoTransversalMm - r.Vigas[8].PosicaoTransversalMm;
            Assert.Equal(400.0, ultimoVao, 6);
            Assert.Equal(700.0, r.MaiorVaoEntreVigasMm, 6);
            Assert.Empty(r.Avisos);
        }

        [Fact]
        public void Primeira_e_ultima_viga_ficam_nas_bordas_do_contorno()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0);

            Assert.Equal(o.VMinMm, r.Vigas[0].PosicaoTransversalMm, 6);
            Assert.Equal(o.VMaxMm, r.Vigas[r.Vigas.Count - 1].PosicaoTransversalMm, 6);
        }

        [Fact]
        public void Retangulo_tem_duas_guias_de_borda_nas_pontas_das_vigas()
        {
            ResultadoLayoutVigas r = Calcular(Retangulo(6000, 4000), null, 400.0);

            Assert.Equal(2, r.GuiasBorda.Count);
            Assert.All(r.GuiasBorda, g => Assert.Equal(6000.0, g.ComprimentoMm, 6));
            Assert.Equal(4000.0, Math.Abs(r.GuiasBorda[0].UMm - r.GuiasBorda[1].UMm), 6);
        }

        [Fact]
        public void Alinhamento_desloca_uma_viga_para_o_apoio_e_avisa_do_vao_excedido()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);
            double apoio = o.VMinMm + 1230.0;

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { apoio }, 50.0);

            Assert.Equal(16, r.Vigas.Count);
            Assert.Equal(apoio, r.Vigas[3].PosicaoTransversalMm, 6);
            Assert.Equal(o.VMinMm + 1200.0, r.Vigas[3].PosicaoGradeMm, 6);
            Assert.True(r.Vigas[3].AlinhadaAoApoio);
            Assert.Equal(1, r.Vigas.Count(v => v.AlinhadaAoApoio));
            Assert.Equal(430.0, r.MaiorVaoEntreVigasMm, 6);
            Assert.Contains(r.Avisos, a => a.Contains("excede o espacamento"));
        }

        [Fact]
        public void Apoio_fora_da_tolerancia_nao_alinha_nenhuma_viga()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 1280.0 }, 50.0);

            Assert.DoesNotContain(r.Vigas, v => v.AlinhadaAoApoio);
            Assert.Contains(r.Avisos, a => a.Contains("sem montante/apoio correspondente"));
        }

        [Fact]
        public void Duas_vigas_no_mesmo_apoio_viram_uma_com_aviso()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 1000.0 }, 250.0);

            Assert.Equal(15, r.Vigas.Count);
            Assert.Contains(r.Avisos, a => a.Contains("mesmo apoio"));
        }

        [Fact]
        public void Sem_apoios_ou_com_lista_vazia_usa_so_a_grade()
        {
            ResultadoLayoutVigas nulo = Calcular(Retangulo(6000, 4000), null, 400.0, null, 50.0);
            ResultadoLayoutVigas vazio = Calcular(Retangulo(6000, 4000), null, 400.0, new double[0], 50.0);

            Assert.DoesNotContain(nulo.Vigas, v => v.AlinhadaAoApoio);
            Assert.DoesNotContain(vazio.Vigas, v => v.AlinhadaAoApoio);
            Assert.Equal(16, nulo.Vigas.Count);
            Assert.Equal(16, vazio.Vigas.Count);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-400.0)]
        public void Espacamento_invalido_retorna_erro_sem_vigas(double espacamento)
        {
            ResultadoLayoutVigas r = Calcular(Retangulo(6000, 4000), null, espacamento);

            Assert.False(r.EhValido);
            Assert.Contains(r.Erros, e => e.Contains("Espacamento"));
            Assert.Empty(r.Vigas);
        }

        [Fact]
        public void Contorno_invalido_retorna_erros_sem_vigas()
        {
            var valido = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(valido, null);
            var gravata = new ContornoLaje(new[] { P(0, 0), P(1000, 1000), P(1000, 0), P(0, 1000) });

            ResultadoLayoutVigas r = LayoutVigas.Calcular(gravata, o, 400.0);

            Assert.False(r.EhValido);
            Assert.Contains(r.Erros, e => e.Contains("autointerseccao"));
            Assert.Empty(r.Vigas);
        }

        [Fact]
        public void Contorno_nao_ortogonal_retorna_erro_na_v1()
        {
            var triangulo = new[] { P(0, 0), P(4000, 0), P(0, 3000) };

            ResultadoLayoutVigas r = Calcular(triangulo, null, 400.0);

            Assert.False(r.EhValido);
            Assert.Contains(r.Erros, e => e.Contains("nao ortogonal"));
            Assert.Empty(r.Vigas);
        }

        [Fact]
        public void Contorno_em_U_gera_dois_segmentos_nas_vigas_centrais()
        {
            ResultadoLayoutVigas r = Calcular(FormaEmU(), 0.0, 1000.0);

            Assert.True(r.EhValido);
            Assert.Equal(new[] { 1, 1, 2, 2, 2 }, r.Vigas.Select(v => v.Segmentos.Count).ToArray());
            Assert.Equal(4, r.GuiasBorda.Count);
            Assert.Empty(r.Avisos);
        }

        [Fact]
        public void Contorno_em_L_tem_vigas_curtas_e_longas_conforme_o_degrau()
        {
            ResultadoLayoutVigas r = Calcular(FormaEmL(), null, 400.0);

            Assert.Equal(16, r.Vigas.Count);
            Assert.Equal(10, r.Vigas.Count(v => Math.Abs(v.ComprimentoTotalMm - 2000.0) < 1e-3));
            Assert.Equal(6, r.Vigas.Count(v => Math.Abs(v.ComprimentoTotalMm - 4000.0) < 1e-3));
            Assert.Empty(r.Avisos);
        }

        [Fact]
        public void Aresta_paralela_sem_viga_de_borda_gera_aviso()
        {
            ResultadoLayoutVigas semAviso = Calcular(FormaEmL(), null, 400.0);
            ResultadoLayoutVigas comAviso = Calcular(FormaEmL(), null, 600.0);

            Assert.DoesNotContain(semAviso.Avisos, a => a.Contains("sem viga de borda"));
            Assert.Contains(comAviso.Avisos, a => a.Contains("sem viga de borda"));
        }

        [Fact]
        public void Ultimo_vao_menor_que_a_largura_da_viga_avisa_quando_a_largura_e_informada()
        {
            ResultadoLayoutVigas sem = Calcular(Retangulo(6000, 4010), 0.0, 400.0);
            ResultadoLayoutVigas com = Calcular(Retangulo(6000, 4010), 0.0, 400.0, null, 0.0, 40.0);

            Assert.Empty(sem.Avisos);
            Assert.Contains(com.Avisos, a => a.Contains("sobrepoem"));
        }

        [Fact]
        public void Argumentos_nulos_lancam_excecao()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            Assert.Throws<ArgumentNullException>(() => LayoutVigas.Calcular(null, o, 400.0));
            Assert.Throws<ArgumentNullException>(() => LayoutVigas.Calcular(contorno, null, 400.0));
        }

        // ---------- Regressao da correcao dirigida ----------

        [Fact]
        public void Apoio_proximo_da_primeira_borda_nao_move_a_borda()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 30.0 }, 50.0);

            Assert.Equal(16, r.Vigas.Count);
            Assert.Equal(o.VMinMm, r.Vigas[0].PosicaoTransversalMm, 6);
            Assert.False(r.Vigas[0].AlinhadaAoApoio);
            Assert.DoesNotContain(r.Vigas, v => v.AlinhadaAoApoio);
            Assert.Contains(r.Avisos, a => a.Contains("sem montante/apoio correspondente"));
        }

        [Fact]
        public void Apoio_proximo_da_ultima_borda_nao_move_a_borda()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMaxMm - 30.0 }, 50.0);

            Assert.Equal(16, r.Vigas.Count);
            Assert.Equal(o.VMaxMm, r.Vigas[15].PosicaoTransversalMm, 6);
            Assert.False(r.Vigas[15].AlinhadaAoApoio);
            Assert.DoesNotContain(r.Vigas, v => v.AlinhadaAoApoio);
            Assert.Contains(r.Avisos, a => a.Contains("sem montante/apoio correspondente"));
        }

        [Fact]
        public void Colisao_com_a_primeira_borda_preserva_a_borda_e_remove_a_viga_interna()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 0.5 }, 450.0);

            Assert.Equal(15, r.Vigas.Count);
            Assert.Equal(o.VMinMm, r.Vigas[0].PosicaoTransversalMm, 6);
            Assert.False(r.Vigas[0].AlinhadaAoApoio);
            Assert.Equal(o.VMinMm + 800.0, r.Vigas[1].PosicaoTransversalMm, 6);
            Assert.Equal(800.0, r.MaiorVaoEntreVigasMm, 6);
            Assert.Contains(r.Avisos, a => a.Contains("viga de borda"));
            Assert.Contains(r.Avisos, a => a.Contains("excede o espacamento"));
        }

        [Fact]
        public void Colisao_com_a_ultima_borda_preserva_a_borda_e_remove_a_viga_interna()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMaxMm - 0.5 }, 450.0);

            Assert.Equal(15, r.Vigas.Count);
            Assert.Equal(o.VMaxMm, r.Vigas[14].PosicaoTransversalMm, 6);
            Assert.False(r.Vigas[14].AlinhadaAoApoio);
            Assert.Equal(o.VMaxMm - 800.0, r.Vigas[13].PosicaoTransversalMm, 6);
            Assert.Contains(r.Avisos, a => a.Contains("viga de borda"));
            Assert.Contains(r.Avisos, a => a.Contains("excede o espacamento"));
        }

        [Fact]
        public void Colisao_com_empate_mantem_a_viga_de_menor_posicao_de_grade()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 1000.0 }, 250.0);

            Assert.Equal(15, r.Vigas.Count);
            Assert.Contains(r.Vigas, v => v.AlinhadaAoApoio && Math.Abs(v.PosicaoGradeMm - (o.VMinMm + 800.0)) < 1e-6);
            Assert.DoesNotContain(r.Vigas, v => Math.Abs(v.PosicaoGradeMm - (o.VMinMm + 1200.0)) < 1e-6);
            Assert.Contains(r.Avisos, a => a.Contains("mesmo apoio"));
        }

        [Fact]
        public void Colisao_sem_empate_mantem_a_viga_mais_proxima_do_apoio()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 1050.0 }, 260.0);

            Assert.Equal(15, r.Vigas.Count);
            Assert.Contains(r.Vigas, v =>
                v.AlinhadaAoApoio
                && Math.Abs(v.PosicaoGradeMm - (o.VMinMm + 1200.0)) < 1e-6
                && Math.Abs(v.PosicaoTransversalMm - (o.VMinMm + 1050.0)) < 1e-6);
            Assert.DoesNotContain(r.Vigas, v => Math.Abs(v.PosicaoGradeMm - (o.VMinMm + 800.0)) < 1e-6);
        }

        [Fact]
        public void Intervalo_maior_que_o_espacamento_apos_alinhamento_gera_aviso_com_o_vao()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(
                contorno, o, 400.0, new[] { o.VMinMm + 1150.0, o.VMinMm + 1650.0 }, 60.0);

            Assert.Equal(16, r.Vigas.Count);
            Assert.True(r.Vigas[3].AlinhadaAoApoio);
            Assert.True(r.Vigas[4].AlinhadaAoApoio);
            Assert.Equal(500.0, r.MaiorVaoEntreVigasMm, 6);
            Assert.Contains(r.Avisos, a => a.Contains("excede o espacamento") && a.Contains("500.0"));
        }

        [Fact]
        public void Alinhamento_da_primeira_viga_interna_pode_deixar_vao_excedido_junto_da_borda()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 450.0 }, 60.0);

            Assert.Equal(16, r.Vigas.Count);
            Assert.Equal(o.VMinMm, r.Vigas[0].PosicaoTransversalMm, 6);
            Assert.False(r.Vigas[0].AlinhadaAoApoio);
            Assert.True(r.Vigas[1].AlinhadaAoApoio);
            Assert.Equal(450.0, r.MaiorVaoEntreVigasMm, 6);
            Assert.Contains(r.Avisos, a => a.Contains("excede o espacamento") && a.Contains("450.0"));
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Tolerancia_de_alinhamento_invalida_retorna_erro_sem_vigas(double tolerancia)
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, new[] { o.VMinMm + 1230.0 }, tolerancia);

            Assert.False(r.EhValido);
            Assert.Contains(r.Erros, e => e.Contains("Tolerancia de alinhamento"));
            Assert.Empty(r.Vigas);
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Largura_da_viga_invalida_retorna_erro_sem_vigas(double largura)
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, null, 0.0, largura);

            Assert.False(r.EhValido);
            Assert.Contains(r.Erros, e => e.Contains("Largura da viga"));
            Assert.Empty(r.Vigas);
        }

        [Fact]
        public void Tolerancia_e_largura_iguais_a_zero_sao_validas()
        {
            ResultadoLayoutVigas r = Calcular(Retangulo(6000, 4000), null, 400.0, null, 0.0, 0.0);

            Assert.True(r.EhValido);
            Assert.Equal(16, r.Vigas.Count);
        }

        [Fact]
        public void Orientacao_de_outro_contorno_retorna_erro_e_a_do_mesmo_contorno_e_aceita()
        {
            OrientacaoVigas deQuatroPorSeis = OrientacaoVigas.Calcular(new ContornoLaje(Retangulo(6000, 4000)), null);

            ResultadoLayoutVigas incompativel =
                LayoutVigas.Calcular(new ContornoLaje(Retangulo(6000, 5000)), deQuatroPorSeis, 400.0);
            ResultadoLayoutVigas compativel =
                LayoutVigas.Calcular(new ContornoLaje(Retangulo(6000, 4000)), deQuatroPorSeis, 400.0);

            Assert.False(incompativel.EhValido);
            Assert.Contains(incompativel.Erros, e => e.Contains("Orientacao das vigas"));
            Assert.Empty(incompativel.Vigas);
            Assert.True(compativel.EhValido);
        }

        [Fact]
        public void Apoio_nao_finito_gera_aviso_e_os_demais_apoios_continuam_valendo()
        {
            var contorno = new ContornoLaje(Retangulo(6000, 4000));
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, null);
            var apoios = new[] { double.NaN, o.VMinMm + 1230.0, double.PositiveInfinity };

            ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, 400.0, apoios, 50.0);

            Assert.Contains(r.Avisos, a => a.Contains("nao finitas"));
            Assert.Equal(1, r.Vigas.Count(v => v.AlinhadaAoApoio));
        }

        [Fact]
        public void Bordas_ficam_nas_pontas_e_nao_ha_vigas_na_mesma_posicao_em_varios_contornos()
        {
            AssertBordasEUnicidade(Retangulo(6000, 4000), null, new[] { 400.0, 600.0, 700.0, 1000.0 });
            AssertBordasEUnicidade(FormaEmL(), null, new[] { 400.0, 600.0 });
            AssertBordasEUnicidade(FormaEmU(), 0.0, new[] { 700.0, 1000.0 });
        }

        private static void AssertBordasEUnicidade(Ponto2DMm[] pontos, double? direcao, double[] espacamentos)
        {
            var contorno = new ContornoLaje(pontos);
            OrientacaoVigas o = OrientacaoVigas.Calcular(contorno, direcao);

            foreach (double espacamento in espacamentos)
            {
                foreach (double tolerancia in new[] { 50.0, 450.0 })
                {
                    double[] apoios =
                    {
                        o.VMinMm + 30.0, o.VMinMm + 0.5, o.VMaxMm - 30.0, o.VMaxMm - 0.5, o.VMinMm + espacamento + 20.0
                    };

                    ResultadoLayoutVigas r = LayoutVigas.Calcular(contorno, o, espacamento, apoios, tolerancia);

                    Assert.True(r.EhValido);
                    Assert.Equal(o.VMinMm, r.Vigas[0].PosicaoTransversalMm, 6);
                    Assert.Equal(o.VMaxMm, r.Vigas[r.Vigas.Count - 1].PosicaoTransversalMm, 6);
                    Assert.False(r.Vigas[0].AlinhadaAoApoio);
                    Assert.False(r.Vigas[r.Vigas.Count - 1].AlinhadaAoApoio);

                    for (int i = 0; i + 1 < r.Vigas.Count; i++)
                    {
                        double distancia = r.Vigas[i + 1].PosicaoTransversalMm - r.Vigas[i].PosicaoTransversalMm;
                        Assert.True(distancia > LayoutVigas.ToleranciaMm);
                    }
                }
            }
        }
    }
}
