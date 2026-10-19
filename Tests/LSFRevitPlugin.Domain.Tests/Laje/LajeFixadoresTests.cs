using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes das posicoes dos fixadores (parafusos): estruturais nas pontas das vigas sobre as
    /// guias, de placa ao longo das vigas no passo pedido, nivel oculto, validacao e persistencia.
    /// </summary>
    public class LajeFixadoresTests
    {
        private static ResultadoLayoutVigas LayoutSimples()
        {
            var layout = new ResultadoLayoutVigas();
            layout.Vigas.Add(new VigaLaje(
                0.0, 0.0, false, new[] { new SegmentoViga(0.0, 6000.0) }));
            layout.GuiasBorda.Add(new GuiaBordaLaje(0.0, -100.0, 100.0));
            layout.GuiasBorda.Add(new GuiaBordaLaje(6000.0, -100.0, 100.0));
            return layout;
        }

        [Fact]
        public void Nivel_oculto_nao_gera_nenhum_fixador()
        {
            ResultadoFixadores r = FixadoresLaje.Calcular(
                LayoutSimples(), NivelDetalheFixadores.Oculto, 0.0, 0.0, 300.0);

            Assert.True(r.Oculto);
            Assert.Equal(0, r.Total);
        }

        [Fact]
        public void Fixadores_estruturais_saem_nas_pontas_sobre_as_guias()
        {
            List<PosicaoFixador> estruturais = FixadoresLaje.Estruturais(LayoutSimples(), 100.0);

            Assert.Equal(2, estruturais.Count);
            Assert.All(estruturais, f => Assert.Equal(TipoFixador.Estrutural, f.Tipo));
            Assert.Contains(estruturais, f => System.Math.Abs(f.UMm - 0.0) < 1e-6);
            Assert.Contains(estruturais, f => System.Math.Abs(f.UMm - 6000.0) < 1e-6);
        }

        [Fact]
        public void Sem_guia_nao_ha_fixador_estrutural()
        {
            var layout = new ResultadoLayoutVigas();
            layout.Vigas.Add(new VigaLaje(0.0, 0.0, false, new[] { new SegmentoViga(0.0, 6000.0) }));

            Assert.Empty(FixadoresLaje.Estruturais(layout, 100.0));
        }

        [Fact]
        public void Fixadores_de_placa_seguem_o_espacamento()
        {
            List<PosicaoFixador> placa = FixadoresLaje.Placa(LayoutSimples(), 50.0, 300.0);

            Assert.Equal(21, placa.Count);
            Assert.All(placa, f =>
            {
                Assert.Equal(TipoFixador.Placa, f.Tipo);
                Assert.Equal(50.0, f.ZMm, 6);
            });
        }

        [Fact]
        public void Fixadores_de_placa_respeitam_limite_do_segmento()
        {
            var layout = new ResultadoLayoutVigas();
            layout.Vigas.Add(new VigaLaje(0.0, 0.0, false, new[] { new SegmentoViga(0.0, 650.0) }));

            List<PosicaoFixador> placa = FixadoresLaje.Placa(layout, 0.0, 300.0);

            Assert.Equal(3, placa.Count);
        }

        [Fact]
        public void Calcular_junta_estruturais_e_de_placa()
        {
            ResultadoFixadores r = FixadoresLaje.Calcular(
                LayoutSimples(), NivelDetalheFixadores.Simplificado, 100.0, 50.0, 300.0);

            Assert.False(r.Oculto);
            Assert.Equal(2, r.Estruturais.Count);
            Assert.Equal(21, r.Placa.Count);
            Assert.Equal(23, r.Total);
        }

        [Fact]
        public void Espacamento_invalido_cai_no_padrao()
        {
            ResultadoFixadores r = FixadoresLaje.Calcular(
                LayoutSimples(), NivelDetalheFixadores.Simplificado, 100.0, 50.0, 0.0);

            Assert.Equal(21, r.Placa.Count);
        }

        [Fact]
        public void Fixadores_de_placa_tem_passo_constante_e_mesmo_v()
        {
            List<PosicaoFixador> placa = FixadoresLaje.Placa(LayoutSimples(), 50.0, 300.0);

            List<PosicaoFixador> ordenados = placa.OrderBy(f => f.UMm).ToList();
            for (int i = 1; i < ordenados.Count; i++)
            {
                Assert.Equal(300.0, ordenados[i].UMm - ordenados[i - 1].UMm, 6);
            }

            Assert.All(placa, f => Assert.Equal(0.0, f.VMm, 6));
        }

        [Fact]
        public void Fixador_estrutural_fica_sobre_a_guia()
        {
            ResultadoLayoutVigas layout = LayoutSimples();
            List<PosicaoFixador> estruturais = FixadoresLaje.Estruturais(layout, 100.0);

            Assert.All(estruturais, f =>
            {
                GuiaBordaLaje guia = layout.GuiasBorda.First(g => System.Math.Abs(g.UMm - f.UMm) < 1e-6);
                Assert.InRange(f.VMm, guia.VInicioMm, guia.VFimMm);
            });
        }

        [Fact]
        public void Fixador_estrutural_entra_ao_longo_da_viga()
        {
            List<PosicaoFixador> estruturais = FixadoresLaje.Estruturais(LayoutSimples(), 100.0);

            Assert.NotEmpty(estruturais);
            Assert.All(estruturais, f => Assert.Equal(DirecaoFixador.AoLongoDaViga, f.Direcao));
        }

        [Fact]
        public void Fixador_de_placa_e_vertical()
        {
            List<PosicaoFixador> placa = FixadoresLaje.Placa(LayoutSimples(), 50.0, 300.0);

            Assert.NotEmpty(placa);
            Assert.All(placa, f => Assert.Equal(DirecaoFixador.Vertical, f.Direcao));
        }

        [Fact]
        public void Filtro_de_contorno_descarta_fixador_fora_da_laje()
        {
            var contorno = new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
            OrientacaoVigas orientacao = OrientacaoVigas.Calcular(contorno, 0.0);

            Ponto2DMm dentro = orientacao.ParaMundo(3000.0, 2000.0);
            Ponto2DMm foraV = orientacao.ParaMundo(3000.0, 5000.0);
            Ponto2DMm foraU = orientacao.ParaMundo(7000.0, 2000.0);

            Assert.True(RecortePoligono.Contem(contorno.Pontos, dentro));
            Assert.False(RecortePoligono.Contem(contorno.Pontos, foraV));
            Assert.False(RecortePoligono.Contem(contorno.Pontos, foraU));
        }

        // ---------- Parametros ----------

        [Fact]
        public void Espacamento_de_fixador_invalido_gera_erro_quando_nao_oculto()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(new[]
                {
                    new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                    new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
                }),
                NivelFixadores = NivelDetalheFixadores.Simplificado,
                EspacamentoFixadoresPlacaMm = 0.0
            };

            Assert.Contains(p.Validar(), e => e.Contains("fixadores de placa"));
        }

        [Fact]
        public void Espacamento_de_fixador_invalido_nao_gera_erro_quando_oculto()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(new[]
                {
                    new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                    new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
                }),
                NivelFixadores = NivelDetalheFixadores.Oculto,
                EspacamentoFixadoresPlacaMm = 0.0
            };

            Assert.DoesNotContain(p.Validar(), e => e.Contains("fixadores de placa"));
        }

        // ---------- Persistencia ----------

        [Fact]
        public void Persistencia_preserva_nivel_e_espacamento_dos_fixadores()
        {
            var p = new ParametrosLaje
            {
                Contorno = new ContornoLaje(new[]
                {
                    new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                    new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
                }),
                NivelFixadores = NivelDetalheFixadores.Detalhado,
                EspacamentoFixadoresPlacaMm = 250.0
            };

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(
                ConfiguracaoLajeTexto.Serializar(p), out lido));

            Assert.Equal(NivelDetalheFixadores.Detalhado, lido.NivelFixadores);
            Assert.Equal(250.0, lido.EspacamentoFixadoresPlacaMm, 6);
        }

        [Fact]
        public void Texto_antigo_sem_fixadores_usa_oculto()
        {
            string texto = "versao=1\ntipo=Seca\ncontorno=0,0;6000,0;6000,4000;0,4000";

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));

            Assert.Equal(NivelDetalheFixadores.Oculto, lido.NivelFixadores);
            Assert.Equal(FixadoresLaje.EspacamentoPlacaPadraoMm, lido.EspacamentoFixadoresPlacaMm, 6);
        }
    }
}
