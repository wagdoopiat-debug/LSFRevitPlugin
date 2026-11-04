using System.Collections.Generic;
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    /// <summary>
    /// Testes do refinamento funcional do modulo de lajes: catalogo de perfis de laje,
    /// simplificacao da orientacao, novas espessuras de telha, composicao de placas
    /// (OSB/cimenticia), vigas compostas e persistencia das novas opcoes.
    /// </summary>
    public class LajeRefinamentoTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        // ---------- Catalogo de perfis de laje ----------

        [Fact]
        public void Catalogo_de_laje_nao_oferece_drywall_nem_chapa_080()
        {
            List<PerfilSpec> perfis = CatalogoPerfisLaje.ObterTodos().ToList();

            Assert.NotEmpty(perfis);
            Assert.All(perfis, p => Assert.Equal(SistemaPerfil.SteelFrame, p.Sistema));
            Assert.DoesNotContain(perfis, p => System.Math.Abs(p.EspessuraChapaMm - 0.80) < 1e-6);
        }

        [Fact]
        public void Catalogo_de_laje_oferece_larguras_e_bitolas_esperadas()
        {
            List<PerfilSpec> perfis = CatalogoPerfisLaje.ObterTodos().ToList();

            foreach (double largura in CatalogoPerfisLaje.LargurasSteelFrameMm)
            {
                foreach (double bitola in CatalogoPerfisLaje.BitolasSteelFrameMm)
                {
                    Assert.Contains(perfis, p =>
                        System.Math.Abs(p.LarguraAlmaMm - largura) < 1e-6
                        && System.Math.Abs(p.EspessuraChapaMm - bitola) < 1e-6);
                }
            }
        }

        [Theory]
        [InlineData(90.0, 0.95, true)]
        [InlineData(140.0, 1.25, true)]
        [InlineData(200.0, 0.95, true)]
        [InlineData(90.0, 0.80, false)]
        public void EhPerfilDeLaje_reconhece_chapas_validas(double largura, double bitola, bool esperado)
        {
            var perfil = new PerfilSpec
            {
                Sistema = SistemaPerfil.SteelFrame,
                LarguraAlmaMm = largura,
                EspessuraChapaMm = bitola
            };

            Assert.Equal(esperado, CatalogoPerfisLaje.EhPerfilDeLaje(perfil));
        }

        [Fact]
        public void EhPerfilDeLaje_rejeita_drywall()
        {
            var perfil = new PerfilSpec
            {
                Sistema = SistemaPerfil.Drywall,
                LarguraAlmaMm = 90.0,
                EspessuraChapaMm = 0.50
            };

            Assert.False(CatalogoPerfisLaje.EhPerfilDeLaje(perfil));
        }

        [Fact]
        public void Parametros_rejeitam_perfil_drywall_na_laje()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Perfil = new PerfilSpec
                {
                    Sistema = SistemaPerfil.Drywall,
                    LarguraAlmaMm = 90.0,
                    EspessuraChapaMm = 0.50
                }
            };

            Assert.Contains(p.Validar(), e => e.Contains("Perfil invalido para laje"));
        }

        [Fact]
        public void Parametros_rejeitam_chapa_080_na_laje()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Perfil = new PerfilSpec
                {
                    Sistema = SistemaPerfil.SteelFrame,
                    LarguraAlmaMm = 90.0,
                    EspessuraChapaMm = 0.80
                }
            };

            Assert.Contains(p.Validar(), e => e.Contains("Perfil invalido para laje"));
        }

        // ---------- Orientacao sem duplicidade ----------

        [Fact]
        public void Orientacao_UI_tem_apenas_tres_escolhas_sem_duplicidade()
        {
            OrientacaoUI[] valores = (OrientacaoUI[])System.Enum.GetValues(typeof(OrientacaoUI));

            Assert.Equal(3, valores.Length);
            Assert.Equal(3, valores.Distinct().Count());
        }

        [Fact]
        public void Orientacao_por_borda_mapeia_paralela_e_perpendicular()
        {
            Assert.Equal(
                OrientacaoVigasModo.ParalelaAresta,
                OrientacaoHelper.MapearParaModo(OrientacaoUI.PorBordaReferencia, perpendicular: false));
            Assert.Equal(
                OrientacaoVigasModo.PerpendicularAresta,
                OrientacaoHelper.MapearParaModo(OrientacaoUI.PorBordaReferencia, perpendicular: true));
        }

        [Fact]
        public void Orientacao_paralela_e_perpendicular_voltam_para_a_mesma_escolha_de_UI()
        {
            Assert.Equal(
                OrientacaoHelper.MapearParaUI(OrientacaoVigasModo.ParalelaAresta),
                OrientacaoHelper.MapearParaUI(OrientacaoVigasModo.PerpendicularAresta));
            Assert.True(OrientacaoHelper.EhPorBorda(OrientacaoVigasModo.PerpendicularAresta));
            Assert.False(OrientacaoHelper.EhPorBorda(OrientacaoVigasModo.Automatica));
        }

        // ---------- Espessuras de telha ----------

        [Fact]
        public void Telha_disponibiliza_043_050_e_063()
        {
            Assert.True(TelhaTp40.EspessuraDisponivel(0.43));
            Assert.True(TelhaTp40.EspessuraDisponivel(0.50));
            Assert.True(TelhaTp40.EspessuraDisponivel(0.63));
            Assert.False(TelhaTp40.EspessuraDisponivel(0.65));
        }

        [Fact]
        public void Telha_tem_peso_para_a_nova_espessura()
        {
            var telha = TelhaTp40.CriarPadrao();

            Assert.Equal(5.80, telha.PesoKgM2(0.63).Valor, 6);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => telha.PesoKgM2(0.65));
        }

        // ---------- Composicao de placas ----------

        [Fact]
        public void Composicao_de_OSB_tem_as_espessuras_esperadas()
        {
            Assert.Equal(new[] { 15.0, 16.0, 22.0, 25.0 }, ComposicaoCobertura.EspessurasOSBMm);
        }

        [Fact]
        public void Composicao_cimenticia_tem_as_espessuras_esperadas()
        {
            Assert.Equal(new[] { 10.0, 20.0 }, ComposicaoCobertura.EspessurasCimenticiaMm);
        }

        [Fact]
        public void Composicao_combinada_tem_cimenticia_sobre_OSB()
        {
            ComposicaoCobertura c = ComposicaoCobertura.CriarOSBMaisCimenticia(22.0, 10.0);

            Assert.Equal(ComposicaoTipo.OSBMaisCimenticia, c.Tipo);
            Assert.Equal(2, c.Camadas.Count);
            Assert.Equal("Cimenticia", c.Camadas[0].Nome);
            Assert.Equal(10.0, c.Camadas[0].EspessuraMm, 6);
            Assert.Equal("OSB", c.Camadas[1].Nome);
            Assert.Equal(22.0, c.Camadas[1].EspessuraMm, 6);
            Assert.Equal(32.0, c.AlturaTotalMm, 6);
        }

        [Fact]
        public void Parametros_rejeitam_espessura_de_OSB_fora_do_catalogo()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Tipo = TipoLaje.Seca,
                ComposicaoPlacas = ComposicaoTipo.OSB,
                EspessuraOSBMm = 18.0
            };

            Assert.Contains(p.Validar(), e => e.Contains("Espessura de OSB invalida"));
        }

        [Fact]
        public void Parametros_aceitam_composicao_valida()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Tipo = TipoLaje.Seca,
                ComposicaoPlacas = ComposicaoTipo.OSBMaisCimenticia,
                EspessuraOSBMm = 25.0,
                EspessuraCimenticiaMm = 10.0
            };

            Assert.Empty(p.Validar());
        }

        [Fact]
        public void Camadas_com_composicao_empilham_OSB_e_cimenticia_sobre_a_viga()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Tipo = TipoLaje.Seca,
                CotaMm = 3000.0,
                ComposicaoPlacas = ComposicaoTipo.OSBMaisCimenticia,
                EspessuraOSBMm = 22.0,
                EspessuraCimenticiaMm = 10.0
            };

            CamadasLaje c = CamadasLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.Equal(TipoLaje.Seca, c.Tipo);
            Assert.Equal(3, c.Camadas.Count);
            Assert.Equal(2990.0, c.Camada(CamadaLaje.NomeCimenticia).BaseMm, 6);
            Assert.Equal(2990.0, c.Camada(CamadaLaje.NomeOSB).TopoMm, 6);
            Assert.Equal(2968.0, c.Camada(CamadaLaje.NomeOSB).BaseMm, 6);
            Assert.Equal(2968.0, c.TopoVigaMm, 6);
            Assert.Equal(2878.0, c.BaseVigaMm, 6);
        }

        [Fact]
        public void Camadas_sem_composicao_continuam_usando_o_piso_do_catalogo()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Tipo = TipoLaje.Seca,
                CotaMm = 3000.0,
                ComposicaoPlacas = ComposicaoTipo.Nenhuma
            };

            CamadasLaje c = CamadasLaje.Calcular(p, CatalogoLaje.CriarPadrao());

            Assert.NotNull(c.Camada(CamadaLaje.NomePisoSeco));
            Assert.Equal(2982.0, c.TopoVigaMm, 6);
        }

        // ---------- Vigas compostas ----------

        [Fact]
        public void Composicao_de_viga_simples_tem_um_subperfil_central()
        {
            List<SubPerfilViga> subs =
                ComposicaoViga.SubPerfis(TipoVigaLaje.PerfilSimples, PerfilSpec.CriarPadrao());

            Assert.Single(subs);
            Assert.Equal(0.0, subs[0].DeslocamentoTransversalMm, 6);
            Assert.False(subs[0].Espelhado);
        }

        [Fact]
        public void Composicao_back_to_back_tem_dois_subperfis_espelhados_e_afastados()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            List<SubPerfilViga> subs = ComposicaoViga.SubPerfis(TipoVigaLaje.BackToBack, perfil);

            Assert.Equal(2, subs.Count);
            Assert.NotEqual(subs[0].DeslocamentoTransversalMm, subs[1].DeslocamentoTransversalMm, 6);
            Assert.NotEqual(subs[0].Espelhado, subs[1].Espelhado);
            Assert.Equal(perfil.AbaMm, System.Math.Abs(subs[0].DeslocamentoTransversalMm - subs[1].DeslocamentoTransversalMm), 6);
        }

        [Fact]
        public void Largura_transversal_dobra_no_back_to_back()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();

            Assert.Equal(perfil.AbaMm, ComposicaoViga.LarguraTransversalMm(TipoVigaLaje.PerfilSimples, perfil), 6);
            Assert.Equal(perfil.AbaMm * 2.0, ComposicaoViga.LarguraTransversalMm(TipoVigaLaje.BackToBack, perfil), 6);
        }

        [Fact]
        public void Composicao_caixao_tem_dois_subperfis_espelhados_no_mesmo_eixo()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            List<SubPerfilViga> subs = ComposicaoViga.SubPerfis(TipoVigaLaje.Caixao, perfil);

            Assert.Equal(2, subs.Count);
            Assert.Equal(0.0, subs[0].DeslocamentoTransversalMm, 6);
            Assert.Equal(0.0, subs[1].DeslocamentoTransversalMm, 6);
            Assert.NotEqual(subs[0].Espelhado, subs[1].Espelhado);
        }

        [Fact]
        public void Largura_transversal_do_caixao_e_a_aba()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            Assert.Equal(perfil.AbaMm, ComposicaoViga.LarguraTransversalMm(TipoVigaLaje.Caixao, perfil), 6);
        }

        // ---------- Correcao 3: caixao Encaixados ----------

        /// <summary>
        /// Extensao transversal (mm) da secao de um sub-perfil, reproduzindo o mapeamento do
        /// LajeVigaBuilder: eixo U da secao = eixo transversal da viga; o deslocamento do
        /// sub-perfil soma ao U local; Espelhado inverte a abertura.
        /// </summary>
        private static (double Min, double Max) ExtensaoTransversalMm(
            SubPerfilViga sub, PerfilSpec perfil)
        {
            bool afrente = sub.Espelhado
                ? !StudSolidBuilder.AbrirParaFrenteDaDirecao
                : StudSolidBuilder.AbrirParaFrenteDaDirecao;

            List<(double U, double V)> local = SecaoMontante.ContornoC(
                perfil.AbaMm / 2.0, perfil.LarguraAlmaMm / 2.0,
                perfil.EspessuraChapaMm, perfil.LabioMm, afrente);

            double min = double.MaxValue, max = double.MinValue;
            foreach ((double u, double v) in local)
            {
                double x = sub.DeslocamentoTransversalMm + u;
                if (x < min) min = x;
                if (x > max) max = x;
            }
            return (min, max);
        }

        [Fact]
        public void Caixao_encaixados_tem_aberturas_voltadas_para_o_centro()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            List<SubPerfilViga> subs =
                ComposicaoViga.SubPerfis(TipoVigaLaje.Caixao, perfil, ComposicaoCaixaoTipo.Encaixados);

            Assert.Equal(2, subs.Count);
            Assert.Equal(perfil.AbaMm / 2.0, System.Math.Abs(subs[0].DeslocamentoTransversalMm), 6);
            Assert.Equal(perfil.AbaMm / 2.0, System.Math.Abs(subs[1].DeslocamentoTransversalMm), 6);
            Assert.NotEqual(subs[0].Espelhado, subs[1].Espelhado);

            // Almas nas bordas externas (+-AbaMm) e pontas das abas se encontrando no centro (0):
            // as duas aberturas ficam voltadas uma para a outra.
            (double min0, double max0) = ExtensaoTransversalMm(subs[0], perfil);
            (double min1, double max1) = ExtensaoTransversalMm(subs[1], perfil);

            Assert.Equal(-perfil.AbaMm, min0, 6);
            Assert.Equal(0.0, max0, 6);
            Assert.Equal(0.0, min1, 6);
            Assert.Equal(+perfil.AbaMm, max1, 6);
        }

        [Fact]
        public void Caixao_encaixados_dobra_a_largura_transversal()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();

            Assert.Equal(
                perfil.AbaMm * 2.0,
                ComposicaoViga.LarguraTransversalMm(
                    TipoVigaLaje.Caixao, perfil, MontagemTrelica.Encaixada,
                    ComposicaoCaixaoTipo.Encaixados), 6);
            Assert.Equal(
                perfil.AbaMm,
                ComposicaoViga.LarguraTransversalMm(
                    TipoVigaLaje.Caixao, perfil, MontagemTrelica.Encaixada,
                    ComposicaoCaixaoTipo.FrenteAFrente), 6);
        }

        [Fact]
        public void Caixao_frente_a_frente_continua_com_abas_sobrepostas_e_largura_aba()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            List<SubPerfilViga> subs =
                ComposicaoViga.SubPerfis(TipoVigaLaje.Caixao, perfil, ComposicaoCaixaoTipo.FrenteAFrente);

            Assert.Equal(2, subs.Count);
            Assert.Equal(0.0, subs[0].DeslocamentoTransversalMm, 6);
            Assert.Equal(0.0, subs[1].DeslocamentoTransversalMm, 6);

            // Dois "C" opostos no mesmo eixo: ambos ocupam [-Aba/2, +Aba/2] (abas sobrepostas),
            // almas nas faces opostas.
            (double min0, double max0) = ExtensaoTransversalMm(subs[0], perfil);
            (double min1, double max1) = ExtensaoTransversalMm(subs[1], perfil);

            Assert.Equal(-perfil.AbaMm / 2.0, min0, 6);
            Assert.Equal(+perfil.AbaMm / 2.0, max0, 6);
            Assert.Equal(-perfil.AbaMm / 2.0, min1, 6);
            Assert.Equal(+perfil.AbaMm / 2.0, max1, 6);
        }

        [Fact]
        public void Largura_unificada_considera_tipo_montagem_e_caixao()
        {
            PerfilSpec perfil = PerfilSpec.CriarPadrao();
            double aba = perfil.AbaMm;

            Assert.Equal(aba, ComposicaoViga.LarguraTransversalMm(
                TipoVigaLaje.PerfilSimples, perfil, MontagemTrelica.Encaixada,
                ComposicaoCaixaoTipo.FrenteAFrente), 6);
            Assert.Equal(aba * 2.0, ComposicaoViga.LarguraTransversalMm(
                TipoVigaLaje.BackToBack, perfil, MontagemTrelica.Encaixada,
                ComposicaoCaixaoTipo.Encaixados), 6);
            Assert.Equal(perfil.LarguraAlmaMm, ComposicaoViga.LarguraTransversalMm(
                TipoVigaLaje.Trelica, perfil, MontagemTrelica.Encaixada,
                ComposicaoCaixaoTipo.Encaixados), 6);
            Assert.Equal(aba * 2.0, ComposicaoViga.LarguraTransversalMm(
                TipoVigaLaje.Trelica, perfil, MontagemTrelica.FaceComFace,
                ComposicaoCaixaoTipo.FrenteAFrente), 6);
            Assert.Equal(0.0, ComposicaoViga.LarguraTransversalMm(
                TipoVigaLaje.Caixao, null, MontagemTrelica.FaceComFace,
                ComposicaoCaixaoTipo.Encaixados), 6);
        }

        [Fact]
        public void Parametros_aceitam_back_to_back_caixao_e_trelica()
        {
            var b2b = new ParametrosLaje { Contorno = Retangulo(), TipoViga = TipoVigaLaje.BackToBack };
            var caixao = new ParametrosLaje { Contorno = Retangulo(), TipoViga = TipoVigaLaje.Caixao };
            var trelica = new ParametrosLaje { Contorno = Retangulo(), TipoViga = TipoVigaLaje.Trelica };

            Assert.Empty(b2b.Validar());
            Assert.Empty(caixao.Validar());
            Assert.Empty(trelica.Validar());
        }

        // ---------- Persistencia ----------

        [Fact]
        public void Persistencia_preserva_tipo_de_viga_e_composicao()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                Tipo = TipoLaje.Seca,
                TipoViga = TipoVigaLaje.BackToBack,
                ComposicaoPlacas = ComposicaoTipo.OSBMaisCimenticia,
                EspessuraOSBMm = 22.0,
                EspessuraCimenticiaMm = 10.0
            };

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(
                ConfiguracaoLajeTexto.Serializar(p), out lido));

            Assert.Equal(TipoVigaLaje.BackToBack, lido.TipoViga);
            Assert.Equal(ComposicaoTipo.OSBMaisCimenticia, lido.ComposicaoPlacas);
            Assert.Equal(22.0, lido.EspessuraOSBMm, 6);
            Assert.Equal(10.0, lido.EspessuraCimenticiaMm, 6);
        }

        [Fact]
        public void Secao_do_banzo_U_remove_os_labios_e_C_preserva()
        {
            Assert.Equal(15.0, SecaoBanzoTrelicaRegra.LabioMm(SecaoBanzoTrelica.C, 15.0), 6);
            Assert.Equal(0.0, SecaoBanzoTrelicaRegra.LabioMm(SecaoBanzoTrelica.U, 15.0), 6);
            Assert.Equal(0.0, SecaoBanzoTrelicaRegra.LabioMm(SecaoBanzoTrelica.C, -1.0), 6);
        }

        [Fact]
        public void Persistencia_preserva_montagem_caixao_e_gussets()
        {
            var p = new ParametrosLaje
            {
                Contorno = Retangulo(),
                TipoViga = TipoVigaLaje.Trelica,
                MontagemTrelica = MontagemTrelica.FaceComFace,
                SecaoBanzo = SecaoBanzoTrelica.U,
                ComposicaoCaixao = ComposicaoCaixaoTipo.Encaixados,
                GussetPlacas = true
            };

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(
                ConfiguracaoLajeTexto.Serializar(p), out lido));

            Assert.Equal(MontagemTrelica.FaceComFace, lido.MontagemTrelica);
            Assert.Equal(SecaoBanzoTrelica.U, lido.SecaoBanzo);
            Assert.Equal(ComposicaoCaixaoTipo.Encaixados, lido.ComposicaoCaixao);
            Assert.True(lido.GussetPlacas);
        }

        [Fact]
        public void Texto_antigo_sem_montagem_caixao_e_gusset_usa_padroes_seguros()
        {
            string texto = "versao=1\ntipo=Seca\ncontorno=0,0;6000,0;6000,4000;0,4000";

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));
            Assert.Equal(MontagemTrelica.Encaixada, lido.MontagemTrelica);
            Assert.Equal(SecaoBanzoTrelica.C, lido.SecaoBanzo);
            Assert.Equal(ComposicaoCaixaoTipo.FrenteAFrente, lido.ComposicaoCaixao);
            Assert.False(lido.GussetPlacas);
        }

        [Fact]
        public void Texto_antigo_sem_composicao_usa_padroes_seguros()
        {
            string texto = "versao=1\ntipo=Seca\ncontorno=0,0;6000,0;6000,4000;0,4000";

            ParametrosLaje lido;
            Assert.True(ConfiguracaoLajeTexto.TentarDeserializar(texto, out lido));
            Assert.Equal(ComposicaoTipo.Nenhuma, lido.ComposicaoPlacas);
            Assert.Equal(15.0, lido.EspessuraOSBMm, 6);
            Assert.Equal(10.0, lido.EspessuraCimenticiaMm, 6);
        }
    }
}
